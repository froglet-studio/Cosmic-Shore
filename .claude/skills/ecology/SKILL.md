---
name: ecology
description: Use for ANY change to the Cosmic Shore cell ecosystem — flora, fauna, cells, crystals, spawn profiles (SpawnProfileSO / CellConfigDataSO), phase/volume (Cell.LiveVolume, CellPhaseThresholds), lifeform powerups (LifeFormCrystal / ElementalCrystal*), wither/starvation, predator-prey, reproduction, evolution, or biome/intensity tuning. Loads the locked invariants + the collider-budget gate + the change protocol so changes start from the correct design and stay performant. Trigger when editing Assets/_Scripts/Controller/Environment/** (Cell, RandomLifeSpawner, Flora*, Fauna, LightFauna), the ecology SOs, or Docs/ECOSYSTEM*.md.
---

# Ecology Change Protocol

You are changing the Cosmic Shore cell ecosystem — a **platform fundamental** on the path to
credible artificial life. The system's design intent has caused repeated rework when guessed at;
this protocol exists to prevent that. Follow it exactly.

## 1. Read the canon first
- `CLAUDE.md ▸ Ecosystem Design Principles (LOCKED)` — the invariants (authoritative).
- `Docs/ECOSYSTEM_MASTERPLAN.md` — north star, the artificial-life scorecard (§3), the **collider
  contract (§4)**, the platform-wiring plan (§5), the phased roadmap (§6), the orchestration (§7).
- `Docs/ECOSYSTEM.md` — the mechanics log (how the current system actually works).

## 2. Restate before you edit (this kills the #1 source of rework)
In one or two lines, state which invariants the change touches and confirm it violates **none**:
**continuity of existence** (nothing pops in/out — everything grows/fades/suctions/withers; PLATFORM-WIDE) ·
no imposed death/decay/lifespan · no domain asymmetry (controlling-color spawn only) ·
wither-to-crystal + mass conservation · volume is the spine (not count) · the lifeform→elemental-
crystal invariant (a connected COLONY is a POPULATION, so every member carries and drops its
own heart — the worm colony's head, body AND tail segments all do, and only its heartless root
anchor does not; the old "body segments are body parts" ruling is RETRACTED,
`Docs/ECOSYSTEM.md` §23.3 + §23.8) · territorial permanence (don't cull the dominant canopy) · endogenous
selection only (survival = fitness, never a scripted fitness function — and there is NO
lifeform LEVEL, rolled or earned: a lifeform is its species and its ELEMENT, which states
everything about itself exactly once, the size of the heart it drops included;
`Docs/ECOSYSTEM.md` §40) · the collider budget.
**If a change might violate one, STOP and ask (AskUserQuestion). Do not guess the design.**

## 2.5 When sign-off IS granted — landing a carve-out that neither leaks nor gets reverted

§2 ends at "STOP and ask." Sometimes the answer is **yes, break it** (the Wanderway rolling
tether, `Docs/ECOSYSTEM.md` §0, is the worked example: recycling trail mass to buy a truly
infinite runner at fixed memory). A granted exception is not a free hand — it is a *fenced*
one, and the fence is half code, half record. Do both.

**Fence it in code.** The exception must be impossible to invoke by accident:
- **One caller, reachable only from the feature.** The removal/override API gets exactly one
  call site, inside the feature's own runtime object. Grep and confirm it before you commit.
- **No knob on the shared system.** Do NOT add a cap/TTL/limit field to the general-purpose class
  (`VesselPrismController` grew no `maxTrailBlocks`). The feature reaches in from outside and only
  while it is live; the shared system stays innocent.
- **The API's own doc-comment names the exception, the sole caller, and the doc that records it** —
  so the next reader who finds it by grep learns it is fenced before they reuse it.
- **Waive only the invariant that was actually granted.** Continuity of existence is a *separate*
  law from mass conservation: a sanctioned removal still has to wither/suction/fade out. Check each
  invariant in the §2 list independently rather than treating "approved" as blanket.

**Record it in three places** (a carve-out recorded once reads as a bug the next time someone greps):
1. `Docs/ECOSYSTEM.md` §0, **beside the rejected version it resembles** — state what it does, the
   reason it was granted, and the fence. Without this, the next session finds the mechanism, matches
   it to the rejected cheat, and reverts it.
2. `CLAUDE.md`, on the invariant's own bullet — the absolute wording ("there is no context in
   which…") needs the exception attached to it or it reads as a contradiction.
3. The system's `Docs/<System>/ARCHITECTURE.md`.

**Frame it as an exception, never a precedent.** Say plainly that it holds *because it was asked
for*, that the protocol still stands, and that the next one needs its own sign-off. Then go find
what the carve-out silently broke — see the traps below.

## 2.6 Prism / trail traps (each of these cost real time)

- **A SHIELD's size is not the prism's size, and the two tiers scale DIFFERENTLY.** Both shield
  meshes are built from the box HALF-extents x `CIRCUMSCRIBING_SCALE` (3), so a prism of full
  size `S` gets a semi-axis of `1.5 S` — **3x the box's own extent**. The octahedron's vertices
  sit ON THE AXES (extent `3S`, circumsphere `3S`); the stella octangula's spikes sit at the
  **CUBE CORNERS**, so its axis extent is also `3S` but its circumsphere is `3S*sqrt(3) ~= 5.196 S`.
  Sizing a super-shielded prism by its bounding box therefore understates what the player sees
  by `sqrt(3)`, and no axis-extent check can see it. Decide which measure the design cares about
  ("fits this slot" = bounding box; "reads this big" = circumsphere), derive the authored scale
  from the generator's own constant, and remember that cycling a tier along same-sized prisms
  TRIPLES every shielded one unless you fit it (authored scale x `1/CIRCUMSCRIBING_SCALE`, which
  restores the envelope exactly - §35's "fit the PRISM, never the pattern"). Full table + the
  clearance and hinge consequences: the `asset-surgery` skill, "Trap: a SHIELD's size is not the
  prism's size".
- **A positional Add/Remove pair is asymmetric for anything that MOVES.** `Cell.AddBlock` files
  the fauna density grids at the position read at add time; a `RemoveBlock` that re-reads
  `transform.position` decrements whichever bucket the prism has since wandered into, stranding
  a permanent phantom count in the bucket it was actually filed under — fauna then steer at
  empty space, forever, with nothing logging. Movers exist (fauna bodies are exempt as
  volume-only, but the Ark's hull and gyroid bonding are grid-tracked movers), so the cell now
  stores each grid entry's FILED position (`gridTracked` is a `Dictionary<Prism, Vector3>`) and
  removes there. The general rule: any add/remove bookkeeping keyed on a re-read position is a
  leak for movers AND for destroyed refs (whose transform is unreadable at remove time) —
  remember what you filed, remove what you remembered. Docs/ECOSYSTEM.md §41.
- **An environment prism is not "pooled", and a devoured one never deactivates.** The
  pooled-vs-instantiated partition everywhere (cell swap, satellite strike, tether recycle) is
  `Prism.OnReturnToPool != null` — and `EnvironmentPrismPool` never wires it, so every
  environment-laid prism is INSTANTIATED-class mass (destroy-drained on retirement; the pool's
  own `TryRelease` is called only by the swap drain). And `Prism.Consume` → `SetupDestruction`
  leaves the GameObject ACTIVE — `destroyed = true`, render hidden, collider off — so an
  aliveness test on `activeInHierarchy` alone counts eaten mass as alive, and a per-frame loop
  gated on it keeps paying for corpses. Test `!prism.destroyed` too, and never write a
  retirement that waits for a pool return that structurally cannot come. A `static` registry/claim book/
  frontier that coordinates a population survives every cell teardown — `Cell.ResetCell`,
  `Initialize`, and the Cell-Selector world swap all destroy the lifeforms and leave the
  static state standing, so the NEXT world inherits the dead one's entries and acts on
  coordinates that no longer contain anything. Anything a lifeform's own `OnDestroy`
  releases is fine (that self-heals); anything only the POPULATION owns must be dropped by
  the **cell**, at all three reset sites. Key it by `(Cell, species)` rather than species
  alone — one cell resetting must not wipe another's book, and it is usually also the fix
  for a second bug, since per-cell clocks/periods were being shared too.
- **`CellTypeChoiceOptions.IntensityWise` silently swaps the SPAWNER class.**
  `Cell.StartSpawnerForMode` picks `IntensityWiseLifeSpawner` for every IntensityWise cell and
  `RandomLifeSpawner` for everyone else. Any spawn-loop feature (a density scalar, a gate, a
  new roll) implemented in only one of them is **dead code in exactly the modes that asked for
  it**. Implement in both, or state why one is deliberately excluded.
- **A POPULATION's cadence must read `Cell.CurrentFaunaSpawnPeriod`, never
  `OnFaunaWaveSpawned`.** This is the spawner-swap trap wearing a different hat: the wave
  EVENT is raised by `RandomLifeSpawner` alone, so subscribing to it makes a colony's
  production dead code in every `IntensityWise` cell — and it is dead in exactly the modes
  (Rampage, PeelTheCage, Scarab Scramble, Wildlife Liberation…) most likely to want it. The
  PERIOD is served by the `Cell` itself off `SpawnProfileSO.BaseFaunaSpawnTime` and is
  therefore correct under both spawners; both `AssembledFlora`'s colony cycle and
  `WormFauna.TickProduction` read it. Two consequences to carry: the period is authored
  per BIOME (shipped range 5s–30s, and the freestyle Lattice boot world's 5s is authored
  for its FLORA build clock), so anything riding it inherits a rate somebody else tuned;
  and a self-clocked cycle must **stamp its clock when the period elapses, before any
  can-I-produce gate** — stamping only on success lets a population sitting at its cap bank
  unbounded elapsed time and then produce instantly the moment a slot opens.
- **A boid term blended into a NORMALIZED direction must itself be BOUNDED.** Steering code
  normalizes the goal pull to unit length and then adds `weight × term`, so a term whose
  magnitude carries world units is being compared against 1. An inverse-square separation
  written `diff / sqrMagnitude` has magnitude `1/|d|` — at real creature spacing (25–160u)
  that is 0.006–0.04, i.e. a few degrees of deflection *at any weight*, and no amount of
  tuning fixes it because reaching parity needs a weight that then explodes at contact. Scale
  a UNIT vector by a falloff in `[0,1]` (`(1 - d/radius)²`) so the authored weight is a true
  ratio against the goal pull. The symptom is "the flocking rule does nothing and the number
  in the config looks reasonable"; check the term's MAGNITUDE at a real distance before
  touching the weight.
- **An "it is wired nowhere" claim is true only on the date it was written.** Deployment
  absence rots faster than anything else in an ecology doc — the next branch adds the species
  to a `SpawnProfileSO.SupportedFaunas` and will never think to go correct a claim it never
  read. The worm colony's "deliberately wired into no SpawnProfile — a boss is opt-in" survived
  into a ship review while Wildlife Liberation's four profiles were running a standing
  population of nine (at what was then `InitialLevel 3` — a field retired with lifeform levels,
  `Docs/ECOSYSTEM.md` §40), which is the one deployment any worm change has to be
  sized against. Re-prove absence by grepping the CONFIG ASSET'S GUID across `_SO_Assets`
  before you inherit the claim — and size collider/volume budgets against the tightest real
  consumer, not against the toy-released case.
- **"Both spawners" is not enough for FAUNA — there are FOUR producers.** `RandomLifeSpawner`,
  `IntensityWiseLifeSpawner`, **`Fauna.TryReproduce`** (reproduction is the actual population
  driver, not the spawner) and the freestyle **`Microscene`** conveyor all read the per-species
  population numbers. Splitting a modifier across them is not merely incomplete, it is
  *incoherent*: a seeder filling to 24 while reproduction stops at 6 is two ceilings for one
  number. So a per-cell modifier of a per-species number resolves on the **`Cell`** — the one
  object all four already hold (`Cell.ResolveFaunaPopulation` / `ResolveFaunaCap` /
  `IsFaunaAtCap`) — and the raw config field then has **no direct reader** outside the config
  and the profile. Write the comparison once too (`IsFaunaAtCap`), or a caller will test the
  unscaled number correctly-looking-ly. Generalizes to any future per-cell modifier.
- **A population scalar that moves the FLOOR but not the CAP is inert.** `MaxLivePopulation` is
  documented as "a performance backstop, not the primary control", which makes it easy to leave
  alone — but it is what actually bounds a standing population. The Blob tadpole floors at 4 and
  caps at 6, so a floor-only scalar is clamped away above ~1.5× and reads as *doing nothing*.
  Move floor and cap together. (Scaling either is production gating, which §0 permits; culling
  to meet a lowered scale is not.)
- **FLORA REPRODUCE TWO WAYS, and tuning one is dead tuning on the other.** A species is on
  exactly one of two paths and the config gives no hint which: the **per-plant growth quota**
  (`FloraConfigurationSO.GrowthPerOffspring`, spent in `Flora.TryReproduce`) or the **colony
  cycle** (a lattice species — gyroid / Schwarz P / quasicrystal — births ONE plant per
  `Cell.CurrentFaunaSpawnPeriod` for the whole population, `AssembledFlora`, §32.7). The quota
  field is **written on every config by `author_flora_populations.py` and is inert on the lattice
  ones**, which is what makes this invisible: the asset says `GrowthPerOffspring: 22`, the tests
  pass, and nothing reports that the number is never read. Measured today: of 102 flora configs,
  50 reproduce at all — and **34 of those 50 are lattice**, including every asset literally named
  "…Flora Time". So a reproduction change applied to the quota alone misses two-thirds of the
  breeding population *and* the families that breed most. Before touching either, split the
  species list by `FloraPrefab in {GyroidFlora, SchwarzPFlora, QuasicrystalFlora}` and state which
  path each change reaches. Sibling of §4.6, one level up: that one asks which CEILING binds, this
  one asks which PRODUCER runs at all.
  **Corollary — a colony's cadence is per (cell, species), never per plant.** The frontier cycle
  book is shared and every plant in the colony ticks it, so a per-plant period is set by whichever
  plant happened to tick first. A colony is mixed-element by construction
  (`LATTICE_MIN_FOUNDERS = 4`, and a colony inherits its founder's element pick), so any
  element-keyed cadence must key on the CONFIG's authored element.
- **A shared species asset is the reason the scalar belongs on the PROFILE.** Rampage's two
  species are referenced straight out of `Blob Cell/`, so stocking its arena by editing them
  would have restocked Menu_Main's lava lamp too. Before tuning any lifeform config, grep who
  else references it — a per-mode number on a shared asset is a cross-mode bug.
- **A tuning field on `FloraVariantTuning` reaches only the flora families that READ it.**
  `MaxTotalSpawnedObjects` was honoured by `AssembledFlora` alone for a long time, so 45
  authored assets were writing a per-plant budget that did nothing on branching and
  phyllotactic species — and the silent fallback was the prefab's own 5000. A field that
  appears on every flora config has to mean the same thing on every flora; check all three
  `ApplyVariantTuning` overrides when you add one.
- **A cell choice that is STICKY and derived from REPLICATED state must be gated on
  replication.** `Cell.AssignConfig` latches `runtime.Config` on its first pass and reads
  `SelectedIntensity`, which reaches a client only in the game-config ClientRpc — while a
  client's cell bootstraps off its FIRST CRYSTAL, ~600 ms earlier. The client then builds a
  different intensity's arena than the host, for the whole match, with no error (the SOAP
  default 0 clamps to a legal index). Gate on `GameDataSO.GameConfigSynced`, and make the
  deferral RETRYABLE — the bootstrap used to latch its "done" flag on its first line, which
  would have left a deferred cell with no cytoplasm and no spawner at all.
- **…but do NOT over-apply that gate: a value the client RECEIVES needs none.** The test is not
  "does this depend on intensity", it is "does a CLIENT derive it?". `CrystalManager`'s
  per-intensity crystal count reads the same late-arriving `SelectedIntensity`, yet needs no
  `GameConfigSynced` gate — it is resolved only inside `NetworkCrystalManager`'s `IsServer`
  paths and reaches clients as the replicated slot-list LENGTH. Gating it would add a race for
  nothing. Ask which machine computes the value before reaching for the gate.
- **`Mathf.RoundToInt` is banker's rounding.** For any authoring-facing scalar (a density
  multiplier, a per-intensity count), use explicit `Mathf.FloorToInt(x + 0.5f)` — otherwise
  `10 x 0.85` lands on 8 for one species and 9 for the next and nobody can explain why.

- **A vessel lays TWO ribbons.** `VesselPrismController.Trail` is only half the trail; the
  double-trail spawn pattern puts every other prism in `SecondaryTrail` (`Trail2`). Anything
  reasoning about "the vessel's whole trail" — length, mass, cleanup, recycling — must walk both,
  or it silently misses half the mass and any budget it enforces never converges.
- **Cached trail indices go stale on front-removal.** `TrailFollower` caches `attachedBlockIndex`
  and advances it itself; removing from the head of a `Trail` shifts every survivor and the rider
  starts racing forward along the ribbon. `Trail.OnOldestRemoved` exists for exactly this — any new
  index-cacher must ride it (hold a prism reference instead, where you can).
- **`OnReturnToPool != null` is the canonical pooled-vs-instantiated test.** It is how `Cell` tells
  a vessel's loose trail mass from instantiated environment mass (flora health prisms, a toy
  conveyor's transported stock). Use it before recycling anything; an unpooled prism handed
  `ReturnToPool()` silently stays in the world as an invisible collider.
- **Continuity-preserving removal, the recipe:** stamp `prism.TargetScale = <near-zero>` (the
  setter IS the grow-clock stamp — one write, GPU runs it), wait the wither duration, *then*
  `ReturnToPool()`. Never pool-return a prism at full scale; that is a pop.
- **The Cell's own visuals are single-instance fields, and the spawn chain reads them too
  early.** Two traps, one root: `Cell` holds ONE `membrane` / `nucleus` / `spawnedCytoplasm`
  and every cleanup path reads only the field, so any *unguarded* re-`Instantiate` (a repeat
  `Initialize`, a lazy-init nudge) orphans the previous one — it renders on top of the real
  one and nothing can ever collect it. Guard each spawn on its own field. And do not size one
  by hand: **a new core size means a new `CellConfigDataSO` pointing at a resized prefab**,
  never a scene-placed copy, a `localScale` tweak on the shared prefab, or a scene override
  (`Docs/ECOSYSTEM.md` §13.1).
  Reading the radius is the second half: `CellRuntimeDataSO.Cell` is assigned *inside*
  `Cell.Initialize`, which runs on `OnInitializeGame` behind `InitDelayMs` (1000 ms), while
  vessels spawn at `preSpawnDelayMs` (200 ms) and AI at `OnNetworkSpawn` (t≈0). Anything
  placing objects relative to the core during the spawn chain must use
  `Cell.FindByRuntimeData` (static registry, joined in `OnEnable`) and
  `Cell.ExpectedNucleusWorldRadius` (measures the config's prefab asset, no instantiate) —
  `cellData.Cell` is null then and `NucleusWorldRadius` returns 0, and a fallback built on
  either silently placed every player *inside* the nucleus.
- **Deferring a lifeform's crystal drop? REPARENT IT FIRST.** Ecology work keeps wanting to move
  *when* something happens in a death — the heart drops at the end of the wither rather than the
  start, a body part leaves later than it used to. A Unity child cannot be rescued once its parent
  is being destroyed, so a deferral that leaves the object parented to the husk loses it on every
  interrupted death (cell drain, turn end, a manager pulling the husk) — and an `OnDestroy`
  backstop cannot fix it after the fact, it can only *report* the loss. Split it: re-home the
  object at the TOP of the death (`Crystal.DetachHeartToCell` — onto the cell, but still
  `IsEmbedded` so it is uncollectable), and let the deferred step only change its *state*. Then
  every later exit is a real recovery instead of a hopeful one. **Corollary, and the part that
  bites twice:** any guard phrased *"is it still attached to / embedded in me?"* silently stops
  firing once you move the detach earlier — grep every reader of that state before you change its
  timing (`GrowCrystalWithPop` was exactly such a guard). `Docs/ECOSYSTEM.md §26.4`.
- **An ORDERED wither needs the spindles isolated first.** `Spindle.ForceWither` recurses into
  child spindles AND destroying a spindle GameObject destroys its children, so any death that
  spends spindles one at a time collapses the creature in one step — *except* outside-in, which
  works by accident because it destroys leaves first. `Spindle.IsolateForOrderedWither` breaks both
  couplings (and suspends `CheckForLife`, so handing a spindle's prisms away doesn't evaporate it
  out of turn). Detach body prisms BEFORE withering spindles — a body prism is parented to a
  spindle, so the wither would destroy the mass you meant to conserve. `Docs/ECOSYSTEM.md §26.3`.
- **A per-lifeform SCALE curve must exempt any species whose geometry is a LATTICE.**
  The MECHANISM that made this concrete is gone — `Flora.LevelUp` grew `leafSize` and
  `Flora.AddHealthBlock` stamped it onto every prism the plant laid, and lifeform LEVELS are now
  retired outright (`Docs/ECOSYSTEM.md` §40), so a leaf is its authored size for the whole of a
  plant's life. **The rule is not gone.** `AssembledFlora`'s families (gyroid, SchwarzP,
  quasicrystal, wall) bond at offsets measured in ABSOLUTE local units
  (`OctagonNeighbor.Center`/`SeedPosition`, `GyroidAssembler.SeparationDistance`, captured once
  in `GyroidAssembler.Start`), so a leaf that grows mid-life lays prisms the bond table no longer
  describes. Making the offsets scale-aware does NOT fix it: the plant's earlier prisms are still
  the old size, and **two prism sizes cannot tile one lattice**.
  `Flora.PrismSizeFixedByGrowthRule` (true on `AssembledFlora`) is deliberately KEPT with its
  reader gone, precisely as the standing guard for the next growth path that tries this — gate on
  it. The trap is that the worst-affected species is invisible from the feature you are writing:
  growing a plant's leaf on reproduction hits the gyroid octagon colony HARDEST, because it is
  the family that reproduces most (one birth per fauna-wave period), so it would have inflated
  fastest against a CI-verified geometry table. **Before adding any per-individual scale curve,
  ask which species' geometry is authored in absolute units.**
- **A prism that stops being body tissue must be RE-FILED, not just reparented.**
  `PrismSpatialIndex.ComputeEnvironmentMass` reads `HealthPrism.OwnerFauna` to keep a live swarm
  out of the targeting grids. Clear the owner without calling `NotifyOwnershipChanged` and the
  prism stays classified as volume-only body mass forever: it feeds `LiveVolume` but the food web
  can neither see nor eat it, which looks exactly like "fauna are ignoring it" with no error
  anywhere.
- **An AUTHORED prism size is SILENTLY CLAMPED, and the clamp is invisible to every offline
  check.** `PrismScaleAnimator.SetTargetScale` clamps per axis into `[minScale, maxScale]` —
  serialized defaults `(0.5,0.5,0.5)`/`(10,10,10)`, inherited unchanged by **363 of 404 prefabs**
  — inside the setter, with no log and no return value. A config saying `60 x 1 x 1` therefore
  produces a `10 x 1 x 1` prism and *nothing reports the difference*. Three passes of flora
  fitting measured, argued about and shipped prism sizes the engine never used that way; every
  cross-section under 0.5 was clamped UP at the same time. Anything that STATES a size calls
  `Prism.AdmitTargetScale(size)` first; anything that GROWS into the bound via `Grow()` leaves it
  alone — and because the widening is permanent on a POOLED instance, `ResetState` restores it.
  **When a fitted size does not read on screen, check what the engine actually STORED before
  re-fitting** — one look at the live Transform beats another round of measurement.
- **A scale applied to a node that PARENTS its own successors compounds.** Flora spindles nest —
  each new spindle is instantiated as a child of its parent branch's spindle — and prisms parent
  to the spindle ROOT. Scaling that root therefore multiplied down the chain as `scale^depth`
  (1024x at ten generations) *and* multiplied every prism's authored `leafSize` on top. Scale the
  node's CHILDREN instead, which are leaves of the chain. Before scaling anything in a hierarchy,
  ask what else inherits that transform; no compile or static check sees this, only geometry, and
  only some distance from the seed.
- **A coherence tolerance written as an ABSOLUTE distance is an unstated dependency on the
  lattice it was measured against.** A gyroid plant's coherence rides `snapDistance` (compared
  against SQUARED distances), a 40u mate-search radius, a reservation floor, and
  `AssembledFlora.MisalignmentRadius` — all sized at `separationDistance 3`. Scaling the lattice
  moved every real distance out from under them, the twin-detection gate stopped catching twins,
  and the plant grew the offset parallel domains that gate exists to prevent. Every constant was
  individually correct; the defect was a RELATIONSHIP. Enumerate every test that decides
  *sameness* — snap, dedupe, reserve, twin-detect — and assert the ORDERING between them
  (`reserve < gate < healthy pair`) rather than the values.
- **A visual state applied before `Prism.IsCreationComplete` is part of BIRTH and must snap.**
  Engaging a morph there holds the exotic-visual window across the creation reveal and eats the
  one-shot grow stamp, so the prism snaps in instead of blooming (`Docs/PRISM_ANIMATION.md` §4).
- **A prism's SHIELD is 3x the prism, so a species fitted for its box is not fitted for its
  armour.** `PrismStateManager.ActivateShield` swaps in the CIRCUMSCRIBING octahedron
  (`OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = 3` on the box HALF-extents — reach `1.5 x
  leafSize`, 4.5x the volume). Both lattice species were fitted TIGHT against their own
  neighbours *unshielded* and cleared by 5–99%; tripling that reach made each Charge plant one
  interpenetrating solid (`Docs/ECOSYSTEM.md` §35). **Any change that turns shielding ON for a
  species is a change to its GEOMETRY** — measure the octahedra, not the boxes, and fit the
  PRISM rather than the lattice (scaling the lattice drags the whole absolute-distance tolerance
  family with it, §34.8; scaling the prism drags nothing). Tool:
  `Tools/Build/fit_shield_clearance.py`.
- **An elemental LAW cannot live in per-element config when the element is ROLLED.** A cadence,
  a size, an immunity authored per CONFIG is applied by `ApplyVariantTuning`, which runs BEFORE
  the element is known and is then overwritten by the cell's own overrides — and a config with
  `SpreadElements` and an EMPTY `ElementPalette` rolls an element and applies its OWN block to
  it, so no value writable on any per-element asset reaches it (both Hesperides topiaries).
  Put the law at the one choke point where prefab + variant + cell overrides + the crystal
  carrying the element have ALL landed — `LifeForm.Initialize` — as a hook a subclass overrides
  (`Flora.ResolveShieldPeriod`), and keep the assets stating the same number with a `--check`.
  Scope it to the class that actually means it: on `Flora`, not `LifeForm`, or every creature
  inherits a rule written about plants.
- **Two fitters must never own one asset field.** `fit_schwarz_p_leaf_sizes.py` sizes that
  species' plates flush and `fit_shield_clearance.py` sizes its Charge plate for the octahedra —
  whichever ran LAST used to win, which is a run-order hazard rather than a bug, so it only
  surfaces months later when somebody re-runs the older tool. Make the losing tool READ the
  value back and print what it would have chosen. Related: a fitter must be an idempotent FIXED
  POINT (`s* = 1/(1-clearance)`), never a fresh multiply of whatever is authored, or every run
  walks the number.
- **A fitted axis below `minScale` is silently clamped, so CHECK the admit call, don't assume
  it.** `HealthBlock.prefab` ships `minScale (0.5,0.5,0.5)` and `PrismScaleAnimator.SetTargetScale`
  clamps inside the setter with no log — Schwarz P Charge's fitted 0.39 thickness survives only
  because `Flora.AddHealthBlock` calls `Prism.AdmitTargetScale` first (§34.9). Any tool that
  emits a size outside the prefab's window must fail if that admit call is ever refactored away.
- **N founders do NOT build one superstructure N times faster.** A lattice colony is one
  continuous surface grown outward from a single founder, and every extra founder is an
  independent lattice FRAME — `AssembledFlora` declines any growth or seed site within
  `MisalignmentRadius` of a foreign frame (the gate that stops visible twins, §34.8). So
  seeding 30 per species does not converge 30x sooner; it builds 30 small structures that stop
  against each other. Identical prism count, reads as a scattered forest, and **nothing in the
  population numbers shows it** — the caps, the floors and the totals are all exactly what you
  authored. Seed ONE per colony and let reproduction extend it; the seeder's remaining job is
  extinction recovery. Verify by what the cell LOOKS like, never by the count.
- **A rule written about ROLLED elements must not be inherited by a FIXED-element config.**
  `author_flora_populations.py` floors lattice species at 4 founders (`LATTICE_MIN_FOUNDERS`)
  because a colony inherits its founder's element PICK, so one seed wastes a config's authored
  element spread. A config that authors ONE fixed element has no spread to protect and the floor
  is actively wrong there. Before inheriting any population/variant rule, check whether it was
  written about the rolled case.
- **A property named for how something is BUILT will eventually be read as a claim about what it
  CONTAINS.** `Cell.EnvironmentFreeConfig` ("first config with no `EnvironmentPrefab`") served
  the cheap-boot chooser AND the Wanderway's bare canvas, and one test satisfied both only
  because Blob happened to be cheap *and* empty. The first config that is cheap to build and
  then GROWS a forest broke the second consumer silently. One config satisfying two questions is
  not evidence they are one question — it is the reason nobody notices until the second config
  arrives. Split it as a PREDICATE over authored data (`Cell.BareCanvasConfig`), never a new
  serialized field, and give it a fallback so it degrades rather than returns null.
- **When two scripts could own one asset, HAND IT OFF BY NAME — do not exclude it silently.**
  The "two fitters must not own one asset field" trap above has a remedy: a table keyed by
  asset-name prefix mapping to the OWNING script, which the non-owner **prints** in its report
  (`author_flora_populations.py`'s `OWNED_ELSEWHERE`). An `EXCLUDE` set is invisible in the
  output, so the next reader cannot tell "deliberately owned elsewhere" from "forgotten".

- **NOTHING may be parented under a prism.** A prism carries its authored leaf as its
  `localScale`, and a non-uniform scale above a ROTATED child is a **shear** — the child is no
  longer a cuboid, and every generation compounds it. `AssembledFlora.ReseedBranches` hung the
  next spindle off the *prism* instead of the prism's *spindle*, so all three lattice species
  grew skewed slivers from a plant's first reseed onward, worst on the most extreme aspect
  ratio (`Docs/ECOSYSTEM.md` §37.9). When you need "the thing this prism belongs to", resolve
  the parent spindle; never take the prism's own GameObject as an attachment point.

- **A ladder claim must be re-read against every cell that LOADS the profile, not the one the
  profile is NAMED after.** A species' "N% of Frenzy" figure is written against one cell and
  then survives that cell being retired: `Blob Cell Config` was deleted while `Blob Cell Spawn
  Profile` lived on as the population of seven other worlds, whose Frenzy volumes span 5×
  (Orrery 253k → Caldera 1.27M). Grep for the profile's GUID, hold the species against the
  **tightest** consumer, and say which one you used.

- **A fitter must not re-derive what a human authored.** Once a designer tunes a value by eye,
  a script that binary-searches "the right" value silently overwrites their intent on the next
  run, in a direction nobody is looking. Convert the fitter into a **verifier**: prove the
  authored value (no overlaps, clear crystal seat, shield census) and *report the headroom*
  alongside, so the next retune can see its room. If an authored value grazes, state the
  tolerance explicitly and print the measured depth every run — an accepted graze must be a
  stated number, never something a later reader discovers (`Docs/ECOSYSTEM.md` §37.10).

- **One CAP across species of different plant sizes is a choice, and it is usually the right
  one.** A colony cap is expressed in **plants** — territory units of that species' own
  lattice — so equal caps give equal *territory*, not equal prism counts. The Lattice cell's
  twelve colonies span 2.5× in struts per plant and 159× in volume per prism. Equalising prism
  counts instead shrinks the biggest species' superstructure below its neighbours', which
  destroys the comparison a multi-species cell exists to make. What you DO owe is a direct
  assert that **no single colony's own volume ceiling reaches `FrenzyEnterVolume`** — otherwise
  the heaviest species can freeze the cell while the others are still building, and the ladder
  is describing one colony rather than the forest (`Docs/ECOSYSTEM.md` §37.11).

- **A value written in WORLD scale is only correct while the parent chain it was divided
  against is FINAL — so re-apply it after anything that rewrites that chain, and never
  conditionally.** `LifeFormCrystal` writes a heart in world units by dividing out its parent's
  `lossyScale`, and `Fauna.AssignLineage` runs `ProvisionHeart` (sizes the heart) BEFORE
  `ApplyVariantTuning` (rewrites the root scale from `BaseBodyScale`). Until Aug 2026 the
  corrective re-size was done by `Fauna.SetLevel`, as an incidental side-effect of seeding the
  spawn level — so retiring levels silently gave every creature with an authored body scale a
  heart of `authored × BaseBodyScale` (0.4 and 0.7 on the shipped tadpoles: a 2.5× and 1.43× cut
  to BOTH the collect reward and the live domain fauna buff, with nothing reporting it). There is
  a SECOND inversion one level up — the Boid/LightFauna path runs `Initialize` (heart sized)
  before `SpawnFaunaBanded` calls `AssignLineage` (body scaled) — which is why the fix belongs at
  the END of `AssignLineage` rather than inside `ApplyVariantTuning`. **A CONDITIONAL re-apply is
  wrong**: the case that breaks is the variant that authors NO size of its own, which is exactly
  the case a `if (authored > 0)` guard skips.
- **When you delete a mechanism, ask what it was incidentally DOING for someone else.** The rule
  above generalises past hearts: a call whose stated purpose is X ("seed the spawn level") can be
  the only caller of Y ("re-establish the heart's world scale"), and deleting X takes Y with it
  silently. Before removing a call, grep what it invokes and ask of each callee whether anything
  else invokes it on that path. This is the mirror of §2's "find the PRODUCER" rule — here you are
  looking for the CONSUMER you are about to strand.
- **A heart authored into a DISABLED variant block is never read.** `CellLifeSpawnerBase` and
  `Fauna.AssignLineage` apply a variant tuning block only when `Enabled` is true, so a per-element
  value written into a block with `Enabled: 0` is invisible from every direction: the asset shows
  a perfectly good number, the number is never wrong, and the lifeform silently renders the
  platform default. Five flora species and every un-migrated fauna shipped with `Enabled: 0` on
  three of their four elements, so this is the common case rather than the edge. Any tool that
  authors into a `Variant` block must flip `Enabled` with it — and a zero-initialised block is
  safe to enable, because every other field's initializer is a keep-the-prefab sentinel.
- **A SIZE that gameplay reads is a REWARD, and the band needs a ceiling with a margin.** A
  lifeform heart's world scale is read in five places (collect reward, live domain fauna buff,
  pickup trigger radius, vacuum speed, capture flourish); the reward is
  `min(scale × levelPerUnitScale, maxLevelGainPerCrystal)`, so it SATURATES. Past that point two
  visibly different hearts pay the same — a size the player can see and a reward they cannot.
  When a band is authored rather than uniform, solve its scale constant so the LARGEST member
  lands on the ceiling: that makes clipping structurally impossible instead of merely unlikely,
  and it uses the whole band. Do NOT answer an overshoot by retuning `levelPerUnitScale` — it is
  shared with every non-lifeform elemental crystal (the Wanderway conveyor, Dog Fight's arena
  scatter). Compress the mapping instead.

## 3. Implement (emergence first, surgically)
- **Favor emergence:** never hard-code an outcome that should emerge from the fundamentals
  (Domain · Mass/prisms · Cells · Elementals · Flora & Fauna · Vessels) interacting. A scripted
  outcome is the same bug as a scripted fitness function — it breaks the gameplay *and* the
  artificial-life claim. Order of preference: use a fundamental → tune it → extend it →
  (with sign-off) propose a new one → bespoke only as last resort.
- **Config-driven:** tunables in ScriptableObjects; cross-system comms via SOAP events/variables;
  no singletons/static events. Variety = biome × intensity × heritable traits, not bespoke code.
- **Surgical:** match surrounding style; three similar lines beat a premature abstraction.

## 4. Respect the collider budget (HARD GATE — perf is collider-bound)
- State the change's impact on **active colliders per cell**.
- Prefer the Burst `BlockDensityGrid` / `PrismSpatialIndex` (`QuerySphere` /
  `IsAnyPrismWithin` / `TryReserve` — see `Docs/SPATIAL_INDEX.md`) for spatial queries over
  `Physics.OverlapSphere` and over adding colliders. Fauna senses already ride the index.
- Honor collider-LOD-by-phase (prism colliders disabled at Frozen) and the per-cell budget.
- If a change adds colliders or queries, say explicitly how the budget stays met.

## 4.5 Cell-environment baselines: measure them, don't guess them

`CellConfigDataSO.PhaseThresholds` must ride the environment's MEASURED baseline
(count + volume) or the cell boots into the wrong phase. `FrogletTools > Ecology >
Measure Cell Environment Baselines` is the in-engine ground truth — but you do
NOT have to block on the human for it. Cell environments are deterministic by
contract (pure function of the serialized seed), so port the generator and
measure offline; the in-editor measurer then CONFIRMS rather than supplies.
Method + the validate-against-a-shipped-baseline rule that makes it trustworthy:
`/asset-surgery` §4.5. Author thresholds as baseline + the Blob deltas
(+700/+500/+3600/+3000 count, +11200/+8000/+57600/+48000 volume).

While you have the emitted points in hand, assert the spatial invariants too —
in particular that **nothing is laid inside the nucleus control radius** (~392u;
see `Docs/ECOSYSTEM.md` §13 + §18.1). An authored environment sitting in the
nucleus hands `DominantDomain` to whatever colour it favours before anyone flies.
That defect shipped undetected in Caldera (89% of its mass) until a one-line
check over the point cloud found it.

### 4.6 "It stopped growing" — prove WHICH gate binds before you turn a dial

A population that stops has at least three ceilings — the species cap
(`MaxLivePopulation`), the cell's **Frenzy volume ladder** (which freezes planting AND
growth), and the count backstop — and the symptom is identical for all three. Compute
which one binds before touching anything, or you will ship dead tuning: a change to a
dial the run never reaches, which reads in-game as "my fix did nothing".

The arithmetic is cheap and offline. Per-prism volume is `LeafSize.x*y*z`, flat, for the whole
of a plant's life. It used to be that TIMES a level spread (`LeafScalePerLevel` applied per
axis, so `scale³` per level: 1.15 → ×1.52 each level, ×2.74 averaged over levels 1-5) and that
factor was the one everybody forgot — lifeform LEVELS are retired (`Docs/ECOSYSTEM.md` §40), so
**it is now exactly 1**. The trap inverted rather than disappeared: **any volume number measured
before §40 is inflated by its cell's old spread**, so divide it out before reusing it. Multiply
the flat per-prism volume by the settled prisms per plant, then by the seed floor, and compare
to `FrenzyEnterVolume` *before* the first birth.

This has bitten twice, and both times the prisms were far from the nominal 16. **Rampage's
cactus leaves** (5×5×3 = 75, 4.7× nominal, §27): the count-derived `×16` ladder came out an
order of magnitude too low, which would have pinned the cell at Frenzy with planting frozen. The
4.31× spread that measurement carried is now 1, so the arena boots that much lighter —
deliberately left as play-tested, since Frenzy arriving LATER is the safe direction
(`Tools/Build/rampage_intensity.py` prints the re-measure note). **The Blob cell's Mass gyroid**
(7×4.5×3.5 = 110, 6.9× nominal, `Docs/ECOSYSTEM.md` §32.7): measured under the ×2.74 spread its
seeded floor alone was ~50,200 volume, 87% of the then-authored `FrenzyEnterVolume 57,600`,
while its population cap sat ~19× further out; on the flat leaf that same floor is ~18,300, and
the ladder has since been authored ×5 (`FrenzyEnterVolume 288,000`) — so neither figure is the
live one, and both are here as worked arithmetic rather than as current tuning. The LESSON is
what is live: **a cell whose prisms are not nominal-sized must author its ladder against
measured volume**, and when a colony stalls, reach for the ladder first and the population dial
last.

## 5. Hand back verification — you cannot run Unity; the human is the gate
- State the exact in-editor steps to verify, the scene to test, and the precise SO knobs to tune.
- Use the collider/volume telemetry overlay when it exists to make the budget observable.
- Never claim something works that you have not seen work. Report honestly (failures, skips, caveats).

## 6. Commit
One coherent step per commit; conventional-commit message; develop on the feature branch (never
`bleeding-edge`); open a PR only when asked. After lifeform-prefab changes, note to run
`FrogletTools ▸ Validation ▸ Validate Lifeform Crystals`.

## 7. Budgeting a new cell WITHOUT Unity (do this before you author thresholds)

You cannot run the measurer, but a generator's cost is analytic — transliterate its
loop structure to Python and compute the exact prism COUNT and the expected VOLUME:

- Counts are pure loop arithmetic (mind index-dependent skips like crenellation and
  `Scaled(n)` under the `density` knob).
- Volume is `Σ count × (x·y·z)` per structure. `Jit(s, a)` multiplies **all three axes
  by one** factor `k ~ U(1-a, 1+a)`, so `E[k³] = ((1+a)⁴ − (1−a)⁴) / (4a)` — ≈ **1.04**
  at the default `a = 0.2`. Noise-driven POSITION jitter never changes volume.
- Print the per-structure table. It shows you immediately which family is eating the
  budget (a thick "ground" slab band is the usual culprit) and is what the human
  checks the measurer's output against.

**Then author PhaseThresholds for what will GROW, not just for the baseline.** §18's
rule (measured baseline + the Blob deltas) assumes the mass above baseline is vessel
TRAIL. For a cell whose mass comes from **flora**, `FrenzyEnter` *is* the planting
budget — planting and growth stop there — so it must be set at
`baseline + the mature planting you actually want`, or the garden freezes while it
still looks bare. Size the planting as `Σ species (plants × maxTotalSpawnedObjects)`
and put Restless somewhere the fauna start hunting a partly-grown cell.

Always hand the numbers back as ESTIMATES with the measurer step attached — analytic
counts are exact, but only the editor proves the generator runs at all.

### 7.1 A ladder of intensities: put the MODEL in a script, and self-test it

A cell with per-intensity configs needs one `PhaseThresholds` block per intensity, each riding
its own forest volume. Authoring four by hand is how four ladders drift apart. Write the model
as a Python script under `Tools/Build/` that:

- holds the species table (plants, budget, leaf prism volume) and the
  per-intensity scalars, and computes prisms + volume from them;
- derives all eight thresholds from ONE set of ratios (Frenzy just above full growth, exit
  ~77%, Restless ~7%), so every intensity is the same shape;
- **emits the assets** and supports `--check` so CI can catch a hand-edit;
- **self-tests by reproducing an already-shipped, play-tested ladder to the digit.** That
  assert is the whole difference between a model and a fresh guess: if the formula cannot
  reproduce the arena a human already approved, it is wrong, and you find out at authoring
  time instead of in a play test.

Note what is NO LONGER in that model. The level spread's expected VOLUME multiplier
(`Σ s^(3(L-1))·f^-(L-1) / Σ f^-(L-1)` for scale-per-level `s` and rarity falloff `f`) used to be
the biggest single factor in it — 4.3x at `s=1.30, f=1.6` — and lifeform LEVELS are retired
(`Docs/ECOSYSTEM.md` §40), so the term is exactly 1 and is gone. Do not re-derive it, and treat
any ladder authored against it as describing a forest heavier than the one that now grows
(`Tools/Build/rampage_intensity.py` carries that note in-script).

Keep one honest soft spot visible: phyllotactic flora size prisms BY ROLE, so there is no
single authored field to read for their volume. Put those estimates in a named `CALIBRATION`
dict so one in-editor measurement corrects every intensity at once.
