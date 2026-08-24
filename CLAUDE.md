# CLAUDE.md — Cosmic Shore / Froglet Inc.

## Prime Directive

You are expected to work autonomously and persistently. Complete the entire task before stopping. Do not pause to ask for confirmation, approval, or clarification unless you are genuinely blocked on ambiguous requirements. If you encounter an error, debug and fix it yourself — attempt at least 3 different approaches before reporting the issue. Do not checkpoint, summarize progress, or ask "should I continue?" mid-task. Continue until all steps are done or you hit a hard wall.

When a task spans multiple files or systems, complete ALL of them in a single pass. Do not stop after the first file and ask if you should proceed to the next.

## Ecosystem Design Principles (LOCKED — read before any ecology change)

The cell ecosystem (flora/fauna/cells/crystals) is a **platform fundamental** on the path to
credible **artificial life**. North star + roadmap: `Docs/ECOSYSTEM_MASTERPLAN.md`. Mechanics
log: `Docs/ECOSYSTEM.md`. These invariants are **locked** — do not relitigate or re-derive them.
They are a direct application of "Favor Emergent Systems / Don't cheat emergence" (below) and —
not by accident — they are also what makes the system credible as artificial life (a scripted
outcome is optimization, not life). Use the `/ecology` skill for any change here.

- **Continuity of existence — nothing pops in or out (PLATFORM-WIDE LAW, all of Cosmic Shore).**
  Nothing may *instantly* appear or disappear. Every entity — prisms, crystals, flora, fauna,
  vessels, projectiles, even UI — must **grow / bloom / fade / suction / wither / evaporate** into
  and out of existence over a visible transition. A bare `Instantiate`-then-show or `Destroy` of
  anything the player can see is a bug. Spawns animate in (scale-from-zero / bloom); deaths animate
  out (wither from the extremities inward, suction toward a point, or fade). This is *why*
  starvation withers and mass is conserved — it is the same law applied to the ecosystem. It is not
  ecology-specific: respect it everywhere.
- **No imposed death.** No decay, lifespan, or fixed-period despawn timers. Populations are
  bounded by **consumption + starvation**, never an imposed clock. (Repeatedly rejected.)
- **No domain asymmetry.** Fauna spawn in **one color — the cell's controlling color**. Never
  cross-domain / prey-weighted / per-domain-biased spawning. The herbivore DIET is spatial in
  nucleus cells (see "Volume is the spine" below): outside the nucleus they graze **any**
  domain's mass voraciously; inside they eat **nothing**. Cells without a nucleus keep the
  legacy opposing-mass diet. **Shielded and super-shielded mass is never food, in any cell** —
  `Prism.Consume` is a no-op on super-shielded mass and only sheds the shield on shielded mass,
  so targeting one is a feed-hold the creature can never finish. Every herbivore edibility
  predicate routes through `Fauna.IsShieldedMass`; do not write a grazer that tests shield state
  itself. Shielded mass is likewise **not a steering target** — it is excluded from the cell's
  targeting grids (`Cell.AddBlock`, re-filed on any shield transition by
  `Cell.NotifyBlockShieldStateChanged`), because "fauna must never be led to mass they cannot
  eat" is one rule, not two. (`Docs/ECOSYSTEM.md §16`, `§22`.)
  A mode may redefine what "controls" a cell — Brood Rush makes it the nucleus claim (Ribcage
  pinned it to the race leader until its fauna were removed; `Cell.SetModeControlOverride`
  survives as the platform capability) — but the spawn colour is still
  exactly ONE colour, the controller's, and that setter also re-colours the LIVE swarm so a
  cell can never hold two fauna colours at once. A mode may also PEN a cell's fauna
  (`Cell.FaunaContainmentRadius` = the OUTER wall, `Cell.FaunaExclusionRadius` = the INNER one —
  together a cell-level annulus a mode can open and close while the match runs; Astro League holds
  its cleanup crew outside the court and drops the inner wall when the cell's own volume ladder
  leaves Calm, so "the pitch is crowded" is read from the spine rather than a bespoke signal) or
  pen a single SPECIES to an
  ANNULUS (`FaunaConfigurationSO.BandInner/BandOuterRadius` — Wildlife Liberation now authors ONE
  arena-wide band shared by every species, having tried and removed a per-tier pen; the annulus
  capability is unchanged): outside the pen nothing is prey
  and every goal is clamped back in — a spatial diet + steering rule, never a wall, and never a
  cull. Both compose, both default to off, and every grazer routes its edibility test through
  `Fauna.IsPreyForMe` — "a creature must never be led to mass it cannot reach or eat" is ONE
  rule, and a per-subclass copy is a rule you can forget to apply in the next grazer.
  A biome's STARTING release state is authored data (`SpawnProfileSO.InitialFaunaReleaseTier`),
  not a runtime call — a runtime-only gate races the cell's own bootstrap and loses.
- **A creature dies when its last body prism is destroyed** — `Fauna.OnBodyPrismExploded`
  (platform-wide since Wildlife Liberation; before it, only the worm colony implemented it, so
  shooting any other creature stripped its body and left an immortal husk swimming). This is an
  ACTIVE force removing mass and therefore squarely inside the conserved-mass law: no timer, no
  lifespan, no cull, and a creature nobody shoots still only ever dies to starvation or
  predation. It routes through the same sealed `Fauna.Die`, so the crystal drop and the wither
  are not bypassable. A subclass may override to add bookkeeping first (the worm colony re-links
  its chain), but an override that lets a creature survive losing its whole body breaks the rule.
- **Starvation = wither-to-crystal, and a joust is that wither RUN BACKWARDS.** A starving creature
  withers from its extremity spindles inward — a shark's fins / a brittlestar's arms evaporate
  *before* the core body (farthest-from-the-heart first, emergent from geometry) — and the heart is
  the LAST thing standing, so its crystal becomes collectable by any vessel only when the wither
  reaches the core. A **jousted** lifeform (the Squirrel's Crystal Joust, flora and fauna alike)
  runs the identical geometry in the opposite direction: it never detonates, the heart is freed at
  the strike and **auto-collected by the jouster**
  (`ElementalCrystalImpactor.CollectBy`), and the spindles unravel *from the heart outward* around
  the hole it left. Both leave the body prisms standing as a **skeleton** — ordinary cell mass the
  food web then grazes, so a creature's frame is conserved instead of dying with its husk (before
  this only the heart survived a death, which was passive mass removal hiding inside a death
  animation). Predation is neither: a devoured body suctions into the mouth, because there the mass
  transfers to the eater. The carrier is `LifeformDeathStyle` (`Withered`/`Jousted`/`Consumed`),
  stamped by the killing force and read by the death animation; the ordered wither is only possible
  because `Spindle.IsolateForOrderedWither` first breaks the parent/child couplings that make
  `ForceWither` recurse and make destroying a spindle destroy its children. It **does not vanish**
  (the continuity law above). **Mass is conserved** (the "self-sustaining economy" that makes the
  system NASA-credible). Sealed into `Fauna.Die`, which releases the heart outright unless a
  subclass opts into a progressive wither (`DefersHeartRelease`) — and that deferral is safe only
  because it is TWO-stage: `Crystal.DetachHeartToCell` re-homes the crystal onto the cell at the
  *top* of the death while leaving it `IsEmbedded` (still uncollectable, still the heart the wither
  unravels around), so an interrupted wither can never destroy it with the husk and every later exit
  (`RemoveHusk`, `OnDestroy`) is a real recovery. The worm colony is deliberately excluded from the
  skeleton (its capitals carry danger prisms). Full record: `Docs/ECOSYSTEM.md §26`.
- **Volume is the spine.** Phase, dominant domain, prey, HUD all key off per-domain **VOLUME**
  (`Cell.LiveVolume`), not prism count. Count is a rare frenzy/perf backstop only.
  **Node control is the NUCLEUS**: in a cell with a nucleus, `DominantDomain` reads only the
  per-domain ENVIRONMENT volume laid **inside the nucleus** (the territorial claim — a fauna
  sanctuary players contest with abilities + out-laying volume); everything **outside** is the
  voraciously-grazed feeding ground and never sways control. The fauna spawner ticks a fixed
  **30s** wave clock (`BaseFaunaSpawnTime`), spawning each wave in the controlling color and
  raising `CellRuntimeDataSO.OnFaunaWaveSpawned` — the heartbeat Brood Rush scores on. See
  `Docs/ECOSYSTEM.md §13` + `_Scripts/Controller/Arcade/NUCLEUSRUSH.md`.
  **A nucleus a mode borrowed as PLAY GEOMETRY is a wall, not a claim** — set
  `Cell.NucleusIsControlZone = false` (default true; collapses the control zone so the cell keeps
  its whole-cell control + diet semantics, exactly as if no `NucleusPrefab` were authored). This is
  not an exception to the rule, it is a declaration that the cell HAS no control zone — a state the
  ecology already supports. It is **load-bearing**: Astro League morphs the nucleus into its whole
  ricochet court, so the control radius became the court's circumscribing radius, every prism in the
  match read as "inside the nucleus", and the sanctuary rule made the entire pitch inedible — the
  trail-grazing food web could not remove one prism and no amount of threshold/food-floor tuning
  could reach it, because the diet predicate returned false first. **Whenever a mode repurposes a
  Cell-owned visual, check what SEMANTICS it borrowed with the geometry.** (`Docs/ECOSYSTEM.md §25.1`.)
