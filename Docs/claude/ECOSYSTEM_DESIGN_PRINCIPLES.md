# Ecosystem Design Principles

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

## Ecosystem Design Principles (LOCKED — read before any ecology change)

The cell ecosystem (flora/fauna/cells/crystals) is a **platform fundamental** on the path to
credible **artificial life**. North star + roadmap: `Docs/ECOSYSTEM_MASTERPLAN.md`. Mechanics
log: `Docs/ECOSYSTEM.md`. These invariants are **locked** — do not relitigate or re-derive them.
They are a direct application of "Favor Emergent Systems / Don't cheat emergence" (below) and —
not by accident — they are also what makes the system credible as artificial life (a scripted
outcome is optimization, not life). Use the `/ecology` skill for any change here, and the **`/flora` skill on top of it for ANY new
plant species** (which growth family, who owns which authored field, the measure/author/verify tool
trio, and the traps).

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
  A mode may redefine what "controls" a cell — Brood Rush makes it the nucleus claim (Cleave
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
- **THE NAMED EXCEPTION: a MultiDomain SWARM grows regional LINEAGES** (`SwarmFaunaConfigSO.MultiDomain`, ON for
  the Swarm cell's sort swarms and OFF by default everywhere else; `Docs/SWARM_FAUNA.md §17`). Birth obeys the law:
  every seed wears the cell's controlling domain and every child its PARENT's - **food never colours anyone** (round
  8's diet colouring was tried and removed on playtest). The body plan's regions (a whale's back and belly) are
  anatomy; each is OWNED by the lineage holding most of its tissue, and a child laid into tissue nobody owns may
  found a new lineage (`LineageDrift`) in a domain the swarm lacks, drawn uniformly. So a swarm can grow a second
  colour in one region, but nothing chooses which colour, which region, or whether it happens. Each member is drawn
  (`Flags` bits 7-8 -> the per-slot palette), fed (`Fauna.IsPreyForMe` with its own domain), spared and credited in
  ITS domain, and its proxy takes it. `Cell.SetModeControlOverride`'s one-colour re-colour **does not apply** to a
  MultiDomain swarm (`Fauna.AcceptsTeamRecolour` is false); a one-colour swarm still re-colours in full, proxies
  included (`SwarmFauna.OnTeamChanged`). Only the SORT model has lineages (a MultiDomain grid/field/evofate swarm is
  one colour). Do not extend this to any other species: a single creature has no population to carry a lineage, so
  for it the one-colour law stands unmodified.
- **A creature dies when its last body prism is destroyed** — `Fauna.OnBodyPrismExploded`
  (platform-wide since Wildlife Liberation; before it, only the worm colony implemented it, so
  shooting any other creature stripped its body and left an immortal husk swimming). This is an
  ACTIVE force removing mass and therefore squarely inside the conserved-mass law: no timer, no
  lifespan, no cull, and a creature nobody shoots still only ever dies to starvation or
  predation. It routes through the same sealed `Fauna.Die`, so the crystal drop and the wither
  are not bypassable. A subclass may override to add bookkeeping first (the worm colony re-links
  its chain), but an override that lets a creature survive losing its whole body breaks the rule.
- **Starvation = wither-to-crystal, and a joust takes the heart FIRST.** A starving creature
  withers from its extremity spindles inward — a shark's fins / a brittlestar's arms evaporate
  *before* the core body (deepest in the spindle tree first, ties farthest-from-the-heart first) —
  and the heart is the LAST thing standing, so its crystal becomes collectable by any vessel only
  when the wither reaches the core. A **jousted** lifeform (the Squirrel's Crystal Joust, flora and
  fauna alike) never detonates, the heart is freed at the strike and **auto-collected by the
  jouster** (`ElementalCrystalImpactor.CollectBy`), and the spindles still wither *outside-in*, back
  toward where the heart was. **Every standing spindle and prism of a lifeform always has a path of
  standing spindles to its crystal** — in growth, under grazing, and through every death; a wither
  never spends a limb before the limbs that hang off it (`Spindle.OrderOutsideIn`,
  `Docs/ECOSYSTEM.md` §26.10, which retired the original heart-outward joust order). Both leave the
  body prisms standing as a **skeleton** — ordinary cell mass the
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
- **LIVING MASS IS THE MASS THAT MOVES — a health prism rides its limb's sway.** §44 bent
  every spindle and stopped there, so the conserved mass BOLTED to the spindle stayed rigid
  and a Clawfish's fluke bent away from the four ribs lying on it. `PrismSway.hlsl` is the
  other half, and it works because of one property of the shear: **the offset is a function of
  z ALONE**, so every point at the same height on the limb moves identically whatever its x and
  y — a prism therefore needs only its own height and the limb's axes, and evaluating the SAME
  field at its own vertices makes the two move together **exactly**. `verify_prism_sway.py` T3
  asserts that as **bit-identical** to `SpindleSway`, which is why the limb height is folded
  into the span BEFORE the sine (`(A·h)·sin` and `A·(h·sin)` are the same real number and
  different float32s — *when two shaders must agree, the order of operations is part of the
  contract*), and why the file `#include`s `SpindleSway.hlsl` instead of copying its two wave
  constants (a copy drifts over exactly the timescale the ratio was chosen to make
  non-repeating, i.e. invisibly at first). Four Hybrid-Per-Instance properties, all **constants
  of the attachment** — so no start time, no duration, and no per-frame CPU at any population.
  **`_SwayAxis` is a ROW of the change of basis, never a normalized direction** (a prism carries
  a non-uniform `leafSize` as its `localScale`, under which a normalized axis is a different,
  wrong number); **the limb frame is the RENDERER's transform, not the Spindle root's**
  (`Spindle.SwayFrame` — `PositionOS` is the rendered mesh's space, and the Clawfish's body is
  a nested FBX carried at an offset); and **the phase is the bucket the limb's own material was
  minted from**, through one shared `Spindle.PhaseBucket`, resolved in `Start` and CACHED
  because a spindle is routinely `Instantiate`d and only THEN posed. The stamp site is the new
  `Prism.OnCreationComplete` — never `Initialize`, which runs before the companion entity
  exists and before `AssembledFlora` re-parents the prism onto its spindle. **A ZERO span is an
  exact no-op and is the DEFAULT, which is the feature rather than a safe default**: a vessel's
  trail, an authored environment and the SKELETON a dead lifeform leaves behind
  (`HealthPrism.LeaveAsSkeleton` clears the stamp) all stay still, so a player can tell a plant
  that is alive from the husk of one that is not without being told. Costs
  zero colliders and zero frame time; the one honest cost is 48 bytes of instance data on every
  prism, living or not. **A DIMENSIONLESS SLOPE IS NOT DIMENSIONLESS UNDER A NON-UNIFORM
  SCALE** — the shear is authored in OBJECT space, so a renderer at `localScale (1, 1, sz)`
  deflects its tip by `atan(Amplitude · sx / sz)` in WORLD terms and a mesh stretched along its
  own bend axis bends that much LESS. `SpindleSway.hlsl`'s header claimed the opposite, and the
  line it offered as reassurance (*"every shipped spindle prefab is scaled on z to match (Branch
  6.2, TadpoleSpindle 3.0)"*) names the two prefabs that DISAGREE: at the shared 0.08 the
  uniformly-scaled tadpole, worms and QuadFish lean **4.57°** while every branch-family spindle
  leans **0.64–1.48°**, which is why the lattice species read as dead. The fix is per-mesh
  amplitudes, never a per-mesh shader: three materials (`GyroidSpindleMaterial`,
  `AssemblySpindleMaterial`, `QuasicrystalSpindleMaterial`), each a verbatim clone of
  `SpindleMaterial` with its amplitude SOLVED from that prefab's own stretch for one authored
  **3°** lean — §46's "a shared material is a claim that everything wearing it moves alike",
  applied one level down. **Equal ANGLE is equal FRACTION OF THE LIMB**, so one number serves a
  family whose bonds span 3 to 24 world units and stays true under
  `FloraVariantTuning.LatticeScale` (which scales branch and bond together). The bound that set
  3° is the JOINT: neighbouring prisms draw independent phases, so the most they move APART is
  ~6.4% of their bond, inside the lattice's own 10% mate-snap tolerance — a breathing lattice
  can never read as a broken one. A prism seated AT its limb's root would not translate at all
  (the shear is zero at z = 0), but **that is not the lattice case**: `SwayFrame` is the
  RENDERER's transform and a lattice branch mesh is posed under the spindle root, so an
  `AssembledFlora` prism at `localPosition = Vector3.zero` sits at `Z0 = ±0.55` of its limb.
  The ordinary flora `Branch` keeps 0.08 and its 0.74°, stated rather than silently changed.
  Authored by `Tools/Build/author_lattice_spindle_materials.py` (`--check`).
  `Docs/ECOSYSTEM.md §47`, `§47.7`.
- **A SPINDLE IS A LIMB, NOT A ROD — and until Sep 2026 no spindle in the game deformed,
  including the one named `AnimatedSpindleGraph`.** That graph's `Add` into
  `VertexDescription.Position` has a hardcoded `(0,0,0)` A input (`Position + 0`), it is
  animated in COLOUR only, and it is worn by zero materials; `SpindleGraph`, which every
  shipped spindle actually uses, had no edge into vertex position at all. So `Spindle.cs`'s
  phase-variant apparatus — 8 shared materials per base material, bucketed by world position,
  deliberately NOT an MPB so renderers stay SRP-batchable — was desyncing an animation that
  did not exist. Everything that visibly moves gets it elsewhere: the shark and brittlestar
  from an FBX ARMATURE + Animation Rigging (`DampedTransform` chains are the dangling arms,
  `MultiParentConstraint` binds prism clusters to bones), the boids from flocking. A creature
  with no rig — the QuadFish, every flora branch, the worm segments — moved not at all.
  `SpindleSway.hlsl` is the fix and it is GPU-only (zero per-frame CPU, off `_PrismClock` +
  the `_Phase` already stamped). **The bend is a SHEAR, which is what makes it unit-free:**
  `offset.x = Amplitude * PositionOS.z * sin(...)` is first-order bending, so it is exactly
  zero at the root (a spindle can never tear off its parent), grows toward the tip, and
  `Amplitude` is a dimensionless SLOPE — one number meaning the same bend on meshes that
  disagree about scale by three orders of magnitude (gyroid branch ~1 unit, QuadFish body
  349). `_SwayAmplitude` **defaults to 0** and multiplies both sine terms, so the splice is a
  PROVABLE no-op (`verify_spindle_sway.py` T2: bit-identical, negative-controlled) and the
  blast radius is an authored list rather than a side effect — the two non-spindle materials
  on that graph (`BranchingMembraneMaterial`, `FireProjectileMaterial`) are untouched.
  **A shared material is a claim that everything wearing it moves alike**: amplitude
  transfers across meshes but FREQUENCY does not, so a fern (0.08 / 1.4 rad/s) and a fish
  (0.13 / 5.2) get different materials rather than a compromise that makes the plant buzz.
  The fins are a separate problem a vertex shader cannot touch — a fin is a `HealthPrism`,
  conserved mass with its own collider — so `QuadFishSwimDriver` (the sibling of
  `SharkJawDriver`) strokes them in diagonal pairs and banks the body. It costs ZERO colliders
  and zero index work: the flap writes `localRotation` and never `localPosition`, so the
  prism's POSITION never changes and `PrismSpatialIndex` sees nothing. **A legacy `Animation`
  component with a NON-legacy clip is a feature that has never run and looks exactly like one
  that works** — both fish carried one (`m_PlayAutomatically: 1` on `QuadFishSwim.anim`,
  `m_Legacy: 0`), excised. `Docs/ECOSYSTEM.md §44`.
- **EVERY SPINDLE IS `SpindleGraph`, fauna and flora alike — a creature-only FORK was built
  and WALKED BACK.** The fork (`FaunaSpindleGraph`) gave creatures the three things
  `CreatureTextureGraph` was doing that say *alive* rather than *limb* — an additive fresnel
  **rim**, a slow brightness **breath**, and a **flow** walking the Voronoi pattern across the
  body — all defaulting to provable no-ops and proven bit-identical to the parent by compiling
  the shipped HLSL. It was removed on a **look call**: the old shader and its settings read
  better. Three findings survive it and are the reason it is written down. (1) That donor graph
  is **80% dead nodes** (an unconnected colour pair, an unconnected gradient, an unconnected
  wave subgraph) and was still carrying the one idea worth keeping — **read a bespoke asset's
  EDGES before deciding it is empty**. (2) A fleet-wide spindle colour belongs in an
  **unexposed global**, never a painted base material: `Spindle` mints EIGHT phase-variant
  materials per base material at runtime, which COPIES the colour at mint time, so painting
  the base is correct only while `ThemeManager.Awake` beats the first spindle — an unenforced
  ordering with a silent failure. (3) **A shared material is a claim that everything wearing it
  moves alike**: amplitude transfers across meshes and FREQUENCY does not, which is why the
  QuadFish keeps its own material (0.13 / 5.2) while every other creature takes the shared
  `SpindleMaterial` (0.08 / 1.4). The **Clawfish** is the species that prompted all of it and
  its revival stands: it had no prisms (so it could not be shot), no Spindle, and a heart
  floating in front of its own open mouth; its body is a nested FBX whose renderer fileID
  cannot be authored from outside, which is why `Spindle.CacheRenderers` now RESOLVES an
  unauthored `RenderedObject` from its own children, skipping anything under a `Prism` or a
  `Crystal` — *when a serialized reference cannot be authored headlessly, ask whether it needs
  to be authored at all*. `Docs/ECOSYSTEM.md §46`.
- **Volume is the spine.** Phase, dominant domain, prey, HUD all key off per-domain **VOLUME**
  (`Cell.LiveVolume`), not prism count. Count is a rare frenzy/perf backstop only.
  **Node control is the NUCLEUS**: in a cell with a nucleus, `DominantDomain` reads only the
  per-domain ENVIRONMENT volume laid **inside the nucleus** (the territorial claim — a fauna
  sanctuary players contest with abilities + out-laying volume); everything **outside** is the
  voraciously-grazed feeding ground and never sways control. The fauna spawner ticks a fixed
  **30s** wave clock (`BaseFaunaSpawnTime`), spawning each wave in the controlling color and
  raising `CellRuntimeDataSO.OnFaunaWaveSpawned` — the heartbeat Brood Rush scores on. See
  `Docs/ECOSYSTEM.md §13` + `_Scripts/Controller/Arcade/BROODRUSH.md`.
  **A nucleus a mode borrowed as PLAY GEOMETRY is a wall, not a claim** — set
  `Cell.NucleusIsControlZone = false` (default true; collapses the control zone so the cell keeps
  its whole-cell control + diet semantics, exactly as if no `NucleusPrefab` were authored). This is
  not an exception to the rule, it is a declaration that the cell HAS no control zone — a state the
  ecology already supports. It is **load-bearing**: Astro League morphs the nucleus into its whole
  ricochet court, so the control radius became the court's circumscribing radius, every prism in the
  match read as "inside the nucleus", and the sanctuary rule made the entire pitch inedible — the
  trail-grazing food web could not remove one prism and no amount of threshold/food-floor tuning
  could reach it, because the diet predicate returned false first. **Whenever a mode repurposes a
  Cell-owned visual, check what SEMANTICS it borrowed with the geometry** — in BOTH directions:
  `Flora.ResolvePlantRadius` (and `ClampToPlantingBand`) also clamped their band outside the
  nucleus unconditionally, so a court-as-nucleus mode could not seed a plant inside its own arena;
  both stated reasons for that clamp ARE the control zone, so it now reads this flag too
  (`Docs/ECOSYSTEM.md §42`). (`Docs/ECOSYSTEM.md §25.1`.)
- **Every lifeform drops one elemental crystal** (Charge/Mass/Space/Time) as a powerup on death,
  enforced by `LifeFormCrystal`. It must not be possible to make a lifeform that violates this.
  **A connected COLONY is a population, not a creature with body parts** (the worm colony):
  every segment — head, body and tail — is its own fauna and carries and drops its own heart,
  because the members are the lifeforms. Only the colony ROOT is heartless: it is the
  population's anchor, and it forwards a config's element pick to every member
  (`Fauna.ProvisionHeart`), so a colony breeds true across growth and across a split
  (which inherits the parent's variant pick). The earlier "body segments are body-parts and
  carry none" ruling is **RETRACTED** — do not cite it, and do not take a body segment's
  crystal away to restore it (`Docs/ECOSYSTEM.md §23.3` + `§23.8`). A split is the same rule
  at the population level: the head and everything attached to it stay the ORIGINAL colony,
  the tail and everything attached to it become a NEW one that strongly separates from every
  other worm population — and "strongly" is load-bearing, because the separation term was a
  normalized-direction-vs-inverse-square mismatch giving 5.7° of deflection at touching
  distance, i.e. numerically inert at any weight (`§23.8`). **A boid term blended into a
  NORMALIZED direction must itself be bounded** — scale a unit vector by a falloff in [0,1]
  so the authored weight is a real ratio, never a raw `1/d`.
  **A colony GROWS on its host cell's fauna production cycle** (`Cell.CurrentFaunaSpawnPeriod`),
  one member per cycle — a head if it has none, else a tail if it has none, else a body
  segment — so growth rate is a property of the BIOME, not the species (5s in the freestyle
  Lattice boot world, 30s in most cells). It is the same population heartbeat the lattice
  flora colonies breed on (`§32.7`), and it is production gating, which `§0` permits. Read
  the PERIOD, never `OnFaunaWaveSpawned` — only `RandomLifeSpawner` raises that, so an event
  subscription is dead code in every IntensityWise cell. Body growth is gated on being fed;
  head/tail regrowth deliberately is not, because a headless colony cannot feed and gating
  its mouth on feeding is a deadlock. A missing end is **GROWN as its real prefab**, never
  hardened out of a body segment — wound differentiation is retired and a member's role is
  fixed at birth. And a heart is seated at the FRONT of its member's own prisms with the
  body trailing (the tadpole arrangement — that prefab puts its crystal at the origin and
  its body at z −5.81), never buried inside them (`§23.9`).
  **A heart's SIZE is AUTHORED PER ELEMENT, in that species' own variant tuning — never a
  curve, and never a per-prefab accident.** A lifeform is its species and its ELEMENT and
  nothing else (`Docs/ECOSYSTEM.md §40`, which RETIRES §33's level curve), so everything an
  element states about itself it states exactly once — including the size of the heart it
  drops: `FaunaVariantTuning.HeartWorldScale` / `FloraVariantTuning.HeartWorldScale` (0 = not
  authored → `ElementalCrystalSet.defaultHeartWorldScale`), applied at the single gate every
  heart passes through (`Crystal.SetEmbeddedIn` → `LifeFormCrystal.ApplyHeartSize`) and
  re-applied by `LifeForm`/`Fauna.ApplyHeartSize` when the variant lands. A crystal's world
  scale is read AS GAMEPLAY by the collect reward
  (`SkimmerAdjustElementLevelByCrystalEffectSO`) — it was read a SECOND time by the live domain
  fauna buff, which was removed (`Docs/ECOSYSTEM.md §15`) — so the size IS the reward, and that
  is the DESIGN rather than the hazard: **a bigger kill pays more**, the largest lifeform's heart being worth 4.0× a
  SchwarzP Charge plant's. It only holds while the whole band stays under
  `ElementalCrystalSetSO.MaxSafeHeartWorldScale` (**4.8**, under the 5.0 world scale at which
  `min(scale × levelPerUnitScale, maxLevelGainPerCrystal)` saturates) — past that, two visibly
  different hearts pay the same, i.e. a size the player can see and a reward they cannot. The
  band is MEASURED, never eyeballed: `Tools/Build/author_lifeform_heart_sizes.py` sizes every
  heart as `K · bodyDiameter^0.5` (ordinary allometry — an organ does not scale 1:1 with body
  length) with `K` SOLVED so the largest lifeform lands on the tool's own `HEART_MAX` (**4.6**),
  which sits deliberately UNDER that 4.8 ceiling — two margins, neither of them slack to spend:
  4.6 → 4.8 is headroom for a future bigger body, 4.8 → 5.0 is headroom against the reward cap
  itself. It **FAILS the build** (`--check`) on an overshoot, on a NON-MONOTONE measurement (a
  bigger lifeform carrying a smaller heart — the one place a body-size bug surfaces), and on any
  hand-edit that drifts an asset off what it would author; the shipped band is
  **1.16** (SchwarzP Charge) → **4.60** (Nerve flora — the anchor is a PLANT since the
  nested-instance measurement fix, `§46.5`; the Shark reads 133.8 across, not 195.1, and its
  heart is 4.23). Do not compensate a sizing change by retuning
  `levelPerUnitScale`: it is shared with non-lifeform elemental crystals (the Wanderway
  conveyor, Dog Fight's arena scatter) — compress the mapping instead. Work in WORLD scale
  (`LifeFormCrystal.SetWorldScale`); a local write drags the heart along with a growing body.
  The §33 finding this SUPERSEDES is still worth carrying: the per-prefab scales it removed
  (0.7 tadpole → 4.0 gyroid) were a 5.7× reward spread **nobody had authored**, and flattening
  them traded that accident for a different one — one number cannot be right for both a tadpole
  and a shark. The answer to an unauthored spread is a MEASURED band, not a constant.
  **Uniform root scale is NOT uniform apparent size, and the fix goes BELOW the root.** Each
  elemental prefab carries a size correction on its model child (Charge 1.0 / Mass 1.38 /
  Space 1.34 / Time 1.42) because the four FBX models are very different sizes in their own
  units — and those children exist to equalize apparent EXTENT, which at 1.0/1.0/1.34/1.42 they
  already did within 7% (measured from FBX `Vertices` bounds normalized by `UnitScaleFactor`;
  Space's file is unit-1, the others unit-100). Mass is raised to 1.38 anyway because it reads
  thin rather than small — four concentric `ShepardGraph` shells vs Space's solid `_spread`
  body — and that number is an eye-calibration pending playtest, not a measurement. A
  per-element size fix belongs on that element's crystal PREFAB child; putting it on the root
  moves the collect reward with it, since that reads the root's `lossyScale`. `Docs/ECOSYSTEM.md §33`.
  **Collecting one is a BEAT, not a journey** — snatch → suction → absorb in **0.44 s**, ending in
  the element's spent-crystal husk bursting into the vessel's wake (`Crystal.Explode`, the same
  payoff an omni pickup plays) and the crystal dissolving out on `_opacity` rather than being
  `Destroy`ed (continuity of existence applies to crystals too). All feel lives in the ONE asset
  `Resources/CrystalCaptureConfig` (`CrystalCaptureConfigSO`) — **never a per-prefab duration**,
  which is how the old capture drifted to 1 s on two fauna and 3 s on eleven flora while reading as
  the crystal chasing the ship. The reward (the element level) lands at CONTACT and so does
  `OnCrystalCollected`, the scoring event: **a mode's objective must never wait on a flourish**, and
  a flourish that outlasts its own payoff reads as lag. **The husk is the DEFAULT, not a law** — a
  vessel may retire the crystal by carrying its own BODY onto whatever the pickup made instead (the
  Scarab's crystal closing into the ball it forges,
  `R_VesselActions/SCARAB_CRYSTAL_MORPH.md`), suppressing the spray through
  `Crystal.ExplodeParams.SuppressHusk` because two retirements would draw the same body. It
  suppresses the SPRAY only: the pickup sound and the impact latch belong to the pickup, not the
  husk. The 0.44 s beat is shared either way (`Resources/CrystalMorphConfig`), so a pickup reads the
  same LENGTH whichever hull took it and whatever it became. `Docs/ECOSYSTEM.md §31`, `§31.1a`.
- **A plant's HEART is a place things can be BUILT on.** Every living flora registers its crystal
  in `FloraHeartRegistry` (an INDEX — it spawns, moves, ages and removes nothing), and the Scarab's
  switch grafts onto the nearest free one on its flight path in every arena
  (`ScarabSwitchAnchors`, `R_VesselActions/SCARAB.md §5.3`). It exists because Tollway built its own
  set of sockets first and that was the wrong owner: a flora crystal is placed by the FOOD WEB (so
  the set is alive — grazeable, re-seeded, never on a timer), is already drawn, is already
  replicable (`FloraConfigurationSO.NetworkSynced` — a mode that RELIES on the anchor must set it,
  because flora are per-peer by default), and is a joustable heart, so denying an anchor pays an
  element level. General rule: **before a mode builds a set of points of interest, check whether the
  platform already grows one.** Membership is the plant's LIFE, not its crystal's existence — a
  plant leaves the registry on death, because death releases the heart and a crystal anyone can
  collect is not a fixture. `Docs/ECOSYSTEM.md §42`.
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
  `Microscene` conveyor, the Spawn Matrix toy) and a cap one producer skips is two ceilings for
  one number. Reproduction is production, so it freezes with planting at Frenzy; a lowered cap stops
  producing and never culls.
  **A CELL MAY ALSO SAY HOW BIG ITS PRISMS ARE** — `SpawnProfileSO.FloraPrismScale`, the third
  flora scalar beside `FloraPopulationScale` (how many plants) and `FloraPlantBudgetScale` (how big
  each plant gets). It exists because a species asset is SHARED (Rampage's five flora configs serve
  its own four intensities), so a per-cell leaf size cannot be authored on them — the same argument
  as `FaunaPopulationScale`, one level down. It resolves on the **Cell**
  (`Cell.ResolveFloraPrismScale`) like its siblings, and is applied once in `Flora.Initialize`
  **before `base.Initialize`**, which binds and stamps the prefab's own seed prism — apply it later
  and that one prism keeps the authored size while everything grown after it is scaled. **It is NOT
  a revived lifeform level**: it is a property of the CELL, so every plant of a species in it is the
  same size and nothing is a per-individual history (§40 intact). Two things it lands on: the volume
  ladder (**volume is the spine**, so a cell that scales its prisms must re-derive its own
  `PhaseThresholds`) and *nothing else* — prism COUNT is untouched, so **this scalar is FREE in
  colliders**. Its two siblings are not, which is the pairing to keep straight: `FloraPrismScale`
  buys size for nothing, `FloraPlantBudgetScale` multiplies prisms without multiplying plants, and
  `FloraPopulationScale` multiplies BOTH the LOD-cullable prisms and the **always-on heart-crystal
  colliders** (one per live plant, culled by no phase — so its ceiling is the CAP, not the seed
  count). When a cell scales its flora, ask which of the three it is reaching for.
  The exponent is **PER FAMILY** and this is the part that is easy to get wrong:
  `BranchingFlora` lays `leafSize` on all three axes (**s³**) while `PhyllotacticFlora` reads only
  `leafSize.x/y` as a CROSS-SECTION and takes its long axes from its own `segment`/`reach` (**s²**),
  so a phyllotactic strut gets THICKER, not longer. A **lattice** species is exempt through
  `Flora.PrismSizeFixedByGrowthRule` — the guard §40 deliberately kept *with no reader*, which is
  now doing exactly the job it was kept for: the next thing that wanted to resize a leaf was gated
  on it on arrival instead of rediscovering the hazard. `Docs/ECOSYSTEM.md §43`.
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
  **6.9× nominal volume**, and when that was measured the level spread multiplied them another
  ~2.7× (that spread is retired — §40 — so the factor is now exactly **1**, and any volume
  measured under it must be re-derived before it is reused), so its seeded floor alone was 87%
  of `FrenzyEnterVolume` and the colony froze after one wave while its caps sat 19× further
  out. Reach for the ladder, not the population dial (`Docs/ECOSYSTEM.md §32.7`
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
  through the Cell Selector. It **was** the boot world between §36.10 and §48 — it replaced Blob at
  `CellConfigs[0]` and `Blob Cell Config` is deleted (only the config; the `Blob Cell` folder's
  SpawnProfile is still the population of all seven authored freestyle worlds); **Garland boots
  today** and Lattice stays at `CellConfigs[0]` as a Cell Selector option. Booting into Lattice was
  affordable because the cost ACCRUES: the cell opens with eight plants and no environment build,
  and reaches the collider line only after ~7 minutes of growth — which is also the reason it was
  replaced, since *accruing* is the one thing a home screen cannot afford: the lava-lamp camera
  shows the cell from 686 units and a world that is nearly empty for its first minutes is empty in
  exactly the shot the screen exists to draw. That swap also split a conflated
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
- **A cell composed for a CAMERA is budgeted differently from one composed for a PILOT — and
  the difference is one rule: spend prisms on LENGTH and SILHOUETTE, never on surface.** The
  freestyle seven are 34–41k prisms each and are all authored for somebody flying *inside* them;
  the home screen shows none of that, because `MenuCam_LavaLamp1` orbits the cell centre at
  **686 units** and at that distance a nominal 2.5-unit prism is a pixel. **Garland**
  (`SpawnableGarland`, `Docs/ECOSYSTEM.md §48`) is the same kind of world at **4,259 prisms**,
  and it gets there by laying every family as a curve of ONE prism per step, with that prism's
  LENGTH **derived from the step** rather than authored (`ChainFill`, 0.82): a 9,377-unit
  torus-knot bough costs **426** prisms and reads, at that range, exactly as a filled sheet forty
  times dearer would. Yggdra's trunk overlaps its prisms 3.8×, which is right for something flown
  through at ten metres and is pure waste at seven hundred. Three consequences travel with the
  rule. The prisms are **big**, so such a cell must author its volume ladder from MEASUREMENT
  (2.18M here) — but big prisms are free in colliders, which is what the budget is actually made
  of. **Depth replaces detail**: a nearly
  still camera reads a composition through its layers, so the cell is a shore on the seed, a
  subject band the camera orbits INSIDE (asserted — that is what makes the bough the subject
  rather than a shell seen from outside), a thinning far edge, and one family (the root falls)
  that CROSSES all three, without which the cell reads as concentric shells instead of one
  object. And the ROSTER follows the same brief: no lattice species (their population-event
  reproduction is the runaway the Lattice cell exists to show), four phyllotactic flora and
  three fauna on hard caps — **37 always-on heart colliders** against Blob's 171 and Lattice's
  1,080, mature at 8,147 prisms. Its 69 super-shielded prisms cost **no collider at all**, and
  the branch's own generator asserted, printed and documented them for three commits as a
  "collider budget" before that was checked — the Breakwater trap (*verify a cost before you
  pay for it*) met from the other side: **a generator's own printed label is read as a
  MEASUREMENT, so an assumption written into an assertion string becomes fact by repetition.**
  The budget is kept on its real justification — armoured mass is inedible (`Prism.Consume` is
  a no-op on it) and leaves the targeting grids, so it is mass the food web can never remove in
  a cell whose equilibrium depends on grazing (`Docs/ECOSYSTEM.md §48.3`). Two ladder rules pull opposite ways and both must hold:
  **Frenzy above the MATURE cell** (it freezes planting, so a ladder authored on the bare
  baseline leaves the garden permanently half-grown) and **Restless EARLY** (~35% of the planting
  budget, or the food web is dormant for the whole of the cell's growth and the equilibrium never
  starts breathing). Note `CellPhaseRules.Compute` never reads the Restless COUNTS — that
  boundary is volume-only. Finally, its generator draws **nothing** from the base class's shared
  `System.Random` or from value noise (every wobble is `Hash01` of the emitting index), which is
  what lets `Tools/Build/garland_harness/` compile and RUN the shipped C# and
  `author_garland_cell.py` assert the model against it — so the thresholds are measured rather
  than believed, and `--self-test` proves both the nucleus-clearance and the no-clipping
  assertions fire.
  **NOTHING IN IT CLIPS ANYTHING, and that is a property of the generator rather than of its
  constants** (`§48.10`). Measured over the whole emitted cloud with a 15-axis separating-axis
  test, the cell shipped at **4,372 interpenetrating pairs of 8,726 near pairs** — half of
  everything that could touch — because the file's own rule was *one prism per step sized to
  close the gap behind it*, i.e. every chain authored at `step × 1.08`. Four different KINDS of
  fix got it to three: a chain's length became a function of its own STEP; **a golden-angle head
  cannot hold non-overlapping petals** (a sunflower packs at constant AREAL density, so the
  head's area runs out before the count does — blossoms became concentric RINGS, which state the
  ring pitch against the petal's LENGTH and the ring count against its WIDTH, `(2R − L) tan(π/n) >
  W`, and the same argument retired a 28-leaf single-cone tuft, whose spacing FALLS as the count
  rises, for a spiral CAP); every family attaches at its own PHASE, because **two families sharing
  a knot sample are two structures sharing a point in space** and no per-family clearance can see
  that; and **a chain that starts at its parent's own lay point starts inside it**, so falls and
  crowns start displaced — DIFFERENTLY, since a crown climbs out of the bough's band and never
  returns (radial lift) while a fall spends three quarters of its length inside it (sideways, out
  of the knot's own osculating plane: 44 clipping pairs against 7). The last three pairs were not
  reachable by tuning, because the falls cross the whole cell and meet *something* at every phase,
  so the three SHORE families are laid LAST and **YIELD** — a prism that would land inside
  something already laid is simply not laid, at a cost of **five prisms**, and it REPLACED the
  band's own landfall-break rule rather than adding to it. Three details of that are load-bearing:
  the yield gap is **0.75, not 0** (a fit that clears by a hair re-reads as clipping, and a
  decision taken ON its threshold is one float64 and float32 can disagree about — tightest shipped
  decision 0.089); the grid's 27-cell neighbourhood only covers every pair that can touch while no
  prism's bounding radius exceeds half a cell (asserted); and **`LookRotation` is undefined when
  up is parallel to forward and Unity does not say so — it invents a pose**, which a fall's
  near-radial last steps hit, found only because the offline model THROWS there instead of
  measuring what the engine made up. General rule, the gyroid lattice-scale finding one level up:
  **two prisms occupying the same space is a relationship between families that were each
  individually correct, so it cannot be derived from any family's own parameters and has to be
  measured over what the generator really emitted, in the ORIENTATIONS it really emitted them** —
  which is why the compile harness's `Quaternion` stopped being a stub.
  **It is the BOOT world**, and getting it there needed the last inference in the boot path made
  explicit. `CellTypeChoiceOptions.EnvironmentFree` picked the first config with **no**
  `EnvironmentPrefab` — a claim about what a config CONTAINS standing in for the thing actually
  wanted, *how cheap it is to BUILD* — and the proxy held only while no config was both. Garland
  is the first that is: 4,259 prisms build in a fraction of a heavy world's veil, and it boots
  into a world rather than into an empty sphere. `CellConfigDataSO.BootDefault` is that
  declaration (`Cell.ResolveBootIndex`, authored flag first, the environment-free scan as the
  fallback, so every other cell in every other scene is byte-identical — Garland is the only
  asset in the project that sets it). This is **§36.10's own rule met from the other side**: there
  a property named for how something is BUILT was read as a claim about what it CONTAINS, and the
  answer was a second predicate (`BareCanvasConfig`); here a property named for what a config
  CONTAINS was being asked how it BUILDS, and no predicate over content can answer that, so the
  answer is an authored bit. The honest cost, stated: Lattice booted instantly and *accrued*,
  while Garland pays its build on **every** entry to Menu_Main (boot and every return from an
  arcade game) behind the standard `EnvironmentLoadVeil` — 12% of Yggdra's 34,340 prisms, and the
  price of a home screen that is furnished in the first frame rather than in the seventh minute.
  Lattice keeps `CellConfigs[0]` and stays a Cell Selector option; `BareCanvasConfig` still
  resolves to Barren, so the Wanderway is untouched.
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
  and note that a lattice species' prism is its AUTHORED size for the whole of a plant's life,
  so the fitted leaf is the only size that ever renders. The per-level leaf curve that used to
  grow it mid-life is retired outright (§40), and it could never have worked here: it scaled the
  prism but not the lattice, so at 1.15 a level-5 plant's prisms were 1.749× the flush size and
  it interpenetrated itself (measured: 0 overlapping pairs at L1, 144 at L3, 212 at L5).
  `Flora.PrismSizeFixedByGrowthRule` (true on `AssembledFlora`) is deliberately KEPT now that
  its reader is gone — a standing guard against the rule returning on some future growth path. **A lattice can be SCALED only where "sameness" is an integer address, never
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
- **A COMPACT form needs none of the lattice machinery — the Borromean membrane.**
  `BorromeanFlora` grows the **minimal-genus Seifert surface of the Borromean rings**: the three
  golden ellipses (semi-axes 1 and φ, the boundaries of three golden rectangles whose twelve
  corners are an icosahedron's vertices — and a FORCED realization, since by Freedman–Skora the
  link admits no three round CIRCLES), spanned by the level set `Ω ≡ 2π` of their summed
  solid-angle potential and relaxed to zero discrete mean curvature. Measured **χ = −3 over three
  boundary loops ⇒ genus 1**, area **11.955** against **15.250** for three flat discs — and three
  flat discs are not an alternative, because they intersect and three DISJOINT ones would split a
  link that is not split, so a CONNECTED spanning surface is forced. **The symmetry is order 6
  (C3ᵢ) and that is the MAXIMUM available, not a shortfall**: the unoriented rings carry the
  order-24 pyritohedral group, but half of those elements reverse some rings' orientations and
  carry this level set to a different one — measured for every orientation assignment. The site
  table is **EXACTLY** invariant (residual `0.00e+00`) because it is a union of whole ORBITS, and
  growth lays **one whole orbit per tick**, so a half-grown plant is exactly as symmetric as a
  finished one. **It grows the way a flora WITHERS, run backwards — the crystal first, then limbs
  out of the crystal, then limbs and prisms out of limbs** (`Docs/ECOSYSTEM.md §49.9`), and that
  cost a second pass: the first ordering was by RADIUS, which on a surface that wraps is several
  disjoint rings, so the plant grew up to **3 separate patches** that sealed up later. The table
  is ordered by **HOP DISTANCE over the surface's own site graph**, which survives the symmetry
  because *the graph is G-invariant, so hop distance is an ORBIT property rather than a site
  property*; every site names its **PARENT** (always earlier in the table) and a plate is never
  laid on the far end of a limb that does not exist — measured, exactly ONE component after every
  one of the 60 ticks. *A radius sort is not a growth order.* **A SPINDLE IS A BOND, NOT A
  MARKER**: each limb is rooted at its parent, aimed at its child and stretched to span the gap
  (a limb runs along one of its own plate's axes to within 21°), which is `BranchingFlora`'s
  shape — the gyroid reaches the same end from the other side with a mirrored PAIR meeting at the
  prism (§34.12), while **Schwarz P's single off-centre arm is the anti-pattern** and must not be
  copied. **A PER-SITE CHOICE AMONG EQUALLY-VALID OPTIONS IS NOISE UNLESS IT IS COMBED** — the two
  asymptotic directions below are orthogonal and interchangeable, so picking one per site from the
  sign of an eigenvector in an arbitrary tangent basis left neighbouring plates **56.6°** apart
  with 49% of edges over 60°, every plate individually flush and the TILING noise; combing the
  choice per orbit REPRESENTATIVE (so it cannot break the symmetry) takes it to **25.9°** at zero
  runtime cost. Three more things generalise. **A centroidal Voronoi
  tessellation can be made exactly symmetric** by running Lloyd's on the orbit set and pulling
  each centroid back through the group — averaging over the orbit is what keeps a representative
  a representative, so there is no symmetrisation pass and therefore no drift for one to mask.
  **Half the group is IMPROPER**, and a right-handed frame mapped by a reflection is not a
  rotation, so `y` is re-derived as `z × x` after the map — a plate is a BOX and is invariant
  under a flip of any one axis, so the geometry is carried exactly and only the quaternion table
  is equivariant up to a symmetry of the box. And **the plate lies on the surface's ASYMPTOTIC
  directions**: on a minimal surface the principal curvatures are equal and opposite, so normal
  curvature vanishes on the two directions bisecting them AND those two are orthogonal — a
  property minimal surfaces alone have, and the reason a flat rectangle sits flush on a saddle
  (measured 0.247/0.251 of the local shear along the plate's two axes, against a mean curvature
  of 0.165 of it; a sphere scores 1.00). **It is NOT a lattice species and deliberately has none
  of their machinery**: a lattice tiles a periodic surface indefinitely and reproduces as a
  COLONY because its growth rule has an opinion about where the next PLANT belongs, while a
  Borromean surface is COMPACT — it closes on itself and is finished — so this plant completes
  and funds an ordinary per-plant offspring out of its growth quota (§32). No frontier, no claim
  book, no mate-snap, no `LatticeScale` family of absolute tolerances (§34.8); *a species whose
  form is bounded does not need them*. It keeps `PrismSizeFixedByGrowthRule` because its offsets
  are a measured table in absolute units, and resizes through `surfaceScale`, which moves the
  sites and the leaf together. **NO PRISM MAY INTERPENETRATE ANOTHER, so a plate's SIZE is not
  authored: an element authors the SHAPE of its plate and the SPACING of its tiling, and the size
  is FITTED to the largest that clears** (by 3%; 10% bigger collides). Spindles are exempt by
  design — a limb may pass through a plate. Three measured facts carry it. **THE FOOTPRINT COSTS
  CLEARANCE AND THE THICKNESS DOES NOT** — a plate's neighbours lie in the membrane beside it, so
  0.1 → 0.8 of its own width in thickness costs 1.3% of the footprint and buys 7.7× the volume,
  which is what lets the element contract survive the rule instead of being flattened by it (Mass's
  volume and Space's equal-volume-at-higher-aspect are SOLVED on that free axis). **COVERAGE IS A
  PROPERTY OF THE TILING, NOT OF THE COUNT** — every plate is fitted against its own neighbours, so
  a looser tessellation covers the same membrane with FEWER, BIGGER pieces (the fitted long axis
  is 0.73–0.93 of the spacing at every count from 24 to 60 orbits), which makes the tiling an
  element's own decision and gives the ladder its direction: **the more ROOM PER SITE, the bigger
  the body it carries** — room per site 6.14 (Time) < 8.23 (Mass) < 8.69 (Charge, whose real body
  is its SHIELD at three times its plate's reach) < **13.68 (Space)**. That ordering is ASSERTED
  from the shipped tables, both sides measured. It is stated in ROOM and not in orbit COUNT
  because **an element buys room two ways — by cutting the membrane into fewer pieces, or by
  growing the MEMBRANE** — and Space does the second, so it has a FINER cut than Mass or Charge
  (288 plates against 216 and 180) and still the most room of the four. Growing the membrane is a
  **SIMILARITY**, the one transform that maps a clearing arrangement onto a clearing arrangement
  exactly, so it costs the guarantee nothing to re-derive. And **the guarantee lives in the CODE, because a guarantee
  any asset edit can break is not one**: the plant takes its leaf from its own table
  (`BorromeanSurfaceData.For(Element)`), resolved from its crystal at the TOP of `Initialize`
  before the base stamps the prefab's seed prism — `Flora.ResolveShieldPeriod`'s argument one field
  over, which also closed the gap where `surfaceScale` scaled the offsets and not the leaf. **The
  plate aspect is still a LOOK call and the rendering is still the
  evidence** — every structural check passes at any aspect, so the choice was made by rendering
  four: at `1.40 × 0.73` the plates lap 61% and the membrane reads as one smooth blob, at
  `0.85 × 0.55` they lap not at all and it reads as a perforated mesh; the aspect shipped from that
  pass survives and its SIZE does not (`1.15` of the spacing along the grain, 36% lap, became a
  fitted `0.76` and zero). **Stated plainly as a cost: the membrane no longer laps, Time's plant
  volume fell 7,499 → 3,279, and Space now reads as a frame of STRUTS rather than a skin.**
  **8 plants per element = 32 always-on heart colliders.** **AN ELEMENT IS A PERTURBATION OF AN
  ANCHOR**, which is what
  makes "what does this element do to the plant" one comparison rather than four independent
  fits: **TIME** is that anchor (9.11 volume per plate, 1 : 0.59 : 0.15),
  **MASS** is more VOLUME *and the CHUNKIEST plate* (8.00×, **1 : 0.83 : 0.50** — and it stops
  short of a cube on purpose, because the footprint is FITTED so the only axis that can move a
  plate toward one is the thickness, and the thickness IS the volume),
  **SPACE** is more ROOM — its MEMBRANE is **2×** the anchor's, so its plant spans **222 against
  111** and its plate is `12.98 × 1.53 × 0.46` at 8.50:1 and the *same* volume (a `k×` membrane
  fits a `k×` footprint, so holding the volume drives the thickness down by `k²`: same plant
  volume, same site count, twice the span, struts 2.6× thinner) —
  and **CHARGE is FITTED to its own shielded form** — not uniformly shrunk. Charge's plate is
  `1 : 1.00 : 0.50`, i.e. SQUARER than Mass's, which is why the chunkiness rule is asserted over
  the three elements whose body IS their plate: a Charge plate is a square slab *because* the body
  it was fitted against is the octahedron three times it, so how cube-like it is says nothing
  about what a Charge plant looks like. *A check that has to be scoped is usually telling you
  something true about the thing you scoped out.* Both halves of that
  fit are measured: the footprint is SQUARE (the clearance is set by the tightest BOND, which
  runs along the grain, so length there is paid for twice — square covers **22.5%** of the
  membrane with octahedra against 15.6% at the anchor's aspect) and the THICKNESS is spent freely
  (along the surface NORMAL, where the neighbours are not). The element spread across one species
  is consequently **19.6×** in plant volume (Charge 804 → Mass 15,739), so a cell that rolls all
  four is pricing an AVERAGE rather than a plant; and Space's 0.46 thickness is under
  `PrismScaleAnimator`'s serialized `minScale` 0.5, surviving only because `Flora.AddHealthBlock`
  calls `Prism.AdmitTargetScale` first — the same rope SchwarzP Charge's 0.39 hangs from. Authored by
  `Tools/Build/measure_borromean_minimal_surface.py` +
  `author_borromean_flora_assets.py`, re-proved from the shipped tables alone by
  `Tools/Build/verify_borromean_surface_tables.py` (every check run four times, **22** negative
  controls, all firing) — whose
  one RETIRED check is worth as much as the new ones: *"the blocks' radii are non-decreasing"*
  was true, cheap, and asserting the very property that made the plant grow wrong. *A green check
  on the wrong invariant is worse than no check.* Per-plant guidance now lives in the **`/flora`
  skill**. **It grows in RAMPAGE (all four intensities, as mass to destroy), WRECKING BALL (all
  four), WILDLIFE BLITZ cells 1 and 2** — every cacti cell but Tollway — **and the freestyle
  ARBORETUM**, the one cell that grows it to be LOOKED at rather than flown through (one
  specimen per element, `cap 1`), plus the freestyle Spawn Matrix toy. **A cell adopts it as FOUR configs, one per element, never as one rolled
  config**: a `FloraConfigurationSO` carries ONE `Variant` block, and the four differ in budget
  (180–360), plate and HEART (2.051–3.379), so a rolled config would author one heart size for
  four plants whose spans run 108 to 222 and `author_lifeform_heart_sizes.py` would be sizing an
  average rather than a lifeform. Adopting it moved Rampage's forest 396,178 → **441,070** and its
  intensity-4 prisms 9,830 → **11,918**, and the answer was to **RE-ANCHOR rather than let the
  gates float**: the authored volume pair is a play-test result, so it holds and the MARGIN
  absorbs the mass (Frenzy 4.11× → **3.70×** the mature forest); the COUNT half is derived and
  legitimately moves (10,000 → 12,250). Three things generalise. **A species whose leaf is a
  MEASURED TABLE is exempt from `FloraPrismScale` and its volume exponent is 0** — a statement
  about the code (`PrismSizeFixedByGrowthRule` → `Flora.ApplyCellPrismScale` returns early), not a
  rounding, so Rampage's prism axis now moves five of its six species and leaves the sixth alone.
  **ONE model row for FOUR configs needs an assert**, because round-half-up does not commute with
  a sum: at `FloraPopulationScale 3.67`, two seeds across four configs is 28 plants and eight
  seeds once is 29 — `forest()` scales per config and `assert_species_aggregation` proves the row
  divides evenly, since *a row that prices a forest the game does not grow is worse than no row*.
  And **a prefix rule for a species-owned family is correct only while every config of that
  species is named for the species alone** — `author_flora_populations.py` matched `"Borromean "`
  as a prefix, and a per-cell config is named for the CELL first (`Rampage Borromean Flora Mass
  Config Data`), so the rule became a SUBSTRING or every adopting cell's copy would have been
  handed silently back to a model that has no input to work from on a measured table. Collider
  budget: Rampage intensity 1 goes 49,150 → **59,590** prisms (Atlantis 69,000) and 440 → **500**
  crystals (the Lattice cell's 1,080), both asserted. Tollway is deliberately excluded — its flora
  are its SCORING SOCKETS, one anchor species per growth FAMILY per intensity, so adding a genuine
  fifth family there is a mode-design decision rather than an adoption; Hesperides too, because it
  sows typed planting SITES and a compact membrane is none of them. Two traps worth more than the species: **a `ROOT` one `dirname` too
  shallow wrote the whole asset tree into `Tools/Assets/` and the verifier, sharing the bug, read
  it back and passed** — *consistent wrongness reads exactly like correctness*, so the definition
  now carries an `assert` that `Assets/` is under it; and **a Jacobi sweep on a cotangent system
  fails by being slightly WRONG rather than by failing** (1,200 sweeps were still 3% above what
  five sparse solves reach in under a second, i.e. an inflated membrane that passes every
  structural check), which is why the tool asserts the AREA — a property of the surface rather
  than of the solver. `Docs/ECOSYSTEM.md §49`.
- **A species' shape may be a FUNCTION, and then the plant's job is only to DISCOVER it — as a cage
  of curves, never as a skin.** `MandelbulbFlora` is the fourth growth family: it traces curves over
  the surface of the **Mandelbulb** (the escape-time fractal of `v -> v^n + c` in triplex
  coordinates), dense ALONG each curve and sparse ACROSS it, so a plant is an open lattice of
  ribbons you see the fractal through. **§34's "grow on the surface's OWN tile, never a fitted grid"
  is a rule about surfaces that HAVE an exact tiling** — a fractal boundary has none (not periodic,
  not quasiperiodic, no repeat unit, not a smooth manifold), so inventing one would BE the fitted
  grid §34 forbids. It addresses on the SPHERE instead, which keeps the property that rule actually
  protects: a prism is stamped ONCE with `(theta, phi)`, its heading in that point's own tangent
  basis, its radial lift and its size, and its pose is then a pure function of that address and the
  surface — nothing baked, no bond table, no tolerance that can drift. **Its first cut plated every
  surface cell and read as a lumpy sphere, and the reason generalises: a Mandelbulb's form IS its
  TERRACING, and a closed crust hides terracing by definition** (raising the resolution makes it a
  FINER lumpy sphere; four closed-surface candidates were built and rejected, and concentric shells
  fail for a reason worth carrying alone — **the interior is a solid blob, so a shell cut inside it
  is just a sphere**). A riser-and-patch PLATING was the second cut and is also retired: it was
  still a sampling of an AREA, and what works is anisotropy. **The fractal is evaluated EXACTLY
  ONCE, offline**: its outer surface is ray-marched into a spherical height field `R(theta, phi)`
  and fitted to spherical harmonics, and the shipped table is those coefficients. Three measurements
  force that representation — **the distance estimator's GRADIENT is unusable as an orientation at
  this scale** (48 degrees of swing between surface points 0.013 apart, so every curve dies on the
  turn gate; the first build of this rule rendered a black screen for that reason alone); **the fit
  does not converge and does not need to** (degree 16 lands at R^2 0.90-0.97, degree 12 lands at
  **0.11** for the power-12 bulb because its structure sits exactly at `l = 12` and aliases, and the
  plant needs the bulb's CHARACTER rather than the bulb); and **three modes explain the family**,
  because the surface's response to the Julia constant is linear over a useful basin — they ARE
  `dR/dc`, so **four ray-marches recover the same 3-space that fifty-six do** and **a whole plant is
  the shared basis plus THREE FLOATS**. Addressing by emission INDEX instead does not work and was
  measured: nudging `c` by 0.0002 moves the median prism 23% of the bulb, because tracing is
  sequential and every discrete decision reshuffles; the same nudge moves a `(theta, phi)` address
  by 0.00002. Two more rules come out of the address: **the lift is RADIAL** (along the ray the
  prism and its surface point share — storing it along the NORMAL leaves the tangential difference
  behind, measured at 2.4 prism lengths), and **LENGTH is AUTHORED, never derived** (deriving it
  welds the chain and lets prisms stretch 20x under a morph, and a prism whose length is a function
  of the morph has a VOLUME that is too, which lands straight on the cell's Frenzy ladder). **Three
  defects it found are one class of mistake and all three generalise.** A **well-spread generator is
  only well-spread over its whole output**: the Fibonacci sphere walks z monotonically, so an NMS
  that stops at `want` seeds yields a polar CAP — measured, 78% of a plant in the top eighth of the
  sphere by area and nothing below the equator. A **budget-limited walk must be breadth-first**:
  seed-major traversal spent 6,000 prisms on two seeds and grew a belt. And **a dial whose reference
  is a ceiling nothing reaches is a dial that does nothing** — the girth taper keyed on the lane
  index, spread over `LanesPerSeed` generations a plant never reaches, and the measured prism-volume
  span was 1.4x; keyed on the RUN LENGTH it is 3.2-8.7x. **It states BOUNDS rather than a zero**,
  because curves CROSS — that is what a cage is — so two ribbons meeting at an angle have bounding
  boxes that must overlap: at most 5% of touching pairs deeply interleaved (shipped 0.0-3.1%) and no
  pair below separating scale 0.35, held by `MandelbulbFlora.Claim`, which refuses a prism within
  **0.70 x its own length** of one already laid — under 1 for a structural reason rather than a
  tuned one, since consecutive prisms sit exactly one length apart so the chain clears BY
  CONSTRUCTION. Its CHARGE variant is fitted against its ARMOUR (§35), and **the lever is the
  LENGTH, not the width**: a prism's `leafSize` includes its length, so a ribbon laid end to end
  fuses into a solid tube along its own curve — the Skein rail's finding — and shrinking the
  cross-section cannot reach it (measured, still 84% fused at a quarter width). **Charge's ribbon is
  DASHED** instead, its prisms shorter than the step that spaces them, and measured it inverts with
  the shield exactly as it should: 34.6% fused armoured against its siblings' bare 94.9%, and a
  silhouette of 22,690 bare / 102,107 armoured against the siblings' 79,962 — the DENSEST of the
  four while shielded and much the sparsest once stripped, with `--check` failing the build if that
  ordering flips. It is in **no `SpawnProfile`** (opt-in from the Spawn Matrix toy), so it costs
  no shipped cell anything until somebody puts it in one — which matters, because at
  `MaxLivePopulation` 3 its Space variant's ceiling alone is more than the Blob cell's whole Frenzy
  ladder. The species ships the tool trio a new flora is expected to plus a BAKE, and the verifier
  **compiles and RUNS the shipped C#** with seven negative controls — while stating plainly what it
  does NOT prove: the walk is held by its statistics and not prism for prism, because a sequential
  recurrence with a turn gate is chaotic in its last bits and the C# runs in float32 where the model
  runs in float64. `Docs/ECOSYSTEM.md §50`.
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
- **TIME breeds faster — the second elemental law.** A Time plant reproduces at **1.25x** the
  fleet rate and Charge/Mass/Space at **0.8x** (`FloraReproductionRules.ReproductionRateFor`).
  Tempo is Time's identity the way armour is Charge's, and reproduction is the only clock a plant
  owns. Like the Charge law it **cannot be authored per config**: `GrowthPerOffspring` is authored
  per CONFIG by `author_flora_populations.py` while the element is ROLLED per plant, and *every*
  species that actually spends that quota (Rampage, Hesperides, Wildlife) sets `SpreadElements`
  with a four-element palette — so no asset field could express it. The authored number stays the
  SPECIES baseline and the element scales it at spawn (`Flora.ResolveGrowthPerOffspring`, the
  sibling of `ResolveShieldPeriod`); the authoring script is unchanged and still `--check` clean.
  It is ONE constant because both reproduction paths measure **cost per child** in different
  units and both divide by the rate: the per-plant **growth quota** (prisms per child) and the
  lattice colonies' **cycle period** (seconds per child, `AssembledFlora.ColonyCyclePeriod`).
  Scaling the quota alone would have been **dead tuning on 34 of the 50 breeding configs —
  including every asset named "…Flora Time"** — because a lattice birth is a POPULATION event on the cell's
  fauna-wave clock and the per-plant quota is inert there (`Docs/ECOSYSTEM.md §32.7`; the ecology skill's
  §4.6 "prove WHICH gate binds" trap from a new direction). The colony period keys on the **CONFIG's**
  authored element, never the ticking plant's — the cycle book is shared per `(cell, species)` and
  every plant ticks it, so a per-plant period would be set by whichever plant ticked first, and a
  colony is mixed-element by construction (`LATTICE_MIN_FOUNDERS = 4`). An authored `0` stays `0`
  (the species saying it does not reproduce), and the Time rate can never floor a small quota to
  `0`, which `ShouldSeed` would read as the same thing. `MaxLivePopulation` is untouched, so the
  always-on heart-collider **ceiling is exactly unchanged** — only how fast a species reaches it.
  `Docs/ECOSYSTEM.md §38`.
- **ONE GROWTH RULE CAN HOLD TWO SPECIES, and the dial that separates them is the concept.** The
  Mandelbulb family traces curves over a baked spherical height field, and that rule holds two
  quite different plants with **no second class** — one prefab each, one component, one bake:
  **Fractal Foliage** (`MandelbulbFlora`) rolls every prism about its OWN curve tangent as the run
  advances (`GrowthRules.TwistDegreesPerStep` 12), so a curve is a helix of plates and the plant a
  dense twisted foliage; **Coral Bloom** (`CoralBloomFlora`) has no twist at all and instead makes
  its curves CONTINUE — high momentum, a low field mix, a long step ceiling, only long runs
  surviving — so it is an open cage of smooth arcs crossing through the whole structure. They
  SHARE the surface family deliberately, so the two read as the same WORLD grown two ways rather
  than as two unrelated objects. The twist is a pure function of the address
  (`PrismAddress.Roll`, stamped once at emission, applied as one cos/sin blend with the binormal),
  and roll 0 is **bit-identical** to before it existed. Both are EXEMPT from §51's runtime leaf
  transform, so each **DERIVES** the elemental law from its OWN neutral prism rather than typing it
  per element — which is what makes a concept persist through four elements while each element
  still expresses itself. **The finding is that an EMERGENT quantity quietly re-authors an
  authored law**: every prism's cross-section is multiplied by its curve's GIRTH, a taper keyed on
  how far that run got, so the mean girth is emergent from the curve family and measured it
  INVERTED the volume ordering the law had just set (Space carried 1.3x Time's plant against an
  authored 0.47x). Fixed by hoisting the taper to a SPECIES constant (it is the plant's texture,
  which is the concept) plus one MEASURED per-element scalar that cancels its own mean girth — and
  the SHAPE of those scalars is the reusable half: the foliage needs a real correction because its
  four curve families are deliberately very different, while the bloom barely moves because its
  concept makes all four uniformly long-running. **Adding a second species also exposed THREE
  verifier constants that were coincidences rather than margins** — a curve-count tolerance stated
  as a percentage of the COUNT (1% of one plant and 21% of another read as the same size of
  disagreement; now stated as the fraction of the PLANT the disputed curves account for); a
  "diverged before prism 16 = transcription error" rule that is really a statement about how rough
  one element's SURFACE is, and which cannot be repaired by measuring the first disagreement's SIZE
  because the prism lists are INDEX-ALIGNED, so one dropped curve makes a drift and a jump identical
  (the transcription test is now **prism 0**, which a transcription error cannot pass and float
  width cannot fail); and a `phi` comparison with **no seam unwrap**, which read a point either side
  of `phi = 0` as 2*pi of error and was making the original species look 50x worse than it is. *A
  gate written against one species is a gate calibrated on one species.* Both species are in **NO
  SpawnProfile** (opt-in from the Spawn Matrix toy). `Docs/ECOSYSTEM.md §52`.
- **EVERY MANDELBULB CURVE REACHES THE HEART, AND TWO OF THE FOUR SPECIES ARE THE SURFACE'S OWN
  STRUCTURE.** The family (§50, §52) is now FOUR species on one rule and one bake — Fractal Foliage
  (helicoids), Coral Bloom (smooth crossing arcs), **the Watershed** (the discovery species: every
  curve a SEPARATRIX of R(θ,φ), seeded at a saddle along a Hessian eigen-direction and run to a
  peak or pit, the surface's Morse–Smale skeleton, whose peaks and pits sit in latitude rings of
  exactly order − 1 = 7/4/2/11) and **Apollonia** (the self-similar species: an Apollonian gasket
  of discs packed over the surface's PEAKS, each disc drawn as a closed ring of prisms lifted
  onto R, so a ring crossing three lobes is a scalloped star and a ring inside one lobe a circle)
  — plus **the Fall**, the mechanism every species authors: a released curve continues as a LOG
  SPIRAL into the heart (a constant angle off the inward radial, never a lerp; wound about ONE
  axis so the pole is a rosette; `up` hung off the RAY; depth a SHELL FRACTION; the azimuthal sign
  behind a dead band; the stride ceiling solved per element because a surface prism is the CHORD,
  1.05–1.20× the step). Seven rules travel with it. **A critical-point census runs in DOUBLE on the
  float32 field** (`Surface.SampleD`) or the shipped C# and the model disagree on which peaks
  exist. **A column that is inert because it was authored at its no-op value is not inert** —
  the Watershed's mix/momentum/swirl were READ and are now short-circuited under `SkeletonSeeds`;
  the inert-column PROBE (grow twice with the column moved, assert byte-identical prisms) is what
  catches it. **A species whose prism length is set by its own geometry must not also take a
  `LengthFactor` that assumes a walk step** — a ring's prisms 21% longer than their chord refused
  their own neighbours through the claim filter, and no existing gate could see it (Apollonia
  ships a ring-integrity gate). **An element's §51 long axis is spent as SAMPLING COARSENESS**
  (prisms per ring derived from the step: Space a 29-gon of blades, Mass a 55-gon of bricks),
  never as a longer prism. **The lane is the size octave the EYE reads, never the recursion
  depth** (measured non-monotone in ρ), which is what makes a budget-stopped gasket lose its
  smallest rings and lay the Fall first. **Every ordering is a TOTAL key** — the bulb's symmetry
  puts children in orbits sharing a ρ to the last bit and `.NET` has no stable `List` sort.
  **Count octaves in SCREEN PIXELS, not prisms** — a ladder that satisfied the ~40-prism repeat
  floor spent 41% of its prisms on 1% of the arena frame; the ladder gate is stated in screen
  terms and the girth allometry carries a FLOOR. No address field changed for the gasket and
  `Pose` is untouched; the recursion is a seed-generation concern, lazy like the saddle census
  (never on the planting frame), Rng-free, and proven level 0 EXACT / children STATISTICAL
  against the compiled C#. Two judged design rounds, nine rendered candidates — the runners-up
  and why each lost are in `Docs/ECOSYSTEM.md §54`. All four are in **NO SpawnProfile** (opt-in
  from the Spawn Matrix toy). `Docs/ECOSYSTEM.md §53`, `§54`; the `/flora` skill §5.2.
- **AND ALL FOUR GROW OUT OF THEIR CRYSTAL — the growth law, applied to a whole family at once.**
  The family shipped scattering N independent seeds over the sphere and tracing a curve from
  each, so a plant was N disconnected patches wearing stub spindles at the plant ROOT, and the
  Fall reached the heart only at a curve's END. Three pieces fix it and **none of them is new
  geometry**: a **seed tree** (Prim on great-circle distance — and **the gasket's tree IS the
  GASKET**, since a child disc is inscribed against three discs it TOUCHES, which is why the
  self-similar species needed no rule at all), a **TRUNK** that is the Fall RUN BACKWARDS (so the
  rise cannot acquire a shape, a stride ceiling or a winding the Fall does not already have), and
  a **STEM** to every seed's first appearance plus a **lane anchor** so lane L hangs off the prism
  at lane L−1's midpoint — the point `Hop` steps across from, i.e. *the gap the cage is made of,
  now spanned by a limb instead of left open*. **Prim's insertion order IS a growth order**, so
  `parent < child` holds by construction rather than by a sort that can be wrong. Within a curve
  the chain already existed (`Emit` lays one prism per SEGMENT) — **only the ROOT was missing**.
  `TryNext` hands out the parent index and a CONNECTOR flag, deliberately NOT folded into
  `PrismAddress`, because a prism's POSE must stay a pure function of its own address. Six
  findings, and five outlive the family: **a greedy pick over a SYMMETRIC point set is decided by
  FLOAT WIDTH** (four saddles at exactly 0.847114625 from the tree, so float32 and float64 grew
  different plants with every statistical gate green — the §54 total-key trap from a second
  direction; fixed with a tolerant compare plus an index tie-break); **a limb must start at the
  prism it hangs off, NOT at the seed** (a gasket seed is a disc CENTRE while its curve starts on
  the RIM, so a stem from the centre spanned the parent's whole radius — 15.2 strides against a
  shipped worst of 4.7); **connectivity alone cannot see a WIRE**, since a star of trunks out of
  the heart is formally ONE component, so the gate also prices the bond LENGTH in the species' own
  stride; **a limb YIELDS to a ribbon** while two ribbons crossing is what a cage IS (every worst
  interpenetrating pair in three of four species was a limb inside a ribbon, and a refused limb
  prism costs nothing because the spindle roots at the nearest STANDING ancestor); **`TanR != 0`
  meant "dive" only while the heart was reached at the END of a curve** — the trunk is free space
  too and runs from the heart OUT, so every Fall gate read the plant's one connection to its
  crystal as the spiral that never arrived; and **when a curve gains a PROLOGUE, every gate that
  says "the curve's first" answers about the prologue** (six gates broke at once that had nothing
  to do with connectors). The plant is **12–32% limb**, so the budget went 2,800 → 4,150 = the
  budget at which the same amount of CURVE is laid as before — Apollonia excepted at 2,900,
  because **a gasket's form is finite and priced by `DiscMinRadius` rather than by a budget**.
  `Docs/ECOSYSTEM.md §55`; the `/flora` skill §2.
- **AND THE REACH IS THE ELEMENT CLAUSE THAT FAMILY HAD NEVER SPENT.** Its four elements grew
  plants of essentially the same SIZE — an **8% extent spread** against the Borromean membrane's
  Space plant spanning **222 to Time's 111** — because `Flora.ElementalReachScale` declines §51's
  assembly clause for a species exempt from the leaf law, on the grounds that *a family whose leaf
  IS its strut needs nothing*. **That sentence is true of the STRUT and false of the PLANT**, whose
  extent is the SHELL. It could not simply be taken: the shell is simultaneously the extent and the
  prism size, so a naive `shell *= reach` is a similarity that scales plant volume by `1/V` and
  EQUALISES all four — deleting the 14x spread and contradicting §51's own Mass clause. Three fixes
  fail in instructive ways: scaling POSITIONS only **dashes** Space's ribbons and **fuses** Mass's
  (the prism's length IS the step); paying on `LengthFactor` needs 2.09x for Mass, the same fusion
  from the other side; and paying UNIFORMLY on both cross axes — which is volume-exact — makes
  **MASS LESS CUBIC** (1.55 → 1.77), because Mass's `k < 1` shrinks the length that was already its
  smallest axis while the pay grows the one that was already largest. What ships pays it on the
  **THINNEST** cross axis, where Mass's growth pulls the axes together and Space's shrink drives
  them apart — one rule, each element **more itself** (Mass max/min 1.55 → **1.45**, Space 7.80 →
  **32.71**, extent span 1.09x → **1.72x**), with `world volume = (L·k)(T·V·k)(K·k) = L·T·K`
  **exactly** since `k³ = 1/V`, so no cell's ladder moves. **CHARGE and TIME are byte-identical** —
  the law read literally, since their identity is a state and a tempo rather than a shape — so
  Charge's armour fit needed no re-solve. Six authored numbers changed and all sixteen (species,
  element) pairs came back with **identical prism counts, curve counts and cumulative volume to the
  digit**, the same nine pre-existing gate rows and no new ones: the walk is untouched in normalised
  space and the claim, a similarity in both its radius and its positions, refuses exactly what it
  refused before. Stated cost: Space's thin axis is now 0.07–0.16 world units, under
  `PrismScaleAnimator`'s `minScale` and surviving on `AdmitTargetScale` alone. General rule: **a
  clause declined because "the leaf IS the extent" was declined about the STRUT, not the PLANT** —
  and when a pay can land on any of several free axes, **the axis is not a detail**.
  `Docs/ECOSYSTEM.md §56`.
- **A CELL MAY BE A COLLECTION RATHER THAN A FOREST.** The **Arboretum**
  (`_SO_Assets/Cell Configs/Arboretum Cell/`, a Cell Selector option) holds **one specimen of
  each of FIVE species in each element — twenty plants and nothing else**: the four Mandelbulb
  species and the **Borromean membrane**; no
  `EnvironmentPrefab`, no second producer, the cell IS its twenty the way the Lattice cell IS
  its twelve colonies. It exists because §56 made the four elements read as four different
  KINDS of plant and there was nowhere to see that; the Spawn Matrix bench lines the same
  species up for COMPARISON, this is a WORLD you meet them in. **The Borromean four earn their
  place by saying that sentence a different way**: theirs are the fleet's only elements each
  FITTED rather than typed (Time the anchor, Mass the chunkiest plate, Space the same volume at
  8.5:1 on twice the membrane, Charge a square slab fitted to its own shielded octahedra), a
  **19.6x** volume spread and a **2x** span spread across ONE species, said by a COMPACT surface
  that is FINISHED when it closes rather than by a fractal cage that traces until its budget
  runs out. They are also the one part of this cell it does NOT author:
  `author_borromean_flora_assets.py` owns that species' configs in every cell that grows it, so
  the Arboretum's generator READS the four it wrote — GUIDs off their own `.meta`, budget and
  plate through that tool's own table reader — and **fails by name** if they are missing, since
  a `SupportedFloras` entry pointing at a GUID nothing owns grows nothing, silently.
  `MaxLivePopulation 1` per config
  is a **cap, never a cull** — each plant keeps its authored growth quota and cannot spend it
  while it is the only one of its kind alive, and the seeder's whole remaining job is
  extinction recovery. **A per-plant budget is GEOMETRY on this family and is quoted, never
  re-authored**: the Lattice cell can cut a lattice plant to 30 prisms because a lattice plant
  is a TILE, and cutting one of these ships a truncated specimen (a Borromean plant's budget is
  its element's whole site table, because that surface CLOSES). Measured: **54,935 prisms,
  259,795 volume, 20 always-on heart colliders** (the Lattice cell's is 1,080), specimens 108 u
  to **294 u** across, per-prism volume 0.07 to 72.87, ceiling 79,700 against Atlantis' ~69,000
  and Lattice's 82,400 — the four Borromean specimens **grew the garden 1.9% in prisms and 9.5%
  in volume** and together weigh less than the single heaviest Mandelbulb specimen; Restless
  0.35x the mature garden and FrenzyEXIT **above** it, since a hard-capped garden can only ever
  be frozen by TRAIL and must release intact. Authored by
  `Tools/Build/author_arboretum_cell.py` (`--check`), which GROWS all sixteen Mandelbulb
  specimens through the shipped rule rather than trusting a typed number, reads the Borromean
  four out of their own measured table, and appends the config to Menu_Main's
  `Cell.CellConfigs` — **`CellSelectorToy` authors no cell list**, it reads
  `Cell.AvailableConfigs`, so adding a world to the selector is an edit to the cell's own
  rotation and to nothing else. Two defects it surfaced generalise. **A species key with a
  SPACE in it can never match a de-spaced name**: `author_lifeform_heart_sizes.py` resolves a
  cell-config variant by prefab GUID and a canonical one by asset NAME through `species_of`,
  which strips spaces, so `"Coral Bloom"` — its only two-word key — missed, and those four
  canonical assets **had never been sized by the tool that owns their heart** (1.514 against
  the band's 1.433, visible only once a second cell copied them). *When one script resolves the
  same identity two ways, the two will disagree, and the name-based half fails silently.* And
  **`EnvironmentPrefab == null` is how a world is BUILT, not what it CONTAINS** — the selector
  labelled every environment-free config *"no environment"*, true of Barren and false of
  Lattice, the Arboretum and every Rampage/Tollway/Wrecking Ball cell; §36.10's rule met by its
  THIRD reader, so `Cell.BareCanvasConfig`'s predicate is now the static `Cell.IsBareCanvas`
  and the rest read *"grown, not laid"*. Stated gap: it shows in the selector as a bare station
  with **no scale model**, because `CellMiniatureBuilder` strides the ENVIRONMENT generator's
  output and a grown world has no lays until it has grown them — Lattice and Barren have the
  same gap; the arcade card's `ModePreviewPlantingModel` would fix all three.
  `Docs/ECOSYSTEM.md §57`.
- **THE FOUR ELEMENTAL IDENTITIES OF A PLANT — one rule, and a species does not get to invent
  them.** **CHARGE armours its leaves** (a state, §35); **MASS is the most cumulative prism volume
  in the most CUBIC leaf** (x, y and z closest together); **SPACE is the highest ASPECT RATIO** —
  its long axis trades that cumulative volume for the **bounding volume of the assembly**; **TIME
  is the fastest clock**, growing *and* reproducing fastest (§38). Two are about SHAPE and two are
  not, which is the design rather than an accident of what was easy: Charge's identity is a state
  and Time's a tempo, so **Charge and Time take the species' own authored form** and only Mass and
  Space restate it — a species authors ONE leaf and the four elements spend it four ways
  (`FloraElementalForm`). **The four are a REDISTRIBUTION, never an inflation**, and that is what
  makes a fleet-wide leaf law shippable at all: the four volume multipliers average to **exactly
  1** and the aspect term is **volume-exact by construction** (a unit-volume shape vector raised to
  any power still has volume 1), so a mixed-element forest holds the mass it held before and **no
  cell's volume phase ladder moves** — a law that gave Mass more material would have landed on
  Rampage's play-tested ladder, on Hesperides and on the Lattice cell. The assembly half falls out
  of the same sentence with **no new constant**: `ReachScale = volume^(-1/3)`, i.e. a plant
  spending a fixed amount of material, so Space reaches **1.35x** where Mass draws in to **0.82x**,
  and the two dials cannot drift because there is only one. Like its two predecessors it **cannot
  be authored** — the leaf is authored per CONFIG while the element is ROLLED per plant — so it is
  resolved at `LifeForm.Initialize` and scoped to `Flora`; it needed one new seam,
  **`LifeForm.OnElementResolved()`**, because `ResolveShieldPeriod` runs AFTER `BindEmbeddedParts`
  and a leaf applied there leaves the SEED prism at the pre-element size (§43's ordering argument,
  met from the other side). **The constants are MEASURED off the eleven species that already
  shipped the law** — three lattice species authoring four fitted leaves each, plus the eight
  Hesperides phyllotactics sharing ONE authored ladder — with **one vote per FAMILY** (eight species
  sharing one table is one decision, not eight) and a MEDIAN rather than a mean, because the fleet
  agrees on the DIRECTION of both dials and on the magnitude of the aspect (Mass 0.39–0.52, Space
  1.34–2.20) while disagreeing 3.3x on Mass's volume. A species whose prism size is dictated by its
  growth rule is **EXEMPT and CHECKED instead** (`PrismSizeFixedByGrowthRule`, the guard §40 kept
  with no reader, now on its third job): a lattice bonds at offsets in absolute units, so it states
  all four clauses in its own fitted data and `Tools/Build/measure_flora_elemental_form.py` verifies
  every one — which is also what keeps this law and `fit_shield_clearance.py` from fighting over a
  Charge leaf. The shipped C# is **compiled and RUN** offline against an independent transcription
  (`Tools/Build/flora_form_harness/`, worst disagreement 2.4e-07) with four negative controls, which
  is why the law lives in its own pure file rather than inside `Flora`. Use the **`/flora` skill
  §5**. `Docs/ECOSYSTEM.md §51`.
- **Endogenous selection only.** When evolution lands, fitness is **survival itself**
  (starvation/predation/reproduction cost), never a designer-scored fitness function — the line
  between artificial life and a mere optimizer, identical to "don't cheat emergence."
  **LEVEL IS RETIRED — a lifeform is its species and its ELEMENT, and nothing else.** There is
  no per-individual acquired growth of any kind, and BOTH earlier attempts at one are retired
  together; do not reintroduce either. The spawn-time **ROLL** (`LifeformLevelSpread`) handed a
  lifeform the record of a life it had not lived — the same mistake as a scripted fitness
  function. The **EARNED** level that replaced it (`Flora.NotifyReproduced`, `Fauna.NotifyFed`
  per `FeedsPerLevel`, `InitialLevel`, `BodyScalePerLevel`, `LeafScalePerLevel`) was the more
  defensible of the two and still failed on two counts: it made "how big is this thing" a hidden
  per-individual HISTORY the player could not read off the species — two creatures of the same
  species and element were different sizes for reasons only the simulation knew — and the three
  LATTICE flora (gyroid / SchwarzP / quasicrystal) could never honour it at all, because they
  bond at offsets measured in absolute local units and **two prism sizes cannot tile one
  lattice**. `Flora.PrismSizeFixedByGrowthRule` (true on `AssembledFlora`) is deliberately KEPT
  with its reader gone, as a standing guard against that rule returning on a future growth path.
  What survives is the part that was always the point: fitness is survival, and feeding still
  MATTERS — it just pays out as a POPULATION rather than as a bigger individual.
  `ILifeFormEntity.Nourish()` is that payout and is deliberately a **FOOD-WEB event, never a
  size**: a creature's starvation clock resets and its birth counter advances; a plant's growth
  quota advances toward its next seeding. (The Squirrel's Space-5 "Shepherd" ally branch,
  `VesselWitherLifeformByCrystalEffectSO`, now feeds a lifeform through exactly that door
  instead of levelling it.) Offspring inherit the ELEMENT, which is the whole of what there is
  to inherit. Two consequences to carry: a flora species with `GrowthPerOffspring = 0` cannot
  breed at all (only 29 of 85 flora configs breed today); and a cell whose ladder was authored
  against the old spread's expected volume multiplier now boots that much lighter (Rampage:
  4.31× on the cactus, 3.21× on the phyllotactics — deliberately left as play-tested, since
  Frenzy arriving LATER is the safe direction; `Tools/Build/rampage_intensity.py` prints the
  re-measure note). `Docs/ECOSYSTEM.md §40`.
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
  **That carve-out is about the TOOL scenes, and five GAMEPLAY scenes carried one anyway** — 2v2
  Co-op, Crystal Capture, Cellular Duel, Freestyle MP and Joust, i.e. the five OLDEST multiplayer
  scenes, against none of the ten newer ones. It is not a harmless backdrop there: `MembraneBase`
  scaled to **1600** at the world origin, drawn with `SkyboxModelGraphMaterial` at
  `RenderType: Opaque`, queue 2000, `_Cull: 2` on an inward-facing sphere — an **opaque,
  depth-writing shell around the playfield**, so everything past ~1600u from the origin is
  occluded and the far side of the arena reads as *black*, which is how it was reported ("looks
  like it's not rendering or culling"). It is also **redundant**: every scene already sets the same
  `m_SkyboxMaterial`, which renders behind everything at infinite depth — *a skybox costs no depth
  and occludes nothing; a mesh pretending to be one does both.* Switched off (not deleted) by
  `Tools/Build/disable_scene_skybox_model.py` (`--check`); the tool scenes keep theirs. General
  rule: **an asset documented as belonging to one class of scene will turn up in another**, so
  audit for it rather than trusting the carve-out.
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
