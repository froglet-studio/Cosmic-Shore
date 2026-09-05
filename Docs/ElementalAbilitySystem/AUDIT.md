# Elemental Ability Upgrades — Systems Audit

**Date:** 2026-07-14 · **Branch:** `claude/elemental-ability-upgrades-tiewmz` (== bleeding-edge `06531a80`)
**Companion docs:** `ARCHITECTURE.md` (the proposed fundamental system), `BACKLOG.md` (sequenced work plan).

**Scope.** Everything the "level-5 elemental ability upgrade" feature touches: the elemental/resource
core, the R_ vessel-action architecture, the Sparrow (guns, projectiles, pools, turret mode, boost,
HUD, animation, prefab wiring), the skyburst/AOE pipeline, prism shielding/stretching, trail
continuity for the barrel roll, the abandoned branch `claude/vessels-review-completion-5weidk`, and
the git history of the gun breakage.

**Confidence labels.** Findings marked **CONFIRMED** were adversarially verified (independent
re-read attempting to refute) or hand-verified against HEAD during this audit, with file:line
evidence. Findings marked **REPORTED** come from a single audit pass and should be re-verified at
the moment of fixing (all carry citations).

---

## 1. The elemental core today

### 1.1 What exists and works

- **Levels.** `ResourceSystem` (plain MonoBehaviour on every vessel, `ResourceSystem.cs:10`) stores
  base levels per element as normalized floats; effective level = clamp(base + Σ temporary
  modifiers, −0.5, +1.5) (`ResourceSystem.cs:168-173`). `GetLevel(element) =
  floor(effective × 10)` → **integer in [−5, +15]** (`ResourceSystem.cs:175-176`).
  **Level 5 = normalized 0.5 = the all-five-petals-white flower state** — the UI math in
  `ElementalBarsConfigSO.DistributePetalValues` reproduces this exactly. The spec's trigger
  condition already has a precise, displayed, deduped representation.
- **The one signal.** `ResourceSystem.OnElementLevelChange(Element, int)` — a per-vessel C# event,
  emitted only on integer-level transitions through the single choke point `EmitElementLevel`
  (`ResourceSystem.cs:301-308`). Subscribers today: `ElementalFloat.ScaleValueWithLevel`,
  `SilhouetteController` → `ElementalBarsView` petals, `VesselAnimation` shape keys. This is the
  natural detection point for a level-5 threshold crossing.
- **Mutators.** Crystals (`AdjustLevel`, permanent), comeback (`SetElementLevel`, permanent —
  see §1.2 bug), temporary decaying debuffs (`ApplyElementalEffect`, e.g. danger prisms −0.5 for
  4 s), passive drift of the base back into the [0,10] resting band at 0.05/s
  (`RecoverBaseLevels`). Level 5 sits inside the resting band, so once earned it is stable
  absent active debuffs.

### 1.2 What is broken (all CONFIRMED by adversarial verification)

1. **The entire SO-side quantitative element→ability layer is dead.**
   `ShipActionSO.Initialize` has the `ElementalFloatBinder.BindAndClone` call commented out with
   "TODO : Not sure what it does" (`VesselActionSO.cs:11-13`). Every `ElementalFloat` inside an
   action data-container SO never binds, never subscribes, and stays frozen at its serialized
   value. **The Sparrow's SPACE→gun-speed mapping is already authored in data**
   (`FullAutoAction.asset`: `speedValue {Enabled:1, Min:1500, Max:4000, element:3=Space}`) **and
   does nothing.** Same for `OverheatingAction.heatDecayRate` (Charge — that asset and its SO were
   deleted in 2026-08 with the overheat mechanic), Rhino's
   `GrowTrailAction.maxSize` (Mass), Serpent's `ConsumeBoostAction.boostMultiplier` (Time,
   also `Enabled:0`).
2. **The binder is doubly broken even if re-enabled.** `ElementalFloatBinder.cs:33` reflects
   `GetProperty("Ship")` — the property was renamed `Vessel`; the `?.` swallows the null → silent
   no-op. The clone at line 31 also drops `Enabled/Min/Max/element`. A "just uncomment it" fix
   would change nothing, silently. (Also: binding a *shared* asset would be wrong anyway —
   last-initialized vessel would drive everyone's values. The fix is executor-side live reads,
   not resurrecting the binder. See `ARCHITECTURE.md`.)