- **Every lifeform drops one elemental crystal** (Charge/Mass/Space/Time) as a powerup on death,
  enforced by `LifeFormCrystal`. It must not be possible to make a lifeform that violates this.
  **Composite creatures** (the worm colony) satisfy this at the CREATURE level, not per part:
  its capital segments (head/tail) carry and drop hearts, its body segments are body-parts and
  carry none, and the colony root is a heartless anchor that forwards a config's element pick to
  the capitals (`Fauna.ProvisionHeart`). Do not "fix" a heartless body segment by giving it a
  crystal, and do not cite one as precedent for a crystal-less standalone fauna — the ruling and
  its reasoning are `Docs/ECOSYSTEM.md §23.3`.
  **A heart's SIZE is ONE curve keyed on LEVEL — never per species, element or prefab.**
  `ElementalCrystalSet.levelOneWorldScale × worldScalePerLevel^(L-1)` (3.5 → 4.25 across
  levels 1..5), applied at the single gate every heart passes through (`Crystal.SetEmbeddedIn`)
  and re-applied on every level change. A crystal's world scale is read twice AS GAMEPLAY — the
  collect reward (`SkimmerAdjustElementLevelByCrystalEffectSO`) and the live domain fauna buff
  (`DomainFaunaBuffSystem`) — so a per-prefab scale is a per-prefab REWARD: the shipped prefabs
  ranged 0.7 (tadpole) to 4.0 (gyroid), a 5.7× spread nobody authored, and four of five species
  CLIPPED the gain cap by level 5 so levelling stopped paying exactly where it should pay most.
  Keep the whole band under the cap (`levelOne × perLevel⁴ < maxLevelGainPerCrystal /
  levelPerUnitScale`), work in WORLD scale (`LifeFormCrystal.SetWorldScale` — a local write
  drags the heart along with a growing body, which is the coupling being removed), and do not
  compensate a sizing change by retuning `levelPerUnitScale`: it is shared with non-lifeform
  elemental crystals (the Wanderway conveyor, Dog Fight's arena scatter).
  **Uniform root scale is NOT uniform apparent size, and the fix goes BELOW the root.** Each
  elemental prefab carries a size correction on its model child (Charge 1.0 / Mass 1.38 /
  Space 1.34 / Time 1.42) because the four FBX models are very different sizes in their own
  units — and those children exist to equalize apparent EXTENT, which at 1.0/1.0/1.34/1.42 they
  already did within 7% (measured from FBX `Vertices` bounds normalized by `UnitScaleFactor`;
  Space's file is unit-1, the others unit-100). Mass is raised to 1.38 anyway because it reads
  thin rather than small — four concentric `ShepardGraph` shells vs Space's solid `_spread`
  body — and that number is an eye-calibration pending playtest, not a measurement. A
  per-element size fix belongs on that element's crystal PREFAB child; putting it on the root
  moves the collect reward and the live domain fauna buff with it, since both read the root's
  `lossyScale`. `Docs/ECOSYSTEM.md §33`.
  **Collecting one is a BEAT, not a journey** — snatch → suction → absorb in **0.44 s**, ending in
  the element's spent-crystal husk bursting into the vessel's wake (`Crystal.Explode`, the same
  payoff an omni pickup plays) and the crystal dissolving out on `_opacity` rather than being
  `Destroy`ed (continuity of existence applies to crystals too). All feel lives in the ONE asset
  `Resources/CrystalCaptureConfig` (`CrystalCaptureConfigSO`) — **never a per-prefab duration**,
  which is how the old capture drifted to 1 s on two fauna and 3 s on eleven flora while reading as
  the crystal chasing the ship. The reward (the element level) lands at CONTACT and so does
  `OnCrystalCollected`, the scoring event: **a mode's objective must never wait on a flourish**, and
  a flourish that outlasts its own payoff reads as lag. `Docs/ECOSYSTEM.md §31`.
- **Flora have POPULATIONS too, and a plant's feeding is GROWTH.** Flora are not scenery that a
  timer keeps extruding: like fauna they have a seed floor, a hard per-cell cap and **reproduction
  as the population driver** (`FloraConfigurationSO.PopulationSize` / `MaxLivePopulation` /
  `GrowthPerOffspring`, `Flora.TryReproduce`, `FloraReproductionRules`). The currency is the one
  thing a plant actually earns — **prisms it grew** — which is what bounds the population with **no
  imposed death**: a plant at its live-prism budget has stopped growing, so it has stopped funding
  children, and it only funds another after the food web grazes it and it regrows. Both spawners are
  demoted to **seeders** (fill the deficit below the floor; bootstrap + extinction recovery only);
  `PopulationSize = 0` keeps the legacy unbounded planting so the model is opt-in per species. The
  cap resolves on the **Cell** (`Cell.ResolveFloraPopulation` / `ResolveFloraCap` / `IsFloraAtCap`),
  never off the config — flora has **five** producers (both spawners, reproduction, the freestyle
  `Microscene` conveyor, the Lifeform Matrix toy) and a cap one producer skips is two ceilings for
  one number. Reproduction is production, so it freezes with planting at Frenzy; a lowered cap stops
  producing and never culls.
  **A LATTICE species is an OCTAGON COLONY.** The gyroid is one plant no longer: its four danger
  block types close into rings of exactly **eight danger prisms** (measured off the bond table —
  the danger-only bond subgraph contains ONLY 8-cycles), and each ring is one lifeform — its
  **crystal at the ring's centre, never growing**, its territory the **24-prism patch** around it
  (8 danger ÷ the ⅓ danger fraction, exact). `GyroidOctagonData` carries the measured constants
  (own-centre offset per danger type; four neighbouring rings per type with a deterministic seed
  pose each), `GyroidOctagonRegistry` is the claim book, `AssembledFlora.OwnsLatticeSite` is the
  territory gate that makes plants TILE the surface instead of racing over it, and **reproduction
  is a POPULATION event**: a plant that COMPLETES its growth contributes its unclaimed
  neighbouring ring centres (full seed poses included) to `GyroidColonyFrontier`, and the whole
  population births exactly ONE daughter per fauna-wave period (`Cell.CurrentFaunaSpawnPeriod`,
  frame-staggered) at a uniformly RANDOM frontier site — random choice across every complete
  plant is what de-spheres the colony into the old single-gyroid's organic wander, now at the
  level of whole flora, and one-at-a-time from the main thread means no race by construction.
  Per-birth validation is a point lookup against the claim book, never a per-prism sweep.
  **Nothing in the code describes a gyroid**; the superstructure is emergent from
  local continuation — proven by simulating the exact algorithm (273 plants from one founder: a
  single connected gyroid, zero overlaps, bijective on the reference lattice, one crystal per
  octagon). Every table row is a MEASUREMENT pasted verbatim from the emit — the one shipped
  symmetry shortcut (z-mirroring DE/EG rows into GEs/EsD) twinned 12 of 16 seed rotations by up
  to 179° and cost five playtests; `Tools/Build/verify_gyroid_octagon_tables.py` now proves the
  SHIPPED file against a fresh reference walk, and a daughter asserts her handoff at birth.
  Mass is preserved (`cap × 24 ≈ the old single-plant budget`); the cost is **crystals**
  (one always-on heart collider per octagon), and `MaxLivePopulation` is the dial. Numbers are
  authored by `Tools/Build/author_flora_populations.py` (`--check`), never by hand; the tables
  regenerate via `Tools/Build/measure_gyroid_octagons.py`. **A colony's ceiling is its CELL'S
  VOLUME LADDER, not `MaxLivePopulation`** — the Blob (freestyle) cell's gyroid prisms are up to
  **6.9× nominal volume** *before* the level spread multiplies them another ~2.7×, so its seeded
  floor alone was 87% of `FrenzyEnterVolume` and the colony froze after one wave while its caps
  sat 19× further out. Reach for the ladder, not the population dial (`Docs/ECOSYSTEM.md §32.7`
  seventh pass). Full record: `Docs/ECOSYSTEM.md §32` (§32.7 the octagon colony).
  **A cell can BE its colonies**: the freestyle `Lattice` cell (`_SO_Assets/Cell Configs/Lattice
  Cell/`, `CellConfigs[9]` in Menu_Main) authors no `EnvironmentPrefab` at all — its whole
  environment is twelve lattice colonies (gyroid ×4, Schwarz P ×4, quasicrystal ×4, one per
  element) growing into one another, ~42,840 grown prisms and 1,080 plants at cap — the same
  order as the heaviest AUTHORED environment in the game (Atlantis ~69k), reached by growth
  rather than by a lay. It holds **exactly TWELVE SEEDS** — one
  founder per colony — and that is the cell, not a tuning value: **N founders do not build one
  superstructure N times faster.** Every founder is an independent lattice FRAME and independent
  frames cannot mate (`AssembledFlora` declines any site within `MisalignmentRadius` of a foreign
  frame, §34.8), so 30 founders per species built 30 small structures that stopped against each
  other — the same prism count, read as a scattered forest. Seeding one and letting reproduction
  extend it IS the mechanic; the seeder's only remaining job is extinction recovery. Note this is
  the case `author_flora_populations.py`'s `LATTICE_MIN_FOUNDERS = 4` guards, and why it does not
  apply: that floor protects the ELEMENT SPREAD of a config that ROLLS its element, and these
  twelve author one fixed element each — **a rule written about rolled elements must not be
  inherited by a fixed-element config**. Three more things it records: a per-plant
  budget is GEOMETRY (24-prism octagon / 36-site tile / a heart's tree cell, mean 59 struts),
  so **plant COUNT is the only lever** and
  `MaxLivePopulation` is simultaneously the crystal-collider count; `FrenzyExitVolume` must sit
  **above** the mature forest so a trail-caused Frenzy always releases with the forest intact;
  and the shipped per-element assets' `PlantRadiusCellFraction 0.2` (240u) is INSIDE the ~392u
  nucleus, where `Flora.ResolvePlantRadius` collapses to one degenerate shell — a multi-colony
  cell must author its own band. With the species spanning **159×** per prism (SchwarzP Charge
  0.85 → quasicrystal Mass 135), one more ordering is asserted: **no single colony's own volume
  ceiling may reach `FrenzyEnterVolume`**, or the heaviest species freezes the cell before the
  other eleven finish and the ladder describes one colony instead of the forest. `CAP` stays ONE
  number for all twelve because it is expressed in **plants** — territory units of each species'
  own lattice; equalising prism counts instead would shrink the quasicrystal's superstructure
  below its neighbours', which is the comparison the cell exists to make. It is the largest collider budget of any cell and is opt-in
  through the Cell Selector, and since §36.10 it is also **the boot world** — it replaced Blob at
  `CellConfigs[0]` and `Blob Cell Config` is deleted (only the config; the `Blob Cell` folder's
  SpawnProfile is still the population of all seven authored freestyle worlds). Booting into it is
  affordable because the cost ACCRUES: the cell opens with eight plants and no environment build,
  and reaches the collider line only after ~7 minutes of growth. That swap also split a conflated
  property — **`Cell.EnvironmentFreeConfig` means CHEAP TO BUILD, not EMPTY**, and the two had one
  test only because Blob satisfied both. The Wanderway run wants empty, so it now reads the new
  **`Cell.BareCanvasConfig`** (no `EnvironmentPrefab` AND a `SpawnProfile` with no flora and no
  fauna — a predicate over authored data, never a serialized field, falling back to
  `EnvironmentFreeConfig`), which resolves to the revived `Barren` config. General rule: *a
  property named for how something is BUILT will eventually be read as a claim about what it
  CONTAINS.* Numbers are authored by
  `Tools/Build/author_lattice_cell.py` (`--check`), which `author_flora_populations.py` hands the
  configs to by name prefix (`OWNED_ELSEWHERE`) rather than excluding them silently.
  Full record: `Docs/ECOSYSTEM.md §36`.
- **A lattice species grows on its SURFACE'S OWN TILE, never on a fitted grid.** A triply
  periodic minimal surface is intrinsically **hyperbolic**, so it admits no Euclidean lattice
  and a square-ish marching walk across it (step a tangent, Newton-project, repeat) can only
  approximate one — it accumulates drift, fronts arriving from different directions disagree
  (which is why such a walk needs a *quantized float* occupancy key), and it has no repeat unit,
  so nothing can be baked, measured or verified. Every TPMS does carry an exact non-Euclidean
  tiling, and for **Schwarz P** it is the hyperbolic **{6,4}** realized as *the patch of surface
  inside one half-period cube*: one flat point per cube, six planar-geodesic edges on the six cube
  faces, six 4-fold corners in the flat point's tangent plane, six neighbours = the six
  face-adjacent cubes. **Tile adjacency is simple-cubic adjacency**, so a prism's address is a
  `Vector3Int` + site index and occupancy is exact integer bookkeeping (`SchwarzPTileData`,
  `SchwarzPAssembler`). Two rules generalise to any future lattice species: **(1) never bake a
  rotation** — half the tile transforms are reflections and a quaternion carried through one is
  silently wrong (the gyroid paid five playtests for this, §32.7); bake positions and tangents,
  which transform correctly, and derive orientation from the closed-form gradient. **(2) a bond
  delta does not ADD** — carrying a canonical bond into tile `(i,j,k)` composes tile transforms,
  and `T_a∘T_b` is `T_(a−b)` when `a` is odd, so a delta is negated on every odd axis
  (`SchwarzPTileData.NeighbourTile`). Getting that wrong is invisible to every static check —
  offsets stay exact, every prism still lands on the surface, occupancy still keys cleanly — and
  shows up ONLY as geometry; it was caught by simulating a plant's growth to its authored budget.
  Measured by `Tools/Build/measure_schwarz_p_tile.py`, and the SHIPPED C# re-proved from the
  implicit function by `Tools/Build/verify_schwarz_p_tile_tables.py` (a separate script on
  purpose: the transcription from a proven measurement to the asset is the step neither the
  measurement nor code review can see). **A lattice species' PRISM SIZE belongs to the
  lattice, not to the plant**: `leafSize` is a footprint in the surface's tangent plane
  (local +z is the normal, +y the site's tangent), so whether plates sit flush is an exact
  OBB question against the measured site set — fit it (`Tools/Build/fit_schwarz_p_leaf_sizes.py`,
  which tests seam pairs too, since a size fitted inside one tile is wrong at the boundary),
  and note that a lattice species' prism must NOT scale with level — it scales the prism but not
  the lattice, so at 1.15 a level-5 plant's prisms are 1.749× the flush size and it
  interpenetrates itself (measured: 0 overlapping pairs at L1, 144 at L3, 212 at L5). That is
  now enforced in CODE by `Flora.PrismSizeFixedByGrowthRule` (true on `AssembledFlora`), so the
  config field needs no pinning. **A lattice can be SCALED only where "sameness" is an integer address, never
  a distance.** `FloraVariantTuning.LatticeScale` (sentinel **−1** = keep the prefab's) scales an
  element's whole lattice while keeping its topology and prism count identical to its elemental
  peers, and is pushed onto the assembler at all three creation sites because it is read BEFORE
  the first growth probe. It is **Schwarz P's alone**: there it scales `periodScale` AND
  `separationDistance` together, which leaves `ResolveLevel`'s argmin — the subdivision — exactly
  invariant (scaling either alone silently ships a DIFFERENT PLANT: Space landed on 6 sites per
  tile instead of 36 that way), and a prism's identity is an integer tile address, so no tolerance
  exists to invalidate. **The GYROID took two attempts** (`Docs/ECOSYSTEM.md §34.8`): its coherence
  rides distances written as ABSOLUTE world units sized at separation 3 — the mate-snap tolerance
  (0.3, compared against SQUARED distances, so scale²), the 40u mate-search radius, the
  reservation floor, and `AssembledFlora`'s lattice-misalignment gate (5.5u, at BOTH the grown-
  and seed-site checks) — so scaling the bond offsets alone moved every real distance out from
  under the gate that exists to catch twins, and the plant grew the offset parallel domains it was
  written to prevent. Every constant was individually correct; the defect was a RELATIONSHIP, which
  is why no static check saw it. `GyroidAssembler.ApplyLatticeScale` now moves the whole family
  together, and the invariant asserted is the **ordering** `reserve < misalignment gate < healthy
  closest pair` (constant 73% gate/healthy at every scale), proven over the shipped bond table by
  `Tools/Build/verify_gyroid_lattice_scale.py`. Three rules come out of it: **a coherence tolerance
  written as an absolute distance is an unstated dependency on the lattice it was measured
  against** — enumerate every snap/dedupe/reserve/twin-detect test before scaling anything, and
  assert the ordering rather than the values; **a prism only reads as STRETCHED against a lattice
  that stayed put** (scale both and it is just a bigger plant, so stretch on the native lattice
  FIRST, then scale); and **a uniform k× scale is a k³ VOLUME change** that lands straight on the
  cell's Frenzy ladder (§4.6) — the Space gyroid's 2× would have taken its ceiling from 13% to
  155% of the Blob cell's `FrenzyEnterVolume` at `60 × 2 × 2`, so its cross-section is held at 1
  (`60 × 1 × 1`, 39%; now 40 × 1 × 1, 26%): a lattice prism's THICKNESS is a volume dial with cubic leverage and is the
  cheapest correction when a scale-up overshoots the ladder. Spindles scale with the lattice (visible branch geometry spanning the
  gap); crystals deliberately do not. **The GYROID's branch is a MIRRORED PAIR of half-branches
  meeting at the prism** (`GyroidBranch.prefab`, gyroid only — Wall and Schwarz P keep the single
  `AssemblyBranch`, per the same no-side-effects rule as the lattice scale): one branch posed with
  its middle on the prism skewered every prism and showed different geometry on each side. The
  general rule it leaves behind is a CONTINUITY one — **a visual element animated through one
  serialized renderer reference cannot be split in two without splitting the animation with it**,
  or the second half POPS; `Spindle.additionalRenderedObjects` is that split, and it must stay an
  explicit list (a `GetComponentsInChildren` sweep would catch the health prism the flora parents
  under the spindle root and fade conserved mass with the branch). Full record:
  `Docs/ECOSYSTEM.md §34` (§34.5 the per-element prism fit, §34.7 the Schwarz P lattice, §34.8 the
  gyroid scale, §34.12 the branch pair).
  **The THIRD lattice species is APERIODIC — and its addressing is still exact integers.** The
  quasicrystal flora grows the icosahedral Ammann–Kramer–Neri tiling (the 3D analogue of the
  Penrose tiling — perfect long-range "forbidden" five-fold order that NEVER repeats) by
  **cut-and-project from Z⁶**: a vertex is six integers whose perp projection lands inside a
  rhombic triacontahedron window (closed-form test, doubles, margins seven orders above rounding),
  a prism is one EDGE (vertex + axis — every strut identical length, a theorem of the projection),
  and "sameness is an integer address" therefore holds with NO mirror composition (bond deltas
  honestly ADD upstairs in Z⁶ — the §34.2 trap cannot arise), no subdivision level and no absolute
  coherence tolerances (the §34.8 family cannot arise): `ApplyLatticeScale` is the single
  `edgeLength` dial. One plant = one **HEART** — a 12-coordinated vertex that is a local max of
  window margin (bare 12-coordination admits ADJACENT hearts; measured, rejected, kept as a
  negative control) — its crystal in a clear twelve-ray alcove (heart-adjacent struts hold back by
  the absolute `heartSeatInset`; hearts are never adjacent so at most one end of a strut holds
  back), and hearts self-organize to a CONSTANT 2.3840-edge spacing. **Territory is a TREE, not a
  radius**: owner(v) follows lex-least parent chains one graph-step closer to a heart — a pure
  integer function, cells connected by construction, measured ZERO unlaid edges where Euclidean
  Voronoi left 47 (graph-disconnected pockets). Reproduction walks the measured 50-delta
  heart-link census one birth per fauna-wave period (`QuasicrystalColonyFrontier` /
  `QuasicrystalHeartRegistry`, keyed (Cell, species), cleared at all three Cell teardown sites).
  Charge buys its 3x shield clearance with LENGTH (a 7u strut on a 24u edge, octahedra clear by 14%) rather than §35's uniform shrink, and
  `fit_quasicrystal_strut_sizes.py` OWNS its leaf — `fit_shield_clearance.py` does not know this
  species. Measured by `Tools/Build/measure_icosahedral_quasilattice.py`, the SHIPPED file
  re-proven by `verify_icosahedral_quasilattice_tables.py` (incl. the Euclidean-Voronoi and
  adjacent-hearts negative controls), populations by `author_flora_populations.py` (cap 14 — 14
  always-on heart colliders in Blob, ~13% of its Frenzy ladder). **A prism carries the authored leaf as its `localScale`, so NOTHING may be parented under one** — a non-uniform scale above a rotated child is a SHEAR, and `ReseedBranches` hung the next spindle off the prism instead of its spindle, so every lattice species grew skewed non-cuboid slivers from its first reseed (`Docs/ECOSYSTEM.md §37.9`). `Docs/ECOSYSTEM.md §37`.
- **An AUTHORED prism size widens its clamp; a GROWN one keeps it.**
  `PrismScaleAnimator.SetTargetScale` clamps PER AXIS into `[minScale, maxScale]` — serialized
  defaults `(0.5,0.5,0.5)`/`(10,10,10)`, which **363 of 404 prefabs** inherit unchanged — inside
  the setter, with no log and no return value. So a config saying `60 x 1 x 1` produced a
  `10 x 1 x 1` prism and *nothing reported the difference*: three passes of flora fitting
  measured, argued about and shipped sizes the engine never used (`Docs/ECOSYSTEM.md §34.9`),
  every Space strut rendered at 10 whatever was authored, and every cross-section under 0.5 was
  clamped UP. Anything that STATES a size calls `Prism.AdmitTargetScale(size)` first
  (`Flora.AddHealthBlock` and `PhyllotacticFlora.AddHealthBlock` do); anything that GROWS into
  the bound via `Grow()` leaves it alone. The per-prefab version of this workaround already
  existed (`SpawnablePrism` max 100, `Manta Prism` max x 40, `Dolphin Prism` max z 100), which is
  why it was easy to miss. **General rule: a silent clamp inside a setter is indistinguishable
  from a config that never applied, and it defeats every offline measurement — when a fitted size
  does not read on screen, check what the engine actually STORED before re-fitting.**
- **CHARGE armours its mass, and a SHIELD is 3x the prism it replaces.** Charge is the element
  whose leaves are SHIELDED, and that is a LAW rather than 15 copies of a number:
  `Flora.ResolveShieldPeriod` floors a Charge plant at `Flora.ChargeShieldPeriod` (1s), asked
  once from `LifeForm.Initialize` — the only point where the prefab, the rolled variant, the
  cell overrides AND the crystal carrying the element have all landed. Authoring cannot replace
  it: the cadence is authored per CONFIG while the element is ROLLED per plant, so a config with
  `SpreadElements` and an EMPTY `ElementPalette` (both Hesperides topiaries) applies its own
  `ShieldPeriod: 0` to a Charge roll and nothing writable on that asset reaches it. An authored
  cadence still wins (faster or slower is fine, *off* is not); **fauna are deliberately exempt** —
  the override is on `Flora`, not `LifeForm`, because a creature's body prisms are not the food
  web's mass. It is not immunity: `Prism.Consume` SHEDS a shield instead of eating the prism, so
  grazing a Charge plant costs two passes, and armoured mass also leaves the cell's targeting
  grids — Charge mass persists by being uninteresting. The **second** half is geometry:
  `PrismStateManager.ActivateShield` engages the CIRCUMSCRIBING octahedron
  (`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = 3` on the box HALF-extents, i.e. reaching
  **1.5 x leafSize** from the prism centre, 4.5x the volume), so **a shielded species must be
  fitted for a body 3x its prism's reach**. Measured by `Tools/Build/fit_shield_clearance.py`
  over each species' own shipped geometry, every element's plain prisms are already clear
  (plates `s*` 1.05-1.99 — a leaf nearly spans its bond but does not touch its neighbours), and
  it is tripling that reach that fuses a plant. Both lattice species are now fitted, uniformly
  so each leaf's aspect (its identity) is exact:
  **gyroid Charge `9 x 3.4 x 1.5` -> `4.28 x 1.62 x 0.71`** (was overlapping 1.89x oversize,
  826 of 15,880 near pairs) and **SchwarzP Charge `4.72 x 2.92 x 1` -> `1.88 x 1.16 x 0.39`**
  (2.25x oversize, 3,654 of 74,952) — plates read as a sparse skeleton, octahedra fill the
  lattice in. **Fit the PRISM, never the lattice**: scaling the lattice drags a whole family of
  absolute-distance tolerances with it (§34.8) while scaling the prism drags nothing, and a
  uniform k shrink is a k^3 volume change landing on the cell's Frenzy ladder (Blob flora
  ceiling 89% -> 74% of `FrenzyEnterVolume` — later Frenzy, so no ladder is re-authored).
  Colliders are unchanged: a shield swaps the MESH and the mass, never the collider. Two traps
  the fitter now CHECKS rather than assumes: a fitted axis below `HealthBlock.prefab`'s
  `minScale` 0.5 (SchwarzP's 0.39 thickness) survives only because `Flora.AddHealthBlock` calls
  `AdmitTargetScale` first, and **two fitters must not own one asset** —
  `fit_schwarz_p_leaf_sizes.py` sizes that species' plates FLUSH and now reads Charge's leaf
  back instead of reverting it. **Open, measured, deliberate:** the Hesperides SchwarzP topiary
  rolls all four elements from ONE authored leaf, so its Charge octahedra still fuse
  (`s* 0.513`); the gyroid topiary happens to clear at `1.003`. `Docs/ECOSYSTEM.md §35`.
- **Territorial permanence.** Take a cell, leave, it stays yours — the claim fauna cannot touch.
  In nucleus cells the permanent claim is the **nucleus interior** (fauna never consume it);
  exterior canopy/trail is deliberately contested churn (voracious any-domain grazing). In
  nucleus-less cells the legacy rule stands: fauna eat only opposing mass, so the dominant
  canopy is never culled. Oscillation lives in the fauna churn *under* that constraint.
- **Endogenous selection only.** When evolution lands, fitness is **survival itself**
  (starvation/predation/reproduction cost), never a designer-scored fitness function — the line
  between artificial life and a mere optimizer, identical to "don't cheat emergence."
  **LEVEL is the first place this bites, and it is now EARNED, never ROLLED.** Nothing picks a
  level at random; an ordinary spawn is level 1 (`InitialLevel` stays a deliberate MODE surface —
  Wildlife Liberation authors its per-cage tiers there, and the Lifeform Matrix bench its band).
  A plant earns a level per reproduction EVENT (`Flora.NotifyReproduced`,
  wired to both reproduction paths), a creature earns one per `FaunaConfigurationSO.FeedsPerLevel`
  feeds (`Fauna.NotifyFed`, on a counter separate from the reproduction quota — a feed pays into
  both and a birth must not reset progress toward a level; authored at 2× `FeedsPerOffspring`).
  The spawn-time `LifeformLevelSpread` roll is **deleted** — do not reintroduce it: handing a
  lifeform the record of a life it has not lived is the same mistake as a scripted fitness
  function. Acquired growth stays non-heritable (offspring inherit the ELEMENT and start at 1).
  **A LATTICE species levels but does NOT grow its leaf** (`Flora.PrismSizeFixedByGrowthRule`,
  true on `AssembledFlora`): gyroid/SchwarzP/wall bond at offsets measured in absolute local
  units, so a leaf that grows mid-life lays prisms the CI-verified bond table no longer
  describes — and the plant's earlier prisms are still the old size, so two prism sizes cannot
  tile one lattice. Three consequences to carry: that one; a flora species with
  `GrowthPerOffspring = 0` cannot breed and so
  is a level-1 forest forever (only 29 of 85 flora configs breed today); and a cell whose ladder
  was authored against the old spread's expected volume multiplier now boots that much lighter
  (Rampage: 4.3×, deliberately left as play-tested since Frenzy arriving LATER is the safe
  direction — `Tools/Build/rampage_intensity.py` prints the re-measure note). `Docs/ECOSYSTEM.md §33`.
- **Collider budget is a hard gate.** No ecology feature ships without stating its active-collider
  impact; respect the per-cell budget (collider-LOD by phase + Burst density-grid fauna queries,
  not `Physics.OverlapSphere`). See `Docs/ECOSYSTEM_MASTERPLAN.md §4`.
- **The Cell owns the environment — minigames don't build parallel systems.** When a mode needs
  ecology, wire the standard **Cell** (`CellConfigDataSO` + `SpawnProfileSO`) and configure it; do
  **not** ship a mode-local duplicate of something the Cell already owns. The Cell's `MembranePrefab`
  is the playfield-boundary read, its `CytoplasmPrefab` (a `SnowChanger`) is the drifting
  atmosphere/motes, its `NucleusPrefab` is the core marker, its `SpawnProfile` is the population, and
  its `PhaseThresholds` are the phase/aggression ladder — a bespoke arena edge cage, plankton
  particle system, per-mode spawner, or mode-local culler is the same class of mistake as cheating
  emergence. A mode owns only its **gameplay-bearing** structure (physics walls a ball must bounce
  off, goal portals, a midfield ring). Tune the ladder in **volume** — modes whose vessel lays
  low-volume prisms (Squirrel trail ≈ 3.1 vol each, ~⅕ the nominal 16) must author explicit
  `*EnterVolume`/`*ExitVolume` (else the ×16 count-derivation sets the ladder ~5× too high and fauna
  never hunt) and lower `SpawnProfile.FaunaFoodFloor` so herbivores seed against the thinner prey.
  Full table + rationale: `Docs/ECOSYSTEM_MASTERPLAN.md §5.1`.
  **Corollary — never hand-place a membrane/nucleus/cytoplasm in a scene.** The Cell instantiates
  each of them itself in `SpawnVisuals` from the config, and *only* that instance is tracked: every
  nucleus consumer (`NucleusWorldRadius`, `NucleusVisualWorldRadius`,
  `RefreshNucleusControlRadius`, `IsInsideNucleus`, `SetNucleusWorldRadius`) reads the Cell's
  private `nucleus` field, and the cleanup/swap paths read
  `membrane`/`nucleus`/`spawnedCytoplasm`. A scene-placed copy is therefore a *pure* duplicate — it
  renders on top of the real one and no bookkeeping can see it (three scenes shipped a coincident
  `Nucleus.prefab` this way). Same rule inside `Cell` itself: every spawn in `SpawnVisuals` plus
  `SpawnCytoplasm` is guarded on its own field, because a repeat `Initialize` pass overwrote the
  field and orphaned an untracked membrane/nucleus/`SnowChanger` that no cleanup path could reach.
  **Anything placing objects relative to the core during the SPAWN CHAIN must read
  `Cell.ExpectedNucleusWorldRadius`, not `NucleusWorldRadius`, and resolve the cell with
  `Cell.FindByRuntimeData` rather than `CellRuntimeDataSO.Cell`** — `Cell.Initialize` runs on
  `OnInitializeGame` behind `InitDelayMs` (1000 ms) while vessels spawn at `preSpawnDelayMs`
  (200 ms) and AI at `OnNetworkSpawn`, so both the field and the radius are still empty then. That
  race shipped once: the player spawn ring silently fell back and put everyone 70u from the centre,
  inside the nucleus. **To change a
  Cell-owned visual's size, author a new `CellConfigDataSO` pointing at a resized prefab** (Scurry's
  `Scurry Cell Config` → `HalfNucleus.prefab`) — do not place, scale, or duplicate one in a scene.
  Guarded by **FrogletTools > Ecology > Audit Cell-Owned Visuals**, which also sweeps the dead
  `Cell` overrides scenes accumulate (72 of them across 12 scenes on the day it was written).
  Note a scene backdrop is NOT this: `SkyboxModel` (`MembraneBase`/`BigMembraneVariant`) is a
  different asset from any config's `MembranePrefab` and is the only geometry in the tool scenes.
- **A world you load is opt-in, and swapping one is ACTIVE removal — not decay.** An authored
  `EnvironmentPrefab` costs a multi-second veiled build, so a scene may boot
  `CellTypeChoiceOptions.EnvironmentFree` (the first config with no environment — Menu_Main does)
  and let the heavy worlds be chosen on demand. The one runtime entry point is
  `Cell.RequestCellSwap` (the freestyle **Cell Selector** toy): it **suctions** the old world away
  and **blooms** the new one in behind the standard `EnvironmentLoadVeil` — continuity of existence
  holds at both ends — and it removes mass only because a player flew into a station and asked, the
  same explicit, active event class as a scene load. **Do not** turn this into anything that runs on
  its own: no auto-rotate, no idle re-roll, no "the cell has been up too long" reset. That would be
  the timed culler §0 rejects, wearing a new costume. Detail: `Docs/ECOSYSTEM.md §19`.

**Protocol:** (1) restate which invariants the change touches + confirm none are violated;
(2) confirm at genuine forks (AskUserQuestion); (3) implement surgically, config-driven; (4) state
the collider-budget impact + exact in-editor verification. The `/ecology` skill encodes this.

## About This Project

Cosmic Shore is a multigenre space game ("the party game for pilots") developed by Froglet Inc., a Delaware C-corp based in Grand Rapids, MI. Different vessel classes embody gameplay from different genres to connect players across demographics.

### Vessel Classes

The game features 11 vessel class types (defined in `Assets/_Scripts/Data/Enums/VesselClassType.cs`):

| Vessel | ID | Genre / Role |
|---|---|---|
| **Manta** | 1 | Feature-complete playable vessel |
| **Dolphin** | 2 | Feature-complete playable vessel |
| **Rhino** | 3 | Feature-complete playable vessel |
| **Urchin** | 4 | Playable vessel — chain-reaction spikes + prismscape rider + a projected rail (see `_Scripts/Controller/Vessel/R_VesselActions/URCHIN_CHAIN_SPIKES.md`, `URCHIN_TRAIL_RIDER.md`, `URCHIN_TRACK_PROJECTOR.md`). Elemental map complete; **HUD prefab not yet authored** |
| **Grizzly** | 5 | Playable vessel (AI in progress) |
| **Squirrel** | 6 | Racing/drift — vaporwave arcade racer, tube-riding along player-generated trails (F-Zero / Redout feel) |
| **Serpent** | 7 | Playable vessel with dedicated HUD |
| **Termite** | 8 | Planned |
| **Falcon** | 9 | Planned |
| **Shrike** | 10 | Planned |
| **Sparrow** | 11 | Shooter — arcade space combat with guns and missiles |
| **Scarab** | 12 | Party/sport — juke-dash ball handler; forges omni crystals into balls with its skimmer (see `_Scripts/Controller/Vessel/R_VesselActions/SCARAB.md`, `_Scripts/Controller/Arcade/SCARABSCRAMBLE.md`) |

Meta values: `Any (-1)`, `Random (0)`

**Use the `/vessel` skill for ANY vessel-class work** — new vessels, abilities/executors,
elemental ability maps + level-5 upgrades, HUD rows/hints/gauges, petal bars, hull morphs/rig
swaps, impact/skimmer containers. It loads the fleet-wide vessel contract, the audit tools, and
the per-subsystem checklists so the requirements are not re-derived per vessel.

### Team Domains

Team ownership is tracked via the `Domains` enum: `Jade (1)`, `Ruby (2)`, `Blue (3)`, `Gold (4)`. **Blue is the "no team / not yet picked / neutral entity" sentinel** and is never present in `GameDataSO.ActiveDomains` (the playable set is `{Jade, Ruby, Gold}`, indices 0..2). Code that previously used `Domains.None` or `Domains.Unassigned` (both removed) now uses `Domains.Blue` for the same "no specific team" semantic — neutral mines, uncommitted crystals, the wildcard "any team" density-grid bucket, and players who haven't yet picked a domain.

Cross-client domain sync is driven entirely by `Player.NetDomain` (server-write `NetworkVariable<Domains>`). Its replication callback `Player.OnNetDomainChanged` propagates every change to:

1. The local `Player.Domain` mirror (read by `IVesselStatus.Domain` and many UI consumers).
2. `RoundStats.Domain` — a local mirror kept in sync on EVERY peer (its `n_Domain` NetworkVariable was retired, see `Docs/ScoringSystem/BUGS.md` B10) — keeps scoreboards, end-game controllers, and `GameToastAPI` colorers live across modal re-picks, `NormalizeUnassignedHumans` rerolls, and shape-mode `SetDomain`.
3. The vessel materials via `ShipHelper.SetShipProperties(_vesselThemeManagerData, Vessel)` — the theme reference is stashed onto `Player` by `ClientPlayerVesselInitializer.InitializePair`/`ReInitializePair` at vessel spawn/swap.

Do not snapshot domain at component-creation time. Either subscribe to `Player.NetDomain.OnValueChanged` directly or read the live `Player.Domain` mirror each time you need it. `RoundStats.Domain` is also live (after Phase 5) so end-game UIs can keep using it.

**Never write domain state from client code.** `NetDomain` is Server-write (clients request picks via `Player.RequestSetDomain_ServerRpc`), and the `Player.Domain` / `RoundStats.Domain` mirrors sync ONLY from `NetDomain` (`InitializeForMultiplayerMode` + `OnNetDomainChanged`) — a local overwrite desyncs that machine until the next NetDomain delta (`Docs/ScoringSystem/BUGS.md` B10/B11). The menu's Jade reset is server-side in `MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync` (the deleted client-local `ApplyMenuDomain` was the root of `Docs/PartySystem/BUGS.md` B9). `ShipHelper.SetShipProperties` is init-aware: it swaps the material references and, once `VesselCustomization.Initialize` has painted the hull, also re-applies the mesh material — so a replicated domain change fully re-themes the vessel with no extra calls.

`ServerPlayerVesselInitializerWithAI.GetBalancedDomain` ties break by `ActiveDomains` enum order (Jade → Ruby → Gold), not RNG, so identical inputs produce identical AI distributions across machines without needing a shared seed.

### Tech Stack

- **Engine**: Unity 6+ with URP (Universal Render Pipeline) — `com.unity.render-pipelines.universal` 17.0.4
- **Language**: C# with UniTask (`com.cysharp.unitask`) for async
- **Architecture**: ScriptableObject-driven config separation + SOAP (Scriptable Object Architecture Pattern) for cross-system communication
- **Networking**: Unity Netcode for GameObjects (`com.unity.netcode.gameobjects` 2.5.0)
- **Camera**: Custom plain-transform rigs — `CustomCameraController` (gameplay) + `MainMenuCameraController`/`MenuCameraConfigSO` (menu) — with per-vessel `CameraSettingsSO` assets. Cinemachine 3.1.2 remains installed for tool scenes only (Recording Studio); the menu and gameplay cameras do not use it
- **VFX**: VFX Graph 17.0.4, custom HLSL shaders, Shader Graph
- **Input**: Unity Input System 1.14.2 with strategy pattern (`IInputStrategy` → platform-specific implementations)
- **Audio**: FMOD Studio (`Assets/Plugins/FMOD`, `FMODUnity`) — every sound is an inspector-exposed `EventReference`, never a hardcoded/temp event. See "Audio (FMOD)" under Architecture Patterns. (The inert `Assets/Wwise/` husk left over from an earlier middleware evaluation was deleted 2026-07-17; no first-party code ever referenced `AkSoundEngine`. Do not reintroduce it.)
- **Haptics**: NiceVibrations for mobile/gamepad haptics. **Two everyday feels**, both local-human-pilot-only (skim-pulse reward + prism-punish thud), plus **one rare alert shake** fenced to match-changing events (only Ribcage's two progress-milestone rungs today) and **one continuous spray buzz** fenced to a held full-auto trigger (only the Sparrow's guns today), which climbs in strength and cadence as accuracy decays and sits at the BOTTOM of the priority order (`alert > punish > skim > spray`) so a texture can never cut off an event; everything else is silent. See `Docs/HAPTICS.md`.
- **Animation**: Timeline 1.8.9, DOTween for procedural animation
- **DI**: Reflex (`com.gustavopsantos.reflex` 14.1.0) for dependency injection
- **Performance**: Unity Jobs + Burst Compiler, Adaptive Performance 5.1.6, DOTS Entities 1.4.2 (installed, incremental adoption)
- **Backend**: PlayFab SDK (legacy, inert), Unity Gaming Services (Analytics, CloudSave, Leaderboards, Multiplayer, Purchasing 4.12.2, Ads 4.12.0)
- **Testing**: Unity Test Framework 1.6.0 (NUnit-based)
- **Target**: Mobile-first with PC/console expansion

## Project Structure

```
Assets/
├── _Scripts/                  # All first-party code (~1,100 C# files)
│   ├── Controller/            # Gameplay systems (~536 files)
│   │   ├── Vessel/            # Vessel core: VesselStatus, Prism, Trail, VesselPrismController, VesselActions/, R_VesselActions/
│   │   ├── Environment/       # Cells, crystals, flora/fauna, flow fields, warp fields, spawning
│   │   ├── ImpactEffects/     # Impactors (11 types) + Effect SOs (20+ types)
│   │   ├── Arcade/            # Mini-game controllers, scoring, turn monitors
│   │   ├── Projectiles/       # Projectile systems, guns, mines, AOE effects
│   │   ├── Managers/          # PrismStateManager, PrismTimerManager, PrismSpatialIndex, ThemeManager
│   │   ├── IO/                # Input strategies (Keyboard, Gamepad, Touch)
│   │   ├── Animation/         # Per-vessel animation controllers
│   │   ├── Camera/            # CustomCameraController, CameraSettingsSO, ICameraController
│   │   ├── Multiplayer/       # Netcode: ServerPlayerVesselInitializer (+ WithAI, Menu variants), ClientPlayerVesselInitializer, MultiplayerSetup, MenuCrystalClickHandler, DomainAssigner, NetworkStatsManager
│   │   ├── Player/            # Player (NetworkBehaviour), IPlayer, RoundStats
│   │   ├── Prisms/            # PrismFactory
│   │   ├── Assemblers/        # Gyroid/wall assembly systems
│   │   ├── Party/             # HostConnectionService, PartyInviteController, FriendsInitializer
│   │   ├── AI/                # AIPilot, AIGunner
│   │   ├── FX/                # Visual effects controllers
│   │   ├── ECS/               # DOTS entity components
│   │   ├── XP/                # Experience point controllers
│   │   └── Settings/          # Runtime settings
│   ├── System/                # Application-level systems (~126 files)
│   │   ├── Bootstrap/         # BootstrapConfigSO, SceneTransitionManager, ApplicationLifecycleManager
│   │   ├── Playfab/           # PlayFab integration (Auth, Economy, Groups, PlayerData, PlayStream)
│   │   ├── Instrumentation/   # AnalyticsServiceFacade (UGS Analytics, single writer)
│   │   ├── Runtime/           # Dialogue runtime (DialogueManager, models, views, helpers)
│   │   ├── RewindSystem/      # Rewind/replay functionality
│   │   ├── Audio/             # AudioSystem (FMOD events + legacy music AudioSources)
│   │   ├── LoadOut/           # Vessel loadout configuration
│   │   ├── CallToAction/      # Promotional/CTA system
│   │   ├── Squads/            # Squad management
│   │   ├── Quest/             # Quest system
│   │   ├── UserAction/        # User action tracking
│   │   ├── UserJourney/       # Funnel analytics
│   │   ├── Favorites/         # Favorites system
│   │   ├── Ads/               # Ad integration
│   │   └── Architectures/     # Shared architectural base classes
│   ├── UI/                    # Game & app UI (~188 files)
│   │   ├── Controller/        # VesselHUD controllers (Manta, Rhino, Serpent, Sparrow)
│   │   ├── View/              # VesselHUD views (all vessel types + Minigame, Multiplayer)
│   │   ├── Interfaces/        # IVesselHUDController, IVesselHUDView, IMinigameHUDController, IScreen
│   │   ├── Elements/          # Reusable UI components (NavLink, NavGroup, ProfileDisplayWidget, etc.)
│   │   ├── Views/             # Screen/view implementations (VesselSelection, Profile)
│   │   ├── Modals/            # Modal dialogs (Settings, Profile, PurchaseConfirmation)
│   │   ├── Screens/           # Screen containers
│   │   ├── ToastSystem/       # ToastService, ToastChannel, ToastAnimation
│   │   ├── Notification System/ # Push notification UI
│   │   ├── GameToastSystem/   # In-game toast feed (situation SOs, per-mode configs, idle hints)
│   │   ├── FX/                # UI visual effects
│   │   └── Animations/        # UI animations
│   ├── Data/                  # Models & enums (~29 files)
│   │   ├── Enums/             # VesselClassType, Domains, ResourceType, ShipActions, InputEvents, etc.
│   │   └── Structs/           # DailyChallenge, GameplayReward, TrainingGameProgress
│   ├── ScriptableObjects/     # SO definitions & SOAP types (~70 files)
│   │   ├── SOAP/              # Custom SOAP types (16 subdirectories)
│   │   └── SO_*.cs            # Game data SOs (Captain, Vessel, Game, ArcadeGame, Element, etc.)
│   ├── Utility/               # Effects, PoolsAndBuffers, DataContainers, DataPersistence, ClassExtensions
│   ├── DialogueSystem/        # Dialogue editor tools, animation, SO assets
│   ├── Editor/                # Editor tools (CopyTool, shader inspectors, scene utilities)
│   ├── Tests/                 # Edit-mode unit tests
│   ├── Integrations/          # PlayFab SDK integration
│   └── SSUScripts/            # Specialized subsystem scripts
├── _SO_Assets/                # ScriptableObject asset instances (48+ subdirectories)
├── _Prefabs/                  # CORE, Cameras, Characters, Environment, Pools, Projectile, Spaceships, Trails, UI Elements
├── _Scenes/                   # Game scenes organized by type
├── _Graphics/, _Models/, _Audio/, _Animations/
├── FTUE/                      # First-Time User Experience / Tutorial system
├── Plugins/                   # Obvious.Soap, Demigiant (DOTween), NativeShare, etc.
├── PlayFabSDK/                # Backend SDK (legacy)
├── NiceVibrations/            # Haptic feedback
└── SerializeInterface/        # Custom [RequireInterface] attribute support
```

Note: A vestigial `_Scripts/Game/` directory exists containing mostly non-code assets (compute shaders, input action mappings, material files, and the `PRISM_PERFORMANCE_AUDIT.md`) plus two live scripts pending relocation (`Game/Environment/CapsuleMembrane.cs` + `CapsuleMembraneAnimationSO.cs`, consumed by `Cell`). All other C# code has been reorganized into the directories listed above.

### Assembly Definitions

Most first-party code still compiles in Unity's default assembly, `Assembly-CSharp`. **That is a
state being actively unwound, not the design.** The monolith means every one-line edit recompiles
~1,481 files, and nothing enforces a dependency direction. The split proceeds bottom-up, one leaf
assembly at a time — full plan, measurement protocol and phase-2 candidates:
**`Docs/ASSEMBLY_SPLIT.md`**.

| Assembly | Scope |
|---|---|
| `CosmicShore.Data` | `_Scripts/Data/` — enums, structs, small interfaces. The first extracted leaf: depends on no first-party code |
| `CosmicShore.PlayFabTests` | PlayFab integration tests |

**The rule that makes extraction safe, and safe in only one direction:** a predefined assembly
(`Assembly-CSharp`, `Assembly-CSharp-Editor`) **automatically references every auto-referenced
asmdef**, so moving code OUT into an asmdef is invisible to everything left behind — no `using`
change, no reference wiring, no big-bang. An asmdef can never reference a predefined assembly, so
code that still reaches back into gameplay cannot be extracted at all. Extraction therefore works
**bottom-up from the leaves**, and the compiler is the forcing function rather than a review
checklist.

Two things that do **not** survive an assembly boundary and must be checked before drawing one:
`internal` members and `partial` types (invisible / illegal across assemblies), and extension
methods (only found when their namespace is `using`-ed). A namespace *may* span assemblies — so
relocating a file that blocks a boundary is a `git mv` of the file **and** its `.meta`, never a
rename. Renaming is what breaks scene and prefab references; changing which assembly a class
compiles into does not.

> An earlier version of this table also listed `CosmicShore.Bootstrap.Tests`,
> `CosmicShore.Multiplayer.Tests` and `CosmicShore.Tests.EditMode`. **Those assemblies never
> existed.** The tests therefore fell into `Assembly-CSharp` and shipped into the player, where the
> IL2CPP linker hit their NUnit attributes and killed the Windows build (`error IL1005` → `Failed
> to resolve assembly: 'nunit.framework'`). Fixed by moving every test under an `Editor/` folder;
> see below.

### **Tests live under an `Editor/` folder, not in an asmdef — until their dependencies are extracted.**

Every first-party test is under a folder literally named `Editor`, which puts it in
`Assembly-CSharp-Editor`:

| Suite | Location |
|---|---|
| General edit-mode tests | `_Scripts/Tests/Editor/` |
| Bootstrap tests | `_Scripts/System/Bootstrap/Tests/Editor/` |
| Multiplayer tests | `_Scripts/Controller/Multiplayer/Tests/Editor/` |
| PlayFab tests | `_Scripts/System/Playfab/PlayFabTests/` (has its own `.asmdef`) |

Two properties make this work, and both are load-bearing:

1. `Assembly-CSharp-Editor` is **never included in a player build**, so NUnit never reaches the
   IL2CPP linker.
2. It **implicitly references `Assembly-CSharp`** — and every auto-referenced asmdef — so tests see
   both the gameplay types still in the monolith and every extracted assembly.

**Do not author a test `.asmdef` for a suite that touches gameplay types.** An asmdef cannot
reference `Assembly-CSharp`, so such a test would be blind to the very types it tests. That
constraint is almost certainly why the three documented assemblies above were never created.

**The constraint is a function of where the code under test lives, not a permanent law.** A suite
whose dependencies are *entirely* inside extracted assemblies (`CosmicShore.Data` today) can have a
real test asmdef referencing those plus the test-runner assemblies — `CosmicShore.PlayFabTests` is
already this shape. Take that per-suite as its dependencies come out; never as a project-wide flip.

**A new test file must be created under an `Editor/` folder** unless it meets the bar above. A test
anywhere else compiles into the player and breaks the Windows build at the linker stage, which the
compile tier and the edit-mode suite are both structurally blind to; only a player build catches it.

**Adding a new runtime assembly** is not a casual change — it alters the build for every branch in
flight. Follow the checklist in `Docs/ASSEMBLY_SPLIT.md` § "Adding an asmdef": prove the folder is a
leaf, check the three things that don't cross a boundary, `autoReferenced: true`, one asmdef per
commit, and run `validate_project.py` + `check_conditional_compilation.py`.

Third-party assemblies: `Obvious.Soap`, `PlayFab`, `Lofelt.NiceVibrations`, `NativeShare.Runtime`

### Scenes & Game Modes

Full scene inventory, `GameModes` enum reference, controller hierarchy, and launch
pipeline: `Docs/SCENES.md`. The always-true rules:

- **No single-player scenes**: solo play is a multiplayer game whose party is one host
  (eager Relay session + AI backfill via `ServerPlayerVesselInitializerWithAI`). The
  single-player controller branch and non-networked spawn path were deleted 2026-07-20.
- **GameModes IDs are never reused** (`Assets/_Scripts/Data/Enums/GameModes.cs`): retired
  IDs stay annotated do-not-reuse (7, 8, 26, 28-32 carry no enum member at all; highest is
  `Benchmark(44)`). `Tournament(36)` is the session-level meta (player-facing name
  "Maelstrom") - it draws a random mode + intensity per round from an AUTHORED pool
  (`TournamentData.asset`'s `GameQueue`: Skim Race, Joust, Crystal Capture, Rampage, Peel
  the Cage, Scarab Scramble, The Bends) and chains them via sequential `Single` loads.
  Adding a mode is one asset edit - every consumer is pool-length-agnostic - but a
  candidate must be domain-scored (standings fold through
  `ScoringRuleSO.ResolvePlacementOrder`), have its scene in Build Settings, and have a
  player/domain range containing the Maelstrom card's 2-4 players / 2+ domains, which is
  NOT re-checked at draw time. A vessel-locked mode needs no extra wiring
  (`GameDataSO.SyncFromArcadeGame` publishes the drawn card's `Vessels` and clamps the
  lobby pick); note `PickRandomModeIndex` avoids the previous INDEX, not the previous
  vessel or arena - Rampage and The Bends share both. Freestyle lives ONLY in Menu_Main as
  the lava lamp - `Freestyle(7)` and `MultiplayerFreestyle(28)` are retired and must not be
  reintroduced. **Exception - `Rampage(2)`**: the legacy solo
  ID was deliberately *repurposed* as a live multiplayer party game (the destruction race,
  Scurry's destructive analog; see `_Scripts/Controller/Arcade/RAMPAGE.md`). It is the one
  reused ID; do not treat mode 2 as retired. `Ribcage(39)` (display name "Peel the
  Cage") is the Rhino-only layered-cage destruction race - first domain to destroy the
  hostile-prism target (2000, the same metric as Rampage) wins, and intensity is how
  many rinds you peel (see `_Scripts/Controller/Arcade/RIBCAGE.md`).
  `WildlifeLiberation(40)` and `DogFight(41)` are the two Sparrow-only modes (see
  `WILDLIFE_LIBERATION.md` / `DOGFIGHT.md`). `Bends(42)` (display name "The Bends") is the
  **Dolphin-only debuff duel** - a dogfight with no guns: the only weapon is the Dolphin's
  crystal blast (armed only by skimming, fired only by touching a crystal) and the only
  thing that scores is catching an OPPOSING pilot in it, for one 4-second all-element
  debuff = one "bend"; race to 3 by DOMAIN, on Rampage's cactus-forest cells referenced
  rather than forked (see `BENDS.md`). `ScarabScramble(43)` is the **Scarab-only
  hoop-court party game** - the accessible sibling of Astro League: fly your SKIMMER
  through an omni crystal and it becomes your ball in place, permanently your colour; roll
  it through a hoop and your DOMAIN scores, goals stop nothing, and there are no own goals
  (see `SCARABSCRAMBLE.md`). `Benchmark(44)` is NOT an arcade mode - it
  is the Settings > Run Benchmark stress-test context (no card, no scoring, endless),
  set by `BenchmarkSceneLauncher` so mode-keyed consumers resolve honestly instead of
  borrowing a retired id. (It was authored 39, then 40, then 42, on Ys-bleeding-edge; it
  landed at 44 on the merge with bleeding-edge, which had already shipped 39/40/41/42/43.
  Safe to renumber because the id is only ever set in code.)
- **Platform rules the newest modes established** (a mode is where these surface; they bind
  everywhere - full detail in `BENDS.md`, `SCARABSCRAMBLE.md`, `DOGFIGHT.md`):
  - **A ward has a SCOPE, because "immune" is not one promise.** An elemental debuff names
    its SOURCE CLASS (`ElementalDebuffSources`: `DangerPrism`/`Explosion`/`VesselContact`/
    `Other`) and a ward holds a MASK, so an ability earned against the arena (the Dolphin's
    Time-5 Drift Ward, authored against danger prisms) cannot cancel a weapon another pilot
    aimed. Two invariants keep it honest: `All` is `~0` (a serialized "everything" ward must
    cover a class added later), and an unclassified debuff falls in `Other` (so forgetting
    to classify fails safe rather than silently widening a narrow ward).
  - **A score must not be able to disagree with the effect it is scoring** - an elementally
    immune victim takes no drain, so a scoring effect that pays for a debuff is authored to
    require a debuffable victim. Immunity then becomes real counter-play for free.
  - **Counted platform-wide, scored in one mode.** A combat hit is reported everywhere and
    weighted only by the mode's own `ScoringRuleSO.PointsForCombatHit` (0 elsewhere). The
    same split gave `AOEConicExplosionImpactorDataContainer` its (previously unwired)
    elemental debuff + combat-hit report, so the Dolphin's blast now debuffs a pilot it
    engulfs in EVERY mode.
  - **A blast that is REPLAYED onto a second machine double-credits.**
    `NetworkCrystalManager.ReplayVesselCrystalEffects` re-runs a server-resolved collection
    on the owning client, so one blast exists on two machines and a per-machine latch cannot
    see across the wire. The gate is `IPlayer.IsNetworkOwner` - never `IsLocalUser`, which
    would drop every AI's hits.
  - **A validator that tests for one enum member and collapses the rest onto a default
    encodes the enum's current SIZE.** Use `Enum.IsDefined`; a mis-filed
    `CombatHitClass.Debuff` let a client fight a whole match and score nothing.
  - **A comeback rate is a function of the TARGET, and re-targeting a mode silently kills
    it** - `bonusLevels = deficit x rate`, so a 60 -> 3 target change makes the same rate
    worth a thirtieth of a level. The mode's generator FAILS the build if a
    quarter-of-target deficit stops buying a whole element level.
  - **Only an OMNI crystal may be spent on a mode's props.** An elemental crystal is the
    platform's element economy, so a mode that consumes one denies the pilot a level they
    flew through; elemental crystals fall through to the hull and collect normally.
  - **A rule enforced at one PRODUCER can only ever see that producer.** A ball enters play
    two ways (forged from a crystal, knocked loose out of the nucleus), so a forge-time cap
    was blind to half of them; the ceiling counts what is actually IN the cell
    (`AstroLeagueBall.cellBallLimit`) and nothing is ever culled on a clock.
  - **When a vessel's ability places an order of magnitude more mass than a mode's own
    traffic, that mode's volume ladder stops describing the mode and starts describing the
    ability** - re-derive the ladder from the ability, and name what the old reading paid for.
  - **The AI needed a narrower hook than steering.** `AIPilot.SetExternalTargetProvider`
    replaces crystal seeking outright and would disarm every AI in a mode whose weapon is
    fired BY a crystal, so `AIPilot` also carries `SetDriftLookTargetProvider` - an opt-in
    override for the DRIFT LOOK-DIRECTION alone (where the nose points once the course is
    already locked on the objective).
  - **A cell with NO NUCLEUS must author `CrystalManager.noNucleusSpawnRadius`** - that
    field is the fallback in `GetAnchorlessSpawnRadius` (nucleus -> `noNucleusSpawnRadius`
    -> the crystal's own `SphereRadius`), so without it every omni crystal spawns on the
    arena's exact centre and reads as the objective. The fix is the radius, never switching
    the crystal off.
  - **A weapon is born at its MUZZLE, so a muzzle transform is gameplay, not decoration** -
    a drifted muzzle spawns every round past a close-range target and the whole fire mode
    does nothing, with correctly-wired scoring.
- **Controller skeleton**: `MiniGameControllerBase` -> `MultiplayerMiniGameControllerBase`
  -> `MultiplayerDomainGamesController` -> per-mode controllers (server-authoritative
  turn/round/game flow via ClientRpc), incl. `RampageController` + `RibcageController`
  (both prisms-destroyed scoring), `BendsController` (bends by domain) and
  `ScarabScrambleController` (goals by domain).
- **Launch pipeline**: `SO_ArcadeGame` (static config) -> `ArcadeGameConfigureModal` ->
  `GameDataSO` (SOAP runtime state) -> `SceneLoader.LaunchGame()` (host-driven Netcode
  scene load) -> scene-placed controller; config syncs to clients in
  `MultiplayerMiniGameControllerBase.OnNetworkSpawn()`.

### Documentation Index

| Document | Location | Content |
|---|---|---|
| `CLAUDE.md` | Project root | Architecture, patterns, systems reference |
| `SCENES.md` | `Docs/` | Complete scene inventory, game modes, launch pipeline |
| `HAPTICS.md` | `Docs/` | The two-feel haptics policy (skim-pulse reward + prism-punish thud), the one-clip priority/rate-limit gate, runtime `.haptic`+GamepadRumble clip generation, local-pilot gating, and in-editor verification. **Read before adding or re-enabling any haptic.** |
| `THREADING.md` | `Docs/` | UniTask / SyncContext threading rules, `.AsMainThread()` contract, `MainThreadDispatcher`, canary, history |
| `PALETTE.md` | `Docs/` | The domain colour set (`SO_ColorSet`): which asset is live, what `_DarkColor`/`_BrightColor` actually are (prism **base face** vs **fresnel rim** — "Outside/Inside" is a misnomer), the **linear-HDR colour-space rule** (Rec.709/CIELAB apply directly; scaling a pair changes brightness but NOT contrast), the measured shielded-tier contract (ΔL\* 29.34 across all domains), and the **danger tier** — which has no colour fields of its own: it composes the domain's *shielded* base face with the shared domain-independent `EnvironmentColors.Danger` rim. **The invariant that outranks every per-tier contract (§4.0): in every tier, on every domain, the rim is brighter than the base** — it held on nine of twelve tier×domain pairs by accident rather than by rule, and each of the three violations was separately rationalised as a local trade-off before being recognised as one defect (danger was inverted on all three domains at ΔL\* −3.8; it is now +9.30). Two warm-hue traps are recorded there and are the reason those violations were invisible: **absolute `C*` is not comparable across hues** (equal chroma leaves a warm hue's blue channel starved, so judge by screen saturation *after clipping*, §4.1), and **authoring a peak channel silently overshoots `L*` at a warm hue** — set lightness against the other tiers first, then solve the channels. §2.2 covers the **crystal tier**: crystal colour signals WHO MAY COLLECT (element is shape, never colour), a free pickup wears the lime CTA, and in every crystal shader the composition is `lerp(dull, bright, (1−N·V)⁴)` — so at that fresnel power **`DarkCTA` paints ~93% of the crystal and `BrightCTA` is a 2.5% hairline**; tune the body, not the rim. It also records the finding that outranks §3 in practice: **gameplay bloom is CLAMPED at 0.5** (URP's default is 65472), so bloom saturates at max channel 0.5 and 56 of the 86 authored colours bloom identically — brightness above the clamp is a **dead dial**, and §3's "channels above 1.0 bloom" is false as shipped. Within the clamp, extra bloom is bought with bright **area**, not intensity. **Read before editing any `*BlockColor` or `*CTA` field, changing which field feeds a prism or crystal material, or trying to make anything glow harder.** |
| `CONDITIONAL_COMPILATION.md` | `Docs/` | `#if UNITY_EDITOR` / `DEVELOPMENT_BUILD` rules, the two safe guard patterns, and the `Tools/Build/check_conditional_compilation.py` CI gate. **Read before writing ANY script that uses a compilation guard or the `UnityEditor` namespace** — this class of mistake has broken the automated build repeatedly and is invisible in the Editor. |
| `ASSEMBLY_SPLIT.md` | `Docs/` | Splitting the single-assembly monolith: the one-way rule that makes leaf extraction safe (predefined assemblies auto-reference asmdefs; the reverse is impossible), the compile-timing measurement protocol + structural baseline, what phase 1 extracted, and the measured phase-2 candidates with what blocks each. **Read before adding ANY `.asmdef`, relocating a file to satisfy an assembly boundary, or authoring a test asmdef.** |
| `SPATIAL_INDEX.md` | `Docs/` | `PrismSpatialIndex` — THE canonical spatial index of prism mass (Burst AOE queries, growth occupancy reservations, bucket grid). **Read before adding any spatial query against prisms.** |
| `PRISM_ANIMATION.md` | `Docs/` | **The clock-material law (LOCKED, STRICT — no legacy fallback)**: no prism may need multiframe CPU updates to animate — animation = pool-pull + one initial-conditions stamp + GPU runs the course off the shader clock + one scheduled end-state swap; colliders and gameplay state go FINAL at the start. There is NO CPU animation tier to fall back to: an unwired graph fails LOUD (`PrismClockDiagnostics`) and the visual snaps until the §4.4 wiring lands (in-editor checklist: `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md`; validator: FrogletTools > Ecology > Prism Animation). Full audit of every prism update path+ migration tracker. §4.7 documents the ONE sanctioned shape for view-dependent prism visuals — a GLOBAL uniform published once per frame, never a per-prism write — and states the camera↔vessel occlusion corridor as a **PLATFORM LAW** with the four layers that make it un-authorable to skip. **Read before touching any prism visual, animation, or state transition.** |
| `SPEED_TUNNEL.md` | `Docs/` | **The speed-tunnel PLATFORM LAW**: every vessel's camera FOV narrows and Panini relaxes as a function of its own measured speed — a quasi dolly zoom, sold entirely through optics with no camera-distance change. The mapping is **absolute and fleet-wide** (the same speed on any vessel looks the same); there is deliberately no per-vessel window, scalar, or normalization. Documents the four layers that make it un-authorable, the one sanctioned suppression (manual replay camera), the single tuning asset, and where every vessel lands in the shared window. **Read before touching vessel speed, the gameplay camera's FOV, or the Panini override.** |
| `PERFORMANCE_OPTIMIZATION.md` | `Docs/` | Frame-cost optimization log + prioritized backlog: shipped de-spike commits (do-not-regress list), the locked slice + per-frame budget + atomic publish fix pattern, instrumentation inventory (markers, DiagnosticsHUD, telemetry), per-task root-cause analyses with verified file/line refs, standing verification protocol. **Read before any perf work.** |
| `PartySystem/` | `Docs/` | Party (Relay) layer: `ARCHITECTURE.md` (locked design, investigation Q&A, error-handling matrix, exit criteria), `REFACTOR.md` (active backlog + deferred items + per-commit protocol), `BUGS.md`, `TESTS.md`, `TODOS.md`. EAGER per-user Relay session is the locked design. |
| `PresenceSystem/` | `Docs/` | Presence-lobby (discovery) layer: `ARCHITECTURE.md`, `REFACTOR.md`, `BUGS.md`, `TESTS.md`, `TODOS.md`. Lobby-only UGS session, coexists with NetworkManager. |
| `NetworkDiagnostics/` | `Docs/` | NetDiag overlay: `ARCHITECTURE.md` (NetworkMonitor + `NetworkDiagnostics` helper, classification rules), `TESTS.md` (Tests A-E), `TODOS.md`. |
| `ScoringSystem/` | `Docs/` | Scoring system (in-game score HUD + final scoreboard): `ARCHITECTURE.md` (shared data layer, event dispatch, per-mode override table, target = one unified networked scoring path), `REFACTOR.md` (sequenced backlog + ground rules: SOAP/observer/SOLID/DRY/KISS; `IsMultiplayerMode` retired 2026-07-20), `BUGS.md`, `TESTS.md`. |
| `TournamentSystem/` | `Docs/` | Tournament mode (`GameModes.Tournament = 36`): `ARCHITECTURE.md` — session-level meta drawing a random mode + intensity per round from an authored seven-mode pool (Skim Race, Joust, Crystal Capture, Rampage, Peel the Cage, Scarab Scramble, The Bends) via sequential `Single` loads, plus the admission criteria for adding an eighth; network-free standings folded from the synced `GameDataSO.Results` by the persistent `TournamentController`; host-only Continue→hub→Summary end-game flow (summary-vs-hub keyed off the authoritative `IsShuffleComplete`, race-to-6); `TournamentDataSO` data + file index. |
| `ToySystem/` | `Docs/` | Freestyle **Toy** system (the new `Toy` fundamental): `ARCHITECTURE.md` — world-space interactive stations the local vessel flies into (no score, no end condition), placed near the Cell membrane in Menu_Main. **§ "The switch"** is the ring law — every toy and every matrix station is drawn inside one continuous ring at the radius of its own trigger collider (`Toy.ConfigureSwitchRing`; the domain changer is the one waiver), so read it before adding a toy, a fly-through station, or anything that draws a ring. Toys are either a `MatrixToy` (ONE station that unfolds into a matrix of choices out along the outward radial and folds away on the next pass — cell selector, painting gallery, vessel changer) or a shared `SwapToySetCoordinator<T>` "flip-set" for small universes (each toy is the option it switches you to; the used one flips to your previous option — the domain changer) — Vessel Changer (mini ship models via `VesselModelBuilder`, reuses `RequestSwap` + restores freestyle control), Domain Changer (two toys tinted the domains you're not, `RequestSetDomain_ServerRpc`), and the "Connect the Dots" Painting toy — a gallery of painting stations (`PaintingToyDefinitionSO` → one `PaintingToy` per `PaintingDefinitionSO`), each running a multi-stroke, multi-domain `PaintingRunner`: per-stroke start gates recolour the trail via `RequestSetDomain_ServerRpc`, pen-up between strokes via `VesselPrismController.SetSpawnerPaused`, shared trail-toy shape language (cones = trail-on pointing at the next point — also worn by the Domain Changer; jacks = stroke-end trail-off; both in the domain prism material), stroke progress AND per-prism drawing state resume across vessel swaps/game modes/sessions (`PaintingProgressStore` + `PaintingPrismStore`, saved prisms regrow via the PrismFactory channel), completion SHARE/REPAINT gates with a self-contained WebGL share export (`PaintingShareExporter` + NativeShare), a 16-painting gallery (on-ramp Star → Rainbow → Saturn → Taj Mahal, then 12 grandiose non-planar constructions — Torus Knot, Buckyball, Double Helix, Nautilus, Lotus, Rose, Spiral Galaxy, Phoenix, Almighty Mountain, Starry Night, Lion's Head, Peacock — composed from `PaintingStrokeToolkit`: deterministic curves + a divergence-free curl "3D-impressionist" field; stroke order is computed at runtime by `OrderForFlightContinuity` — each stroke starts near the previous stroke's end, domain-contiguous, curvier strokes deferred on near-ties) — plus the **Wanderway microscene conveyor** (`ConveyorToy` + `WanderwayRun` + `WanderwayReturnToy` + `MicrosceneConveyor` + `Microscene` + `MicroscenePatterns` + `MicroscenePatternsGrand` + `MicroscenePainter`): a toy you fly into to LEAVE for a wander — the run reverts the host cell to its bare-canvas config (`Cell.BareCanvasConfig` → `Barren`: no environment AND no flora/fauna) through `Cell.RequestCellSwap`, then streams a speed-scaled field of procedurally-varied microscenes ahead of your flight path anywhere you fly, recycling the scene farthest behind into a fresh arrangement ahead — a *closed* system that transports a fixed stock of conserved prisms. **Grand scale (shipped 2026-08-02):** the belt's whole stock — `poolSize × prismBudgetPerScene`, **20 × 1500 = 30,000 prisms**, the same order as an authored cell environment — is built ONCE on the first pass through the toy, behind the same `EnvironmentLoadVeil` + arena-ready gate the Cell Selector uses for a world swap (`MicrosceneConveyor.PrimeAsync` → `PrismTrailBuilder.LayBudgetedAsync`); after that it never instantiates again. 48 recipes in two families: the **classic forty** (gate runs, tunnels, orchards, menageries, shingled domes, torus knots, Möbius rails, banked ribbon chicanes, spine×motif "Medley" composers, …), hand-tuned at `MicroscenePatterns.DesignRadius` and scaled bodily (POSITIONS only — never prism scales, so a bigger belt does not inflate per-prism volume into the host cell's phase ladder), and the **grand eight** (`MicroscenePatternsGrand`: Cathedral, World Tree, Orrery, Sunken City, Leviathan, Geode Vault, Aurora Veil, Hypersphere), which take the scene radius as their basis and multiply their part counts with the budget — borrowing the construction idioms of the freestyle six cell environments. The recycle is fully clock-driven (`Docs/PRISM_ANIMATION.md` §5 C8): collapse = one grow-clock re-stamp per prism, transport = hide + ONE container transform write (legitimate only because the off-screen removal gate proves it unseen), bloom = the standard creation stamps — the old per-frame container scale + per-prism spatial/entity re-sync (~180k writes per recycle at this scale) is deleted. **The run (shipped 2026-08-02)** makes the wander a place you go to and come back from: your trail becomes a **rolling tether** — a ribbon of exactly `tetherPrisms` (100) that follows you, the tail withering and RECYCLING into the pool the head lays from — with the **return station riding that tail**, so the way home is always one tether-length behind you. That recycle is the **one authorized exception** to *Mass is conserved* (explicit sign-off, 2026-08-03): it buys a truly infinite runner at fixed memory, and it is fenced to a live run — `WanderwayRun.RollTether` is the sole caller of `Trail.RemoveOldest`, `VesselPrismController` grew no cap field, and outside a run the law holds in full. Continuity of existence is NOT waived: a retiring prism withers on the grow clock and only then returns to the pool. Full record: `Docs/ECOSYSTEM.md` §0 — do not generalise it, do not revert it. Three exits call the same `WanderwayRun.End`: that station, another pass through the toy, and the **overview button** / gamepad Start (which drop freestyle — the run watches `ToyContext.IsFreestyleActive` for the edge, so no new wiring). Ending a run stops the belt, clears the pen, and repositions the vessel home via `IVessel.SetPose`; the belt's scenes and the bare cell stay (restoring a world is the Cell Selector's job). It paints every scene structurally from the full domain triad (per-structure rainbows, gradients, pinwheels) with danger/shielded/supershielded prisms as capped palette tools, lays skimmable elemental crystals, and releases flora/fauna into the containing cell as ordinary citizens. `ToyboxSO` registry + deferred unlock-state hook; `ToyboxController` self-wires (Resources/default fallback); `FrogletTools > Scene Setup > Setup Freestyle Toybox` authors assets + wires the scene. **Second pass (shipped):** `VesselModelBuilder` hull-filters the skimmer sphere + paints an opaque domain-tinted preview material (all six ships render, not just Rhino); `Toy` re-arms only after the vessel flies clear + the flipped toy re-grows slowly (can't switch you back before you escape); a vessel swap keeps your domain (`ReInitializePair` re-syncs `Player.Domain` from `NetDomain` before repaint) and inherits pose + speed (`IVessel.SetInitialSpeed`) and re-shows the HUD (`OnPlayerPairInitialized`); mini ships recolour on any domain change (`SwapToySetCoordinator.OnTick`); gamepad **Start** exits freestyle and `EventSystem.sendNavigationEvents` is off in freestyle so the pad stops double-driving the UI. **Cell Selector pass (shipped):** the freestyle six cost an `EnvironmentLoadVeil` hold on EVERY entry to Menu_Main (boot and every return from an arcade game), so the Cell now boots `CellTypeChoiceOptions.EnvironmentFree` (the first config with no `EnvironmentPrefab` — no build, no veil; Blob originally, the Lattice cell since `Docs/ECOSYSTEM.md` §36.10) and the six heavy worlds become OPT-IN through `CellSelectorToy` + `CellSelectorToyDefinitionSO`: fly the toy and a matrix of mini-cells blooms outward (the Lifeform Matrix pattern, now sharing `ToyMatrixStation`), each slot a bare genuine SCALE MODEL of the world it creates (no cage, no orb — the model speaks for itself) — `CellMiniatureBuilder` strides the generator's own output (`GetTrailData` + the new `CellEnvironmentSpawnableBase.CachedLays` for per-prism domain) into one mesh with a submesh per domain, spawning NO prisms, streamed one per frame and released after sampling; fly a mini-cell and `Cell.RequestCellSwap` suctions the old world away, drains it 500 prisms/frame, and grows the chosen one back behind the standard veil — picking the cell you are already in IS the freestyle reset (it also retires the pooled trail mass). The toy authors no cell list: it reads `Cell.AvailableConfigs`. `BACKLOG.md` tracks per-toy follow-up (own branches) + known limitations. |
| `ShuffleSystem/` | `Docs/` | **"Maelstrom" is the player-facing display name of Tournament mode** (the docs folder keeps the legacy "Shuffle" name) — the `ArcadeGameTournament.asset` card carries `DisplayName = "Maelstrom"`. It is **not** a separate mode: code/data/enum stay **Tournament** (`GameModes.Tournament = 36`); the scene file was renamed to `Maelstrom.unity` in the v2 rework. `ARCHITECTURE.md` is a **pointer** to `TournamentSystem/ARCHITECTURE.md`; the former Shuffle-specific behavior deltas (randomized lineup, per-domain `{2,1,0}` scoring + crystal-wallet credit, race-to-6) are now **shipped**. |
| `ElementalAbilitySystem/` | `Docs/` | Vessel elemental-ability contract: `ARCHITECTURE.md` (4 abilities × 4 elements × 4 upgrades; §7 four-icon row + control hints), `FLEET_MAPS.md` (per-vessel map status + un-approved proposals), `AUDIT.md` (dated evidence, CONFIRMED/REPORTED labels), `BACKLOG.md` (sequenced plan). Per-ability deep docs live beside the code in `_Scripts/Controller/Vessel/R_VesselActions/*.md`. Work here routes through the `/vessel` skill. |
| `CameraMigrationReview.md` | `Docs/` | Camera system migration tracking |
| `BOOTSTRAP_AUDIT.md` | `_Scripts/System/Bootstrap/` | Bootstrap scene audit, execution order, DI registration |
| `HEXRACE.md` | `_Scripts/Controller/Arcade/` | HexRace game mode technical reference |
| `CRYSTAL_CAPTURE.md` | `_Scripts/Controller/Arcade/` | Crystal Capture game mode technical reference |
| `JOUST.md` | `_Scripts/Controller/Arcade/` | Joust game mode technical reference |
| `ASTROLEAGUE.md` | `_Scripts/Controller/Arcade/` | Astro League game mode technical reference |
| `RAMPAGE.md` | `_Scripts/Controller/Arcade/` | Rampage technical reference (Dolphin-only demolition race). **Read before touching flora planting dispersal or a cell's volume phase thresholds** — this mode moved the planting shell onto the cell centre platform-wide and is the worked example of authoring a volume ladder for a cell whose prisms are not nominal size. |
| `RIBCAGE.md` | `_Scripts/Controller/Arcade/` | Ribcage / "Peel the Cage" technical reference (Rhino-only cage-breaking race; the layered-orange intensity model, the open-weave generator, the shielded-mass targeting-grid rule, and the record of the removed fauna ladder) |
| `SQUIRREL_DRIFT.md` | `_Scripts/Controller/Vessel/R_VesselActions/` | The Squirrel's drift AND the fleet's two flight models. **Read before touching `VesselTransformer.MoveShip`, any drift tuning, or anything that writes `VesselStatus.Course` from outside the transformer.** Documents the scalar model's thrust-along-COURSE defect (throttling mid-drift digs you deeper into the slide), the opt-in vector model that fixes it, the proof + numeric verification that the two are identical outside a drift (which is why the flag needs no fleet retune), why grip must resolve BEFORE thrust, and the four constraints the migration had to respect — the AI's Course write, the live damage channels, the Rhino's latched speed-tracking rate, and replication. |
| `URCHIN_CHAIN_SPIKES.md` | `_Scripts/Controller/Vessel/R_VesselActions/` | The Urchin's chain-reaction spikes — ONE trigger, two shots (tap = the ring shotgun, hold-and-release = an omni burst sized by the hold): the faithful projectile-per-hop recursion, the THREE brakes in authority order (territory conversion is primary and emergent; generation depth and the per-frame volley budget sit under it), the `[Embed, Steal, ChainFire]` container order and why it is load-bearing, and why the charge timer lives on the per-vessel EXECUTOR rather than the shared SO. **Read before touching `Gun`, `LoadedGun`, `Projectile`'s swept detection, or any projectile effect container.** |
| `URCHIN_TRAIL_RIDER.md` | `_Scripts/Controller/Vessel/R_VesselActions/` | The Urchin's prismscape ride — the 1D rail grind and the 2D marble roll — plus the **`AssignTrail`-after-`Initialize` membership contract** every lay site must honour, the ride-surface envelope for shielded and skewed prisms, the vessel-material role convention (Body / Domain / Window) read off the Squirrel FBX, and the **end-of-ribbon LAUNCH**: running out of open ribbon detaches and carries the grind's speed into free flight, bled off at a constant rate (a loop never reaches it, so the two topologies now feel different). **Read before adding a prism lay site, or before touching `Trail`, `TrailFollower`, `BlockscapeFollower` or `GunVesselTransformer`.** |
| `URCHIN_TRACK_PROJECTOR.md` | `_Scripts/Controller/Vessel/R_VesselActions/` | The Urchin's projected rail — a straight 100-unit stretch of single-lane trail laid ahead of the nose so the vessel has something to grind in open space. Ordinary conserved mass in the pilot's own domain (ridden, grown, stolen and grazed like any trail), laid through `BoostRingBuilder.LayOne` for a full-size collider from frame 0 and the `AssignTrail`-after-`Initialize` stamp, on the Squirrel boost ring's 20-second cooldown. **Read before adding another "place a structure" ability.** |
| `SELF_TRAIL_CONTACT.md` | `_Scripts/Controller/ImpactEffects/` | The self-trail contact grace — a pilot does not skim or ram the ribbon still coming out of their own ship. **Read before adding any self/own-mass guard to an impact path, or before touching `waitTillOutsideSkimmer`.** Documents why the gate is owner-scoped and time-boxed rather than domain-scoped (`Skimmer.AffectSelf` compares domains AND runs after the effect loop), why another player's — and a teammate's — trail stays interactable from the frame it appears, and the clearance-delay geometry bug that made MASS-stretched prisms pop in inside the ship. |
| `DOGFIGHT.md` | `_Scripts/Controller/Arcade/` | Dog Fight technical reference (Sparrow-only gun duel). **Read before touching the combat-hit path, the Sparrow's weapon effect containers, or the skyburst's AOE prefabs** — this mode added the platform's first vessel-vs-vessel scoring metric and gave the skyburst's conic blast the explosion container it never had (so a rocket's blast can now reach a pilot at all). Also documents why the mode is a TEAM race rather than a free-for-all: teammates cannot damage each other, so domains ARE the sides. |
| `BENDS.md` | `_Scripts/Controller/Arcade/` | The Bends technical reference (Dolphin-only debuff duel). **Read before touching the Dolphin's conic blast effect container, the combat-hit path, or `AIPilot`'s drift look-direction** — this mode gave the Dolphin's crystal blast the vessel effects it never had (so the blast now debuffs a pilot it engulfs, in every mode), added `CombatHitClass.Debuff`, and gave `AIPilot` a drift-aim hook that is deliberately separate from the steering hook. Also records two networking bugs it surfaced: a client→server hit-class validator that mis-filed any new enum member, and the double-credit a REPLAYED blast causes. |
| `WILDLIFE_LIBERATION.md` | `_Scripts/Controller/Arcade/` | Wildlife Liberation technical reference (Sparrow-only three-cage hunt). **Read before touching the fauna kill path or the per-species containment bands** — this mode made every creature in the game shootable, and generalized the cell's single fauna pen into a per-species annulus. Also documents why a per-player (free-for-all) winner was tried here and reverted, the client-local-fauna kill RPC, and the very-heavy collider budget. |
| `AI_ORBIT_BREAK.md` | `_Scripts/Controller/AI/` | Why every AI orbited its objective, and the extend-and-re-attack that fixes it. **Read before touching `AIPilot`'s steering, `PursuitReachability`, or any vessel's Pitch/Yaw scalers** — the orbit is a Dubins minimum-turn-radius result, not a tuning miss, and turning harder is the wrong response. Carries the measured before/after and the two rejected alternatives (an entry dwell, a running-minimum progress gate). |
| `PRISM_PERFORMANCE_AUDIT.md` | `_Scripts/Game/Prisms/` | Prism system performance analysis (vestigial location) |
| `UNIT_TESTING_GUIDE.md` | `_Scripts/Tests/` | Unit testing guidelines and inventory |
| `BENCHMARK_TOOL.md` | `_Scripts/Utility/PerformanceBenchmark/` | Performance Benchmark tool guide (tabs, score/hints, sweep, Load Time Insights, customization) |
| `DIAGNOSTICS.md` | `Docs/` | The **FrogletTools ▸ Diagnostics** family: the editor **Crash Detector** (off-thread error journal + heartbeat sentinel; abnormal exits — crashes, PC faults, hangs-then-kills — are reported on the next launch from the journal + Unity's own `Editor-prev.log`) and the **Bug Ledger** (the team's live bug list: every distinct red-error signature auto-files ONE issue file into the gitignored `BugLedger/local/` store — version control sees ledger data only when it is explicitly staged & pushed from the tool's Stage & Push tab, which commits ledger paths ONLY; a fix is only believed once the game validates it — clean play/editor sessions for captured errors, a clean full re-run for tool-filed findings — then archived to `BugLedger/local/resolved/`; a recurrence reopens the issue as a regression). The signature core is the runtime-safe `CosmicShore.Utility.BugSignature`, shared with the planned in-game reporter. Also the opt-in **Compile Timing** tab (compile + domain-reload seconds per edit, and which assemblies rebuilt — the measurement behind `Docs/ASSEMBLY_SPLIT.md`). **Read before touching anything under `Assets/_Scripts/Editor/Diagnostics/`, `BugSignature`, or the `BugLedger/` store — and before wiring an auditor's findings into the ledger.** |
| `TOOLING.md` | `Docs/` | **The editor-tooling convention.** One menu root (`FrogletTools/`), one auto-discovering board (Froglet Master Tool), one shared palette, and — for any tool that WRITES assets — the ship contract: record what you wrote, draw `FrogletToolShipPanel` (Validate & Push / Retire Tool), because a tool's output is the deliverable and it lands in the working tree, not the branch. **Read before adding ANY `[MenuItem]`** — a tool outside `FrogletTools/` is flagged as non-conforming by the board itself. |
| `GAMECANVAS.md` | `Docs/` | GameCanvas as one source of truth: the two forked prefabs, the 1,734 identical-in-every-scene overrides that masked the prefab, the ~20 that are genuinely per-mode, the dangling cross-prefab refs, the code fixes that removed per-scene wiring, and the in-editor unification steps. **Read before touching any game-mode scene's canvas.** |
| `unity-cli-setup.md` | `Docs/` | Unity CLI first-time setup (per-machine install, `unity doctor`, connecting to the open Editor, eval token hygiene, troubleshooting). Team-facing; the CLI is experimental and `unity --help` on the installed version is authoritative. |
| `GIT_RULES.md` | Project root | Git commit conventions |
| `BOOTSTRAP_AUTH_FLOW.md` | `Docs/` | Bootstrap → Authentication → Menu_Main full flow: scene-by-scene diagrams, `ApplicationStateMachine`, auth SOAP data flow, key-file tables, auth patterns |
| `MULTIPLAYER_SPAWNING.md` | `Docs/` | Netcode component reference, player/vessel spawn chains (menu, game, party join, freestyle flight), Player NetworkVariables, player-count & AI-backfill pipeline, team balancing |
| `PARTY_SOCIAL.md` | `Docs/` | Party/invite lobby + friend system reference: services, SOAP types, facade API, presence, UI components, SO assets, patterns |
| `HEXRACE_SUMMARY.md` | `Docs/` | Condensed HexRace reference (canonical deep-dive: `_Scripts/Controller/Arcade/HEXRACE.md`) |
| `MENU_NAVIGATION.md` | `Docs/` | Menu_Main screen navigation: `ScreenSwitcher`, `IScreen`, screen inventory, reusable UI components |
| `LAVALAMP.md` | `Docs/` | Lava-lamp / menu-freestyle merge: Game UI hierarchy, HUD lifecycle, vessel selection, phased shape-drawing/scoring rollout |
| `ELEMENTAL_BARS.md` | `Docs/` | Elemental bars petal-flower HUD: level→colour math, `ElementalBarsConfigSO` single source of truth, per-vessel rollout |
| `ECOSYSTEM.md` + `ECOSYSTEM_MASTERPLAN.md` | `Docs/` | Ecosystem mechanics log + north-star roadmap (the LOCKED invariants above summarize these) |
| `NUCLEUSRUSH.md` | `_Scripts/Controller/Arcade/` | Brood Rush (nucleus-control fauna-wave race) technical reference |
| `README.md` | `Docs/` | Party / Presence / NetDiag docs index + shared conventions and locked designs |

## Architecture Patterns

Follow these established patterns. Do not introduce alternative architectures without discussion.

### ScriptableObject Config Separation

All tunable gameplay parameters live in ScriptableObjects, not in MonoBehaviours. MonoBehaviours reference SO configs at runtime. Example pattern:

- `SkimmerAlignPrismEffectSO` (config) → referenced by the vessel's prism controller system
- `VesselExplosionByCrystalEffectSO` (config) → defines explosion parameters for crystal impacts
- `CameraSettingsSO` (config) → per-vessel camera follow/zoom settings
- `BootstrapConfigSO` (config) → bootstrap scene flow settings (target framerate, splash duration, timeouts)
- Use `[CreateAssetMenu]` with organized menu paths: `ScriptableObjects/Impact Effects/[Category]/[Name]`

### SOAP — Scriptable Object Architecture Pattern (Primary Architecture)

This project uses the **SOAP asset** (Obvious.Soap v2.7.0, installed at `Assets/Plugins/Obvious/Soap/`) as the backbone for modular, event-driven, and data-container-based architecture. **Use SOAP whenever possible** for cross-system communication and shared state — do not introduce singletons, static events, or direct references between systems when a SOAP variable or event can do the job.

**Fail-loud policy**: Do not add if-null guards on `ScriptableEvent` serialized fields. Missing references should produce immediate, obvious errors rather than silent failures.

#### Core SOAP Primitives

- **`ScriptableVariable<T>`** — Persistent data containers that live as assets. Any system can read/write to them without knowing about other consumers. Use these for shared state (player health, score, vessel class, authentication data, etc.).
- **`ScriptableEvent<T>` / `ScriptableEventNoParam`** — Decoupled event channels. Raise events from any system; listeners subscribe via inspector-wired `EventListener` components or code. Use these for one-to-many notifications (game over, boost changed, crystal collected, etc.).
- **`EventListener<T>`** — MonoBehaviour that subscribes to a `ScriptableEvent` and exposes `UnityEvent` responses in the inspector. Preferred for UI and scene-bound reactions.

#### When to Use SOAP

| Scenario | SOAP Solution |
|---|---|
| Sharing state between unrelated systems | `ScriptableVariable<T>` asset |
| Broadcasting an event to multiple listeners | `ScriptableEvent<T>` asset |
| UI needs to react to gameplay changes | `EventListener<T>` on the UI GameObject |
| New system needs data from another system | Reference the existing `ScriptableVariable` — do not add a direct dependency |
| Request/response pattern between systems | `GenericEventChannelWithReturnSO<T, Y>` (custom extension at `Assets/_Scripts/ScriptableObjects/SOAP/ScriptableEventWithReturn/`) |

#### Creating New SOAP Types

Custom SOAP types live in `Assets/_Scripts/ScriptableObjects/SOAP/` organized by data type. When you need a new type:

1. Create a folder: `Assets/_Scripts/ScriptableObjects/SOAP/Scriptable[TypeName]/`
2. Create the variable class: `[TypeName]Variable : ScriptableVariable<[TypeName]>`
3. Create the event class: `ScriptableEvent[TypeName] : ScriptableEvent<[TypeName]>`
4. Create the listener class: `EventListener[TypeName] : EventListenerGeneric<[TypeName]>`
5. Use namespace `CosmicShore.ScriptableObjects` for all custom SOAP types

Existing custom SOAP types (16 subdirectories): `AbilityStats`, `ApplicationState` (`ApplicationStateData` + `ApplicationStateDataVariable` + `ScriptableEventApplicationState` — written by `ApplicationStateMachine`), `AuthenticationData` (+ `NetworkMonitorData`), `ClassType` (VesselClassType + VesselImpactor + debuff events), `CrystalStats`, `FriendData` (`FriendData` struct + `FriendPresenceActivity` `[DataContract]` + `ScriptableEventFriendData` + `ScriptableListFriendData` + `EventListenerFriendData` — relationship & presence data for UGS Friends integration, written by `FriendsServiceFacade`), `GameplaySFX` (gameplay sound effect category events for decoupled audio), `InputEvents`, `PartyData` (PartyInviteData, PartyPlayerData + list variant), `PipData`, `PrismStats`, `Quaternion`, `VesselHUDData`, `Transform`, and `ScriptableEventWithReturn` (generic return channel + `PrismEventChannelWithReturnSO`). Also contains `VesselPrefabContainer.cs` for vessel-class-to-prefab mapping.

#### SOAP Anti-Patterns

- **Do not** use singletons or static events for cross-system communication — use `ScriptableEvent` instead
- **Do not** add direct MonoBehaviour-to-MonoBehaviour references for data sharing — use `ScriptableVariable` instead
- **Do not** use `FindObjectOfType` or service locators to get shared data — wire a `ScriptableVariable` in the inspector
- **Do not** create C# events or `Action` delegates on MonoBehaviours for things that multiple unrelated systems need to observe — use `ScriptableEvent`
- **Do not** duplicate SOAP types — check `Assets/_Scripts/ScriptableObjects/SOAP/` for existing types before creating new ones
- **Do not** put gameplay logic inside ScriptableVariable/ScriptableEvent classes — they are data containers and channels, not controllers
- **Do not** add if-null guards on ScriptableEvent serialize fields — fail loud on missing references

### Threading & Main-Thread Affinity

See `Docs/THREADING.md` for the full reference. The short version:

UGS SDK (`Unity.Services.*`) and Netcode methods return `System.Threading.Tasks.Task` whose
continuations complete on the .NET ThreadPool. From the ThreadPool, any `UnityEngine.Object`
access throws `EnsureRunningOnMainThread`, and any `Obvious.Soap` `ScriptableEvent.Raise()` runs
its listeners **inline on the off-thread**, surfacing the same crash one level deeper.

**The contract:** every `await` of a UGS / Netcode `Task` uses `.AsMainThread()`:

```csharp
ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(opts).AsMainThread();
```

`.AsMainThread()` (in `Assets/_Scripts/Utility/ClassExtensions/UniTaskExtensions.cs`) awaits
the original task and then awaits `MainThreadDispatcher.SwitchToMainThreadAsync()`, which
marshals onto Unity's captured `SynchronizationContext`. Four overloads cover
`Task`, `Task<T>`, `UniTask`, `UniTask<T>`.

**Why UniTask's own primitives don't work on this version (`com.cysharp.unitask@86b6e6a2e286`):**

UniTask 2.x intentionally bypasses `SynchronizationContext` and `ExecutionContext`:

> *"UniTask always works like `Task.ConfigureAwait(false)` and is not guaranteed that the thread
> before awaiting may match the thread after awaiting."* — UniTask docs.

Consequence:

- `UniTask.SwitchToMainThread()` — awaiter's `IsCompleted` reports `true` from ThreadPool →
  continuation runs **inline** on ThreadPool. Switch is a no-op. ([Cysharp/UniTask#319](https://github.com/Cysharp/UniTask/issues/319), [#151](https://github.com/Cysharp/UniTask/issues/151))
- `UniTask.Yield(PlayerLoopTiming.Update)` — yields, but the resumption is *not* guaranteed on
  main thread because UniTask's `ContinuationQueue` doesn't capture the SyncContext.
  ([Cysharp/UniTask#561](https://github.com/Cysharp/UniTask/discussions/561) — exact symptom we hit.)

Neither primitive is a reliable main-thread switch on this version. The `MainThreadDispatcher` +
`.AsMainThread()` boundary helper bypasses UniTask's bypass by using Unity's own
`SynchronizationContext`, which IS properly main-thread-bound.

**The canary** lives in `SceneTransitionManager.SetFadeImmediate`
(`Assets/_Scripts/System/Bootstrap/SceneTransitionManager.cs`). It reads
`MainThreadDispatcher.IsOnMainThread` and logs `Debug.LogError` with the call stack if a future
UGS call site forgets `.AsMainThread()`. Both the canary and the helper share one main-thread-ID
source — no risk of divergent capture sites.

**When to use which primitive:**

| Situation | Use |
|---|---|
| `await` a UGS / Netcode / cross-thread `Task` | `.AsMainThread()` |
| `await` a `UniTask` you wrote that internally awaits UGS with `.AsMainThread()` | nothing extra at the caller |
| Need main thread without a Task to attach to (e.g., top of a `catch` block) | `await MainThreadDispatcher.SwitchToMainThreadAsync()` |
| Yield one frame for PlayerLoop processing (NOT thread marshaling) | `await UniTask.Yield(PlayerLoopTiming.Update)` — fine for sequencing, not for affinity |
| Assert main thread (debug) | `MainThreadDispatcher.IsOnMainThread` |

The three remaining `Yield(PlayerLoopTiming.Update)` calls in
`Controller/Party/PartyInviteController.cs` (in catch / recovery blocks) are intentional —
they are "wait for the next PlayerLoop tick before handling this exception" semantics, not
threading.

**Anti-patterns to avoid:**

- **Do not** add `await UniTask.SwitchToMainThread()` or `await UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — neither works on this UniTask version. Use `.AsMainThread()`.
- **Do not** raise a SOAP `ScriptableEvent` from a UGS / Netcode callback continuation without ensuring the continuation has resumed on the main thread first — SOAP raises invoke listeners inline.
- **Do not** touch a `UnityEngine.Object` (incl. `== null` checks) in a `Task` continuation without `.AsMainThread()` upstream.
- **Do not** capture `Thread.CurrentThread.ManagedThreadId` in random places to make per-class main-thread checks — read `MainThreadDispatcher.IsOnMainThread` instead, single source of truth.

### Bootstrap, Authentication & App State

Full flow (scene-by-scene execution diagrams, `ApplicationStateMachine` graph, SOAP data
flow, key-file tables): `Docs/BOOTSTRAP_AUTH_FLOW.md`. The rules that must hold:

- `AppManager` is the Reflex DI root and bootstrap orchestrator (`[DefaultExecutionOrder(-100)]`,
  `IInstaller`); all persistent services/SO assets register in `InstallBindings()`.
- **Single writer**: only `AuthenticationServiceFacade` writes `AuthenticationData`; only
  `ApplicationStateMachine` writes `ApplicationStateDataVariable`; scene controllers and
  UI read state and subscribe to SOAP events - they never mutate directly.
- All auth async uses UniTask + `CancellationToken` with linked-CTS timeouts (no polling
  loops, no raw `Task.Delay`); disable buttons during async ops instead of boolean
  guards; get facades via `[Inject]`, never by creating controller GameObjects.
- `SceneLoader` (DontDestroyOnLoad, Bootstrap) owns game launch / restart /
  return-to-menu via code-subscribed SOAP events; scene names come from `SceneNameListSO`.

### Dependency Injection (Reflex)

The project uses Reflex DI with `AppManager` as the root `IInstaller`. All persistent services and shared assets are registered in `AppManager.InstallBindings()`:

**SO asset registration** (`RegisterValue`): `SceneNameListSO`, `GameDataSO`, `AuthenticationDataVariable`, `NetworkMonitorDataVariable`, `FriendsDataSO`, `HostConnectionDataSO`, `ApplicationLifecycleEventsContainerSO`, `ApplicationStateDataVariable`. These are project-level assets wired via inspector on AppManager.

**MonoBehaviour singleton registration** (`RegisterFactory`, Lazy): `GameSetting`, `AudioSystem`, `PlayerDataService`, `UGSStatsManager`, `CaptainManager`, `IAPManager`, `SceneLoader`, `ThemeManager`, `CameraManager`, `PostProcessingManager`, `StatsManager`, `SceneTransitionManager`. These use a lazy factory that prefers the serialized reference and falls back to a scene search at first injection time.

**Pure C# singleton registration** (`RegisterFactory`, Lazy): `AuthenticationServiceFacade`, `NetworkMonitor`, `FriendsServiceFacade`, `ApplicationStateMachine`.

#### DI Patterns to Follow

- **Use `[Inject]` for shared assets**: `GameDataSO`, `SceneNameListSO`, and other DI-registered assets should be accessed via `[Inject]`, not `[SerializeField]`. This eliminates manual inspector wiring and serialization drift.
- **Injection timing**: `[Inject]` fields are populated after `Awake()` but before `Start()`. Access injected fields in `Start()` or later — never in `Awake()`. If you need to subscribe to events in `OnEnable()`, use a deferred pattern: attempt in `OnEnable()`, retry with duplicate guards in `Start()`. The same hazard applies to runtime creation: `AddComponent<T>()` runs the new component's `Awake`/`OnEnable` INLINE, before the caller's next line can assign any field — so a factory that assigns dependencies after `AddComponent` must also explicitly complete the deferred subscription (reference: `ElementalComebackSystem.EnsureExists` + `TrySubscribeToGameEvents`).
- **ContainerScope per scene**: Each scene that uses `[Inject]` must have a Reflex `ContainerScope` component (via the `ContainerScope.prefab` in `_Prefabs/CORE/`). The Bootstrap scene's scope is the root; other scenes get child scopes.

### Input Strategy Pattern

Platform-agnostic input via `Assets/_Scripts/Controller/IO/`:

- `IInputStrategy` — interface for all input handlers
- `BaseInputStrategy` — shared logic
- `GamepadInputStrategy`, `TouchInputStrategy`, `KeyboardInputStrategy` (dual-WASD, the **desktop default**), `DualMouseInputStrategy` (opt-in two-mice flight) — platform-specific implementations. `KeyboardInputStrategy` maps two digital "sticks" (WASD left, P/;/L/' right; Left/Right Shift = the two triggers) and mixes them through `DualStickMix` (the yaw/pitch/speed/roll formulas shared with — and unit-tested against — `GamepadInputStrategy.Reparameterize`). The legacy `KeyboardMouseInputStrategy` remains in the project but is no longer selected.
- `InputController` — manages active strategy and input state. Flight input is gated on `Player.IsLocalPilot` (AI and remote `Player` replicas carry an `InputController` but must not consume local WASD/sticks).
- `IInputStatus` / `InputStatus` — input state container
- Input strategies are swappable per platform/context at runtime
- **Only the local human's `InputController` runs.** `Player.InitializeForMultiplayerMode`
  sets `InputController.enabled = IsLocalUser` (the earliest point AI-ness is reliable —
  `NetIsAI` is written by the AI spawner *after* `Spawn()`, so `OnNetworkSpawn` cannot gate
  this). AI pilots write `InputStatus` directly; remote vessels replicate theirs.
- **Gamepad triggers are rest-calibrated.** Triggers do not universally rest at zero (worn
  springs drift upward; some DirectInput-style pads rest mid-range by design), and a rest
  value above the deadzone reads as "permanently held" — the press edge never fires and
  trigger-bound actions (e.g. the Squirrel's drift) go dead. `GamepadInputStrategy`
  min-latches each trigger's observed resting baseline (re-latched on `Gamepad.current`
  device change) and remaps `[rest..1] → [0..1]` before edge detection and the
  `InputStatus` analog writes. Do not compare raw trigger reads against an absolute
  threshold anywhere else.

### Impact Effects Architecture

The collision/impact system (`Assets/_Scripts/Controller/ImpactEffects/`) uses a matrix of impactors and effect SOs:

**Impactor types** (all extend `ImpactorBase`): `VesselImpactor`, `NetworkVesselImpactor`, `PrismImpactor`, `ProjectileImpactor`, `SkimmerImpactor`, `MineImpactor`, `ExplosionImpactor`, `CrystalImpactor`, `ElementalCrystalImpactor`, `OmniCrystalImpactor`, `TeamCrystalImpactor`

**Effect SO pattern**: `[Impactor][Target]EffectSO` — e.g., `VesselExplosionByCrystalEffectSO`, `SkimmerAlignPrismEffectSO`, `SparrowDebuffByRhinoDangerPrismEffectSO`. Per-vessel effect asset instances exist for each vessel class. Organized into subdirectories: `Vessel Crystal Effects/`, `Vessel Prism Effects/`, `Vessel Explosion Effects/`, `Vessel Projectile Effects/`, `Vessel Skimmer Effects/`, `Skimmer Prism Effects/`, `Projectile Crystal Effects/`, `Projectile Prism Effects/`, `Projectile Mine Effects/`, `Projectile End Effects/`.

Key interfaces: `IImpactor` / `IImpactCollider`

**An EMPTY slot in a serialized effect array names itself — dispatch it through `ImpactorBase.IsEffectSlotEmpty`.** `DoesEffectExist` only gates on length, so a hole *inside* the list reached `effect.Execute(...)` and threw a bare `NullReferenceException` at the call site, naming neither the container nor the index. That is survivable in a PhysX callback and is not once the **shell tier** owns the pair: `PrismShellContactManager` dispatches from `Update`, so one bad slot threw **once per frame** for the life of the contact, and each throw aborted the rest of that frame's shell contacts *and* skipped `SweepStalePairs`. `IsEffectSlotEmpty` reports container + field + index **once** (`CSDebug.LogError`, keyed so it can't spam) and returns true so the caller skips that slot and the sibling effects still run — the missing effect cannot be invented, but nothing else in the chain needs to die with it. Wired through every dispatch loop in `VesselImpactor` and `SkimmerImpactor`, the two impactors registered as shell probe owners (`RegisterProbeOwner`) and therefore the two whose dispatch left the callback and became a per-frame path. Route any new effect-dispatch loop through it** rather than dereferencing the slot directly. This is not a fail-soft exception to the fail-loud policy: it fails loud *once, with the offender's address*, instead of anonymously forever.

**Its companion is `ImpactorBase.RunEffectIsolated` — for a slot that is FILLED but THROWS.** An exception inside an effect's `Execute` is reported once per (effect, impactor type) with its stack, and the rest of the contact's list still runs. Same doctrine, opposite failure: a hole vs. a thrower. The Urchin forced it — its spike container is `[Embed, Steal, ChainFire]` in a load-bearing order, so one throwing effect silently killed both the steal and the cascade while the embed had already visibly landed, and the weapon read as dead with nothing in the console. Wired into `ProjectileImpactor`; `VesselImpactor` and `SkimmerImpactor` still dispatch bare and are the open item. Route a new dispatch loop through BOTH helpers.

**A vessel and its own skimmer never impact each other.** `SkimmerImpactor` and `VesselImpactor` carry mirrored self-guards on their vessel<->skimmer dispatch — required because the Rhino's sword capsule permanently overlaps its own hull, which otherwise ran the full victim-effect chain against the pilot (muting their own `RightStickAction` via `VesselDamageBySkimmerEffect`, impact-SFX spam). Skimmer-vs-own-PRISM handling is separate and stays flag-controlled (`Skimmer.AffectSelf`). See `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`.

**A pilot does not interact with their own trail while they are MAKING it** — `SelfTrailContactConfigSO` (`Resources/SelfTrailContactConfig`), asked by both `VesselImpactor` and `SkimmerImpactor` at the top of their prism branch. A trail prism is laid a fixed offset behind the vessel and the spawner assumes the vessel then leaves it; a **drift** slides the hull sideways across the ribbon it is extruding, **MASS scaling** stretches the prism further back than the clearance delay was sized for, and a **skimmer sphere** (15–30 u on the Squirrel) outlasts the hull by a long way. So a Squirrel fed itself skim energy off the ribbon it was laying, a Dolphin *rammed* its own fresh trail and lost **half its banked skim energy and half its charged boost** (`VesselChangeResourceByPrismEffectSO` / `VesselChangeBoostByPrismEffectSO`, neither of which carries a self-guard). **The gate is OWNER-scoped and TIME-boxed, deliberately not domain-scoped**: `Skimmer.AffectSelf` compares DOMAINS (so switching it off also blinds a vessel to its teammates' trails) and is evaluated AFTER the skimmer effect loop, where it gates only the skim bookkeeping — it changes nothing for effects. The test is `prism.ownerID == vessel.PlayerName` within the grace since `prismProperties.TimeCreated`, using `ownerID` (which records who LAID it and survives a steal) rather than `PlayerName`, and excluding `IsEnvironmentOwned` mass outright. Consequently **another player's trail — and a teammate's — is skimmable from the frame it appears**, so a trailing Squirrel still farms an opposing ribbon all the way into joust range, and a pilot's own older trail is ordinary mass again. Both guards sit ABOVE the shell-ownership check so the Squirrel's MASS-5 shielded drift armour is covered on the analytic tier too. Nothing is culled, decayed, or hidden — the mass is live for the whole world from the frame it is laid; one vessel declines to act on it, so conserved mass is intact. Its companion fix: `VesselPrismController.CreateBlock`'s `waitTillOutsideSkimmer` delay measured `TrailZScale` (= `BaseScale.z`), which omits BOTH `ZScaler` and the MASS volume multiplier applied a few lines above it, so an upgraded vessel's collider came on while the prism was still inside the ship — it now measures the length actually being laid (`scale.z`), which is identical for un-upgraded vessels and only ever lengthens. That delay hides the prism from EVERYONE, which is exactly why it can never be the lever for an owner-scoped rule. Full record: `_Scripts/Controller/ImpactEffects/SELF_TRAIL_CONTACT.md`.

**A skimmer only skims if `VesselStatus` points AT it — and the failure is silent.**
`VesselController.Initialize` initializes **only** `VesselStatus.NearFieldSkimmer` /
`FarFieldSkimmer`, and `SkimmerImpactor` drops every contact while `skimmer.IsInitialized` is
false. So a vessel can carry a perfectly wired skimmer — trigger sphere, kinematic rigidbody,
`ImpactCollider`, effect container, layer 7 — and skim **nothing at all**, with no error
anywhere, because the reference points at a different (or disabled) skimmer object. The Dolphin
shipped that way for its whole life: an active `EnergySkimmer` doing the physics and a disabled
legacy nested `Skimmer.prefab` holding the reference. **Audit it, don't infer it from feel:**
`FrogletTools > Vessels > Audit Vessel Skimmers` checks assignment, active state up the whole
ancestor chain, the components the trigger path needs, and whether the container holds any
prism effects — asset-only, no play mode. *(Serpent currently fails it.)* Note that a skim's
feedback signals are each individually invisible — the haptic is a **no-op on desktop**,
the legacy beam VFX (`SkimmerFXPrismEffectSO`, `[Obsolete]`) is per-vessel wiring that only
draws when the container asks for it AND the skimmed prism authors a `ParticleEffect`, and a
gauge that moves a tenth of its range per skim reads as nothing — so "I feel no skimming" is
not evidence about the wiring in either direction. **The crackle is meant to be a vessel's ONLY
skim visual**: the beam is the effect it replaced, so a container holding both draws a beam to
every prism in the sphere *on top of* the crackle. The Dolphin ran both for three hours of
branch history and now wires the crackle alone; the Squirrel still carries both, which is the
open item, not the reference. The forcefield crackle needs **three** pieces to be
present or `SkimmerForcefieldCracklePrismEffectSO.Execute` returns silently: the effect in the
container, a `ForcefieldCrackleController` on the impactor's own GameObject, and an overlay
`MeshRenderer` assigned to it (vessels whose skimmer IS `Skimmer.prefab` get the last two free;
standalone skimmer objects do not). Detail:
`_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` §5.

**Danger prisms are not safe to their own domain (locked design).** `IsDangerous` effects apply to every vessel that touches the prism, regardless of domain — friendly fire included (the fire-trail action literally sets `IsDangerous` from a `FriendlyFire` flag). Danger-prism effect SOs must not gate on domain. **Danger is mutually exclusive with BOTH shield tiers**: `PrismStateManager.MakeDangerous` clears `IsShielded` AND `IsSuperShielded` (and disengages the shield visuals), just as `ActivateSuperShield` clears `IsDangerous` — a danger prism carrying a stale super-shield flag is invulnerable and kills any AOE explosion that touches it. `Prism.ResetState` also clears `IsSuperShielded` on pool reuse (no spawner requests super-shield pre-`Initialize`; it is always engaged post-spawn). This is what makes danger trails a risk/reward surface: a danger trail grants 10x skim energy (`SkimmerBoostPrismEffect.dangerEnergyMultiplier`, gated behind the skimming vessel's Charge level-5 "Live Wire" upgrade — below it danger skims pay base energy) but slams its owner on contact — volume-independent full-stop slow at the danger max (`VesselChangeSpeedByPrismEffectSO`: `maxSlowStrength * dangerSlowMultiplier`), all-element decaying debuff for 4s (`VesselElementalDebuffByDangerPrismEffectSO`), and boost reset. (The Sparrow's own overheat danger trail was retired with its overheat mechanic; the `EnableDangerMode` machinery survives for a future caller. The one thing that can deny a danger prism's bite is the general **elemental-debuff immunity** state — `ResourceSystem.IsImmuneTo(ElementalDebuffSources.DangerPrism)`, held by the Sparrow while boosting at Time 5, by the Serpent while stopped, and by the Dolphin while drifting at Time 5 — and it denies ONLY the elemental drain: the slow, the input mute and the boost reset still land. It is a gate inside `ApplyElementalEffect`, not a domain exception, so the locked law is intact. A ward is held against a MASK of debuff SOURCE CLASSES, not as a bare bool: the Sparrow's and Serpent's cover everything, while the **Dolphin's covers `DangerPrism` alone** — it is a ward against the ARENA, so an opposing pilot's blast still debuffs a drifting Dolphin (which is what keeps The Bends scoreable; see §"The Bends" and `SPARROW_AFTERBURNER.md` §1.1).)
**The slow half of that punishment is PER-VESSEL WIRING, not a platform given** — it only happens
if the vessel's `VesselImpactorDataContainerSO.vesselPrismEffects` actually contains a
`VesselChangeSpeedByPrismEffectSO`, and for most of the fleet's life most vessels did not. The
Dolphin had an authored `DolphinVesselChangeSpeedByPrism` asset referenced by **no** container, and
the Sparrow — the only vessel Dog Fight flies — had neither asset nor entry, so neither took a speed
penalty from any prism, danger ribs included. Three shipped docs asserted the slow anyway
(`DOLPHIN_ENERGY_ECONOMY.md`'s drift-hold clause, `SPARROW_AFTERBURNER.md`'s ward step, and
`DOGFIGHT.md`'s danger-rib paragraph) because a vessel that simply never slows reads as a vessel
that is fast, and because a correctly-designed passthrough — the immunity gate really does leave
`ModifyThrottle` alone — looks verified even when nothing is being pushed through it. **Wiring
status is therefore a thing to CHECK, never to assume**: Squirrel / Dolphin / Sparrow / Manta carry
it and are pinned to one shared tuning (`speedModifierDuration 1`, `massScaling 0.1`,
`maxSlowStrength 0.5`, `dangerSlowMultiplier 3`, `dangerSlowDurationMultiplier 3` — a prism should
read the same whichever hull hits it, so moving one asset off these numbers un-shares the fleet's
collision read); **Rhino and Serpent still have no speed effect at all** and are the open item.
Note also that a *name* is not evidence of a slow: `SparrowDebuffByRhinoDangerPrismEffectSO` carries
a `vesselSlowedByRhinoDangerPrismEvent` field and a "Slow Viewer Integration" header, and only ever
muted an input.

**A prism's DEATH VISUAL wears the palette of the TIER it was wearing, never just its domain.** The dying prism's `PrismKind` rides `PrismEventData.Kind` — stamped by `Prism.Explode`/`Implode` from `PrismKinds.Of` *before* the destruction pass — and both the batched debris path and the pooled fallback tint from `SO_ColorSet.GetPrismKindColors`, the ONE composition `ThemeManager` also paints the live block materials with (`ThemeManager.PaintPrismTier`). Before this, debris was tinted from the domain alone at the PLAIN tier, so a danger prism — a frosty shielded base under the hot domain-independent danger rim — shattered into ordinary domain-coloured debris and read as a plain prism dying; shielded/super-shielded mass had the same defect on a devastating hit. **Never re-inline a tier's colour pair** at either consumer, and never fix a debris colour on the per-domain `SO_MaterialSet.ExplodingBlockMaterial` copies — nothing draws with those (`PrismDebris` reads mesh+material off the pool prefab and overrides colour PER ENTITY, which is also why a mixed-tier burst is still ONE batch and one draw: the tier must never become a reason to split a batch or swap a material). Danger alone also detonates HARDER — `PrismExplosion.DetonationGain`, authored as `dangerDetonationMultiplier` on `PrismExplosion.prefab` (1.6, set 1 for palette-only) — and that gain scales debris speed, shatter rate and the clamp band as ONE quantity, per the AOE-impulse contract above. Detail: `Docs/PALETTE.md §2.1`, `Docs/PRISM_ANIMATION.md §4.6`.

**AOE blast impulse — `Inertia` only reaches the screen with a ceiling of its own.** Every
explosion entry point (`ExplosionImpactor.ProcessBatchFrame` / `ProcessBatchConeFrame` /
`DrainPendingBatchFrame` → `PrismSpatialIndex.ProcessExplosionFrame` / `ProcessExplosionConeFrame`
/ `DrainPendingExplosionDamage`) takes ONE `ExplosionImpulse`
(`_Scripts/Controller/Projectiles/ExplosionImpulse.cs`) instead of a loose `(speed, inertia)` pair,
because debris speed is `min(Speed * Inertia, ceiling)` and the ceiling is the third number that
cannot travel separately. With no ceiling of its own a blast falls back to
`PrismExplosion.prefab`'s authored `maxSpeed` (**33.33 u/s**) — a guard sized for the legacy
`impactVector / volume` gain, not a physical bound — and **every** AOE magnitude sits far above it
(the Dolphin cone's wavefront is `height / (duration * 4)` ≈ 222 u/s, 6.7x over), so every blast
saturates to the same speed and `Inertia` is dead tuning. `AOEExplosion.proportionalDebris` opts a
blast onto the true-velocity contract `PrismEffectHelper.DamageProportional` already defines: the
vector IS the debris velocity (`speed * debrisRestitution * Inertia`) and the blast passes a matching
ceiling. Off by default; **on** for `AOEConicExplosion.prefab` (the Dolphin crystal blast) at
`debrisRestitution 1/3 x Inertia 1.8 = 0.6`, and for `AOEScarabCavitation.prefab` (the Scarab's
swept-plate dash blast, `AOECylindricalExplosion`) at `1/3 x 3 = 1.0` — the product is deliberately
**1** there because that blast's whole read is "the wall goes the way you dashed": at 1.0 the
debris velocity IS the plate's sweep velocity, so a prism leaves at exactly the speed the blast
crossed it. That is the third AOE shape (sphere / cone / swept cylinder), and a new shape must
carry the ceiling through BOTH prism paths and supply a matching narrowphase — a squat cylinder's
bounding sphere reaches ~43u BEHIND the pilot, which is harmless for prisms (they are tested
exactly) and NOT harmless for the crystal sweep, which SPENDS what it touches. Debris speed and **shatter rate are one number** on this
contract (`PrismExplosion.TriggerExplosion` re-reads `Speed` off the clamped velocity when an
override is supplied — otherwise raising the ceiling finishes the shatter in a frame while the debris
crawls), so `Inertia` scales both together; do not split them. Both prism paths carry the ceiling —
the Burst resolve and the Physics-trigger fallback (`ExecuteCommonPrismCommands`) — so a blast throws
mass at the same speed with or without the spatial index. Detail: `Docs/SPATIAL_INDEX.md` § "Impulse".

**Forcefield Crackle (Skimmer)**: `SkimmerForcefieldCracklePrismEffectSO` (at `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`) is a shader-driven alternative to `SkimmerFXPrismEffectSO` that visualizes the Skimmer's invisible sphere collider on prism impacts. It computes the impact point via `Collider.ClosestPoint` between the prism box and skimmer sphere, projects it onto the sphere surface, and forwards the event (position + duration + intensity + radius) to a `ForcefieldCrackleController` MonoBehaviour on the vessel (`_Scripts/Controller/Vessel/ForcefieldCrackleController.cs`). The controller owns all visual parameters (colors, arc density/sharpness, ring thickness, ripple speed, fresnel) as serialized fields and feeds a ring buffer of up to 16 simultaneous impacts to the shader via MaterialPropertyBlock arrays each frame. `[ExecuteAlways]` allows edit-mode preview via `ForcefieldCrackleControllerEditor` (at `_Scripts/Editor/`). The shader's custom-function HLSL file `ForcefieldCrackle.hlsl` (at `Assets/Materials/Graphs/`) uses FBM-based electrical arcs with expanding wavefronts on a geodesic distance metric so arcs follow the sphere's curvature. All three code files use the `CosmicShore.Gameplay` namespace.

### Audio (FMOD) — every sound is an exposed, editable field (LOCKED convention)

FMOD Studio is the audio middleware (`FMODUnity`, `Assets/Plugins/FMOD`). The rule below is not a
style preference — it is what makes the game's audio *authorable by whoever owns audio*, without a
programmer, a recompile, or a merge.

> **Every noise anything makes must be an inspector-exposed `EventReference` on the prefab/component
> (or SO) that makes it.** If a sound exists, an audio designer must be able to find it in the
> component view of the thing that produces it, and swap it — without touching code, and without
> hunting for which shared category it borrowed.

**Corollaries — all three are load-bearing:**

1. **Never plug in a "temp" event.** Do not point a new sound at a borrowed/placeholder FMOD event
   just to hear something. Ship the `[SerializeField] EventReference` **empty** and let it be
   silent — an empty slot is a visible, greppable TODO in the inspector; a temp event is an
   invisible one that survives to release and gets mistaken for an intentional sound. FMOD's
   `EventReference.IsNull` makes an empty slot a clean no-op, and `AudioSystem` already warns once
   per unwired category (`warnOnUnwiredCategory`) rather than failing. Follow that pattern: check
   `IsNull`, return, optionally warn once — never substitute another event.
2. **Every ship ability gets its own dedicated FMOD event field** — boost, gun fire, drift, shield,
   turret, missile, ability start/stop, whatever. One field per ability per distinct sound (a
   start/stop or charge/release ability gets a field for each). Do **not** route a new ability
   through an existing `GameplaySFXCategory` because it is "close enough" — sharing a category means
   two abilities can never be tuned independently, which is exactly what the audio owner needs.
3. **The sound is a trigger's payload, not an implicit side effect.** When something should sound on
   contact, the collider/trigger that detects the contact is where the `EventReference` lives and is
   played from. Same for a state change: the component that owns the state plays its own field.

**How to add a sound (the shape to copy):**

```csharp
[Header("Audio")]
[SerializeField, Tooltip("FMOD event played when this ability fires. Leave empty for silence.")]
EventReference fireEvent;

// at the trigger / state change:
if (!fireEvent.IsNull)
    audioSystem.PlaySFXEvent(fireEvent, transform.position);   // spatialized
```

Play through `AudioSystem` (`PlaySFXEvent` / `PlaySFXEventAttached`) or
`FMODOneShotVolumeHelper` — **never** `RuntimeManager.PlayOneShot` directly, which has no
per-instance volume and therefore ignores the SFX slider when the bus fails to resolve
(`_Scripts/Controller/FX/FMODOneShotVolumeHelper.cs` documents why). For a **looping/continuous**
sound (engine, drift, ambient, creature loop) use a `StudioEventEmitter` on the prefab — again with
the event exposed — or a small controller that owns its own `EventReference` field, like
`ShipAudioController`, `DriftAudioController`, `ProximityBoostAudioController`,
`FloraAmbientAudioController`.

**The two tiers, and which to use:**

| Tier | What it is | Use when |
|---|---|---|
| **Per-prefab field** (preferred) | `[SerializeField] EventReference` on the component that makes the noise; edited in that prefab's component view | The sound belongs to a *specific thing* — a vessel ability, a projectile, a trigger volume, a creature, a toy, a UI widget with its own voice |
| **Central category** | `AudioSystem.PlayGameplaySFX(GameplaySFXCategory.X)` / `PlayMenuAudio(MenuAudioCategory.X)`, wired once on the AudioSystem GameObject | The sound is genuinely *shared platform-wide* and must stay identical everywhere — prism destruction, crystal collect, generic vessel impact, menu clicks |

Both tiers keep the event in an inspector slot; they differ only in *where* the slot lives. If you
find yourself adding a `GameplaySFXCategory` member for one vessel's one ability, that is the signal
you wanted a per-prefab field instead. **Existing ability call sites that pass a shared category
(`BoostActionSO` → `BoostActivate`, `DriftActionSO` → `DriftStart`/`DriftEnd`) are the legacy shape**
— when you touch one, give it its own `EventReference` field (falling back to the category only when
the field is empty) rather than adding another category consumer.

Data-driven variants override the same way — a config SO carries the `EventReference` and stamps it
onto the emitter at spawn (`FaunaConfigurationSO.OverrideAudio` + `AudioLoopEvent` →
`Fauna`'s `StudioEventEmitter`), so a species can be re-voiced or silenced from its asset.

**Anti-patterns:**

- A hardcoded `RuntimeManager.PlayOneShot("event:/some path")` or any string event path in code
  (first-party code is currently clean of this — keep it that way)
- A new sound wired to an unrelated existing event "for now"
- A sound that can only be changed by editing C# or by finding one shared enum member
- An `AudioClip` + `AudioSource` for a *new* gameplay sound — the Unity AudioSource path is legacy
  (music, plus stragglers like `IconEmitter` and `AudioSystem.PlaySFXClip`) and is being retired;
  new SFX is FMOD
- Adding an if-null-guard *fallback to another event*. Guard for silence, never for substitution

### Multiplayer / Netcode & Player Spawning

Component reference, full spawn chains (menu, game, party join, freestyle flight),
`Player` NetworkVariable tables, and the player-count/AI-backfill pipeline:
`Docs/MULTIPLAYER_SPAWNING.md`. Load-bearing rules:

- Vessel spawning is ONE unified Netcode+SOAP pipeline for menu and game
  (`ServerPlayerVesselInitializer` → `ClientPlayerVesselInitializer`; menu adds autopilot
  via `MenuServerPlayerVesselInitializer`, game scenes pre-spawn AI via
  `ServerPlayerVesselInitializerWithAI`). Never add a parallel spawn path.
- **AI players/vessels spawn server-owned with `destroyWithScene: false`** (same-tick
  scene-load batching would destroy them on clients as they spawn) - so every cleanup
  path must explicitly despawn AI (`ExecuteSceneReloadReplay`,
  `SceneLoader.ClearPlayerVesselReferences`, disconnect shutdown).
- Track processed players by `NetworkObjectId`, never `OwnerClientId` - AI shares the
  host's OwnerClientId. Locality (`Player.IsLocalUser`) is reliable only after pair-init
  sets `IsInitializedAsAI` (the AI spawner writes `NetIsAI` AFTER `Spawn()`).
- The spawner never shuts down the NetworkManager - the eager Relay session persists
  across all scene transitions; teardown is explicit party-leave
  (`PartyInviteController`) or transport failure (`MultiplayerSetup.OnTransportFailure`).
- `SceneLoader` guards `if (nm.IsListening && !nm.IsServer) return` before any local
  scene load (MPPM shared-SOAP double-load protection).
- AI team assignment is deterministic (`GetBalancedDomain`: lowest total → fewest humans
  → enum order Jade→Ruby→Gold) - identical results on every machine, no shared seed.
- Menu domain reset (Jade) happens ONLY server-side in
  `MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync`; a runtime vessel swap
  keeps the player's current `NetDomain` and inherits pose + speed.
- **`IsLocalUser` vs `IsLocalPilot`.** `IsLocalUser` (= `IsMultiplayerOwner`) is the
  networked path's "locally-owned, non-AI player" and requires `IsSpawned`. `IsLocalPilot`
  is broader by exactly one case - a non-AI Player whose NetworkObject is not spawned.
  **Anything that must hold in EVERY game mode binds on `IsLocalPilot`**, so no spawn path
  can slip a human past a platform system; the prism occlusion corridor is the reference
  case (`Docs/PRISM_ANIMATION.md` §4.7).

### Party / Invite / Friends (social layer)

Two UGS sessions layer here: a **presence lobby** (lobby-only, no Relay, ≤100 players -
discovery + invite property exchange) and a **party session** (Relay-backed, ≤4 - actual
gameplay networking); both coexist with the active NetworkManager.

**Local authoritative state is never answered from a remote-published mirror (LOCKED).**
"How big is MY party / is X in it?" is answered by `IPartyRoster` (implemented by
`HostConnectionDataSO`, fed by the party **session**) - local, authoritative, zero latency.
"How big is THEIR party?" is answered by `PartyPlayerData.AdvertisedPartyMemberCount` /
`AdvertisedPartyMaxSlots` - presence-**lobby** properties, a hint, poll latency. Answering
the first with the second's data is what let three players in ONE party render 2/4, 1/4 and
3/4 simultaneously: with N members that is N independently-published scalars and nothing
that reconciles them, so **no polling cadence can ever fix it** (which is why several
cadence/push/jitter passes did not). The `Advertised` prefix and the `IPartyRoster` surface
exist to make the mistake unspellable - do not remove either as redundant. Full analysis:
`Docs/PresenceSystem/BUGS.md` B15. Roster changes broadcast on the coalesced
`HostConnectionDataSO.OnPartyRosterChanged` (one raise per settled mutation) - listen to
that for repaints, not to the per-member joined/left events.

Single writers:
`HostConnectionService` → `HostConnectionDataSO`; `FriendsServiceFacade` → `FriendsDataSO`
(UI reads SOAP lists/events, never calls UGS directly; presence updates go through
`FriendsInitializer` only; invites are per-player lobby properties so no host privilege is
needed). Service/UI/SO-asset inventories + facade API: `Docs/PARTY_SOCIAL.md`; spawn
chains for party join + menu freestyle flight: `Docs/MULTIPLAYER_SPAWNING.md`.

#### Party / Presence / NetDiag docs — start at `Docs/README.md`

Full engineering docs for these subsystems live under `Docs/` (the index +
shared conventions are in `Docs/README.md`). Route by task:

| If your task… | Read first |
|---|---|
| Touches `HostConnectionService` / `PartySessionService` / `NetworkTransitionService` / `PartyInviteController` | `Docs/PartySystem/ARCHITECTURE.md` (+ `BUGS.md`) |
| Touches `PresenceLobbyService` / `LobbyPropertyWriter` / `LobbyRefreshScheduler` / `InviteService` / `AcceptanceSignalService` | `Docs/PresenceSystem/ARCHITECTURE.md` |
| Classifies / logs a party·lobby·session·transition catch failure | `Docs/NetworkDiagnostics/ARCHITECTURE.md` |
| Run the MPPM regression before a commit | `Docs/PartySystem/TESTS.md` (S-series) + `Docs/PresenceSystem/TESTS.md` (P-series) |
| Validate the NetDiag overlay itself | `Docs/NetworkDiagnostics/TESTS.md` (Tests A–E) |
| Log / triage a bug | `Docs/PartySystem/BUGS.md` (B2/B3/B5/B7) · `Docs/PresenceSystem/BUGS.md` (B1/B4/B6) |
| Pick up refactor work | `Docs/PartySystem/REFACTOR.md` · `Docs/PresenceSystem/REFACTOR.md` |
| Read what was already tried (session history) | `Docs/PartySystem/MPPM_SESSION_LOG.md` |

**Locked design (do not relitigate):** EAGER per-user Relay — every player
hosts their own Relay-backed party session on entering `Menu_Main`. **Do not
reintroduce LAZY / on-first-invite creation** (the shutdown-and-recreate
cascade it caused is the root of every recurring party-invite bug). Full rule
+ rationale: `Docs/README.md` § "Locked design" and
`Docs/PartySystem/ARCHITECTURE.md` § "Locked design" / "Unbreakable exit
criteria".

**Threading prerequisite (shipped):** the UGS / Netcode `Task` continuation → SOAP
off-thread → `EnsureRunningOnMainThread` cascade is resolved by `MainThreadDispatcher`
+ `.AsMainThread()` at every UGS / Netcode `await`. See `Docs/THREADING.md`.
**Do not** introduce `UniTask.SwitchToMainThread()` or
`UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — both have
been tried and proven unreliable on this UniTask version.

### Domain Game Modes (HexRace / Joust / Crystal Capture / Astro League / Brood Rush)

Per-mode technical references live next to the controllers
(`_Scripts/Controller/Arcade/HEXRACE.md`, `JOUST.md`, `CRYSTAL_CAPTURE.md`, `RAMPAGE.md`,
`ASTROLEAGUE.md`, `NUCLEUSRUSH.md`; condensed HexRace notes in
`Docs/HEXRACE_SUMMARY.md`). Cross-mode rules:

- **Domain-aggregated scoring**: modes end on a per-domain sum via the mode's
  `ScoringRuleSO.IsObjectiveReached` (`ScoringMetrics.SumByDomain`) - at most three
  scores ever exist (Jade/Ruby/Gold); teammates contribute to one total. The comeback
  system (`ElementalComebackSystem`, REQUIRED in every party game, auto-created by
  `MultiplayerMiniGameControllerBase.EnsureExists`) keys off domain aggregates too.
- **Replay is a full network scene reload** (`UseSceneReloadForReplay = true`) for all
  shipped modes - flora/fauna/environment don't reset in place.
- **Server-authoritative winners**: detection runs in `OnTurnEndedCustom()` on the
  server; results broadcast via the shared `SyncFinalResults` template.
- **End-game/win-condition COUNTS** are authored ONLY through FrogletTools > Game Modes >
  End Game Conditions (`Resources/EndConditionOverrides.asset`) - never per-scene
  inspector fields.

### FTUE (First-Time User Experience)

Tutorial system at `Assets/FTUE/` (25 C# files) using adapter pattern with clean interface separation:

- **Interfaces**: `IFlowController`, `ITutorialExecutor`, `ITutorialStepHandler`, `ITutorialUIView`, `IAnimator`, `IOutroHandler`, `ITutorialStepExecutor`
- **Adapters**: `TutorialExecutorAdapter`, `FTUEIntroAnimatorAdapter`, `TutorialUIViewAdapter`
- **Data models**: `TutorialStep`, `TutorialPhase`, `TutorialSection`, `TutorialSequenceSet`, `TutorialStepPayload`, `TutorialStepType`, `FTUEProgress`
- **Drivers**: `FTUEIntroAnimator`, `TutorialFlowController`
- **Step handlers**: `FreestylePromptHandler`, `IntroWelcomeHandler`, `LockModesExceptFreestyleHandler`, `OpenArcadeMenuHandler`
- **UI**: `TutorialUIView`, `InGameTutorialFlowView`
- **Events**: `FTUEEventManager` (SOAP-based event broadcasting)

### Dialogue System

Custom dialogue system spanning two locations:

- **Editor & assets**: `Assets/_Scripts/DialogueSystem/` — animation controllers, shader graphs (SpriteAnimation, UI_NoiseDissolve), SO dialogue data assets, prefab
- **Runtime code**: `Assets/_Scripts/System/Runtime/` — `DialogueManager`, `DialogueEventChannel`, `DialogueUIAnimator`, `DialogueViewResolver`, `DialogueAudioBatchLinker`
- **Models**: `Assets/_Scripts/System/Runtime/Models/` — `DialogueLine`, `DialogueSet`, `DialogueSetLibrary`, `DialogueSpeaker`, `DialogueVisuals`, `DialogueModeType`, `IDialogueService`, `IDialogueView`, `IDialogueViewResolver`
- **Views**: `InGameRadioDialogueView`, `MainMenuDialogueView`, `RewardDialogueView`
- **Editor tools**: `DialogueEditorWindow`, `DialogueLineDrawer` (in `_Scripts/Editor/`)

### AI Opponent System

Runtime-configurable AI opponents at `Assets/_Scripts/Controller/AI/`:
- `AIPilot` controls AI vessel behavior
- `AIGunner` controls AI targeting/shooting
- AI profiles configured via `SO_AIProfileList` (`MainAIProfileList.asset`)
- AI profiles used for score cards and multiplayer backfill
- Configurable AI ship selection and behavior at runtime

**A pursuing AI ORBITS anything inside its own minimum turn radius, and that is geometry rather
than tuning** (`AI_ORBIT_BREAK.md`, `PursuitReachability`). A vessel at speed `v` with max turn
rate `ω` cannot fly tighter than `R = v/ω`, so pure pursuit can never reach a target inside either
circle of radius `R` tangent to its velocity — it turns as hard as it can, forever. **Turning
harder is exactly the wrong response, which is why the failure survived every tuning pass.** It is
the Dubins (1957) reachability condition and the fix is the manoeuvre pilots use — *extend and
re-attack*: fly out, come around, come back in. Five things to carry: (1) the test collapses to
**`|d| < 2R·sin θ`** (the circle test with the `R²` cancelled — proven exact against the long-hand
definition over 20k cases), and its **exit condition falls out of the same line**, since `sin θ ≤ 1`
means `2R` of separation is a GUARANTEE of reachability rather than a tuned threshold; (1b) **an
objective is not a POINT — it has a capture radius `c`, and leaving it out is a defect rather than
a simplification**: at `c = 0` the test asks whether the vessel can fly onto an infinitely small
target, which 20u out is false for any bearing error over 14°, so the AI peels away from crystals
it was about to collect. The generalisation is exact (`|d|² + 2Rc − c² < 2R·|d⊥|`, guaranteed
separation `2R − c`) and its OTHER root, `|d| ≤ c`, IS the don't-peel-away-on-final-approach case —
not a special case bolted on but the second half of the same solution. Err GENEROUS on `c`: too
small peels off with nothing to catch it, too large just means the orbit detector catches it a
beat later; (2) **`R` is
a property of the vessel AT THIS SPEED, never an authored constant** — a boosted Dolphin's
unreachable bubble is 372u against 83u at cruise, off the same authored 110°/s, so it is derived
live in `VesselTransformer.MinTurnRadius`; (3) reason from **`Course`, not the nose** — the radius
applies to the direction of TRAVEL, and the two differ during a drift (which is also why the
break-off is suppressed there: a locked course reads as an infinite radius, i.e. an unbreakable
orbit); (4) a geometric test cannot catch an orbit it does not describe, so `OrbitDetector` is the
empirical backstop — swept angle with no progress — and its progress gate must compare against the
range at the START of the window, because **a running minimum tracks a steady approach downward and
can then never register progress at all**, silently degrading the detector to "constant range only";
and (5) **how far the break-off flies out is a TIME, not a distance** — `2R/v = 2/ω` is constant for
a given turn rate, so `speed × approachRunSeconds` buys the same straight run at 60 u/s and at 357,
and it is simultaneously how long the pilot spends leaving and how long the return leg lasts. The
fleet runs 1.5s; **the Dolphin is authored at 2.5s** because it is the one vessel that AIMS on the
way in (it locks course on the crystal then swings its nose onto a rival — 180° at 110°/s is 1.64s,
so the run has to cover it). Measured: 373/400 randomized objectives reached → **400/400**, for
+0.18s of mean time.
  **A reported "the AI dodges the crystal at the last second" was NOT the break-off** — two
  plausible fixes were measured and rejected first (a commit range trades the dodge for orbiting,
  because the turning-circle test is structurally a sub-1s test; a look-ahead factor moves the
  median but not the earliest and costs 9× the break-offs). The cause was
  `AIPilot.UpdateCellContent` comparing a squared distance against `MinDistance * MinDistance`
  while `MinDistance` already held a squared distance — a `d⁴` threshold that **every** later
  candidate passed, so the pilot took the LAST eligible crystal rather than the nearest, and
  re-picked arbitrarily on every `OnCellItemsUpdated` (which every respawn raises). General rule:
  **a comparison that mixes a squared quantity with a linear one is invisible to review and to
  every static check, and its symptom is a behaviour nobody attributes to arithmetic.** Selection
  now lives in `AIObjectiveScoring.Select` — pure, list-based, so the shipped path IS the tested
  one — with commitment hysteresis (`objectiveSwitchImprovement` 0.75) so a crystal event can no
  longer re-point a pilot that is a second from arriving. Two more from the same report, both
  about the Dolphin never aiming at the player: **a provider that NAMES an aim point must not
  inherit the fallback's "would this drift actually turn the vessel" test** (the mass cluster's
  only job is to find somewhere interesting to point, so it defers when the objective is already
  ahead; an explicitly named rival being ahead is the BEST case, and rejecting it turned the nose
  away from exactly the pilot it was lining up on), and **an AI's engagement range must track its
  weapon's** (Bends capped `aiAimMaxRange` at 900 against `AOEConicExplosion.prefab`'s authored
  `height: 2400`).

**AI pilot lifecycle (do not regress):**

- `VesselController.ToggleAIPilot(bool)` is the single choke point for AI control of a
  vessel — it also stops any `AICinematicBehavior` flourish (the `EndGameSequencer` starts
  its behavior *after* enabling the pilot, so the end-game flourish still works). Never
  call `AIPilot.StartAIPilot`/`StopAIPilot` or start a cinematic around this seam.
- **A human-controlled turn never starts with autopilot on**: `Player.StartPlayer`'s human
  branch calls `ToggleAIPilot(false)` (symmetric with the AI branch). A leaked `AutoPilotEnabled`
  blocks every button action in `R_VesselActionHandler` while `AIPilot.Update` fights the
  player's input — the root of the "AI drifting conflicts with my drifting" class of bug.
- `AIPilot` and `AICinematicBehavior` keep their `enabled` flag mirrored to their active
  state: no `Update()` dispatch on vessels they aren't driving. `StartAIPilot` is
  idempotent; `StopAIPilot` uses `StopAllCoroutines` (a `StopCoroutine(new enumerator)`
  never stops the running coroutine).

### Menu Screen Navigation (Menu_Main)

`ScreenSwitcher` slides side-by-side panels and discovers `IScreen` implementors
automatically (`OnScreenEnter`/`OnScreenExit`) - never hard-wire screen references.
Reuse `ProfileDisplayWidget`, `NavLink`/`NavGroup`, `ModalWindowManager`; cache component
lookups; pair every subscribe with an unsubscribe; prefer `[Inject] AudioSystem` for new
code. Screen inventory + component reference: `Docs/MENU_NAVIGATION.md`.

**The menu camera uses NO Cinemachine, and what a config frames is decided by its
`MenuCameraRigKind`, never by a target field.** `MainMenuCameraController` drives the scene
camera directly through `MenuCameraConfigSO`; orbit / trail / chase / top-down frame the
LOCAL VESSEL, while **`LavaLamp` frames the CELL** - the ambience shot, a ~2-minute orbit of
the cell centre aimed at the crystal. Being the only vessel-free rig it runs from scene load
instead of waiting on the spawn chain, and it is the only kind that reads `CellRuntimeDataSO`
(optional). Two numbers on it are derived, not tuned: its **radius is 686, not the legacy
350, because the nucleus roughly doubled** (at 350 the camera now orbits *inside* the
nucleus), with a hard ceiling at the toy ring (`MembraneRadius x membraneFraction` = 984
minus a 42-unit trigger, so any radius under 942 stays clear) - re-derive it if the nucleus,
membrane or toy ring changes; and **roll comes only from `lavaLampPoleBlendStart`**, a ROLL
dial rather than a numerical-safety limit, defaulted to **0.99** for provably zero roll
(0.85 fired on 43% of crystal spawns for a median 5.3 deg tilt). A pole-CROSSING orbit must
lower it instead. Full derivation + tables: `Docs/CameraMigrationReview.md`.

### Lava-Lamp Mode (Menu Freestyle)

**"Lava lamp" and "freestyle" are the same thing** - one system, two names (viewed from
the menu vs player-controlled). BOTH standalone freestyle games are retired and must not
be reintroduced; party members fly the lava lamp together in Menu_Main. **Mass is
conserved in the menu too**: the lava-lamp vessel IS the freestyle gameplay vessel - no
trail caps, prism TTLs, or idle cullers (a menu ring-buffer cap shipped once and was
reverted; see Design Philosophy). Manage menu-idle prism growth with fauna cleanup or by
pausing the spawner. Freestyle input ownership (gamepad vs UI `sendNavigationEvents`),
HUD-after-swap, Game UI hierarchy, and the phased HUD/shape/scoring rollout:
`Docs/LAVALAMP.md` + `Docs/ToySystem/ARCHITECTURE.md`.

### Elemental Bars (per-vessel buff/debuff display)

Every vessel conveys elemental buffs/debuffs through `ElementalBarsView` - a 5-petal
"flower" per element driven by `ElementalBarsController` (null-safe, opt-in rollout per
vessel; named `SilhouetteController` until the vessel silhouette / trail-display HUD
element it also drove was removed). **All shared look/feel lives in `ElementalBarsConfigSO`**
(`Resources/ElementalBarsConfig.asset`) - never per-vessel SerializeFields. Petals are
pure-white silhouettes multiply-tinted at runtime - **never hue-shift**. Petal math,
level→colour table, juice, wiring tool, and perf notes: `Docs/ELEMENTAL_BARS.md`.

**The maintained-mechanism law (LOCKED).** No sustained/held mechanism may HOLD an element
above integer level **10** — the 10..15 overcharge band belongs to **transients only**, and
everything in it drains back to (at most) 10: temporary effects decay to zero, crystal-earned
base overcharge bleeds down (`RecoverBaseLevels`), the domain fauna buff's held layer fills
only to 10 with over-ceiling increases converted to draining spikes, and the comeback bonus
fills toward 10 and never past it. The player always gets to *feel* a reward above 10, and the
drain always restores the headroom to feel the next one. Enforced in `ResourceSystem`
(`SustainedCeiling`, `CompositeEffectiveLevel`); mechanics log: `Docs/ECOSYSTEM.md §15`.

### Elemental Hull Morphs (the vessel model is an element display)

The vessel's own hull conveys its element levels: vessel models carry **blend shapes on their
skinned meshes labeled by element name** (`charge` / `mass` / `space` / `time`, case-insensitive —
authored into the FBX), and `VesselAnimation` (base class, runs on every vessel) discovers them **by
name** at `Initialize` and glides each between its extremes as the effective element level moves
through the **[0, 10] progression band** — the deficit band [-5, 0) holds the level-0 silhouette,
the overcharge band (10, 15] holds the level-10 authored extreme (the same effective level the HUD
flowers read, so hull and flowers always agree). Transitions are DOTween glides, never snaps —
continuity of existence applies to the vessel's own body.

- **Single source of feel — `VesselElementalMorphConfigSO`** (`_Scripts/ScriptableObjects/`, asset
  at `Resources/VesselElementalMorphConfig.asset`): morph duration + ease, plus the pure helpers
  (`NormalizedMorphWeight`, `TryResolveElement` — both edit-mode tested in
  `VesselElementalMorphTests`). Spec changes go in the asset, never per-vessel fields.
- **Opt-in by art, zero wiring.** A vessel morphs the moment its model ships element-labeled shape
  keys — no per-prefab flags (the old `UseShapeKeys` bool + hardcoded shape indices are retired).
  Non-element art shapes (jaws, tendrils) are untouched; a name mentioning two elements is ambiguous
  and ignored. The shape's authored extreme is read from its last frame weight, so 0-100 and custom
  frame weights both work.
- **Fleet status**: audit with **FrogletTools > Vessels > Audit Vessel Elemental Morphs** (asset-only,
  no play mode, uses the exact runtime discovery). Manta/Termite/Falcon/Shrike (Manta meshes),
  Sparrow, Serpent, and Squirrel ship labeled shapes; Dolphin/Urchin/Rhino prefabs still wire
  shape-less test/placeholder meshes and need the rig swap below; Grizzly has no labeled shapes yet.
- **The Squirrel's FBX is a spliced hybrid of two historical exports — do not re-export over it
  blindly.** The 2024-10-29 export (`aa5046d41`, "add squirrel with shapekeys") carried
  `Time/Mass/Space/Charge` but its takes were broken; the 2024-11-15 re-export (`dc2c8ea54`,
  "fixed squirrel animations") repaired 2,622 of 3,483 bone curves across all 9 takes **and
  silently dropped all four shape keys** — which also silently killed the elemental morph surface.
  The shipped file is the fixed export with the four shape-key subtrees grafted back at the FBX
  binary level (valid because both exports share byte-identical topology and vertex drift ≤2e-6;
  verified by byte-level structural diff: base objects untouched, takes byte-identical to the fixed
  export, shapes byte-identical to the shape-key export, and **zero blend-shape animation curves**
  — the donor's constant-zero residue curves were deliberately left out). Same path + GUID; the
  mesh fileID is a name-hash shared by both exports, and the `.meta` pins each clip's take name to
  an explicit internalID matching `SquirrelAnimatorController 1`'s motion references — so the
  nested prefab instance, the animator clips, and the blend-space puppetry
  (`MantaAnimationContoller` → Animator floats `Pitch/Yaw/Roll/Throttle`) all keep binding. Any
  future Squirrel re-export must carry BOTH the fixed takes and the four element shape keys.
- **Morph weights are written in `LateUpdate`, which is load-bearing.** Unity's Animator writes
  bound curves every frame during the animation update — after `Update`, where tweens run — so an
  export that carries even constant-zero blend-shape curves would stomp script-set weights every
  frame. Tweens therefore drive a cached weight and `VesselAnimation.LateUpdate` is the single
  writer to the renderers, making the element level authoritative over any stray animation curve on
  any vessel (the current Squirrel takes are clean, but the defense is deliberate). Do not
  "simplify" the tween to write the renderer directly.
- **Animated parts resolve BY NAME too** (`VesselAnimation.ResolvePart`, `ResolveParts()` hook):
  an authored inspector reference always wins, and an empty one is looked up among the model's
  descendants by candidate name — current rig bone first, legacy part name as fallback. This is
  what makes an art swap cheap: the stale references come back null and the rig's bones bind
  themselves. Unbound parts are reported (`ReportUnresolvedParts`) and degrade to "that limb
  doesn't move", never a per-frame `NullReferenceException`.

#### The rigged-model swap (Dolphin / Urchin / Rhino)

These three are the fleet's only vessels whose art cannot morph, and it is **not** a wiring
oversight — their prefabs wire fundamentally different models. `Dolphin_Test.fbx` is 17 separate
static part meshes, `Urchan_Test.fbx` 14, and Rhino wires `Vessel_Placeholder_1.fbx` (a literal
placeholder); none carries a single blend shape. Their `*_shapekey_with_animations.fbx` rigs are
one skinned mesh on an armature **plus** the four element shapes — and each rig was authored FOR
that vessel's script: the dolphin rig's `jetT/jetm/jetB × .l/.r` + `jaw.u`/`jaw.b` are exactly
`RiptideAnimation`'s six thrusters and two jaws; the rhino rig's `wing1.*`/`jet.*` are
`RhinoAnimation`'s wings and engines (its `wing2.*` back wings host colliders, nothing drives them);
the urchin rig's `gunM.*`/`jetT.*`/`jetB.*` are `UrchinAnimation`'s guns and jets. The three scripts
name those bones as their primary resolution candidates, so the **code half of the port is done**.

**Rest poses are the reason a rig needs more than a name swap.** Puppetry drives a part *toward* an
absolute local rotation, which silently assumes it rests at identity — true of part-per-mesh art
placed by translation alone, false of a rig, where the bone's rest angle is what fans the engines
out (`wing1.l` rests at ~42°, `jet.l` at ~115°, `gunM.l` at ~90°). So `VesselAnimation` gained
`CaptureRestRotations` / `RotatePartFromRest` / rest-aware `ResetAnimation`: parts are driven
**relative to the pose they were authored in**. Identity-rest art is unaffected; rigged art holds
its shape. Two Dolphin bugs surfaced from the same root and are fixed: `RiptideAnimation` re-homed
its drift parts onto `Chassis` every non-drifting frame (a no-op on the old art, where they were
already its children — on the rig it would have permanently flattened the armature onto `fuse` and
collapsed the six jets onto one point; it now restores each part's **own** captured parent), and its
`InitialRotations` list was indexed two slots out of step with `animationTransforms`, so each engine
animated around a neighbour's rest pose. **That second fix changes the Dolphin's current look** — its
six engine cases rest at 26–169° and were being dragged toward identity.

The prefab half is a **hands-on editor pass**, not an automated one: a `SkinnedMeshRenderer`'s bone
list, bindposes, bounds and imported mesh IDs are owned by Unity's FBX importer, collider volumes
were authored against the old silhouettes and must be re-fitted by eye, and every legacy part
carries its `MeshRenderer` alongside its collider — so moving one onto a bone without retiring its
renderer welds the placeholder ship to the new skeleton. Run **FrogletTools > Vessels > Plan Vessel
Rig Swap** (report only, never writes): it prints, per vessel, which gameplay object belongs on
which bone, which objects have **no mapped bone** and would go dark when the old model is disabled
(Rhino's `ForceFieldSkimmer` parents to the legacy root), the rig's element shapes, and the ship-
geometry re-point. The printed procedure ends by clearing the animation's part fields — leave them
**empty** so they resolve to bones — and re-running the morph audit.
- **Seeding**: `VesselAnimation` snaps to live levels at `Initialize` (the live initial emit is
  `ResourceSystem.Start`), and `ResourceSystem.InitializeElementLevels` now emits
  `OnElementLevelChange` (deduped) so a mid-session re-seed repaints every consumer — hull morphs,
  HUD flowers, and ability unlock state alike. Note `SetResourceLevels` currently has **no live
  caller** (its historical MiniGame turn-reset and Hangar call sites are commented out); the emit
  future-proofs any revived re-seed path.

### The Four-Icon Ability Row (LOCKED structure — every vessel HUD)

Every vessel HUD shows **exactly four ability icons in the lower right — one per ability** — and the
order is not a layout preference, it is the element contract made visible:

> **The icons run charge → mass → space → time, left to right — the same order as the element
> flowers above them.** Each icon sits under the element that upgrades that ability (per the vessel's
> `ElementalAbilityMapSO`), so "which flower do I fill to upgrade this?" is answered by position alone.

`VesselHUDView.AbilityDisplayOrder` is the single source of that order — `VesselHUDController`'s
upgrade-seeding loop and `ElementalBarsView`'s flower layout read the same array. `OnValidate` keeps
the `abilityIcons` list sorted into it; `VesselHUDView.ValidateAbilityIconRow()` (editor-only, called
once from `VesselHUDController.Initialize`) warns on the wrong icon count, an out-of-order binding, or
a layout whose left-to-right order contradicts the bindings.

**The upgrade signal** (element hits its unlock level, default 5 — the all-petals-white flower):
`R_VesselElementalAbilityHandler.OnUpgradeStateChanged` → `VesselHUDController` →
`VesselHUDView.SetAbilityUpgraded`. Three independent layers, so the signal survives any per-vessel
presentation: (1) **authored sprite swap** (`AbilityIconBinding.upgradedSprite`, restored on re-lock —
authored art only, never runtime-generated); (2) the **element badge** — that element's petal in the
level-5 white from `ElementalBarsConfigSO`, blooming in / withering out per the continuity law, and a
*child* of the icon so per-frame icon repaints can never stomp it; (3) an optional **tint + persistent
scale bump** with a one-shot unlock punch.

- **Icons that are live gameplay gauges** (cooldown fill, heat tint, drift lean, impact flash) set
  `tintIconOnUpgrade = false` — never overload a gauge colour with upgrade meaning — and their view
  **must** override `SetAbilityUpgraded` to re-anchor its captured rest scales to
  `AbilityIconRestScale(element)`, or its own tweens settle back to the pre-upgrade scale and wipe the
  bump. `SquirrelVesselHUDView` is the reference implementation.
- **Fleet status** (audit it yourself: **FrogletTools > Vessels > Audit Vessel Ability Rows**, which
  reports every vessel's compliance against this contract from assets alone, no play mode):

  | vessel | map | icons | order | uniform | hints |
  |---|---|---|---|---|---|
  | Squirrel | complete | 4/4 | ✅ | ✅ | ✅ bound |
  | Sparrow | 4/4 named, **4/4 upgrades** (Time re-scoped 2026-08: indefinite boost, base roll, Elemental Ward. **Mass L5 = Shielded Prisms again** — it briefly moved to Space 5 in 2026-08 round 4 and was returned by design sign-off on 2026-08-13, settling the split: **MASS owns the SUBSTANCE of what you fire** (turret prism stretch, in-flight round growth, armour) and **SPACE owns its REACH** (range, and pierce on both fire modes)) | 4/4 | ✅ | ✅ | ⚠ no switcher on its HUD |
  | Dolphin | complete | 4/4 | ✅ | ✅ | ⚠ no switcher on its HUD |
  | Urchin | complete (4/4 named, 4/4 upgrades; re-cut 2026-08-18 — Charge owns the merged spike weapon, Space the new track projector) | 0/4 | — | — | n/a — **no `UrchinHUDVariant.prefab` exists**, so `UrchinVesselHUDController`/`View` are unreferenced code |
  | Manta | 3/4 named, 0/4 upgrades | 0/4 | — | — | n/a |
  | Rhino | 1/4 named, 0/4 upgrades | 0/4 | — | — | n/a |
  | Serpent | 1/4 named, 0/4 upgrades | 0/4 | — | — | n/a |

  The Dolphin deliberately runs with **both** `tintIconOnUpgrade` and `showUpgradeBadge` off —
  all four of its icons are live gauges, so the persistent scale bump is its only upgrade
  signal, which is why nothing in `DolphinVesselHUDView` writes an icon transform per event.
  Its Space slot **does** tint — the jaw pair blends to `ElementalBarsConfigSO.limeColor` over
  the top 15% of banked skim energy — but that is a GAUGE colour carrying gauge meaning, and it
  lands on the jaw halves, not on the row's (fully transparent) Space icon, so it never collides
  with the upgrade path. Reading it as an upgrade tint is the mistake to avoid.
  Mechanics: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.
  **Since 2026-08-17 the whole map is cut around ONE weapon**, because the Dolphin has
  essentially one offensive act — bank energy by skimming, fly into a crystal, release a cone —
  and each element now owns one ORTHOGONAL DIMENSION of it, so the four-icon row reads left to
  right as the whole weapon: **energy → gape · Charge → thickness · Space → reach · Mass → when
  the next crystal arrives · Time → the boost that gets you there**. Charge owns the **Echo
  Sight** (RT) *and* the blast's capsule DIAMETER (`0.75x` the authored core at rest rising to
  `1.5x` at level 10) — the profile you are widening is the profile the sight draws — and since
  `halfLength + radius` is always `maxScale / 2`, Charge does not enlarge the blast, it
  REDISTRIBUTES its extent, trading a long thin fan for a short fat capsule. That pair is the
  fleet's first use of **`ElementalScaling.MultiplierFromRest`**, the opt-in un-anchored twin of
  `Multiplier`: the default anchors at exactly 1 at the resting level so an element can only ADD
  to a vessel's baseline, and handing an element a parameter's whole RANGE means the authored
  value becomes what a MID-level vessel gets — a real, deliberate baseline change, not a bug.
  Charge 5 ("Pilot Echo") extends the sight from mass to PILOTS, and **a highlight competes with
  everything else the same trigger lights up** — the first version only raised `_ColorMultiplier`
  and was invisible in Rampage, because the sight lights all ~9,800 cactus prisms at once so
  brightness was the one channel already saturated, and a hull tint says nothing at all about a
  pilot standing BEHIND mass. It now marks a vessel two ways, each covering a case the other
  cannot: the hull is driven to its own **saturated domain colour** (`_Color1`/`_Color2` as well as
  `_ColorMultiplier`, lerped from each material's own authored values, so it is a shift and a Ruby
  pilot can never read as Jade — HUE is what separates a ship from lit mass), and an additive
  **halo** (`EchoSightHalo.shader` — a soft disc with a hard RING at the hull's silhouette) drawn
  `ZTest Always` so it reads through prisms and in empty space. Three render states there are
  load-bearing: `ZTest Always` (the only way "behind mass" can read), `Blend One One` (can only ADD
  light, so it never darkens what it marks and never needs a sort order), `ZWrite Off` (can never
  occlude the world). It is hand-written ShaderLab because Shader Graph cannot express "ignore the
  depth buffer" on a URP Unlit target; it billboards in the VERTEX shader from the object origin so
  the halo costs no per-frame CPU transform write and one shared unit quad serves every size (the
  radius is a shader property, never a transform scale); and it is sized by
  `PrismOcclusionCorridor.MeasureCircumscribedRadius`, the corridor's own hull measurement, so a new
  vessel of any size is correct with nothing authored. **A locator must not obey perspective** — a
  world-sized disc vanishes exactly when it is most needed, so the radius is
  `max(what it subtends at this depth, a screen-space FLOOR)`: hull-sized and silhouette-tracing up
  close, constant angular size past the crossover (measured 59 px at 1080p out to the 2400u max
  reach, vs ~20 px before). That is why the offset is applied in CLIP space and pre-multiplied by
  `w` — surviving the perspective divide is what turns a world size into a screen size — and why
  the x offset carries the inverse aspect. The cost is that the ring stops tracing the silhouette at
  range and becomes a reticle, which is the correct trade: the trace separates a ship from mass it is
  tangled in (a close-range problem) while at range the job is only "there is a pilot over there".
  **The sight's RANGE gate needs nothing added** — `BlastVolume.Height` is already the Space-scaled
  cone reach and both consumers reject past it, and fauna/flora are already covered because a
  creature's body prisms are `HealthPrism : Prism` and draw with the two graphs the sight is spliced
  into; crystals are the one thing it does not reach (`DOLPHIN_CRYSTAL_SEEDING.md` §11). **Per-vessel CPU is correct there and would
  be a violation on prisms** — the prism half of the same sight is a global uniform only because
  there are tens of thousands of them; a dozen vessels already individually simulated, lit only
  while a trigger is held, is the ordinary tool. Both halves share ONE predicate
  (`BlastVolume.Contains`, the CPU transcription of `AOEConicSweepQueryJob`), so a highlighted
  vessel and the prisms around it light up together.
  **Mass took crystal seeding** off Charge (recharge multiplier renamed
  `cooldownMultiplierAtFullMass`), **Twin Seed is retired** — one crystal per cycle at every
  level — and Mass 5 ("Claimed Seed") changes the seed's TIER instead of its count: below it the
  seed is a free-for-all OMNI crystal wearing the lime CTA, so your own ammunition stands in open
  space for whoever reaches it first; at Mass 5 it lands TEAM-locked. Both halves of that gate
  move together — the prefab swap (`OmniCrystalImpactor` → `TeamCrystalImpactor`) AND the
  `ownDomain` stamp, which is simultaneously `Crystal.CanBeCollected`'s gate and what
  `ResolveActivationMaterial` paints from, so a crystal always LOOKS exactly as collectable as it
  is (`Docs/PALETTE.md` §2.2). **Mass gave up the trail entirely** (`trailVolume` disabled,
  `massUpgradeShieldsTrail` off — the machinery stays, it is the Squirrel's Heavy Trail, it is
  just no longer wired here). Its HUD row was re-cut to match: Charge draws a **procedural**
  blast-profile capsule (`BlastProfileGraphic` — a sprite ladder would quantize a continuous
  function of two live meters and silently stop matching the blast on the first retune), Mass the
  seeding recharge, Space the jaws plus a widened prism tally, Time the boost ring. **Space reports
  what a blast did to MASS and Charge what it did to the LIVING** — pilots debuffed and creatures
  killed, two stacked bare numbers in the prism tally's own grammar, told apart by palette colour
  (pilots in `whiteColor`, the colour the engaged sight wears; creatures in `blueColor`, the
  neutral-lifeform range a living heart already wears). The two counts arrive differently and the
  asymmetry is the lesson: the blast can report PILOTS itself (`ExplosionImpactor` keeps a per-blast
  vessel ledger, so a target loitering in a growing cone counts once, and `OnBlastResolved` now
  carries a `BlastTally` struct so the next quantity is an added field rather than two silently
  reordered ints), but it cannot report CREATURES — a creature dies when its last body prism is
  destroyed and the ECOLOGY announces that several steps downstream
  (`CellRuntimeDataSO.OnFaunaKilled`, carrying the killer's NAME), so fauna are counted over the
  blast's own lifetime between the new `OnBlastBegan` and `OnBlastResolved`. That window is exact
  only because the blast is the Dolphin's ONLY prism-destroying force, and two blasts overlapping
  inside the 0.15 s cooldown would share a count — fine for a tally, **never** for scoring, which is
  `StatsManager`'s job off the same channel. **Colour is a
  LANGUAGE across that row, not per-icon decoration** (second pass, same day): the Charge profile
  crosses the shared `ElementalBarsConfigSO` ladder's **grey → white** — already the HUD's words for
  "not in use" / "in use", since a petal steps through exactly those two between levels 0 and 1 — and
  the Mass slot crosses **lime → the pilot's own DOMAIN colour**, because the upgrade's whole point
  is that the seed becomes a TEAM crystal, so the slot says which team. It uses **`SO_ColorSet.GetDomainSignalColor`** — the domain UI colour with its
  brightest channel driven to 1 — resolved LIVE off `GameDataSO.ThemeManagerData.ColorSet`, the path
  every other domain-tinted UI reads, so the domain-changer toy re-colours it and nothing is
  snapshotted at component-creation time. **A crystal colour is NOT a domain's UI colour**: the slot
  first sampled `DullCrystalColor` on the sound reasoning that the icon should wear what the crystal
  wears, and rendered BLACK — that field is authored `(0,0,0)` on Jade, Ruby AND Gold, which is right
  on a faceted crystal (a near-black body with a bright fresnel rim) and unusable in UI, while
  `BrightCrystalColor` tops out at value 0.75. The new accessor returns white for an unauthored
  domain, because a colour accessor that can return black can make a UI element vanish, and a
  vanished element reads as "not implemented" rather than as "mis-tinted" (`Docs/PALETTE.md` §2.4). A **Space reach bar was tried and dropped**: reach only moves when the
  element moves, so a near-static line competed with two live gauges, and the slot says more by
  saying only ANGLE and AMOUNT. One general lesson from the same pass: **a centre-fan triangulation
  of a generated `MaskableGraphic` is only as good as its outline ORDERING** — the profile's caps
  were swept from the wrong basis vector, so the outline jumped across the shape and the fan drew a
  bowtie with hollow wedges; a mis-ordered outline does not fail, it renders a plausible wrong shape,
  so check for a simple convex loop (area, and that no step between consecutive vertices crosses the
  interior) rather than for "vertices roughly in the right places". Record:
  `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` §8.
  Before that, **from 2026-08-14 its Charge ability was PASSIVE and its Space ability owned the
  right trigger** — crystal seeding runs on a cooldown loop that plants crystals in the cell's
  CYTOPLASM (volume-uniform across the band, never inside the nucleus, and at the live cap the
  clock PAUSES rather than culling — not creating mass is allowed, aging it out is not), which
  freed RT for the **Echo Sight**: hold it and every prism inside the crystal blast's live
  destruction volume lights up. It touches nothing but photons — no camera write, no speed
  change, and it cannot destroy, move or protect a prism. (A zoomed first-person view was built
  alongside it and **cut**:
  it would have needed the speed tunnel to grow a public FOV-home surface for one vessel's view
  effect, and the highlight carries the ability on its own. If it is ever revisited, the one
  safe shape — move the tunnel's HOME, never `Camera.fieldOfView`, which a live tunnel
  overwrites every frame and then bakes in permanently as the home it restores to — is recorded
  in `DOLPHIN_CRYSTAL_SEEDING.md` §2.) One general lesson: **a passive ability is bound to no
  input event, so `CollectBoundActions` can never resolve its SO** — wire the config directly on
  the executor; the binding sweep is a fallback, not the path.
  The prism highlight is the second citizen of the §4.7 global-uniform shape
  (`Docs/PRISM_ANIMATION.md` §4.7.1) — five globals per frame, zero per-prism CPU, and the
  previewed volume is built by the same helper the detonation uses so the two cannot drift. **It
  lights WHOLE prisms, and that is a correctness fix rather than a look preference**: the volume test
  samples the prism's own ORIGIN (from the object matrix, the idiom `PrismClockAnimation.hlsl`
  already uses) because `AOEConicSweepQueryJob` tests exactly one point per prism and destroys the
  whole prism — a per-fragment test paints the geometric intersection, which is a shape the blast
  does not operate on. It is also cheaper: the branch can no longer diverge across a prism. **A
  highlight's colour has to stay out of the palette's language** — the cast was a warm amber
  precisely because no tier owns warm, and moving it to a pale cool blue (2026-08-17, at gain 1.15 →
  0.70) enters the shielded tier's neighbourhood, so it is held clear by being DESATURATED (S 0.55 vs
  a tier's 0.9+) and by a gain low enough that the prism's own tier shows through the cast; if a lit
  shielded prism ever reads as a tier change, lower the gain before touching the hue.
  **Since 2026-08-19 EVERY player sees it, and a rival's cone wears that pilot's DOMAIN colour** —
  the sight was local-only on the reasoning that it is a thing the pilot looks through, which was
  right about the camera and wrong about the arena: mass is the shared object both Dolphin-only
  modes are fought over, so "which prisms is that rival about to take" is the most useful fact on
  the field, and the jaws already telegraph the aim. **Your own cone is untouched and wins outright
  on every prism it covers** — a rival sweeping across your mass cannot recolour, brighten or dim
  it, because an instrument that changes appearance when somebody else moves is one you cannot
  read; peers only ever mark mass your own sight is not marking, and blend among THEMSELVES by
  weight-averaged hue at the brightness of the strongest, never summed (four overlapping cones
  would otherwise blow the arena white exactly where the fight is). Hue is the right channel for a
  peer even though the sight otherwise stays out of the palette's language, because "whose is
  this" is the one question the platform always answers with domain colour — held clear by
  desaturating toward white, so it reads as coloured LIGHT rather than the prism changing team.
  Three things generalise. (1) **The trigger needed no new networking**: `R_VesselActionHandler`
  already round-trips every press/release through the server, so the executor was ALREADY running
  on every peer's replica and only an `IsLocalPilot` guard discarded it — check that channel before
  building one. (An ability bound under a device override rather than the shared map would resolve
  against the OBSERVER's input device, so it would not replicate consistently.) (2) **The cone's
  SIZE did need replicating** (`NetEchoSightShape`, owner-write, 3 floats, ~0.5%-change gated),
  because element levels never replicate and a crystal's effects are replayed to the OWNER alone,
  and because banked skim energy is simulated locally and never SPENT remotely — so a third
  client's replica would draw a cone of the wrong reach, thickness and gape. Only the scalars
  travel; the apex and axes come off the already-replicated transform, so a peer's mark turns at
  full frame rate and only resizes at the tick, and **a peer with no shape yet draws nothing**
  rather than guessing. (3) **A bounded bank of N globals is still O(1) in prisms** — four array
  slots packed once per frame in `LateUpdate`, frame-stamped so a despawned ship cannot leave a
  cone burned in; the arrays must be declared at FILE SCOPE in the HLSL (Shader Graph has no array
  property type — which is why this needed no graph edit) and OUTSIDE every CBUFFER. The Charge-5
  PILOT highlight stays local on purpose: prisms are shared because mass is the shared object, a
  mark on a person is not. Composition is proven by compiling and RUNNING the shipped HLSL
  (`Tools/Shaders/verify_prism_sight_composition.py`) — which is what caught that routing your own
  sight through the same weighted average was algebraically identical and **not bit-identical**
  (`x/x*x` rounds; 3,381 of 89,301 lit samples drifted).
  **The AI holds it too** (same day): `AIPilot` lights a vessel's aim telegraph while it is
  DRIFTING and its course is locked on its objective — the commit window, where the cone's
  direction is already decided — so an AI's blast is as readable as a human's rather than the
  only unannounced one on the field. It asks for a CAPABILITY, never for a vessel:
  **`IAimTelegraphAction`** marks an action whose whole effect is showing others what you are
  lining up, and `R_VesselActionHandler.TryGetInputForAction<T>` answers which control this hull
  puts it on — so the AI that flies all eleven vessels names neither the Dolphin nor a trigger,
  most of the fleet is a silent no-op, and the next telegraph opts in with one interface. The
  interface carries no members but does carry a CONTRACT, because the AI holds it blind: no
  cooldown, no resource, no ammunition, no effect on motion. Three general findings came out of
  it. (1) **Replicate an AI's press when the ability's output does not already ride some other
  replicated channel** — an AI pilot runs SERVER-ONLY, so its local `PerformShipControllerActions`
  is right for the drift (motion, and the transform already replicates) and useless for photons;
  hence `PerformShipControllerActionsReplicated`. (2) **Owner and local pilot coincide for every
  human and diverge for every AI**, so a gate that conflates them works perfectly until something
  autonomous uses it — `NetEchoSightShape` was published on `IsLocalPilot` and therefore never for
  an AI, whose sight could then draw on no machine at all including the host's. (3) **A behaviour
  loop needs its own re-goal event**: the AI's `UpdateCellContent` was driven only by the cell's
  `OnCellItemsUpdated`, a CRYSTAL event rather than a "this pilot needs a new target" event, so an
  AI that overshot kept circling a crystal it could no longer reach; it now re-seeks once per
  commit cycle (latched, because `IsDrifting` does not fall on the frame the control is released).
  Both modes with AI Dolphins get all of this from `AIPilot` with no per-mode code.
  Detail: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` (§14, §15).

  Manta / Rhino / Serpent are blocked on **design, not wiring**: their
  `ElementalAbilityMapSO` entries are still `(open design slot)` with `Input = 0` and no
  `UpgradeLabel`, and their HUDs have 0–2 lower-right icons rather than four. Author the map
  (`Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 holds the un-approved proposals) and the icons
  before wiring — do not invent an element→ability mapping to satisfy the audit. Once the map
  exists, the mechanical half is one click: **FrogletTools > Vessels > Wire Vessel Ability Row**
  (`VesselAbilityRowWirer`) places the four buttons at the fleet-standard bands, creates a
  `{Element}Icon` in each, and binds `abilityIcons` in `AbilityDisplayOrder` — on ANY vessel, from
  nothing. It is idempotent (find-by-name, re-bind only) and never touches sprites, so it is a
  repair path as well as a bring-up path. A slot whose gauge is authored art is ADOPTED by name
  rather than re-created, and a vessel with its own live gauges adds a per-vessel step there (the
  Dolphin's is the only one today).
- Full reference: `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.1. The `/vessel` skill
  encodes this contract (plus the rest of the per-vessel checklist) — use it for any vessel work.

**Control hints attach to the ability, never to a position.** The `(LT)`/`(RT)` glyphs are bound to
an ability and their placement is *derived*: `hint.binding` (the physical control) →
`InputHintBindingMap` → `InputEvents` → the ability bound to that input (`ElementalAbilityMapSO`,
falling back to a shared action asset via `R_VesselActionHandler.CollectBoundActions` when a vessel's
touch and gamepad maps use different events) → `VesselHUDView.TryGetAbilityIcon`.
`InputDeviceIconSetSwitcher.BindHintsToAbilities` runs this once from `VesselHUDController.Initialize`
and re-anchors each hint onto its icon — **without reparenting**, so the Xbox/PS/keyboard set
switching still works. Reassign an ability to a different input event, or move an icon in the row,
and the label follows on its own. Editor warnings flag both a hint that labels nothing and an
input-bound ability with no hint. Do NOT hand-position control glyphs against a HUD layout — that is
the brittleness this replaced. See `Docs/ElementalAbilitySystem/ARCHITECTURE.md` §7.2.

### Namespace Convention

All game code lives under `CosmicShore.*` with 8 primary namespaces:

- `CosmicShore.Core` — foundational systems: PlayFab integration, authentication, bootstrap, rewind, FTUE, dialogue runtime
- `CosmicShore.Gameplay` — all gameplay controllers: vessel, input, multiplayer, camera, impact effects, arcade, projectiles, environment, player, AI
- `CosmicShore.Data` — enums (VesselClassType, Domains, ResourceType, ShipActions, InputEvents, etc.) and data structs
- `CosmicShore.ScriptableObjects` — SO definitions (SO_Captain, SO_Vessel, SO_Game, etc.) and all custom SOAP types
- `CosmicShore.UI` — all UI: vessel HUD controllers/views, modals, screens, toast system, scoreboards, elements
- `CosmicShore.Utility` — utilities: Effects, PoolsAndBuffers, DataContainers, DataPersistence, ClassExtensions, interactive SSU components
- `CosmicShore.Editor` — editor tools: dialogue editor, shader inspectors, copy tools, scene utilities
- `CosmicShore.Tests` — edit-mode unit tests

### Key Systems & Classes

| System | Key Classes | Location |
|---|---|---|
| Vessel core | `VesselStatus` (extends `NetworkBehaviour`), `VesselTransformer`, `VesselController`, `VesselPrismController` | `_Scripts/Controller/Vessel/` |
| Flight model (two, one base) | `VesselTransformer` carries BOTH movement models, selected per vessel by `vectorFlightModel` (default **off**). **Scalar** (historical): a smoothed `speed` eased toward `ComputeThrottleTarget()` and integrated along `Course`. **Vector**: a world-space velocity integrated directly, with thrust applied along the **NOSE** and `Speed`/`Course` DERIVED from it. The scalar model's throttle is a number with no direction, so it can only push along `Course` — fine outside a drift (`Course == forward`), but inside one it pushes along the SLIDE, so squeezing the throttle mid-drift digs you deeper in. No tuning fixes a thrust vector pointing the wrong way; it needs the second vector. **Outside a drift the two models are provably the same computation** (both call the same `StepTowardTarget`; verified numerically to ~1e-14 over 4000 frames incl. hard turns and slow modifiers), so the flag changes behaviour ONLY inside the drift window and needs no fleet retune. **Grip runs BEFORE thrust and the order is load-bearing** — thrust-then-grip breaks the identity whenever the nose is turning (0.4 u/s at 8°/frame). A vessel supplies only `ComputeNoseAcceleration` (how hard it pushes along the nose) and `ShapeSpeed` (what bounds its speed); everything else — grip, publish, modifier channels, integration, external-write re-seeding — is the base's. `driftThrottlePolicy` picks Live (Squirrel/Scarab) vs **Locked** (Dolphin: no acceleration for the drift's duration, which with its authored grip 0 freezes the velocity vector outright — entering a drift at speed then costs nothing, which the SCALAR path cannot express at all because there speed is a value that chases the target, and the Dolphin's drift collapses its own target by cancelling the boost). The drift overshoot ceiling takes the pre-thrust speed as a FLOOR so it bounds gain and never brakes; clamping to the throttle target outright slams a vessel that entered the drift fast (the Dolphin's boosted 357 u/s met a 55 u/s ceiling) and makes the throttle read as a speed dial mid-drift. `DriftDamping` was renamed **`Grip`**. **The AI's `VesselStatus.Course` write at drift entry (`AIPilot`) must survive** — it IS the AI's drift — so `SyncExternalWrites` detects an externally-written Course and re-aims the velocity, symmetrically with how `SetInitialSpeed` writes are detected; a vector model that derived Course purely from its own state silently kills the manoeuvre. `throttleMultiplier` / `velocityShift` stay live through a drift (a drifting vessel is never immune to danger prisms). See `_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_DRIFT.md` | `_Scripts/Controller/Vessel/` |
| Vessel actions | `VesselActionSO` (base config), `VesselActionExecutorBase`, `ActionExecutorRegistry` + 40+ action SOs | `_Scripts/Controller/Vessel/R_VesselActions/`, `VesselActions/` |
| Prism lifecycle | `Prism`, `PrismFactory`, `Trail`, `TrailFollower` | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/Prisms/` |
| Prism occlusion corridor (**PLATFORM LAW**) | Prisms between the player's camera and their ship go see-through so the ship is never hidden — **not a feature a vessel or mode may choose**; it must not be possible to author one in which it is off (the retired `ClearPrisms` was per-vessel opt-in on 3 of 11 vessels and had been silently dead on all three). Four layers make that structural: (1) the fade lives in the prism SHADER GRAPHS themselves (`PrismOcclusionFade` spliced into `SurfaceDescription.Alpha` on **every graph a live prism can render with** — BlockGraph + ExplodingBlockGraph), so new prisms inherit it; (2) the target binds in `VesselController.Initialize` under `IPlayer.IsLocalPilot` — the one method every vessel must call on every spawn path — plus `ChangePlayer`, which hands a LIVE vessel to a different player (the Cellular Duel ownership swap) and never reaches `Initialize`, so there is no per-vessel or per-scene wiring to forget; (3) `PrismOcclusionDiagnostics` screams once per material from `Prism.SyncRenderMaterial` if a prism can't fade; (4) `PrismOcclusionCoverageTests` + FrogletTools > Ecology > Prism Animation > **Validate Occlusion Corridor** fail on new content authored outside it. `PrismOcclusionCorridor` publishes just 2 `Shader.SetGlobalVector` per frame (vessel position + (outer, inner, coreAlpha)); the camera end is read on the GPU from `_WorldSpaceCameraPos`. The corridor is a **CONE** — a point at the lens, widening to the sphere that circumscribes the hull and ending ONE HULL RADIUS SHORT of the vessel's plane (`PRISM_OCCLUSION_NOSE_CLEARANCE`, 2026-08-11 — the cone used to run flush to the origin with its gradient still in progress, so a prism was still half-dematerialised when the ship hit it and the impact did not read; the fade now completes with a solid buffer the whole nose sits inside, at the stated cost that mass inside that buffer can occlude the ship at contact range) (no caps, and the base graded on the same shell thickness as the sides so the whole boundary is seamless), the minimal volume that can occlude the ship (the old constant-radius capsule was an artefact of the retired `ClearPrisms` CapsuleCollider and massively over-cleared near the camera; tapering makes the cleared region a constant ANGULAR size). It is **ship-sized**: the radii are multiples of the vessel's OWN circumscribing radius, measured hull-only and rotation-invariantly at bind (skimmers and DISABLED renderers excluded; a skinned hull measures its `localBounds` in ROOT-BONE space — the culling bounds that actually render — never `sharedMesh.bounds`, whose bind-pose mesh-space extents overstate an armature-scaled rig by the full armature factor: the Sparrow's rig carries 0.2 in its armature and shipped a ~5× oversized corridor that way, 2026-08-11) — outer edge on that circle, fully-clear core at a quarter of it — so a new vessel of any size is correctly scaled with nothing authored. Per-vessel audit: **FrogletTools > Vessels > Audit Corridor Vessel Radii** runs the exact runtime measurement over every vessel prefab and names each hull's top contributing renderers, so an inflated radius arrives with its offender attached. **Zero per-prism CPU**, no extra draw calls, corridor prisms stay in the OPAQUE queue (screen-door dither into `SurfaceDescription.AlphaClipThreshold`; kernel selected by `PRISM_OCCLUSION_KERNEL` — 4 = SHATTER, **current** (shipped 2026-08-06 at polygon 16.26 px / wall 20 px): the lattice's Voronoi polygons filled between straight lines so the NEGATIVE space is the motif — a cracked lattice of walls, with two independent dials (polygon 8–20 px, wall up to ~1.25× the polygon — the wall window is RELATIVE, not absolute; no CDF needed since `frac` of a hash is uniform); 5 = SHATTER3D, the same proposition lifted into the WORLD as Voronoi polyhedra cut by crack planes — **carried, REJECTED ON LOOK the day it shipped (2026-08-10)**: every fidelity number passed (0.0006 uniform / 0.0031 in-situ via a clang build of the shipped file), but a crack plane lying near-parallel to a viewed surface makes a face-sized plate share one threshold and flash at one alpha — glitchy clipping around the vessel that no flat measurement could see (a candidate must pass the number AND earn its look on real mass at speed; a 3D-SHARD distance-to-owner fill is the noted successor direction, since its level sets are closed surfaces that can't lie flat against a face); 3 = SHARD, **triangular** flecking, carried; 2 = the same arrangement with round flecks (Worley, now the calibration reference — its CDF-fitted `smoothstep` remap is load-bearing: raw F1 measures 0.140 coverage error, remapped 0.0048); 1 = corridor-relative spiral, an iris anchored to the corridor; 0 = interleaved gradient noise, a dissolve anchored to the screen — only these hold coverage fidelity over a short band; `PRISM_OCCLUSION_MORPH_RATE` slowly evolves the pattern off `_Time.y` at zero CPU — the cellular kernels' cells orbit, the spiral's phase drifts, IGN can't morph because a hash has no continuity to move; and the LAYERED BEAT — surfaces stacked along one camera ray (a prism's own interior through its clipped front face, parallel trail walls) read the same screen-anchored threshold and moiré-beat — is answered by two dials after the depth-parallax domain shear was **rejected on look** (it moved the whole lattice, so at speed it crawled coherently and read as worse flicker than the beat; a fix that moves the pattern globally cannot win against speed): `PRISM_OCCLUSION_SHATTER_DEPTH_PHASE` shifts only each cell's WALL by view depth (lattice still, coverage-neutral) but **ships at 0** — measured, useful decorrelation needs ~50× the rate the speed budget allows, the same conflict — and `PRISM_BACKFACE_POWER` (`PrismBackFaceFade`, spliced after the corridor) sharpens `alpha^power` on away-facing surfaces so the prism's own interior leaves the gradient band while the exterior is still dissolving: the one fix that REMOVES the interference rather than scrambling it, with no temporal cost, at the stated cost of interiors reading as thinner shells mid-fade). **Since 2026-08-10 the dither is ALL prism transparency, not just the corridor's**: `PrismOcclusionFade` engages its threshold for ANY fractional final alpha anywhere, so the exploding-debris fade-out (`PrismExplosionClock`'s Opacity) and the cloak family's authored near-zero alpha ride the same screen door as the corridor, composing in coverage — and every prism material is OPAQUE + `_ALPHATEST_ON` with NO prism in the transparent queue (the seven blending materials were converted, authored `_Alpha`/`_Opacity` preserved as dither coverage; `Tools/Shaders/enable_prism_alpha_clip.py` enforces and converts strays, `PrismOcclusionDiagnostics` faults a transparent prism material at runtime, and the coverage test fails one in CI). **The exploding prism's FADE carries its own dither** — `PrismErosionFade`, anchored to **UV0** so it is never a function of view angle or motion: each face of the debris cube gets ONE jagged erosion front that wipes across it — a HARD edge, seeded off the stamped `_Velocity` so no two prisms peel alike (a dithered fringe shipped briefly and was removed 2026-08-11: a graded debris edge dissolves in the same visual language as the corridor and the two read as one confused surface; the soft half of the motif is the unbroken face and the front's own irregularity). UVs are mesh attributes no vertex animation can move, so the front rides the face through flight AND the shatter spin (the earlier body-position anchoring broke under the spin — fragments migrated across dominant-axis face boundaries as pieces rotated). The wipe completes 15% of the fade early by construction (`PRISM_EROSION_END_MARGIN`) on the 1.5×-extended `PrismExplosion.DefaultDuration` (7.5s), so retirement can never beat it. Spliced between the clock and the corridor node by `Tools/Shaders/wire_prism_explosion_erosion.py` (migrates old wirings in place; CDF fit over the UV square: `fit_prism_erosion_cdf.py`) — the corridor keeps owning occlusion, a view effect by definition, and the two compose in coverage when a fading chunk sits in the cone. **The dither's unit shape obeys the house soft-hard-soft motif**: a circle is soft with a soft gradient either side of it (soft-SOFT-soft), so the 2026-08-06 pass replaced it with two hard-edged candidates and shipped SHATTER after judging both in motion. SHARD changes Worley's METRIC only — Euclidean distance becomes the gauge of an equilateral triangle, `max(q.y, 0.866·|q.x| − 0.5·q.y)` — keeping the lattice, jitter, orbit, 3×3 search and remap while the flecks gain hard straight edges. The gauge is area-normalised (×1.28607) to the circle it replaces, which is BOTH why they are "triangles of the same size" and why one CDF fit (`PRISM_OCCLUSION_CELL_CDF_*`) serves both cellular kernels — retune the area constant and both must be refitted. `PRISM_OCCLUSION_SHARD_ORIENT` turns them (FIXED/FLIP/SPIN). **Choose the look in FrogletTools > Ecology > Prism Animation > Occlusion Dither Lab, not by editing `#define`s** — it drives kernel + scale as shader globals live *in play mode* (the `PRISM_OCCLUSION_LIVE_TUNING` gate, fail-safe to the constants when nothing is published and compiled away entirely at 0), previews through the shipped GPU code rather than a C# copy, runs the real |coverage − alpha| admission rule against the shipped baseline measured in the same pass, and bakes the result back into the constants. `PRISM_OCCLUSION_CELL_SIZE` is a **free** dial inside 4.5–11 px (sweet spot 6–8): the fit is scale-invariant, so the old "re-fit the CDF or the fade degrades 19×" warning was wrong — 19× is the cost of dropping the remap entirely, and what actually bounds the pitch is sampling at both ends (pixel floor below, too few cells per gradient band above), which no re-fit can buy back. Tuning: `PrismOcclusionConfigSO` (`Resources/PrismOcclusionConfig`). The ONE sanctioned hold is `SetSuppressed`, used only by the manual replay camera. See `Docs/PRISM_ANIMATION.md` §4.7 | `_Scripts/Utility/`, `_Scripts/ScriptableObjects/`, `_Scripts/Controller/Vessel/`, `_Graphics/Materials/Graphs/` |
| Speed tunnel (**PLATFORM LAW**) | Every vessel's gameplay camera narrows its FOV below home while the URP Panini distance drops below the profile baseline, both proportional to that vessel's LIVE speed — a quasi dolly zoom with no camera-distance change. **Not a feature a vessel or mode may choose.** The mapping is **ABSOLUTE**: `SpeedTunnelConfigSO.Effect01` takes a speed and nothing else, so the same speed on ANY vessel produces the same visual and a faster vessel reaches deeper because it IS faster — never add a per-vessel window, scalar, or normalize-to-own-top-speed (considered and rejected; it is also what leaves nothing for a vessel to author around). Four layers: (1) bound in `VesselController.Initialize` under `IPlayer.IsLocalPilot` — the one method every vessel calls on every spawn path — plus `ChangePlayer` for the Cellular Duel ownership swap, with identity-guarded release in `OnDestroy`; (2) ONE static driver (`VesselSpeedTunnel`) with a hidden `DontDestroyOnLoad` LateUpdate publisher installed at `BeforeSceneLoad`, because `PostProcessingManager.SetSpeedTunnelPanini` is a single global override with **no ref-counting** and N per-vessel writers stomp each other across a vessel swap; (3) warn-once diagnostics naming the fix (a null camera controller — Cinemachine in the menu — is a designed state and stays silent); (4) `SpeedTunnelLawTests` + FrogletTools > Vessels > **Validate Speed Tunnel Law** fail on any prefab that grows its own driver, on a binding that isn't on `IsLocalPilot`, on a drive site that stops passing raw speed, and on an insane config — every one of those predicates written ONCE (`SpeedTunnelConfigSO.IsSane` + `SpeedTunnelLawSource`) and called by both gates, which compile into assemblies that cannot see each other. Drive signal is measured `VesselStatus.Speed`, never boost state, so every current and future speed source (trigger boosts, ramps, skim charges, throttle modifiers) is covered with nothing to wire. Home FOV/Panini are whatever the game is actually running with and are restored exactly; the law re-captures home when the player's FOV setting changes mid-effect. Tuning: `SpeedTunnelConfigSO` (`Resources/SpeedTunnelConfig`) — the ONLY tuning surface for the fleet. The ONE sanctioned hold is `SetSuppressed`, used only by the manual replay camera. See `Docs/SPEED_TUNNEL.md` | `_Scripts/Utility/`, `_Scripts/ScriptableObjects/`, `_Scripts/Controller/Vessel/`, `_Scripts/Controller/Managers/` |
| Prism performance | `PrismStateManager`, `PrismTimerManager`, `BlockDensityGrid` (the CPU animation managers — `PrismScaleManager`/`MaterialStateManager`/`AdaptiveAnimationManager` — were deleted under the clock-material law; see `Docs/PRISM_ANIMATION.md`) + `PrismDebris` (batched pure-entity death VFX for **both** death visuals: a frame's prism deaths spawn as ONE `em.Instantiate(prototype, N)` batch per family — explosions AND fauna-consumption suctions — with full-duration clock animation and sweep-based batch retirement. A live explosion costs zero per-frame CPU; a live suction costs ONE `float3` (its convergence target MOVES — every implosion comes from `Prism.Consume` and every call site passes a live creature Transform — so the §1 exception rides a per-record refresh with a CPU-mirrored culling envelope). The per-death path is split by five `Prism.Destroy.*` markers. **The pooled `PrismExplosion`/`PrismImplosion` GameObjects are NOT a working visual fallback** — under strict clock mode an explosion with no render entity draws nothing and an implosion draws a static block, both loudly, by design; their live job is being the CONFIG source (mesh/material/layer/clamp band/duration) the batch reads off the pool prefab. Retiring them is tracked as `Docs/PRISM_ANIMATION.md` D4/§4.6.1 — a refactor, not a deletion) | `_Scripts/Controller/Managers/`, `_Scripts/Utility/Effects/` |
| Worm colony kaiju | `WormFauna` (colony brain: follow-the-leader slither, apex-omnivore feeding — grazes prism mass AND devours creatures at the jaws AND hunts pilots, feeding-funded growth, mid-body-kill splitting, wound differentiation, boid separation between colonies) + `WormSegmentFauna` (`WormSegmentRole` Head/Body/Tail — danger prisms + elemental heart on the capitals, one high-volume core prism on the body) + `WormColonyConfigSO` (all tuning). Spawns via `WormColonyFaunaConfig`/`Worm Colony <Element>` species assets; wired into the Lifeform Matrix toy, deliberately in NO SpawnProfile (a boss is opt-in). Design record + invariant rulings + collider budget: `Docs/ECOSYSTEM.md` §23 | `_Scripts/Controller/Environment/FloraAndFauna/`, `_SO_Assets/Lifeforms/` |
| Cell environments | `CellEnvironmentSpawnableBase` (shared deterministic lay/stream/noise contract) + `SpawnableAtlantis` (Scurry intensity 4, ~69k prisms) + the freestyle seven `SpawnableYggdra`/`Daedala`/`Orrery`/`Zephyr`/`Caldera`/`Geode`/`Ourobor` (~34-41k each, rolled by Menu_Main's Cell via `CellConfigDataSO.EnvironmentPrefab`). Two are built AROUND the nucleus and lay **nothing inside the node-control radius** (an authored environment in there pre-awards node control): **Caldera** — four inward-aimed volcanic massifs in tetrahedral symmetry, no ground plane (`Docs/ECOSYSTEM.md` §18.1) — and **Ourobor** — three interlocked ULTRAWIDE Möbius bands of rolling countryside with a cityscape on BOTH faces, so stalagmites become stalactites and no global "up" survives a lap (`§18.2`). Alongside them, **`SpawnableHesperides`** — the GARDEN cell, the one environment whose world is the **planting**: ~12k authored prisms of architecture (terraces, pergolas, trellises, aqueduct, hanging baskets, super-shielded orchard gate, danger brambles) that `Sow`s ~560 `FloraPlantingSite`s — each tagged with its ground kind (`FloraSiteKind`: Bed/Climb/Basket/Water/Ledge) — which the Cell hands to its ordinary flora spawner (`Cell.TryTakePlantingSite(cfg.PreferredSites, …)` → `Flora.SetPlantPositionOverride(pos, up)`), so a mature Hesperides reaches Yggdra's ~33k prisms by GROWTH — living, grazeable `PhyllotacticFlora` in eight forms (Arbor/Rosette/Frond/Coral/Spire/Tendril/Reed/Lantern) plus gyroid + Schwarz P topiary — not by lay. One growth model, forms are parameters; prisms are shaped by ROLE (stem spans its segment, leaf spans its reach and attaches to the stalk) with depth taper, per-prism jitter, cupped alternating whorls, gravity droop and spiral twist. See `Docs/ECOSYSTEM.md` §23. `EnvironmentLoadVeil` (gate-less scenes defer past boot then hold a connecting-style veil), `CellEnvironmentBaselineMeasurer` (FrogletTools > Ecology > Measure Cell Environment Baselines - PhaseThresholds must ride each measured baseline; see `Docs/ECOSYSTEM.md` §18) | `_Scripts/Controller/Environment/Spawning/`, `_Scripts/Controller/Environment/MiniGameObjects/`, `_Scripts/Editor/` |
| Prism spatial index | `PrismSpatialIndex` (formerly `PrismAOERegistry`) — THE canonical spatial index of all live prism mass: Burst AOE damage queries + growth occupancy (`TryReserve` claim-before-spawn closes the disabled-collider spawn race) + bucket hash grid. One registration lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`), multiple query views. Do not build parallel spatial stores or query prisms via physics — see `Docs/SPATIAL_INDEX.md` | `_Scripts/Controller/Managers/` || Shield octahedra | `PrismOctahedronShield` (the SHIELDED state's octahedron: per-face bloom engage + a disengage that sheds the shield AS ORDINARY PRISM-EXPLOSION DEBRIS — **the shatter is not a sibling effect of the prism explosion, it IS the prism explosion**, applied to the shield's own mesh: one explosion-debris entity on ExplodingBlockGraph with the same material, clock stamps, per-face `RotateFacesAlongAxis` rotation, UV0 erosion wipe and fade a dying prism's pieces get, 1 triangle per octahedral face where the cube carries 4 per side (`PrismShieldShatter` groups a frame's disengages per shield mesh and spawns through `SpawnExplosionDebrisBatch`; velocity/clamp semantics are `PrismDebris.TryRequestExplosion`'s byte for byte, and `Prism.Damage`'s shield-shed branch forwards its impact vector + true-velocity ceiling so armour knocked off flies on exactly the terms the prism itself would have). The port lives in the MESH, not the pipeline: the shield generators bake the debris attribute set — UV0 wipe frame, flat per-face normals, per-face tangents — and the pipeline is never forked. Three bespoke shapes shipped first and were all reverted (an HLSL re-expression of the rotation, a mirrored back-face bake that z-fought under Cull Off, a BlockGraph erosion splice); the rule they bought: **when the base effect already looks right, port the mesh into the pipeline, never the pipeline into the mesh** (`Docs/PRISM_ANIMATION.md §4.8.1`). **The shards' palette is the `GetPrismKindColors` tier LOOKUP for what the shield was showing (`PrismStateManager.GetShedColors` — Shielded pair for the octahedron, Plain for the stellation, which wears the opaque plain material), never a read of the renderer at disengage** — a state change binds its end-state material before it disengages, so by then the renderer already wears the INCOMING tier and reading it painted every shard in plain colours. Beyond the shatter: mass scales with volume; the COLLIDER stays the authored primitive box TRIGGER — the octahedron is a look-only change, because a convex-mesh trigger is invisible to trigger-skimmers and a convex-mesh solid is invisible to solid swipes, whereas the primitive box trigger is seen by both, exactly like an unshielded prism; shape-precise shielded collision is SHIPPED as the spatial-index shell tier: `PrismShellContactManager` + `PrismSpatialIndex.CollectShellContacts` + `ShieldShellMath` run an exact Burst narrowphase — sphere/capsule/OBB probes vs the octahedron and vs the stella as the NON-CONVEX union of its two tetrahedra (spike-tip grazes hit, inter-spike gaps inside the bounding box do not) — dispatching through the same AcceptImpactee effect chain while Skimmer/VesselImpactor suppress box-trigger dispatch for shell-owned pairs; see Docs/SPATIAL_INDEX.md § Shell view). **A super-shielded prism that is HIT but not destroyed now DEFLECTS visibly** — every face wobbles on a precessing/nutating axis and settles, GPU-only off the prism clock (`PrismJiggleClock` + three Hybrid-Per-Instance stamps; composes ON TOP of the shield morph below rather than replacing it; `Docs/PRISM_ANIMATION.md §4.9`). Super-shielded mass stays fully invulnerable — this changes photons and nothing else — but the `IsSuperShielded` early-return that used to be copied into FOUR damage gates (`Prism.Damage`, `Prism.Consume`, `PrismSpatialIndex.ResolveExplosionHit`, `ExplosionImpactor.ExecuteCommonPrismCommands`) is now ONE method, **`Prism.AbsorbSuperShieldHit`**; route every new damage source through it rather than re-testing the flag, or that source's hits go back to reading as misses. A source that BREAKS a super-shield (the Rhino energy sword) calls `DeactivateShields()` first, so it never reaches the gate — everything that does is a hit the prism survived), `PrismStellatedOctahedronShield` (the SUPER-SHIELDED state's stellated octahedron / Stella Octangula — the Skim Race track look; engaged by `PrismStateManager.ActivateSuperShield` with the OPAQUE team material, reversed by `DeactivateShields`), testers, `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator` (`PopulateMesh` + `GetSharedShieldMesh` quantized-geometry caches). **Both integrate with the instanced prism render path via the `SetExoticVisualActive` / `SetRenderMeshOverride` handoff — see the anti-pattern below on why a bare MeshFilter swap renders nothing.** **Both morphs are GPU-CLOCKED since 2026-08-15** (`Docs/PRISM_ANIMATION.md` §4.8, §5 B4 — the migration that deleted the last sanctioned CPU prism ticker, `PrismOctahedronShieldManager`): the generators bake each vertex's FACE CENTROID into TEXCOORD1, which makes the **cache-shared settled mesh also the morph mesh**, so engage and shatter are one `PrismShieldMorph_float` expression off four Hybrid-Per-Instance properties and same-size shields stay in ONE batch through the whole animation. Consequences to respect when editing: everything is FINAL AT t = 0 (`Engage` applies the entire shielded pose, then stamps — there is no completion callback, because the shader clamps at t = 1 which IS the settled shield); the stamp must be CLEARED at disengage and on pool reuse (the prism's own box mesh carries no centroids, so a live stamp would collapse it toward the object origin); the disengage overlay is batched pure-entity debris (`PrismShieldShatter`) and is deliberately **not cancellable** on re-engage, because deleting visible shards mid-flight breaks continuity of existence; and the per-face CPU mesh rebuilders (`PopulateMeshFaceScale`/`PopulateMeshFaceShatter`) and the `AnimationCurve` fields are RETIRED — `AnimationCurve.EaseInOut(0,0,1,1)` is exactly `smoothstep` (zero end tangents), which is what the shader runs, so every runtime-added shield is unchanged; `BlueBlock.prefab` and `OctahedronShieldTest.prefab` serialized a hand-altered curve and now ease like the fleet | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
| Impact effects | `ImpactorBase` + 11 impactor types, 20+ Effect SO types | `_Scripts/Controller/ImpactEffects/` |
| Swing kinematics | `SkimmerSwingKinematics` (rigid-body velocity of any point on a skimmer that MOVES relative to its vessel — the Rhino's sword: `v = v_vessel + omega_vessel x r + R * v_rel`, every rate differentiated in the VESSEL's frame so translation/teleports can't leak in; `ClosestBladePoint`/`NormalizedAlongBlade` recover WHICH part of the blade a contact landed on, hilt/tip derived from the pivot, never authored) + `SkimmerSwingKinematicsConfigSO`; composed into impacts by `PrismEffectHelper.ContactVelocity` so a destroyed prism gets the velocity of the part that hit it (a tip strike, not the hull). Skimmers without the component collapse to the previous `Course * Speed` exactly. The magnitude survives to the screen via `PrismEffectHelper.DamageProportional`, which hands the debris velocity over **as final** — `Prism.Explode` passes it through untouched (the supplied `DebrisSpeedLimit` marks it) instead of applying the legacy `/ prismProperties.volume`. **That divide is dead code**: `SetupDestruction` disables the scale animator before reading the volume, `GetCurrentVolume()` returns 0 once disabled, so `Max(0,1)` pins the divisor to exactly 1 for every prism — the legacy gain is just `inertia`. Never pre-multiply by volume expecting it to cancel; the leftover is a straight volume multiplier that damps small prisms (a Rhino trail sliver is ~0.75) and pins large ones to the ceiling. Opt-in per effect (`proportionalDebris`) — on for the sword AND the hull (`VesselDamagePrismEffectSO`), since every vessel's `Inertia` is 1 and the legacy hull formula therefore landed under the clamp's FLOOR, making every ram produce an identical 30 u/s; with both proportional a hull hit and a parked-sword hit at the same velocity now impart the same magnitude. Debris ships at **1/3** the physical read via one tuning group that must move together — `restitution` + `debrisSpeedLimit` on the three damage SOs, `debrisRestitution` + `Inertia` on `AOEExplosion` (the **AOE blasts** joined the group; see below), and `minSpeed`/`maxSpeed` on `PrismExplosion.prefab` (the band also carries the clamp-bound legacy paths, so the retune is uniform). On the three damage SOs `inertia` is NOT the lever — proportional paths ignore it and legacy paths are saturated — but on an AOE blast running `proportionalDebris` it IS the single lever: the blast supplies its OWN ceiling, so `Inertia` scales throw AND shatter linearly, and `debrisRestitution x Inertia = 1` holds the pre-existing shatter rate. `restitution` also drives the shatter rate, so shatter violence tracks impact force. A parked sword must add exactly zero, so elongation (ambient shield scaling, +15/-5 u/s at the tip) defaults off, `restDeadbandSpeed` zeroes sub-threshold residue (which rectifies upward, `|v+n|>|v|`), and `AngularVelocity` reads the angle off the quaternion's vector part via `atan2` — `ToAngleAxis`/`acos` returns exactly zero below ~0.01 deg/frame in float32 and drops slow vessel rotation. See `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md` § "Swing velocity model" | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/` |
| Forcefield crackle | `SkimmerForcefieldCracklePrismEffectSO` (computes impact points via `Collider.ClosestPoint`), `ForcefieldCrackleController` (`[ExecuteAlways]`, 16-impact ring buffer + MaterialPropertyBlock arrays, owns all visual params), `ForcefieldCrackle.hlsl` (FBM electrical arcs on geodesic sphere), `ForcefieldCrackleControllerEditor` (edit-mode preview) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`, `_Scripts/Controller/Vessel/`, `Assets/Materials/Graphs/`, `_Scripts/Editor/` |
| Charge crystal | `ChargeCrystal.shader` + `ChargeCrystal.hlsl` (URP unlit **opaque**: static faceted body + plasma discharge that crackles vertex-to-vertex along **crease edges only**, in the forcefield-crackle visual language) and `CrystalEdgeArcMeshBaker` + `CrystalEdgeArcs` (bake the crease data a fragment shader cannot derive — barycentric basis, signed edge heights in model-radius fractions, per-edge hash + direction flag — once per source mesh, **shared**). **This crystal is STATIC: its spread is the model's** (`ChargeCrystalExport1_7-11-25.fbx` = 60 disjoint pentagonal extruded prisms, 420 faces, 900 edges). It must NOT go back on the generic `CrystalGraph`, which is the *exploding* crystal shader — vertex `_spread` along the normal + a Cosine-Time spin + stacked overlay blends, which double-applies the spread and clips the colour. Two traps recorded in the source: 120 of the 300 side quads are **non-planar** (5.21°, vs a 57.5° shallowest real dihedral), so diagonals are identified structurally rather than by a tight angle test; and the vertex-terminal glow must be gated on the bolt, because its corner metric has wedge-shaped level sets and an always-on term draws permanent starbursts. Honours `_opacity` so `FadeIn` still blooms it in (continuity of existence) — spent as screen-door **coverage**, not blending, per `Docs/PRISM_ANIMATION.md` §4.7. Source mesh needs Read/Write enabled. **Only the CHARGE crystal moved**: `SpaceCrystalMaterial` (plus the domain/fake crystals) is still on `CrystalGraph` and may carry the same model-vs-shader mismatch — check whether its model already encodes its own spread before assuming the shader should add one. A crystal's colour is its **collectability**, not its element (`Docs/PALETTE.md §2.2`), and a crystal's **state CHANGE travels** rather than snapping (`§2.3`): a lifeform heart crosses blue → lime when `ActivateCrystal` drops it, on the same clock-stamped shape as a prism domain change — state final at the start, start pair stamped once against `PrismClock`, the pairs between computed analytically, ONE `PrismTimerManager` settle at the known end. Two ordering rules it depends on: the start pair is read BEFORE `EmbeddedIn` is cleared or any material lerp drops the block (read it later and Charge and Time — whose inactive material *is* the lime one — start already-lime and travel nowhere), and `ClearColorSetTint` forgets the resting pair because a cleared block no longer describes the screen. **Never fix a crystal's colour by editing its material** — the material is what shows when the tint has failed | `_Graphics/Materials/Graphs/`, `_Scripts/Utility/`, `_Scripts/Controller/Environment/FlowField/` |
| Camera | `CustomCameraController`, `VesselCameraCustomizer`, `CameraSettingsSO`, `ICameraController`, `ICameraConfigurator` | `_Scripts/Controller/Camera/` |
| Vessel HUD | `IVesselHUDController`, `IVesselHUDView`, per-vessel controllers & views (Sparrow, Squirrel, Serpent, Manta, Rhino, Dolphin) | `_Scripts/UI/Controller/`, `_Scripts/UI/View/`, `_Scripts/UI/Interfaces/` |
| Elemental bars | `ElementalBarsView` (5-petal flower per element), `ElementalBarsConfigSO` (shared colour/sprite/juice spec), `ElementalBarsController` (per-vessel driver), `ElementalPetalBarWirer` (editor setup) | `_Scripts/UI/View/`, `_Scripts/ScriptableObjects/`, `_Scripts/Controller/Vessel/`, `_Scripts/Editor/` |
| Arcade games | `MiniGameControllerBase`, `MultiplayerMiniGameControllerBase`, `MultiplayerDomainGamesController`, `ScoringRuleSO` family | `_Scripts/Controller/Arcade/` |
| Resource system | `ResourceSystem`, `R_VesselActionHandler`, `R_VesselElementStatsHandler` | `_Scripts/Controller/Vessel/` |
| Elemental debuff immunity (general state) | `ResourceSystem.SetElementalDebuffImmunity` / `IsImmuneTo` / `ImmuneDebuffSources` / `OnElementalImmunityChanged` (grantor-keyed grants, one gate on the NEGATIVE branch of `ApplyElementalEffect` — buffs still land, live debuffs still decay, `AdjustLevel` crystal progression is untouched), read via `IVesselStatus.IsImmuneToElementalDebuff(source)`, held declaratively by `VesselElementalImmunity` (`Always`/`WhileBoosting`/`WhileTranslationRestricted`/`WhileDrifting` × optional element upgrade gate × a `wardedSources` mask). **A ward has a SCOPE**: every elemental debuff names its source class (`ElementalDebuffSources` — `DangerPrism`/`Explosion`/`VesselContact`/`Other`, `All` = `~0`) and a grant holds a mask, because "immune to the arena" and "immune to another pilot's weapon" are different promises. There is deliberately **no bare `IsElementallyImmune` bool** — a reader that assumes total immunity from a true answer is wrong for the Dolphin, and wrong silently. **Not owned by any vessel** — Sparrow holds it while boosting at Time 5 and Serpent while stopped (ungated), both warding everything; the Dolphin holds it while drifting at Time 5 ("Drift Ward") warding **`DangerPrism` alone**, because unscoped it cancelled the Dolphin crystal blast and with it the entire scoring event of The Bends. Any vessel or mode can grant it. Detail: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` §1/§1.1 | `_Scripts/Controller/Vessel/`, `_Scripts/Data/Enums/` |
| Object pooling | `GenericPoolManager` (Unity `ObjectPool<T>` with async buffer maintenance) | `_Scripts/Utility/PoolsAndBuffers/` |
| Player system | `Player` (NetworkBehaviour, `IPlayer`), `RoundStats` | `_Scripts/Controller/Player/` |
| Cell-relative spawn ring | `CellSpawnFormation` (pure math, N players around the cell, all facing it) in two formations: `Symmetric` — spread over a SPHERE (4 tetrahedral, 3 equilateral triangle, 2 antipodal, 5+ Fibonacci), the default; and `EquatorialRing` — evenly spaced on ONE horizontal great circle the way Joust authors its points by hand, for an arena with a meaningful "up" or a pole feature (Ribcage: a latitude-hoop cage is densest where the ribs converge, so a tetrahedral spread would hand two of four players a much harder approach). Driven by `ServerPlayerVesselInitializer.arrangeSpawnPointsAroundCell` + `spawnFormation` at `Cell.ExpectedNucleusWorldRadius + spawnDistanceOutsideNucleus`. Opt-in per scene (Symmetric for Crystal Capture, EquatorialRing for Ribcage). Tests: `CellSpawnFormationTests` | `_Scripts/Utility/`, `_Scripts/Controller/Multiplayer/` |
| Menu navigation | `ScreenSwitcher`, `IScreen`, `ModalWindowManager`, `ProfileDisplayWidget`, `NavLink`/`NavGroup` | `_Scripts/UI/`, `_Scripts/UI/Interfaces/`, `_Scripts/UI/Elements/`, `_Scripts/UI/Modals/` |
| Freestyle toys | `Toy` (base world-trigger; bloom, local-user + freestyle gating, exit-gated re-arm), `MatrixToy` (the one-toy-opens-into-many base: a pass unfolds a matrix of choices out along the outward radial, another folds it away — shared by the cell selector, painting gallery, and vessel changer), `SwapToy` + `SwapToySetCoordinator<T>` (a small set of toys showing "the options you're not on", each flips to your previous option on use — the domain changer), `VesselChangerToy` (one toy opening into a matrix of mini ship models via `VesselModelBuilder`, reuses `RequestSwap` + restores freestyle control after swap), `DomainChangerToySet` (two toys tinted the domains you're not, `RequestSetDomain_ServerRpc`), `PaintingGalleryToy` + `PaintingToy` + `PaintingRunner` (one toy opening into a matrix of painting stations; multi-stroke multi-domain connect-the-dots: domain gates, pen-up, cone/jack stroke markers in prism material, resumable progress that survives folding the gallery away) + `PaintingDefinitionSO`/`PaintingPresetLibrary`/`PaintingStrokeToolkit` (stroke data + 16 grandiose 3D presets + the curl-field stroke library + Star/Rainbow/Saturn/Taj Mahal generators; runtime flight-continuity stroke ordering via `OrderForFlightContinuity`) + `PaintingProgressStore`/`PaintingPrismStore` (local JSON progress + per-prism drawing state, regrown on return) + `PaintingShareExporter` (self-contained WebGL HTML → NativeShare), `ConveyorToy` + `MicrosceneConveyor` + `Microscene` + `MicroscenePatterns` + `MicroscenePatternsGrand` + `MicroscenePainter` (Wanderway: on/off toggle streaming a speed-scaled field of procedurally-varied microscenes — 48 recipes: the classic forty incl. spine×motif Medley composers, plus the monument-scale grand eight — ahead of the vessel, structurally painted across the full domain triad with capped danger/shield accents; a closed conveyor of a 30k-prism conserved stock built once behind an `EnvironmentLoadVeil` + skimmable crystals + cell-released lifeforms), `CellSelectorToy` + `CellSelectorToyDefinitionSO` (the world picker AND the freestyle reset: a matrix of bare `CellMiniatureBuilder` scale models over `Cell.AvailableConfigs`, sampled from the generator's real output with no prisms spawned; selection routes through `Cell.RequestCellSwap`), `ToyMatrixStation` (shared fly-through choice station), `ToyboxController` (places toys near the membrane), `ToyboxSO`/`ToyDefinitionSO` (registry + deferred unlock state), `ToyboxSetupTool` (editor) | `_Scripts/Controller/Toys/`, `_Scripts/ScriptableObjects/Toys/`, `_Scripts/Editor/` |
| Menu screens | `HomeScreen`, `ArcadeScreen`, `StoreScreen`, `HangarScreen`, `LeaderboardsMenu`, `EpisodeScreen` | `_Scripts/UI/Screens/` |
| UI | Elements, FX, Modals, Screens, Views + `ToastService` / `ToastChannel` (menu) + in-game toast feed (`GameToastAPI`, `GameToastController`, `GameToastView`, per-mode `GameToastConfigSO` — see `_Scripts/UI/GameToastSystem/GAME_TOASTS.md`) | `_Scripts/UI/` |
| Telemetry | `VesselTelemetryBootstrapper`, `VesselTelemetry` (abstract) + per-vessel subclasses, `VesselStatsCloudData` | `_Scripts/Controller/Vessel/` |
| Analytics | `AnalyticsServiceFacade` (UGS Analytics, single writer; consent/age-gated), `UGSStatsManager` (leaderboards) | `_Scripts/System/Instrumentation/`, `_Scripts/UI/` |
| Bootstrap / DI | `AppManager` (orchestrator + IInstaller), `BootstrapConfigSO`, `SceneTransitionManager`, `ApplicationLifecycleManager`, `ApplicationLifecycleEventsContainerSO` | `_Scripts/System/`, `_Scripts/System/Bootstrap/`, `_Scripts/ScriptableObjects/` |
| Threading / Main-thread affinity | `MainThreadDispatcher` (captures Unity's `SynchronizationContext` at `BeforeSceneLoad`, exposes `IsOnMainThread` + `SwitchToMainThreadAsync()`), `UniTaskExtensions.AsMainThread<T>()` (boundary helper for UGS / Netcode `Task` awaits), `SceneTransitionManager.SetFadeImmediate` (canary that fires if a UGS continuation reaches it off-thread) | `_Scripts/Utility/`, `_Scripts/Utility/ClassExtensions/`, `_Scripts/System/Bootstrap/`. See `Docs/THREADING.md`. |
| App state machine | `ApplicationStateMachine` (single-writer phase tracker), `ApplicationStateData` / `ApplicationStateDataVariable` (SOAP state), `ApplicationState` enum | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableApplicationState/`, `_Scripts/Data/Enums/` |
| Scene management | `SceneLoader` (MonoBehaviour, DontDestroyOnLoad in Bootstrap, game launch + restart + return-to-menu, SOAP code subscriptions), `SceneNameListSO` (centralized scene names, DI-registered) | `_Scripts/System/`, `_Scripts/Utility/DataContainers/` |
| Authentication | `AuthenticationServiceFacade` (facade/writer), `AuthenticationController` (MonoBehaviour adapter), `AuthenticationSceneController` (scene UI), `SplashToAuthFlow` (splash routing), `AuthenticationData` / `AuthenticationDataVariable` (SOAP state) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Friends | `FriendsServiceFacade` (facade/single-writer for UGS Friends SDK), `FriendsInitializer` (MonoBehaviour bridge + presence), `FriendsDataSO` (SOAP container: 4 lists + 4 events), `FriendData`/`FriendPresenceActivity` (SOAP data types) | `_Scripts/System/`, `_Scripts/Controller/Party/`, `_Scripts/Utility/DataContainers/`, `_Scripts/ScriptableObjects/SOAP/ScriptableFriendData/` |
| Friends UI | `FriendsListPanel` (combined Online + Requests, no tabs), `OnlineInfoEntry` (online row = invite/cancel/kick button), `RequestInfoEntry` (accept/decline; friend-request + party-invite) | `_Scripts/UI/Elements/` |
| Player data | `PlayerDataService` (cloud profile, XP, rewards), `PlayerProfileData` | `_Scripts/UI/Views/` |
| Network monitoring | `NetworkMonitor` (polling), `NetworkMonitorData` / `NetworkMonitorDataVariable` (SOAP events) | `_Scripts/System/`, `_Scripts/ScriptableObjects/SOAP/ScriptableAuthenticationData/` |
| Multiplayer | `MultiplayerSetup` (NetworkManager lifecycle + UGS sessions), `ServerPlayerVesselInitializer` (base spawner), `ClientPlayerVesselInitializer` (pair initializer + RPCs), `ServerPlayerVesselInitializerWithAI` (AI pre-spawner), `MenuServerPlayerVesselInitializer` (menu autopilot), `MenuCrystalClickHandler` (play-from-menu), `DomainAssigner` (team pool) | `_Scripts/Controller/Multiplayer/` |
| Party / Invite | `HostConnectionService` (presence lobby + party sessions, single-writer to `HostConnectionDataSO`), `PartyInviteController` (Netcode host↔client transitions), `FriendsInitializer` (Friends service bridge) | `_Scripts/Controller/Party/` |
| Party UI | `ArcadeLobbyList` (4-slot party panel; per-slot kick ✕ for host) + `FriendInfoSlot` (single slot), `FriendsListPanel` (Online + Requests), `OnlineInfoEntry` (online row = invite button; "IN YOUR PARTY" + cancel-✕/kick states), `RequestInfoEntry` (accept/decline), `PartyInviteNotificationPanel` (bottom-left global invite popup) | `_Scripts/UI/Elements/` (`PartyInviteNotificationPanel` in `_Scripts/UI/Screens/`) |
| Menu scene controller | `MainMenuController` (sub-state machine: None→Initializing→Ready→LaunchingGame), `MainMenuState` enum | `_Scripts/System/`, `_Scripts/Data/Enums/` |
| Audio (FMOD) | `AudioSystem` (DI singleton; inspector-wired `EventReference` per `MenuAudioCategory` / `GameplaySFXCategory`, SFX-bus volume, per-category throttle), `FMODOneShotVolumeHelper` (slider-respecting one-shots — use instead of `RuntimeManager.PlayOneShot`), continuous emitters `ShipAudioController` / `DriftAudioController` / `ProximityBoostAudioController` / `FloraAmbientAudioController`, `ScriptableEventGameplaySFX` / `EventListenerGameplaySFX` (decoupled gameplay SFX via SOAP). **Every sound is an exposed `EventReference` field — never a temp/borrowed event; every ship ability gets its own field.** See "Audio (FMOD)" under Architecture Patterns | `_Scripts/System/Audio/`, `_Scripts/Controller/FX/`, `_Scripts/Controller/Vessel/Audio/`, `_Scripts/ScriptableObjects/SOAP/ScriptableGameplaySFX/` |
| App systems | Favorites, LoadOut, Quest, Rewind, Squads, UserAction, UserJourney, Xp, Ads, IAP, DailyChallenge, TrainingGameProgress | `_Scripts/System/` |
| ScriptableObjects | `SO_Vessel`, `SO_Captain`, `SO_Game`, `SO_ArcadeGame`, `SO_Element`, `SO_Mission`, etc. | `_Scripts/ScriptableObjects/` |

### Async Pattern

- Prefer UniTask over coroutines for new code
- For ScriptableObjects that need async: use a `CoroutineRunner` singleton proxy or async/await with cancellation tokens
- Always include `CancellationToken` for anything non-trivial — UniTask respects play mode lifecycle better than raw `Task`
- Bootstrap uses `UniTaskVoid` with `CancellationTokenSource` for the async startup sequence
- Prefer SOAP event channels (`ScriptableEvent`) over `UniTask.WaitUntil` polling for waiting on state changes from other systems. Subscribe to the relevant event and react when it fires, rather than polling a condition every frame
- **Every `await` of a UGS / Netcode `Task` uses `.AsMainThread()`** — see the "Threading & Main-Thread Affinity" section above and `Docs/THREADING.md`. UniTask's own `SwitchToMainThread()` and `Yield(PlayerLoopTiming.Update)` are unreliable on this UniTask version and must not be used as thread-marshaling primitives.

### Anti-Patterns to Avoid

- `FindObjectOfType` / `GameObject.Find` in hot paths
- `Instantiate`/`Destroy` in gameplay loops — use object pooling
- Excessive `GetComponent` calls — cache references
- Mixed coroutine/async patterns in the same system
- Singletons, static events, or direct references for cross-system communication — use SOAP `ScriptableVariable` and `ScriptableEvent` instead
- C# `event Action` / delegates on MonoBehaviours for broadcast patterns — use SOAP `ScriptableEvent` channels
- `renderer.material` (clones material) — use `renderer.sharedMaterial` + MaterialPropertyBlock instead
- Swapping a prism's `MeshFilter` mesh (or its `MeshRenderer` materials) directly to restyle it — **prisms draw through the instanced companion entity (`PrismRenderService`), so a GameObject-local swap renders NOTHING**: the companion keeps drawing the plain box while your new mesh sits on a renderer that isn't drawing (exactly how the stellated super-shield first shipped invisible). Any per-prism visual override must hand rendering across explicitly: `Prism.SetRenderMeshOverride(sharedMesh)` + `SetExoticVisualActive(false)` for anything shareable (fetch it from the quantized-geometry caches — `OctahedronMeshGenerator.GetSharedShieldMesh` / `StellatedOctahedronMeshGenerator.GetSharedShieldMesh` — so same-size prisms batch as ONE mesh instead of a per-prism draw-call storm), `Prism.SetExoticVisualActive(true)` ONLY while genuinely showing per-prism-unique geometry, and `ClearRenderMeshOverride()` + `SetExoticVisualActive(false)` on the way back (including pool-return `OnDisable`). **Reach for a GPU morph over the shared mesh before you reach for unique geometry**: the shield engage/shatter morphs were the last holders of the `true` side and gave it up in 2026-08-15's B4 migration (`Docs/PRISM_ANIMATION.md` §4.8) by baking per-face centroids into TEXCOORD1 — the settled shared mesh became the morph mesh, and the animation kept its batch instead of minting a mesh (and a draw call) per prism. Nothing in the project drives `SetExoticVisualActive(true)` today. `PrismOctahedronShield` and `PrismStellatedOctahedronShield` are still the reference implementations of the handoff. **Two corollaries an exotic visual must respect** (`Docs/PRISM_ANIMATION.md` §4.5, learned the hard way from §3.8 #10): (1) taking over *rendering* must never suppress companion-entity *creation* — clock stamps are one-shot, so a prism with no entity at the instant it is stamped loses that animation permanently; entity existence and entity visibility are separate concerns, and the transient morph mesh must never be registered with Entities Graphics (it mints a `BatchMeshID` per prism) — read the batchable geometry from `Prism.EffectiveRenderMesh()`/`SyncRenderMesh()`; (2) a visual state applied while `!Prism.IsCreationComplete` is part of the prism's BIRTH, not a transition on live mass — it snaps (`PrismStateManager.IsBirthTransition`), because the grow-in bloom already carries continuity of existence and a morph there is invisible by construction while costing draw calls, per-frame mesh rebuilds, and one SFX per prism laid
- **Any multiframe CPU update that animates a prism** — per-frame/per-tick writes of a prism's transform scale, colors, shader parameters, positions, or morph meshes to play out a visual transition (coroutines, DOTween, UniTask loops, manager passes, per-frame `SetPropertyBlock`/`SetComponentData`). **The clock-material law (`Docs/PRISM_ANIMATION.md`, LOCKED)**: prism animation is a pool-pull whose material accepts initial conditions, ONE stamp of those conditions (start time, rate/duration, endpoints — per-instance Hybrid-Per-Instance properties), the GPU runs the course off the shader clock with zero further CPU writes, and ONE scheduled swap to the end-state prism at the analytically-known end frame (`PrismTimerManager`-class scheduler, never per-frame progress polling). Colliders and gameplay state (spatial index, volume, state flags) go to their FINAL values at the START of the animation — only photons animate. Interruptions re-stamp (current value is analytic). **STRICT: there is no legacy fallback tier** — never reintroduce a CPU animation path "just until the shader is wired"; an unwired graph fails loud (`PrismClockDiagnostics`) and snaps, which is the intended forcing function. If a visual seems impossible to express as `f(clock, initial conditions)`, that's a design discussion (live gameplay data vs. animation — see the doc), not a license for a per-frame loop
- Per-object coroutines at scale — use centralized timer/manager systems (see Prism Performance Audit)
- **A sound that isn't an inspector-exposed `EventReference` on the thing that makes it** — a hardcoded FMOD event path in code, a "temp" event plugged in so something is audible, a new ability routed through an existing `GameplaySFXCategory` because it's close enough, or a new gameplay sound built on `AudioClip` + `AudioSource`. Every noise must be findable and swappable in the component view of its own prefab, and every ship ability needs its own dedicated event field. Guard an empty reference for **silence**, never for substitution. See "Audio (FMOD)" under Architecture Patterns
- `RuntimeManager.PlayOneShot` / `PlayOneShotAttached` directly — they take no per-instance volume, so the sound ignores the in-game SFX slider whenever the FMOD bus fails to resolve. Go through `AudioSystem.PlaySFXEvent` / `PlaySFXEventAttached` or `FMODOneShotVolumeHelper`
- **Guarding `using UnityEngine;` (or any using an unguarded declaration needs) behind `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD`.** A guard must cover a self-consistent unit: if the class declaration is outside the guard, everything it depends on must be too. `#if UNITY_EDITOR\nusing UnityEngine;\n#endif` above an unguarded `class Foo : MonoBehaviour` compiles fine in the Editor and in Development builds, then fails the **Release** player build with `CS0246: 'MonoBehaviour' could not be found` — which is the automated build, not yours. Likewise, never touch the `UnityEditor` namespace outside `#if UNITY_EDITOR` in a file that isn't under an `Editor/` folder. Run `python3 Tools/Build/check_conditional_compilation.py` (~1s, no Unity needed) before committing any guarded script. Full rules + the two safe patterns: `Docs/CONDITIONAL_COMPILATION.md`
- A per-vessel component that drives the gameplay camera's FOV or the Panini override — the speed tunnel is a PLATFORM LAW driven by the single static `VesselSpeedTunnel` (`Docs/SPEED_TUNNEL.md`). `PostProcessingManager.SetSpeedTunnelPanini` is one global override with no ref-counting, so a second writer silently stomps the first and an outgoing vessel's teardown releases the incoming vessel's effect mid-swap. Bind platform-wide vessel behaviour in `VesselController.Initialize` under `IsLocalPilot`, never on a prefab
- New spatial queries against prisms via `Physics.OverlapSphere` / `Physics.CheckBox`, or building a new grid/registry/octree over prisms — `PrismSpatialIndex` is THE canonical spatial index of prism mass (occupancy, AOE, proximity). Physics queries are also structurally blind to fresh prisms (colliders disabled for the first 0.6s after spawn). Add new query shapes to `PrismSpatialIndex` instead — see `Docs/SPATIAL_INDEX.md`
- `await UniTask.SwitchToMainThread()` or `await UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — they don't reliably switch threads on this UniTask version. Use `.AsMainThread()` (see `Docs/THREADING.md`)
- Raising a SOAP `ScriptableEvent` from a UGS / Netcode `Task` continuation without ensuring the continuation has resumed on the main thread first — SOAP `Raise()` invokes listeners inline, so off-thread raises crash any listener that touches Unity state
- Touching a `UnityEngine.Object` (incl. `== null` checks routing through `op_Equality`) in a `Task` continuation without `.AsMainThread()` upstream — throws `EnsureRunningOnMainThread`
- **Relying on an `[Inject]` field in a prefab that a gameplay system spawns at runtime, without finding the injector.** Reflex populates `[Inject]` for objects present at scene load (via the scene's `ContainerScope`) and for anything a call site explicitly runs `GameObjectInjector.InjectRecursive` on — vessels, players, projectile/AOE pools. **Everything else gets a null field**, and the whole of `Controller/Environment` is in that set: nothing there injects, so every cell-spawned flora, fauna and crystal has null injected dependencies. The failure is invisible because the correct defensive shape and the broken one are identical — `if (audioSystem != null) audioSystem.Play…()` is exactly what a good null-guard looks like, and it silently swallowed the crystal pickup sound for the entire food web's output for as long as the ecology has dropped crystals (`Docs/ECOSYSTEM.md §31.2`). Before depending on an injected field, grep for who injects that object; if nobody does, resolve at the call site instead (`AudioSystem.Instance`, the live-`Instance` property pattern below), or inject it at the spawner
- Caching a UGS singleton `*.Instance` (e.g. `MultiplayerService.Instance`) in a service **constructor** — lazy DI singletons are constructed during Bootstrap DI resolution, *before* `UnityServices.InitializeAsync()` completes, so `*.Instance` is null at construction and gets pinned null forever. Instead expose a private property that resolves at use time: `private IMultiplayerService _multiplayerService => MultiplayerService.Instance;` — always reads the live `Instance` at the call site (see `PartySessionService` / `PresenceLobbyService`)
- Subscribing to per-`RoundStats` C# stat events (`OnScoreChanged`, `OnAnyStatChanged`, `OnCrystalsCollectedChanged`, …) with cleanup gated on `OnMiniGameTurnEnd`, or unsubscribing by iterating `gameData.RoundStatsList` — `RoundStats` lives on the **persistent** Player NetworkObject (survives every scene transition), a mid-turn scene exit never fires the turn-end cleanup, and `SceneLoader.LoadSceneAsync` clears the roster lists via `ResetRuntimeData()` BEFORE the old scene's objects are destroyed, so list-based unsubscribe loops detach nothing. The leaked delegates fire inside the next game's stat-setter raise chains and can silently kill the game-end flow (`Docs/ScoringSystem/BUGS.md` B15). Instead: track the stats you actually subscribed to and detach from that record in `OnDestroy` (see `NetworkCrystalCollisionTurnMonitor` / `MultiplayerHUD`); `Player.PrepareForNewScene` / `InitializeForMultiplayerMode` purge any stragglers via `RoundStats.ClearEventSubscriptions()` at every scene entry

## Shader & Visual Development

### HLSL / Shader Graph

- Custom Function nodes use HLSL files stored in a consistent location
- Function signatures must follow Shader Graph conventions (proper `_float` suffix usage, sampler declarations)
- Blend shapes are converted to textures for shader-driven animation (no controller scripts — animation is entirely GPU-driven for performance)
- Edge detection, prism rendering, Shepard tone effects, and speed trail scaling are active shader systems
- Procedural HyperSea skybox shader with Andromeda galaxy, domain-warped nebulae, and configurable star density

### Performance Standards

- Use `Unity.Profiling.ProfilerMarker` with `using (marker.Auto())` for profiling, not manual `Begin`/`EndSample`
- Watch for `Gfx.WaitForPresentOnGfxThread` bottlenecks — usually indicates GPU sync issues, not CPU
- Static batching, object pooling, and draw call management are always priorities
- Test with profiler before and after optimization changes — don't assume improvement
- GPU instancing enabled on all prism and VFX materials
- Prism scale/material/effect animation is GPU-clock-driven (the clock-material law, `Docs/PRISM_ANIMATION.md`) — the former CPU Jobs+Burst animation managers are deleted
- Burst-compiled spatial queries replace Physics-based AOE prism damage (`PrismSpatialIndex` — see `Docs/SPATIAL_INDEX.md`)
- Cache-line-aware data layouts with hot/cold splitting and bit-packed flags (`PrismSpatialData` / `PrismDamageData` in `PrismSpatialIndex`)
- Growth occupancy checks use `PrismSpatialIndex.TryReserve` (claim-before-spawn), never `Physics.CheckBox` — prism colliders are disabled for the first 0.6s after spawn, so physics queries are structurally blind to fresh prisms

### Prism System Performance

The prism system is the most performance-critical gameplay system. See `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` for the full audit (note: audit doc remains in the vestigial `Game/` directory). Key facts:

- Each prism is a full GameObject with 5-6 MonoBehaviours + BoxCollider + MeshRenderer
- At 2,000 prisms: ~12,000 MonoBehaviour instances + 2,000 colliders
- Scale and material animation are already Jobs + Burst optimized
- Main bottlenecks: explosion/implosion VFX (per-object UniTask), physics colliders, material instancing leaks
- Active optimization: `PrismTimerManager`, per-frame explosion VFX cap, `EventListenerBase` GC elimination

## Testing

### Test Infrastructure

- **Framework**: Unity Test Framework 1.6.0 (NUnit-based)
- **Edit-mode tests**: `Assets/_Scripts/Tests/Editor/` — 17 test files covering enums, data SOs, geometry utils, party data, resource collection, disposable groups, camera settings, etc.
- **Bootstrap tests**: `Assets/_Scripts/System/Bootstrap/Tests/Editor/` — `AppManagerBootstrapTests` (file: `BootstrapControllerTests.cs`), `BootstrapConfigSOTests`, `SceneTransitionManagerTests`, `ApplicationLifecycleManagerTests`, `ApplicationStateMachineTests`, `SceneFlowIntegrationTests`
- **Multiplayer tests**: `Assets/_Scripts/Controller/Multiplayer/Tests/Editor/` — `DomainAssignerTests`
- **PlayFab tests**: `Assets/_Scripts/System/Playfab/PlayFabTests/` — `PlayFabCatalogTests`
- **SOAP framework tests**: `Assets/Plugins/Obvious/Soap/Core/Editor/Tests/`
- **Test scenes**: `Assets/_Scenes/TestInput/`, `Assets/_Scenes/Game_TestDesign/`

### Build & CI

No automated CI/CD pipeline is currently configured. Builds are manual. Build profiles live in `Assets/Settings/Build Profiles/`.

### Unity CLI verification — `/verify-unity` gates every C# commit (LOCKED rule)

The repo carries **`com.unity.pipeline`** (`0.5.0-exp.1`, experimental) so a session can drive the
**open** Unity Editor from the terminal via the standalone `unity` binary — real compiles, Play
mode, in-editor verification, no human alt-tabbing and no copy-pasted console errors. Setup +
troubleshooting: `Docs/unity-cli-setup.md`. The CLI is experimental and changes often; **`unity
--help` for the installed version is authoritative** — never assume a command or flag from memory
or from another machine.

- **The rule: any C# change must pass `/verify-unity` before it is committed.** A green edit-mode
  suite or a clean mental compile does not substitute for the Editor actually compiling and loading
  the change. If the CLI genuinely isn't available in the session (no editor open, no `unity`
  binary), say so explicitly in the commit/PR and file the change in
  `Docs/UNITY_VERIFICATION_CHECKLIST.md` as before — never claim a verification that didn't run.
- **`[CliCommand]` wrappers** are the editor methods exposed to the CLI's `unity command` surface —
  the sanctioned way to give the CLI a repo-specific operation. They are editor tooling: they live
  under an `Editor/` folder (Assembly-CSharp-Editor), never in runtime code.
  <!-- TODO: no [CliCommand] wrappers exist in-repo yet; when the first one lands, record its
       location and the naming convention here. -->
- **The runtime component never ships.** `UnityPipelineReleaseGuard`
  (`Assets/_Scripts/Editor/Build/`) is an `IPreprocessBuildWithReport` that throws
  `BuildFailedException` on any **non-development** build whose shipped content (build scenes,
  prefabs they instantiate, Resources, preloaded assets) would include the `com.unity.pipeline`
  runtime component. Development builds are exempt — that is the sanctioned way to debug the CLI
  integration in a player. Do not weaken or bypass this guard: release builds go to paying Steam
  customers.

## Editor Tooling (LOCKED convention — read `Docs/TOOLING.md` before adding any `[MenuItem]`)

**Every first-party editor tool lives under ONE menu root, `FrogletTools/`, and appears
automatically in `FrogletTools > Froglet Master Tool`.** The `Tools/Cosmic Shore/…` and
`Cosmic Shore/…` roots were retired — do not reintroduce them, and do not add a tool under
`Tools/`, `Window/`, or a new root of your own.

- **Discovery is automatic, never registered.** `FrogletToolRegistry` reflects over `[MenuItem]`
  attributes; a tool shows up on the board the moment its path starts with `FrogletTools/` and it
  compiles. There is no manifest to update. That prefix is also the only filter, so third-party
  package menus (PlayFab, FMOD, Soap, Quick Scene Pro) are never picked up and are left where
  their vendors put them.
- **The board is a card grid**: one collapsible colour-coded section per category, one card per
  tool (title, description, five-dot importance), most important first, flowing into as many
  columns as the window is wide enough for.
- **`[FrogletTool(category, Importance, Description)]`** on the same static method as the
  `[MenuItem]` controls the section, the ranking (1–5, which is also the dot rating on the card)
  and the blurb. It is optional — omit it and the registry infers a category from the path/type
  name and uses importance 3. The attribute compiles into the **editor** assembly, so only files
  under an `Editor/` folder can use it; a runtime-assembly tool behind `#if UNITY_EDITOR` still
  appears, just with inferred metadata.
- **Draw through `FrogletEditorPalette`** (banner, `ColorButton`, `StatusPill`, `DrawCard`,
  accent stripes, semantic Ok/Warn/Error/Info colours, light-skin adaptation) so every Froglet
  window reads as one product. Do not hand-roll `GUI.color` juggling in a new window — extend the
  palette instead.
- **Prefab drift is a first-class check.** `PrefabInstanceSceneScanner` reads prefab-instance
  overrides straight out of scene YAML (fast, read-only, no scenes opened) and
  `PrefabDriftFixer` performs every write through `PrefabUtility` on a properly loaded scene.
  Use these rather than opening scenes to interrogate `PrefabUtility`, and never hand-edit scene
  or prefab YAML to "apply" an override. **FrogletTools > Ecology > Audit Cell-Owned Visuals**
  rides the same scanner for the Cell's half of this: it reports scene-placed membrane/nucleus/
  cytoplasm instances that duplicate what the scene's Cell already spawns, and Cell overrides whose
  `propertyPath` names a field the script no longer has (Unity never prunes an unresolvable
  modification, so retired fields linger for years pointing at guids no asset carries).
- **Editor-tool config belongs in a ScriptableObject**, not a hard-coded list in the window
  (`GameModePrefabKitSO` is the reference) — same config-separation rule as gameplay.
- **A tool's OUTPUT is the deliverable; the tool is scaffolding.** A wirer/setup/migration tool
  writes a scene, prefab or SO into the human's **working tree**, while the branch carries only
  the tool — so the tool merges and its data does not, and the feature is broken on every other
  machine with nothing in the diff to explain it. Any tool that writes assets therefore
  `FrogletToolChangeLedger.Record(ToolName, path)`s in the same block that writes each one and
  draws `FrogletToolShipPanel.Draw(Ship, this)`: **Validate & Push** (saves, validates, stages
  ONLY that tool's recorded paths — never `-A` — commits, pushes; protected branches refused) and
  **Retire Tool** (deletes the one-off + scratch assets, refusing while its output is still
  unpushed, so retirement can't strand it). The catch-all is **FrogletTools > Build > Pending Tool
  Changes**, which also lists dirty files no tool claimed. Contract:
  `Docs/TOOLING.md` § "Tool output is a deliverable". Agent-side gate: the `/ship-tools` skill,
  and `/ship` §2.5 — which `/ship-quick` and `/ship-deep` inherit and **no mode may skip**. A
  READER tool (audit/report only) needs none of this; say so in its doc comment.

## Shared prefabs are single sources of truth (see `Docs/GAMECANVAS.md`)

`GameCanvas.prefab` is the in-game UI surface for every mode; the same rules apply to any prefab
shared across scenes.

- **A scene override always beats the prefab.** Overrides parked in a scene are why editing the
  prefab stopped changing anything — six game-mode scenes each carried ~1,770 unapplied overrides,
  1,734 of them byte-identical. If a change should apply to every mode, **Apply to Prefab**.
- **A variant, never a copy.** If a mode needs a different canvas, use **Create ▸ Prefab Variant**.
  `GameCanvas-HexRace.prefab` is a hard copy, which severed propagation and left 8 references
  dangling into the other prefab asset.
- **Genuinely per-mode values go in config or code**, not a scene override: an SO keyed by
  `GameModes`, or a runtime resolve. There is exactly one `MiniGameControllerBase` per gameplay
  scene, so the canvas finds it itself (`MiniGameHUD.EnsureReadyButtonWiring`,
  `Scoreboard.ResolveGameController`) — an explicit inspector assignment still wins.
- **Never bind a UnityEvent to a concrete controller subclass.** `OnReadyClicked` is public on
  `MiniGameControllerBase`; naming `HexRaceController` in the inspector creates a per-scene
  override for no gain.
- **Run `FrogletTools > Game Modes > Game Mode Prefab Kit` ▸ Validate before committing a scene**
  that contains a shared prefab.

## Code Style

- Clean, maintainable C# — favor readability over cleverness
- Use `[Header("Section Name")]` and `[Tooltip("...")]` attributes generously on serialized fields
- Use `[SerializeField]` with private fields, not public fields
- Pattern match where it improves clarity: `effects is { Length: > 0 }`
- Use `TryGetComponent` over `GetComponent` + null check
- Prefer expression-bodied members for simple accessors: `public Transform Transform => transform;`
- Anti-spam / cooldown patterns belong in the SO config, not hardcoded
- Always assign static numeric values to enum members to prevent Unity serialization drift
- Commit messages follow conventional commits: `type(scope): summary` (see `GIT_RULES.md`)

## Debugging Methodology

When investigating issues, follow this systematic approach:

1. Reproduce the issue consistently
2. Add `ProfilerMarker`s to isolate the hot path
3. Check the call stack in Timeline view for self-time
4. Narrow to the specific derived class (base class profiling often hides the real culprit)
5. Fix, profile again, confirm improvement with data

Do not guess at performance problems. Profile first.

## Communication Preferences

- Be direct and technical. Skip preamble and motivational framing.
- When presenting solutions, lead with the code, then explain if needed.
- If you need to make a judgment call between two valid approaches, pick the one that's simpler to maintain and mention the tradeoff briefly.
- When refactoring, preserve the existing naming conventions and folder structure unless explicitly asked to reorganize.
- For shader work: always specify which render pipeline stage and what Shader Graph node types are involved.
- Don't repeat back what I just told you. Acknowledge briefly and move to the solution.

## What Claude Code Should Never Do

- Stop to ask "would you like me to continue?" after completing one of several related files
- Introduce new packages or dependencies without flagging it first
- Restructure folder organization or namespaces without explicit instruction
- Use `Debug.Log` as a fix — it's a diagnostic tool, not a solution
- **Leave a finished system's bring-up telemetry on `CSDebug.Log` (or, worse, raw `Debug.Log`).** The dense per-step trace you need while building a system is console spam the day it works, and every such trace outlives the cycle that wrote it — the `[FLOW-n]` spawn trace and the `[GyroidColony]` census each shipped ~60 and 1-per-5s log lines forever. Put it on a **`CSLogChannel`** (`CSDebug.LogVerbose(channel, …)`, `CSDebug.IsVerbose(channel)`), which defaults to OFF and is toggled per-channel in **FrogletTools > Toolbox > Logging** — the trace stays in the tree as knowledge without shouting. Guard with `IsVerbose` first anywhere the interpolated message itself is expensive (a `[Conditional]` method's arguments are still evaluated in the Editor). Warnings and errors never move to a channel; a real fault must always be loud. And **nothing per-frame or per-contact gets a log at all** — not even a channelled one: the offenders that surfaced here were a per-skim resource log and a per-frame camera-zoom readout, both simply deleted
- Write a tooling, diagnostics, benchmark, or debug-overlay script that uses `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD` without reading `Docs/CONDITIONAL_COMPILATION.md` and running `python3 Tools/Build/check_conditional_compilation.py` first. "It compiles in the Editor" proves nothing here — the Editor always defines `UNITY_EDITOR`, so this whole bug class is invisible until the Release build fails
- Leave TODO comments as a substitute for completing the work
- Generate code that compiles but ignores the established architecture patterns above
- Add if-null guards on SOAP ScriptableEvent serialized fields — fail loud
- Plug a placeholder/temp FMOD event into a new sound, or hardcode an event path in code. Add the `[SerializeField] EventReference` (per ability, per trigger, per emitter), ship it **empty**, and say so — an unwired slot is a visible TODO; a temp event is one nobody ever finds
- Use `renderer.material` when `renderer.sharedMaterial` + MaterialPropertyBlock works

## Design Philosophy: Favor Emergent Systems Over Bespoke Solutions

Cosmic Shore aims to be built on a small, carefully curated set of
**fundamentals** whose interactions produce a large number of desirable
emergent outcomes. When solving a problem, maintain active awareness of
these fundamentals and prefer solutions that work *through* them rather than
*around* them.

### The fundamentals (working list)

Use the canonical term, not a casual synonym. This list is the team's current
best understanding and will be refined over time — propose additions or
corrections through the process below rather than silently inventing new
ones.

- **Domain** — team/affiliation identity attached to mass, vessels, and
  structures. Sometimes referred to casually as "color"; the canonical term
  is *domain*.
- **Mass** — the produced/consumed quantity that drives scoring, fueling,
  and cell control. **Mass is conserved: it has no passive decay.** A prism
  (the concrete unit of mass), once created, is only ever removed by an
  *active* force — a vessel using an ability, or fauna eating it. There is no
  aging, lifespan, timed culler, or growth/decay oscillator anywhere in the
  mass pipeline. Population homeostasis is the job of the **food web** (fauna
  consume mass; fauna starve when prey is scarce), never of artificial decay.
  A large accumulation of prisms is therefore a *valid* state, not a bug to
  auto-correct: it persists until an active force consumes it, and when the
  fauna that would eat it can't reach prey, the correction surfaces as fauna
  starving — not as prisms vanishing. This holds in **every scene the
  simulation runs in** — including Menu_Main's lava-lamp/freestyle, where the
  autopilot vessel *is* the gameplay vessel. There is no "cosmetic" or
  "menu-only" exemption. See "Universality" and "Don't cheat emergence" below
  and `Docs/ECOSYSTEM.md`.
- **Cells** (with `CellType`) — the regions of play that are the unit of
  territorial control. Casual language sometimes calls these "biomes"; the
  canonical term is *cell*.
- **Elementals** — the single system that governs **all** buffing and
  debuffing across vessels and their environment. If a buff or debuff isn't
  expressed through elementals, that's a smell.
- **Prisms / Prismscapes** — the geometric primitive of player-generated
  structure. Trails are the 1-dimensional case of a prismscape; higher-
  dimensional prism constructions reuse this primitive rather than
  introducing parallel structure types. **The DIMENSION ladder is shipped**:
  `PrismscapeDimension` names it (Singleton 0 / Trail 1 / Surface 2 /
  Volume 3) and `PrismscapeTopology.DimensionOf` resolves a prism's
  prismscape from authored evidence (`Trail.Dimension`) first, else a
  neighbourhood census. A vessel that ATTACHES rides 1D through
  `TrailFollower` (a rail grind) and 2D through `BlockscapeFollower`
  (marble-madness rolling); **0D — an isolated prism — is deliberately not
  rideable**. In both dimensions the prismscape constrains POSITION only:
  attitude is always the pilot's. Prisms *are*
  conserved mass (see **Mass**): only active forces — vessel abilities and
  fauna consumption — remove a prism. Whether a prism is a lifeform's health-
  prism or vessel-spawned makes no difference to this rule.
- **Flora & Fauna** — populations that live on and respond to the
  fundamentals above (e.g. fauna attraction to prisms, flora growth on
  cells).
- **Vessels** — the player/AI actors whose class-specific abilities compose
  with the fundamentals above.
- **Toys** — interactive world-space stations the player's **Vessel** flies into,
  surfaced in the Menu_Main lava-lamp/freestyle "toybox". A toy has **no score and
  no end condition** — something to play with indefinitely (toys are to freestyle
  what party games are to the rest of Cosmic Shore). Added at the prompter's request;
  it earns its place by composing with the others rather than bypassing them: the
  vessel-changer cycles **Vessel**, the domain-changer cycles **Domain** (server-RPC,
  never a client write), the painting/"connect the dots" toy lays a conserved **Mass**
  prism pattern, and the **Wanderway conveyor** streams **Prisms/Mass** (a fixed stock
  it *transports* — suction-out → bloom-in — never creates or destroys), **Crystals**
  (skimmable elemental pickups), and **Flora & Fauna** (released into the containing
  **Cell** as ordinary citizens) into an endless field ahead of the vessel, and the
  **cell-selector** picks the **Cell** itself (a matrix of mini-cells over the Cell's
  *own* config rotation — the toy never authors a parallel list — routed through the
  one `Cell.RequestCellSwap` entry point; choosing the cell you are already in is the
  freestyle reset). Toys are placed relative to the **Cell** membrane (read, not
  duplicated). **A toy is activated by a SWITCH** (below): every toy root and every
  choice a toy unfolds into is drawn inside one continuous ring at the radius of its
  own trigger collider, so "how do I use this?" is answered by the shape. Drawn by
  `Toy.Initialize` from that collider — not by each toy's builder — so a toy authored
  tomorrow wears one; two explicit opt-outs (`Toy.ConfigureSwitchRing`): a smaller
  radius where a matrix's stations would otherwise interpenetrate, and **waived
  entirely for the domain changer**, whose cones already carry the read. A toy imposes
  no decay/timer/win-lose, so it stays inside *Mass is conserved* + *don't cheat
  emergence* — a cell swap removes mass only because a player flew into a station and
  asked for a new world, the same **active**, explicit event class as a scene load,
  never a clock. Unlock *conditions* are deferred; the toybox registry + per-toy
  unlock state live in `ToyboxSO`.
  See `Docs/ToySystem/ARCHITECTURE.md` and `Docs/ECOSYSTEM.md §19`.
- **Switch** — *a ring you thread, and threading it activates something.* The one word
  the platform has for "this does something when you go through it", and deliberately
  **threader-agnostic**: a **Vessel** threads a freestyle **Toy**, a ball threads a
  Scarab switch or an Astro League goal. Named as a fundamental at the prompter's
  request; the reach was already there before it was named — freestyle toy roots +
  matrix stations, the painting toy's stroke gates and milestones, the SHARE/REPAINT
  completion gates, the Wanderway return station, `ScarabSwitch` (`SCARAB.md §5`) and
  `AstroLeagueGoal`. It composes rather than duplicating: with **Vessel/Toys** (the
  activation affordance), with **Prisms/Mass** (a Scarab switch fills its ring with
  conserved prisms, and threading it BLOWS THAT MEMBRANE OUT along the ball's velocity
  and pays a **scarab-wing dais** in its place — 255 prisms wrapping five super-shielded
  sun cores, each aiming a spike back at the spent switch; both the removal and the
  payout are active events caused by a specific strike, never a clock, `SCARAB.md`
  §5.1), with **Domain** (a switch wears
  the domain's *prism* material, and whose colour it is decides who it pays), and with
  **Cells** (rings are placed against arena/membrane geometry, never a parallel system).
  **The law that makes it teachable is one line: THE RING IS THE TRIGGER VOLUME, DRAWN
  AT ITS OWN RADIUS** — so a ring can never advertise a volume the collider does not
  have. A ring drawn *smaller* than its trigger is legal (crossing it still always
  fires); a ring drawn *larger* is a lie. It is not a new atom in the toy shape
  vocabulary — it is the existing ring, promoted: the reserved cone (*trail ON*) and
  jack (*trail OFF*) are untouched, and an emblem stays a **tilted** ring of discrete
  objects so it can never be mistaken for a switch. See `Docs/ToySystem/ARCHITECTURE.md`
  § "The switch".

### Process for curating fundamentals

The goal is an *exhaustive, minimal* set of fundamentals — expressive enough
to solve every problem through composition, small enough that the team can
hold the whole set in their head. Every fundamental costs mental overhead
for everyone who touches the codebase, so adding one must be a deliberate
act, not a side-effect of a feature ticket.

Before treating something as a fundamental (or before proposing a new one),
run this check:

1. **Name it precisely.** Use the canonical term. If no canonical term
   exists, propose one explicitly and get it agreed before using it.
2. **Show its reach.** A fundamental earns its place by being load-bearing
   for many features. Enumerate at least three distinct features or
   behaviors that depend on it; if you can't, it's probably not fundamental.
3. **Show how it composes.** Describe how it interacts with each existing
   fundamental. Emergence comes from the cross-products between
   fundamentals, so a system that doesn't meaningfully combine with the
   others is a bespoke feature wearing a fundamental's costume.
4. **Prefer extension over addition.** If a proposed fundamental is a
   special case of, or expressible through, an existing one, extend or
   rename the existing one instead.
5. **Budget the weight.** A new fundamental must be *very* useful to justify
   the weight it adds to the set. Flag any proposed addition to the
   prompter and get explicit agreement before committing to it.

### Order of preference

When addressing a task, try these approaches in order and stop at the first
one that fits:

1. **Use an existing fundamental.** Can the goal be achieved by composing
   behaviors the current fundamentals already produce?
2. **Tune parameters.** Can it be achieved by adjusting the parameters,
   weights, or configuration of an existing fundamental?
3. **Extend a fundamental.** Can it be achieved by adding a small, general
   capability to an existing fundamental that other features could also
   benefit from?
4. **Propose a new fundamental.** Only after the steps above have been
   rejected for clear reasons, *and* after running the curation process
   above with explicit prompter sign-off.
5. **Add a bespoke solution.** Last resort, and only when a new fundamental
   would be unjustified weight.

Three similar lines is better than a premature abstraction, but a bespoke
feature that duplicates or bypasses an existing fundamental is worse than
either.

### Don't "cheat" emergence without asking

A "cheat" is any solution that directly hard-codes the desired outcome
instead of letting it arise from the interaction of the fundamentals.
Cheats are tempting because they are shorter and more predictable, but they
erode the systems that make the game's behavior rich and surprising, and
they tend to accumulate special cases.

If the most direct path to a goal would require reaching past the
fundamentals and using privileged information or a shortcut to explicitly
produce the outcome, **stop and ask the prompter for explicit permission
before doing so.** Describe the emergent alternative you considered and why
you were tempted to bypass it, so the prompter can make an informed call.

**Example.** Suppose the task is to balance the ecosystem by creating fauna
that are attracted to prisms. The emergent approach is to place prisms and
configure fauna attraction parameters (working through the Flora & Fauna
and Prism fundamentals), then let the fauna find them. A cheat would be to
use the known planted locations of the fauna to directly place or steer
things so the balance is achieved by construction. Before taking that
shortcut — for instance, before reading fauna placement data and acting on
it to short-circuit the attraction behavior — ask the prompter whether they
want the cheat or the emergent solution.

**Example (resolved): prism decay is a cheat — mass is conserved.** Cells fill
with the dominant domain's flora and "freeze solid": fauna only eat *opposing*
mass, so the leader's flora have no predator and the prism count never falls.
The tempting fix is **passive prism decay** — prisms age and die on a timer (or
a cell-level reaper culls N per tick) so the count drops on its own and flora
resume growing through the phase hysteresis. **That is a cheat** — a timed
culler is just the flora regrowth-pulse inverted, a hard-coded oscillator
reaching past the fundamentals to manufacture the breathing we want to *emerge*.
The decided answer (do not relitigate): **prisms are conserved; the only sinks
are active — vessel abilities and fauna consumption.** The down-force on a
dominant accumulation is the **food web**: opposing-domain fauna graze it down,
or, when no fauna can reach edible prey, the population crashes via starvation.
A large accumulation that nothing is eating is a *valid* equilibrium, not a
defect to auto-correct. If a future cell "freezes," fix it by giving an active
force a reason/ability to consume that mass (or by tuning fauna diet, reach, and
spawning) — never by adding decay. The flora regrowth pulse that currently
exists is the growth-side counterpart of this same cheat and is flagged for
retirement, not extension. See `Docs/ECOSYSTEM.md`.

**Example (resolved & reverted): the menu trail cap is a cheat — no "cosmetic"
exemptions.** The Menu_Main autopilot vessel lays prisms indefinitely, so a
perf-motivated commit added a per-trail ring-buffer cap (`maxTrailBlocks` /
`Trail.RemoveOldest`, commit `64d8f0c8`) that silently recycled the oldest
trail prism on every new spawn, rationalized as "cosmetic, menu-only —
gameplay unaffected." That rationale was false by construction: the lava lamp
*is* freestyle (one system, two names — see "Lava-Lamp Mode"), so the same
capped vessel is the one the player flies, and the cap followed them into
freestyle flight as an age-based trail limit — exactly the passive-removal
cheat §0 of `Docs/ECOSYSTEM.md` rejects. The commit was reverted. The decided
answer (do not relitigate): **there is no context in which trail caps, prism
TTLs, or idle cullers are acceptable.** If prism accumulation in the menu (or
anywhere) is a perf problem, solve it with the universal systems: **fauna
cleanup** (cleanup is one of the fauna's jobs — foragers consume trail mass
through the food web) or **pause/throttle the spawner** (not creating mass is
allowed; aging it out is not). **One authorized exception exists** — the
Wanderway rolling tether, granted by explicit sign-off to make that toy a truly
infinite runner at fixed memory, fenced to a live `WanderwayRun` and recorded in
`Docs/ECOSYSTEM.md` §0. It is an exception *because it was asked for*, not a
precedent: the protocol still stands, and the next one needs its own sign-off.

### Universality — one HyperSea, one rule set

The fundamentals are universal. The HyperSea has rules and **everything in it
follows them** — game scenes, Menu_Main's lava-lamp/freestyle, tools and test
scenes alike. Do not create context-specific exemptions ("it's only the menu,"
"it's just cosmetic," "it's a perf special case"). Every carve-out creeps
confusion into best practices about when the rules apply, and carve-outs are
precisely how rejected cheats re-enter the codebase — both resolved examples
above came back wearing a special-circumstance costume.

When a context creates pressure (performance, pacing, visuals), solve it with
the universal systems that already exist — fauna have many jobs and cleanup is
one of them; spawners can pause; abilities can consume — never with a bespoke
mechanism that exists only in that context. Build systems once, use them
everywhere. If a universal system genuinely can't serve the context, that is a
fundamentals discussion (see the curation process above), not a license for a
local workaround.

### When in doubt

Name the fundamentals involved, describe how each candidate solution
interacts with them, and prefer the solution that leaves the fundamentals
intact and more expressive for future features.