3. **`ElementalComebackSystem` clobbers crystal progression.** Every 1 s tick it calls
   `rs.SetElementLevel(baseline + bonus)` where `baseline` was snapshotted at turn start
   (`ElementalComebackSystem.cs:122-126, 183-188`). Any crystal-earned `AdjustLevel` gain mid-turn
   is erased within ≤1 s in any mode with a comeback profile. **Crystal-driven progress toward
   level 5 is impossible while comeback is active.** Fix: comeback must composite through the
   modifier layer (or apply deltas), never write the base.
4. **Element levels are 100% local — no replication path.** `ResourceSystem` has zero
   NetworkVariables/RPCs; impact effects mutate levels un-gated on every peer from local physics;
   comeback runs independently per peer. Today's consumers are cosmetic-ish, so drift is
   tolerable. The proposed unlocks (piercing, shielded prisms, domain-sparing explosions) change
   **which prisms get destroyed** — conserved, world-visible mass. Divergent unlock state across
   peers permanently desyncs the prismscape. Unlock state needs an authoritative owner/server
   derivation replicated via NetworkVariable on `VesselStatus` (NetworkBehaviour), mirroring the
   `Player.NetDomain` single-writer pattern.
5. **`SilhouetteController` loses element-bar updates over a disable/enable cycle** (OnDisable
   unsubscribes; OnEnable only re-subscribes the resource gauges; the element subscription only
   exists in `Initialize`, which hard-rejects re-init). Latent today (vessels are never toggled),
   but it will bite the moment anything pools/hides a vessel.

### 1.3 Initial-level policy is inconsistent (CONFIRMED — design decision required)

- Single-player arcade: `MiniGame.ResourceCollection = new(.5f, .5f, .5f, .5f)` (`MiniGame.cs:61`)
  → **vessels spawn at level 5 in all four elements** → every level-5 unlock would be ON at spawn.
- Multiplayer: `ClientPlayerVesselInitializer.InitializePair` never calls `SetResourceLevels` →
  vessels start with an empty dict (effective level 0) → unlocks unreachable until crystals.
  The unlock feature forces this policy decision (see `BACKLOG.md` § Open decisions).

### 1.4 Misc core findings

- **No threshold/unlock mechanism exists anywhere** — the level-5 tier is genuinely new. Nearest
  artifact: `AdjustLevel`'s "integer level rose" bool return, ignored by all callers.
- `ElementPipsView` is orphaned dead code (false doc comment; nothing builds/calls it).
  Superseded by `ElementalBarsView`. Candidate for deletion.
- Inspector test-harness sliders on `ResourceSystem` pin base levels every frame when non-zero and
  ship in builds (REPORTED).
- The dead `ResourceEvents { AboveThreeQuartersAmmo, AboveHalfAmmo }` pipeline is populated but
  never raised (REPORTED).

---

## 2. Why the Sparrow's guns are broken (forensics)

Full-history forensics (`git fetch --unshallow`, 7,248 commits) plus code verification. Ranked:

### #1 — Pool-replenished projectiles are never DI-injected → NRE kills the fire loop (CONFIRMED, root cause)

- `Projectile` has `[Inject] AudioSystem audioSystem` (`Projectile.cs:14`) dereferenced
  **unguarded** in `LaunchProjectile` (`Projectile.cs:116`).
- `GenericPoolManager.CreateFunc` is a bare `Instantiate` with no injection
  (`GenericPoolManager.cs:174-179`), and `BufferMaintenanceAsync` keeps creating instances long
  after the one-time vessel-spawn `InjectRecursive` pass (`GenericPoolManager.cs:201-238`).
- Sparrow full-auto: 2 muzzles × 30 Hz against a 25-deep buffer → replenishment begins within
  ~1 s of held fire; the next `Get` returns an uninjected instance → NRE →
  `FullAutoActionExecutor.FireLoopAsync`'s catch **exits the fire loop**. Symptom: healthy opening
  burst, then the gun goes dead; re-press dies again almost immediately. Skyburst (5-capacity
  pool) NREs by roughly the second missile.
- **Introduced** by `242ca2830` (2026-02-26, the AudioSystem→Reflex DI conversion).
  **The fix already exists**: `f1f278ab4` "fix(projectile): inject DI into pool-replenished
  projectiles" (2026-05-29) on the never-merged branch `origin/claude/fix-gun-focus-vvYxp` —
  verified present on that branch and absent from HEAD. Its commit message describes this exact
  crash. Cherry-pick it (BACKLOG Phase 0.1).
- Related trap: `4861801bd` (in HEAD) fixed client-side vessel injection but explicitly does not
  cover pool replenishment. And `origin/claude/falcon-brittlestar-reintro-oyx1q1` carries a
  `maxSyncPrewarm=8` pool cap **without** `f1f278ab4` — merging it as-is would make the NRE fire
  after ~8 shots, i.e. strictly worse.

### #2 — Skyburst missiles become permanent duds via pooled collider state (CONFIRMED)

`ProjectileDetonatorSO.DetonateAsync` disables the projectile's root collider
(`ProjectileDetonatorSO.cs:65-69`, `DisableColliderNow` defaults true in the wired assets) and
**nothing re-enables it on pool reuse** (verified: no re-enable in `Projectile.OnEnable`/
`Initialize`/`LaunchProjectile` or the pool's `OnGetFromPool`). Every prism hit permanently
converts that pooled missile into a fly-through-everything dud that only air-bursts at timeout.
With a 5–20 pool and `friendlyFire: 1` (the missile detonates on the shooter's own trail —
`SkyBurstProjectile.prefab:4962`), the entire pool corrupts within seconds of combat.

### #3 — Detonator's un-cancellable delayed return can steal a re-issued live projectile (CONFIRMED)

`DetonateAsync` awaits `ReturnDelay` then calls `proj.ReturnToFactory()`
(`ProjectileDetonatorSO.cs:107-111`) with no cancellation link to the projectile's flight
generation — if the instance was already returned and re-issued, the stale continuation yanks a
live projectile back into the pool mid-flight. Needs a flight-generation counter (or per-flight
CTS) guard.

### #4 — Shared-SO state on `SparrowModeSwitchingFireSO` (CONFIRMED mechanism)

The mode-switching SO keeps `_active/_registry/_isHeld` + the base `vesselStatus` **on the shared
asset** (`SparrowModeSwitchingFireSO.cs:15-17`, re-`Initialize` on every press at line 21);
`ShipHelper.InitializeShipControlActions` never clones for human vessels (`VesselHelper.cs:31-36`).
With ≥2 human Sparrows (input actions broadcast to all clients via
`R_VesselActionHandler` ServerRpc→ClientRpc), interleaved Start/Stop stomps `_active` → runaway
infinite fire or guns that die mid-hold. `AIPilot` avoids this by cloning its SOs
(`AIPilot.cs:262-270`) — the human path is the hole. The unmerged BrittleStar work hit this same
class of bug (`c8b244243` decoupled its SO from Sparrow's).

### #5 — Cross-vessel mode flip via the global `stationaryModeChanged` event (CONFIRMED)

`OnStationaryModeChanged` (`SparrowModeSwitchingFireSO.cs:43-51`) carries no vessel identity —
**any** vessel's turret toggle (replicated to all clients) hot-swaps the held fire mode of every
Sparrow currently shooting. Textbook instance of the "global SOAP event as per-vessel signal"
anti-pattern.

### #6 — Gun aims along the Gun component's forward, not the muzzle's (CONFIRMED)

`Gun.FireSingle`: `direction = customDirection ?? transform.forward`, spawn at
`containerTransform.position` (`Gun.cs:157-158`) — muzzle orientation is discarded. Fix exists
unmerged: `dad9f2ffc` on `claude/fix-gun-focus-vvYxp`. Presents as "guns don't hit what they aim
at."

### #7 — Latent traps in the fire path (CONFIRMED unless noted)

- **`Gun._onCooldown` latch**: set `true` unconditionally, reset coroutine only when
  `!ignoreCooldown` (`Gun.cs:49-66`). Both Sparrow executors always pass `ignoreCooldown:true`,
  so the latch sticks `true` forever — any future caller using the default is permanently blocked.
- **Domain snapshot**: `Gun.Initialize` captures `domain = vesselStatus.Domain` once
  (`Gun.cs:33`) — violates the locked "never snapshot domain" rule; after any live domain change
  all projectiles carry a stale `OwnDomain`, inverting friendly-fire checks.
- **Full-auto bullets stay parented to the moving muzzle all flight** (REPORTED) —
  `FullAutoActionExecutor` omits `detachAfterSpawn`; bullets swerve with the shooter. The skyburst
  path already has the world-anchor + detach fix; full-auto was missed.
- **Skyburst ammo starvation** — RETRACTED (false positive, re-verified 2026-07-14): the crystal
  restock IS wired. `SparrowVesselChangeResourceByCrystalEffect.asset` (`_resourceIndex: 0,
  _resourceAmount: 1, _overrideAmount: 1`) sits in `SparrowImpactorDataContainer.asset`'s
  `vesselCrystalEffects` — any crystal impact refills Missiles to full (2 rockets). The economy
  is 2 rockets per crystal, as the ability card promises.
  **SUPERSEDED 2026-09** — that asset is now DELETED. The Sparrow's missiles reload by destroying
  hostile prisms (`VesselRearmOnPrismDestruction`, 0.02 per prism) and the omni crystal grants an
  elemental-debuff ward instead. The finding above is kept as the dated record it is; do not read
  its present tense as current wiring.
- **Dead `Gun` with `projectileFactory: {fileID: 0}`** on the prefab (`Sparrow.prefab:212-227`,
  since Oct 2025) — unreferenced rewire trap; any `FireGun` call on it NREs.
- Pool return is **delegated entirely to impact end-effects** (`Projectile.cs:197-198`); a
  container with an empty end-effect array leaks projectiles silently (REPORTED; Sparrow's two
  containers are wired today).
- `Projectile` uses `meshRenderer.material` (clones materials) for spike opacity — violates the
  sharedMaterial+MPB rule on pooled objects (REPORTED; Sparrow's projectiles are not spikes).

### Timeline (condensed)

| When | What |
|---|---|
| 2024-08 | Sparrow created (pulsefire + skyburst) |
| 2025-09 | Legacy ShipActions → R_ migration (`056933803`); pooling introduced (`9267d1ecb`) |
| 2025-10 | Turret mode + `ModeSwitchingFire` born; the null-factory orphan Gun appears (`81f3a7dd1`) |
| 2026-01 | `f8ee769dc`: AIPilot clones ability SOs; humans still share raw assets |
| **2026-02-26** | **`242ca2830` DI conversion opens the pool-injection hole → guns break under sustained fire** |
| 2026-05-29 | `f1f278ab4` fixes it on `claude/fix-gun-focus-vvYxp` — **never merged** (also `dad9f2ffc` muzzle aim, `676a8f994` right-stick population) |
| 2026-06-12 | `4861801bd` (in HEAD) fixes client vessel injection — not pool replenishment |
| 2026-07 | Abandoned `vessels-review-completion-5weidk` — contains **no gun fixes** |

No Docs/ file records the gun breakage; the only written record was the unmerged fix branch.

---

## 3. Object-pool audit (summary)

1. **No DI at creation** (root of §2#1). Affects `ProjectilePoolManager` (pools `Projectile`,
   which has `[Inject]`); does not affect `BlockProjectilePoolManager` (pools `Prism`, no injects).
2. **Stale state on reuse**: root collider `enabled` (§2#2) and material instances are not reset;
   velocity/charge/scale are re-set per shot correctly. `Prism.ResetState` does not clear
   `IsShielded/IsSuperShielded/IsDangerous` → shield state leaks across prism pool reuse
   (REPORTED — matters once MASS-5 ships shielded prisms).
3. **Weak double-release protection**: `ObjectPool` built with `collectionCheck:false`; the only
   guard is `activeSelf` in `ProjectilePoolManager.Release` — which cannot catch the §2#3
   stale-return race on a re-issued (active) instance.
4. **Return-path coupling**: pool return rides impact end-effect SOs; leaks are silent.
5. **Turret-prism pool is one-way**: `BlockProjectileFactory.ReturnBlock` is fully commented out —
   *correct* for mass conservation (blocks become permanent prisms), but `_activeObjects` grows
   unboundedly and a hypothetical `ReleaseAllActive()` would suck anchored world prisms out of the
   world (REPORTED).
6. **Both Sparrow projectile factories register only `ProjectileType.Normal`** — any future
   `energy > 0` variant returns null and silently doesn't fire (REPORTED).

---

## 4. Per-element state vs. the Sparrow spec

### SPACE → gun range · L5 piercing

- Range today = `speed × projectileTime × 2/π` (cosine ease, `Projectile.cs:183-184`): full-auto
  ≈ 286 u, skyburst ≈ 229 u. The Space→speed mapping is **already authored** in
  `FullAutoAction.asset` but dead (§1.2). `projectileTime` is a plain float (0.3 s).
- **Inversion discovered (CONFIRMED): full-auto bullets already pierce today.** The full-auto
  impact container has only a damage effect for prisms (no stop/return effect), so bullets fly
  their full lifetime through everything. The spec's *default* — destroy on first impact — is the
  **missing** piece; piercing is currently free. Implementing the spec means adding the
  first-impact stop as the default and gating it OFF at Space ≥ 5.
- Attach points: scaled speed/lifetime computed at fire time in the executors from
  `GetLevel(Element.Space)`; piercing as a per-shot flag on `Projectile` set in
  `Gun.FireSingle`/`Initialize`, or a `StopOnFirstPrismImpactEffectSO` in the container gated on
  the shooter's live level. Whichever path stops the projectile must NOT reuse
  `DisableColliderNow` until §2#2 is fixed.

### TIME → boost speed · L5 barrel roll

> **SUPERSEDED (2026-08).** Both halves of this section shipped and were then re-scoped:
> TIME's quantitative IS boost speed (`VesselTransformer.CurrentBoostAmount()` reads
> `Multiplier(Element.Time)`), the input gap below was closed, and the barrel roll shipped as
> `BarrelRollController` — but it is no longer the L5 upgrade. Overheat is deleted, the boost is
> indefinite, the roll is BASE kit, and TIME-5 is now **Elemental Ward** (elemental-debuff
> immunity while boosting). Current design:
> `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md`. The findings below are kept
> as the dated evidence that motivated the work.

- Sparrow boost today = flat `VesselStatus.boostMultiplier` 4.0. `OverheatingAction` wraps the
  common `BoostAction` (pure `IsBoosting = true`); **no elemental hook anywhere on the Sparrow's
  boost path**. `VesselTransformer.ThrottleScalerMultiplier` (an ElementalFloat) is never bound by
  anything, and `SingleStickVesselTransformer.MoveShip` doesn't even read it.
- **Input gap (CONFIRMED)**: no input strategy ever writes
  `Left/RightNormalizedJoystickPosition` — the exact field family needed for "stick at the circle's
  perimeter." Touch already computes the radially-clamped vector and discards it; gamepad's
  deadzone-processed stick clamps magnitude to exactly 1.0 at the rim. The unmerged `676a8f994`
  (gun-focus branch) populates these fields — adapt it. Do not use
  `EasedRightJoystickPosition` for perimeter detection (per-axis ease breaks diagonals:
  magnitude ≈ 0.79 at full diagonal).
- **The right stick is free on the Sparrow** (single-stick vessel — left stick flies). CW/CCW =
  `sign(stick.x)` per spec. The left stick doubles as flight control — using it for the
  orthogonal nudge requires attenuating rotation input for the roll duration.
- **Displacement channel exists**: `VesselTransformer.ModifyVelocity(vector, duration)` →
  `velocityShift`, added on top of `speed * Course` by both MoveShip implementations — the
  codebase's sanctioned "displacement orthogonal to travel while forward motion continues."
  Camera already handles lateral motion (`_lateralDominance`); owner-authoritative
  NetworkTransform replicates the displacement. Caveats: the modifier's cosine profile ends at
  0.5× (velocity step at expiry — ramp it); `velocityShift` itself is not replicated, so remote
  bridging-prism math must derive travel from position deltas or the blockRotation route below.
- **Visual roll**: roll the `OrientationHandle`/model child (or a new Animator state) — never the
  root (camera corkscrews; `accumulatedRotation` is a slerp target, so 360° ≡ identity). The
  prefab's animator has Pitch/Yaw/Roll/Throttle params but **no roll state**, and the Sparrow
  actually runs `MantaAnimationContoller` — `SparrowAnimationController` is dead code with a
  `"Boost"`/`"Boosting"` param bug (REPORTED).
- **Bridging prism**: trail prisms are positioned along the flight path but oriented by
  `vesselStatus.blockRotation` (= facing, captured every frame, replicated via `n_BlockRotation`).
  Cleanest: a transformer-level **blockRotation override** during the roll
  (`LookRotation(actualTravelDir)`) so the ordinary spawn loop lays travel-aligned blocks and
  remote peers get it for free; alternative: direct spawn through the public
  `VesselPrismController.PrismSpawnChannel` with a custom rotation (the painting-toy/AOE
  precedent). Bloom-in is preserved either way (continuity law).

### MASS → turret prism stretch · L5 shielded prisms

- Sizing today: `prism.transform.localScale = so.BlockScale` — a raw write
  (`FullAutoBlockShootActionExecutor.cs:115`; asset `{0.8, 0.5, 5}`). Stretch = scale the long
  z-axis by a Mass multiplier at fire time; volume (x·y·z of lossyScale) then feeds
  `Cell.LiveVolume` automatically — the stretch buff quantitatively feeds the ecosystem spine for
  free. Non-uniform scale is native end-to-end (per-axis clamps, Burst per-component lerp).
- **Turret prisms currently bypass `Prism.Initialize` entirely** (REPORTED, high-confidence):
  never registered in `PrismSpatialIndex` (invisible to Burst AOE, growth occupancy,
  fauna density queries), never bound to a Cell (no `LiveVolume` contribution), and **pop in with
  no bloom — a continuity-law violation**. This must be fixed before Mass stretch/shield ships
  (route through `TargetScale` + `Initialize`).
- Shielding: the canonical API is `Prism.ActivateShield()` / flag-before-Initialize
  (`prismProperties.IsShielded = true` pre-`Initialize`, the trail-spawner pattern). Regular
  shield = one-hit ablative armor; steals pop it; **fauna can still eat shielded prisms via
  devastate** — so MASS-5 must grant the *regular* shield, not SuperShield (which nothing in the
  game can remove — an ecosystem freeze vector).
- **Collider budget**: RESOLVED for the collider itself — shields no longer swap in a convex
  MeshCollider; the engaged shield keeps the authored `blockCollider` (a trigger), so it IS
  collider-LOD-cullable and there is no convex cook. 14 shielded prisms/sec from a MASS-5 turret
  is now the same collider cost as 14 plain prisms/sec. (Interaction is at authored box size;
  shape-precise shielded collision is the planned three-LOD follow-up.) Separately,
  `PrismOctahedronShield` is still auto-added to every prism (REPORTED — pre-existing background
  cost); it is ticked centrally only while a shield is transitioning, not per-frame at rest.

### CHARGE → skyburst blast radius · L5 spare own domain

- **The blast-radius pipe already exists and is inert (CONFIRMED)**:
  `targetScale = Lerp(MinScale, MaxScale, Clamp01(proj.Charge))`
  (`ProjectileDetonatorSO.cs:82-83`) — but `FireGunActionExecutor.Fire` passes the literal `0`
  (`FireGunActionExecutor.cs:131`) and all skyburst effect assets author min == max == 100.
  Quantitative Charge = pass `GetLevel(Element.Charge)/10` clamped as the charge argument + author
  real min/max ranges. Zero new plumbing.
- **Domain reality check (CONFIRMED)**: the AOE explosions **already spare own-domain prisms**
  (`affectSelf: 0` on both AOE prefabs; own-domain prisms get a 2 s shield instead — batch path
  `PrismSpatialIndex.ProcessExplosionFrame:924-932`, physics path
  `ExplosionImpactor.ExecuteCommonPrismCommands:167-174`). What destroys your own prisms today is
  the **direct-hit path**: `friendlyFire: 1` on `SkyBurstProjectile.prefab` defeats
  `DisallowImpactOnPrism`, and `SkyBurstProjectileDamagePrismEffectSO` damages the struck prism
  unconditionally before detonating. **The L5 gate belongs on the direct-hit damage** (and
  optionally: below L5, flip AOE `affectSelf` to true so the upgrade is felt more broadly — see
  BACKLOG open decisions).
- **Prerequisite (documented gap)**: `PrismSpatialIndex` stores prism domain at registration and
  `UpdateDomain` has no callers — **stolen prisms keep stale domains in the AOE cold data**
  (`Docs/SPATIAL_INDEX.md` §214-220 documents this). "Own domain" must be live before a
  domain-sparing unlock can be correct.
- **Design-law check: no conflict with the locked "danger prisms are not safe to their own
  domain" invariant.** That law governs Prism→Vessel danger effects; this feature gates
  Explosion→Prism destruction — a different cell of the impactor×impactee matrix that is
  *already* domain-gated in shipped code (`affectSelf`). Keep the gate strictly in the
  explosion/projectile layer; never add a domain check inside `Prism.Damage` (shared sink for
  fauna/danger/trail interactions). A L5 Sparrow that stops destroying its own prisms is still
  hurt by its own danger trail — the two rules act in opposite directions and coexist.
- Explosion pipeline hygiene (REPORTED): AOE objects are Instantiate/Destroy per detonation
  (unpooled); both AOE prefabs have `explosionImpactorDataContainer: null` (kills explosion→vessel
  effects; `AOEExplosion.prefab` carries stale pre-refactor serialization); the conic skyburst AOE
  bypasses the Burst batch path (physics trigger storm, 50 s live collider) and leaks a cloned
  material per shot. All pre-existing; Charge-scaled bigger blasts will exercise them harder.

---

## 5. Sparrow prefab wiring defects (beyond the guns)

| # | Defect | Where | Status |
|---|---|---|---|
| 1 | **Element flowers dead on Sparrow HUD**: prefab serializes `elementPips:` but the field was renamed `elementBars` with no `[FormerlySerializedAs]` → deserializes null → `InitializeElementBars` early-outs. The vessel that pilots the elemental feature **cannot display element levels** | `Sparrow.prefab:1325` vs `SilhouetteController.cs:28` | CONFIRMED |
| 2 | Orphaned `ElementPipsView` GO on the HUD (superseded, driven by nothing) | `Sparrow.prefab:3150-3199` | CONFIRMED |
| 3 | Dead `Gun` with null factory | `Sparrow.prefab:212-227` | CONFIRMED |
| 4 | `OverheatingActionExecutor` turn-end wired to `EventOnResetForReplay` instead of `EventOnMiniGameTurnEnd` → heat state not reset at turn end | `Sparrow.prefab:575` | **MOOT (2026-08)** — overheat removed entirely; the executor, its SO, its asset and the Heat resource are deleted. See `SPARROW_AFTERBURNER.md`. |
| 5 | `TrailScaleModulator.controller: null`; its `GetComponent` fallback looks on the wrong GO → overheat trail-squeeze silently no-ops | `Sparrow.prefab:605`, `TrailScaleModulator.cs:14-17` | **MOOT (2026-08)** — the component instance went with the overheat executor GameObject. The class had zero callers of `Apply()`/`Revert()` regardless, so it was inert either way; the script file is left for a general dead-code sweep. |
| 6 | `SparrowPrismController.skimmer` points at the INACTIVE DummySkimmer (scale 15) instead of the active one (scale 60) → prism `waitTime` computed from the wrong skimmer | `Sparrow.prefab:66-93, 1592-1620` | REPORTED |
| 7 | `FireGunActionExecutor.defaultAmmoIndex = 2` (FullAuto resource) while SkyBurst uses index 0 → HUD ammo readout wrong until first shot | `Sparrow.prefab:497` | REPORTED |
| 8 | "Redirection" ability card has no gameplay counterpart; the actual 4th ability is the turret-stance toggle. GO naming rot ("ExhaustBarrage" hosts the turret toggle; an unused ExhaustBarrage resource + orphaned `SparrowExhaustProjectile.prefab` linger) | `SO_Class_Sparrow.asset`, prefab | REPORTED |
| 9 | Sparrow runs `MantaAnimationContoller`; `SparrowAnimationController` is referenced by nothing (and buggy) | prefab:1288-1307 | REPORTED |
| 10 | Legacy half-dead code flagged for cleanup: `StopGunsAction`/`ToggleProjectileActionWrapper` (zero references), `VesselStatus.GunsActive` (written, never read), `Gun.DetonateProjectile` stub, `ExplodableProjectile` (fully commented out), `AIGunner` (husk), `LoadedGun` (environment spikes only) | various | REPORTED |

---

## 6. Abandoned branch `claude/vessels-review-completion-5weidk` — mining verdicts

42 files, ~1.4k insertions, 9 commits. Only one touched file has since diverged on HEAD
(`SquirrelVesselHUDView.cs`, tube rework) — everything else cherry-picks cleanly.

| Artifact | Verdict | Notes |
|---|---|---|
| `7924a4e4` — five fleet runtime bug fixes (Serpent 256× boost `Pow(4,stacks)`, Dolphin drift NRE in cell-less modes, Rhino HUD double-subscribe on swap, Manta static overcharge dict leak, Sparrow telemetry log spam) | **CHERRY-PICK as-is** | All five bugs re-verified still live on HEAD; files undiverged; fixes minimal. Pick first — later commits stack on two of its files. Contains **no gun fixes**. |
| `ElementalScaling.cs` (from `0a7c1165`) | **CHERRY-PICK file, ADAPT API** | The right quantitative foundation: executor-side live reads, anchored at 1× at resting level, null-safe, extrapolates the debuff band. Add the level-5 tier predicate/event; hoist the scattered `atFull:` literals into SO config. |
| `0a7c1165` non-Sparrow executor hunks (Manta Yawstery/overcharge, Dolphin ChargeBoost, Rhino GrowTrail, Serpent ConsumeBoost) | **CHERRY-PICK** (after 7924a4e4) | Match the fleet element convention; 2–5 lines each. |
| `0a7c1165`/`96327603` **Sparrow** hunks (Mass→projectile size, Time→lifetime, Charge→heat-decay) | **ADAPT — do not pick verbatim** | Element assignments conflict with the new spec (SPACE→range, TIME→boost, MASS→turret stretch, CHARGE→blast). Reuse the code pattern, change element + target parameter. Notably the new spec fits the branch's own fleet convention (Space=reach, Time=rate, Charge=energy, Mass=size) *better* than its own Sparrow wiring did. |
| `Docs/VESSELS/FLEET_STATUS.md` | **ADAPT** | Best prior spec (five pillars, code-vs-asset boundary table, executor-not-ElementalFloat rationale, fleet element convention, per-vessel identities). Update the Sparrow table to the new spec; drop its "Sparrow ✅ complete" status claims (guns were broken when it was written). |
| `VesselAbilitySetSO` + per-vessel assets | **ADAPT** | Right "4 abilities per vessel" registry, presentation-only scope. Extend slots with `Element` + unlock metadata (see ARCHITECTURE). The Sparrow asset's verified input map (Fire=1 / SkyBurst=2 / TurretStance=6 / OverheatBoost=7) is exactly our four-ability skeleton. Squirrel asset/binding is stale vs the tube rework. |
| `VesselAbilityBar` + `GetAbilitySlotImage` view-binding (final `f5a0b601` shape only) | **ADAPT (later, presentation)** | Only the final view-binding form; fix Squirrel field renames; downgrade the missing-set LogError. |
| `AbilityIconPlaceholder`, `VesselAbilityIconValidator` (+ build gate) | **IGNORE** | Runtime programmer-art in the shipping HUD; the build gate shipped with a compile error. An edit-mode test is the right enforcement shape. |

**Lessons from the branch's "mess" (avoid repeating):** runtime UI construction on the spawn
hotpath; shipping the fallback as the primary (parallel placeholder icon row next to real icons);
a build gate that didn't compile; player-visible placeholder art; four reworks of the same feature
in 36 h; presentation data going stale against a moving trunk; scattered magic tuning numbers at
call sites; docs overstating status.

---

## 7. Full bug inventory

Adversarially CONFIRMED (5): dead SO elemental binding; broken `ElementalFloatBinder` reflection;
comeback clobbers crystal progression; element levels unreplicated; SilhouetteController
subscription asymmetry.

Hand-verified this audit (12): pool-injection NRE chain + unmerged fix; skyburst dud colliders;
detonator stale-return race; ModeSwitchingFire shared state; global mode-flip cross-talk; gun
forward-aim; cooldown latch; domain snapshot; Sparrow `elementBars` unwired; charge literal 0 +
min==max; SP spawns at level 5 / MP at 0; full-auto bullets already pierce & `friendlyFire:1` on
both projectile prefabs.

REPORTED (spot-verify at fix time): ~70 further findings cited throughout §§1–6 — the highest-value
ones are folded into `BACKLOG.md` items; the raw audit reports (with full evidence chains) are
preserved in the session transcript.
