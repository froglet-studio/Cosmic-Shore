# PRISM ANIMATION — the clock-material law (LOCKED)

> **The law:** No prism may ever need multiframe CPU updates in order to animate.
> Prism animation is: **pull** the right prism from the right pool (its material already
> accepts initial conditions), **stamp** the initial conditions once, let the **GPU run the
> course off the clock** with zero further CPU writes, and **swap** to a prism in the
> end state at the pre-computed end frame. **Colliders — and gameplay state generally —
> go to the FINAL state at the START of the animation.** Only photons animate.

This is a required practice, platform-wide, and it cannot be broken by future work.
It is the animation-side counterpart of two locked laws that already exist:

- **Continuity of existence** (CLAUDE.md ▸ Ecosystem Design Principles): everything must
  visibly grow / bloom / wither / suction / fade. This law does not weaken that — every
  transition stays. It states **who runs the transition**: the GPU, from initial
  conditions, not the CPU, frame by frame.
- **The instanced-render handoff** (CLAUDE.md ▸ Anti-Patterns, `Docs/PRISM_ECS_MIGRATION.md`):
  prisms draw through companion entities; per-prism visual state rides per-instance
  material overrides. This law extends that: the overrides carry *initial conditions*,
  never per-frame samples.

Status of the migration is tracked in §5. Until a path is migrated it is a **known
violation** — do not add new code in its style, and do not add ANY new prism update
path that is not clock-material from day one.

---

## 1. The law, precisely

A prism visual change has exactly three CPU touchpoints, ever:

| # | Touchpoint | When | What it may do |
|---|---|---|---|
| 1 | **Start stamp** | The frame the change begins | One write of initial conditions: per-instance material properties (start time, rate/duration, endpoints), material/mesh selection from the pool, and ALL gameplay state at its **final** value (collider size/enabled, spatial index registration + shell + occupancy, volume, state flags). |
| 2 | **(nothing)** | While the animation plays | The GPU evaluates the visual as a **pure function of the shader clock and the stamped initial conditions**. The CPU writes nothing — no transform, no MaterialPropertyBlock, no entity component, no mesh rebuild. |
| 3 | **End swap** | The pre-computed end frame (scheduled, not polled per-prism) | One write that makes the prism *be* the end state: swap to the settled shared material/mesh (so it batches with every equivalent prism), clear animation overrides, fire completion callbacks. A swap may be in-place (same instance re-stamped) or a literal pool exchange — either way it is one write at one known time. |

Everything in the visual between touchpoints 1 and 3 is `f(clock, initial conditions)`.
The end time is **analytic** — computable at stamp time — so the swap is scheduled
through a flat timer list (`PrismTimerManager`-class), never discovered by per-frame
progress polling.

### What conforms and what does not

| Pattern | Verdict |
|---|---|
| One-shot `sharedMaterial` swap + `SyncRenderMaterial()` | ✅ conforming (this IS the end-swap primitive) |
| One-shot per-instance override stamp (`[MaterialProperty]` IComponentData / one MPB write) | ✅ conforming (this IS the start-stamp primitive) |
| Flat timer list executing one callback at a known future time (`PrismTimerManager`) | ✅ conforming (this IS the swap scheduler; an O(active timers) due-check per frame is bookkeeping, not animation) |
| A single O(1) global uniform write per frame (a clock publisher) | ✅ conforming (it is not per-prism) |
| Per-frame/per-tick writes of transform scale, colors, shader params, or positions to animate a visual | ❌ violation |
| Per-frame mesh rebuild for a morph | ❌ violation |
| Per-frame collider size compensation | ❌ violation — and unnecessary: the collider is at final size from touchpoint 1 |
| Coroutine / UniTask / DOTween loop stepping any prism visual | ❌ violation |
| Polling animation progress per frame to detect completion | ❌ violation — the end time is analytic; schedule it |

### Animation vs. live gameplay data

The law governs **animation**: visual changes that are a pure function of time given
their initial conditions. It does not govern **live gameplay data** — a fauna body
prism moves because the creature swims; a gyroid mover steers a block into a bond
site. Those are gameplay transform updates at gameplay cadence, and they keep the
existing contracts (`NotifyPositionChanged` → spatial index + `SyncRenderTransform`).

The dividing question: *could the GPU have computed this frame's value from what was
known at the start?* If yes, it is animation and belongs on the clock. If no (the
value depends on live simulation — a moving target, player input, physics), it is
gameplay data. Two consequences:

- An implosion converging on a **moving** eater is gameplay-coupled today
  (`PrismImplosion.RefreshConvergence`). The migrated form snapshots the target at
  stamp time unless play-testing shows the tracking read matters; if it stays, it is a
  **documented exception** carrying exactly one float3 write per frame per implosion,
  and nothing else.
- "It's easier to lerp it on the CPU" is never an exception. If the curve is
  expressible from initial conditions — linear, exponential approach, eased by a fixed
  curve — it goes in the shader.

### Gameplay state goes final at start

The visual blooms from zero; the *game* does not. At touchpoint 1:

- **Collider**: enabled at its full authored size immediately (the physics footprint
  never tracks the visual — `HoldColliderAtFullSize`'s per-frame compensation loop is
  obsolete under this law and is deleted by the migration).
- **Spatial index**: registered with final position/shell/occupancy at stamp time.
- **Volume** ("volume is the spine"): stamped at its final value at touchpoint 1. The
  per-domain cell sums see the prism's full mass the moment it exists, not a ramp.
  This is the decided semantic — mass accounting is gameplay state, and gameplay state
  does not ride the visual.
- **State flags** (`IsShielded`, `IsDangerous`, domain): final immediately; only the
  material transition rides the clock.

Predicates that used to read live animation state become time predicates:
`IsGrowing` ⇒ `now < growEndTime`; `IsSettledForReveal` ⇒ `now >= growEndTime`
(evaluated by the caller's own sweep — no per-prism update needed).

---

## 2. The conforming primitives (they already exist)

The codebase already contains every primitive the law needs — the migration is
re-plumbing, not invention:

| Primitive | Where it exists today |
|---|---|
| Per-instance initial conditions on batched prisms | `PrismRenderProperties.cs` — `[MaterialProperty]` IComponentData mirrored to Hybrid-Per-Instance ShaderGraph properties (`Docs/PRISM_ECS_MIGRATION.md` §7). Adding `{_AnimStartTime, _AnimDuration, …}` is the same pattern as `_BrightColor`. |
| One-structural-op pooled entity creation | `PrismRenderService.Create` (prototype instantiate + non-structural `SetComponentData`) |
| The end-state material swap | `MaterialPropertyAnimator.UpdateMaterial`'s `OnAnimationComplete` — already ends every color transition with a `sharedMaterial` swap + `SyncRenderMaterial()` |
| The end-state mesh swap (pool-equivalent exchange) | The shields' settled handoff: `OctahedronMeshGenerator.GetSharedShieldMesh` + `Prism.SetRenderMeshOverride` + `SetExoticVisualActive(false)` — a per-prism-unique animation settling into a cache-shared batched end state. **This is the reference implementation of "swap to a prism in the end state."** |
| The swap scheduler | `PrismTimerManager` — flat timer list, one callback at a known future time, already used for shield deactivation |
| The clock | `_Time.y` in shaders (= scaled `Time.timeSinceLevelLoad`). CPU stamps use `Time.timeSinceLevelLoad` so both sides read the same epoch. If editor/scene-load semantics ever bite, the fallback is a single global `_PrismClock` uniform written once per frame by one publisher — O(1), conforming. Note `_Time` scales with `Time.timeScale`, so hitstop/pause freeze prism animation for free — desired. |
| GPU-driven animation precedent | Blend shapes are already converted to textures for shader-driven animation with no controller scripts (CLAUDE.md ▸ Shader & Visual Development); the explosion/implosion shaders are already parametric in an "amount" — the CPU merely feeds them per frame today. |

---

## 3. The audit — every way prisms are updated (2026-07-31)

Verdicts: ✅ conforming · ❌ violation (multiframe CPU animation) · ⚠ exception/decision.

### 3.1 Scale / growth

| Path | Trigger | Mechanism today | Verdict |
|---|---|---|---|
| Grow-in bloom | Every spawn (`Prism.Initialize` → `CreateBlockCoroutine` → `BeginGrowthAnimation`) | `PrismScaleManager.ProcessAnimationFrame` — sliced per-tick `transform.localScale` writes (exponential approach, `1−exp(−k·dt)`), plus per-step `SyncRenderTransform()` + `RefreshVolumeCache()` (`PrismScaleManager.cs:158-165`) | ❌ |
| Grow ability / volume add | `Prism.Grow(amount)`, `ChangeSize()`, `TargetScale` setter | Same manager pass | ❌ |
| Load-gate fast-grow | Connecting screen (`IsLoadGateHolding` budget raise) | Same manager pass at higher budget | ❌ (obsolete under the law — behind the veil the stamp can simply set t₀ in the past or swap instantly) |
| `CompleteGrowthImmediately` | Loading gate settle sweep | One-shot snap + bookkeeping | ✅ (becomes trivial: re-stamp) |
| `HoldColliderAtFullSize` | Boost rings / joust rings / Squirrel tube | Per-frame coroutine compensating `BoxCollider.size` against animated transform scale (`Prism.cs:282-322`) | ❌ — deleted by the law (collider is final-size at start for every prism) |

### 3.2 Color / material state

All of these funnel through `MaterialPropertyAnimator.UpdateMaterial(...)` → a
multi-frame CPU lerp in `MaterialStateManager.ProcessAnimationFrame` (default 0.8 s,
per-tick `smoothstep` color lerp → `PrismRenderService.SetColors` or MPB write,
`MaterialStateManager.cs:77-110`), ending in the conforming `sharedMaterial` swap.

| Path | Trigger | Verdict |
|---|---|---|
| Domain paint at spawn | `PrismTeamManager.SetInitialTeam` | ❌ (lerp) → ✅ (end swap) |
| Steal / ChangeTeam repaint | `PrismTeamManager.Steal/ChangeTeam` → `HandleTeamChange` | ❌ (lerp) |
| Danger state | `PrismStateManager.MakeDangerous` | ❌ (lerp) |
| Shield engage/disengage repaint | `PrismStateManager.ApplyShieldState/ApplyNormalState/ActivateSuperShield` | ❌ (lerp) |
| Transparency toggle | `MaterialPropertyAnimator.SetTransparency` | ✅ one-shot `sharedMaterial` swap |
| Timed shield drop | `ActivateShield(duration)` → `PrismTimerManager.ScheduleShieldDeactivation` | ✅ scheduler (the deactivation itself then runs the ❌ lerp) |

### 3.3 Destruction / restoration

| Path | Trigger | Mechanism today | Verdict |
|---|---|---|---|
| Explosion debris | `Prism.Damage` → `Explode` → `PrismExplosion` | `PrismEffectsManager.ProcessExplosions`: per-frame Burst job computing `pos = p₀ + t·v`, `amount = speed·t`, `opacity = 1 − t/dur` → per-frame `transform.position` + `SetExplosionParams`/MPB writes (`PrismEffectsManager.cs:242-331`). Every output is linear in t. | ❌ |
| Implosion / consume | `Prism.Consume` → `Implode` → `PrismImplosion` | `ProcessImplosions`: per-frame progress `±t/dur` + per-frame moving-target `RefreshConvergence()` → `SetImplosionParams`/MPB | ❌ (progress) · ⚠ (moving target — see §1) |
| Destruction state | `SetupDestruction` | One-shot: collider off, render off, spatial `MarkDestroyed`, volume zeroed | ✅ |
| Restore | `Prism.Restore` | One-shot re-show (`SetRenderVisible(true)`) | ✅ by this law (⚠ it pops in — a continuity-law gap to fix WITH clock-material bloom, not with a CPU animation) |

### 3.4 Shields (exotic geometry)

| Path | Trigger | Mechanism today | Verdict |
|---|---|---|---|
| Octahedron engage bloom | `PrismStateManager.ApplyShieldState` → `PrismOctahedronShield.Engage` | Per-frame CPU morph-mesh rebuild (8 faces grow from centroids, 0.35 s) on the GameObject renderer via `SetExoticVisualActive(true)` | ❌ |
| Shatter overlay (disengage) | `Disengage` | Per-frame CPU shatter-mesh rebuild (faces fly outward, 0.6 s) | ❌ |
| Stella engage/disengage | `ActivateSuperShield` → `PrismStellatedOctahedronShield` | Same pattern | ❌ |
| Settled shield state | Morph completes | Swap to cache-shared mesh (`GetSharedShieldMesh`) + return to instanced path | ✅ — the reference end-swap |

### 3.5 Spawn / pool / trail paths

| Path | Mechanism today | Verdict |
|---|---|---|
| Pool pull + `Initialize` | `ResetState` + property stamps, then `CreateBlockCoroutine` (spawn-stagger wait + per-frame creation-budget polling loop, `Prism.cs:667-758`) | ⚠ the budget loop is scheduling, not animation — but under the law it collapses to a stamp with a staggered t₀ |
| Spawn stagger (`waitTime`) | `WaitForSeconds` per prism | ⚠ same — becomes part of the stamped start time |
| Creation-completion budget | Static per-frame counter + retry loop | ⚠ same |

*(Rows for trail lay, boost rings, segment/track spawners, spawnable shapes, painting
regrow, conveyor recycle — see §3.7 pending the full sweep.)*

### 3.6 Movers (gameplay data — not animation)

| Path | Mechanism | Classification |
|---|---|---|
| Gyroid bonding movers | Steer transform + `NotifyPositionChanged` | live gameplay data — out of the law's scope, keeps its contracts |
| Fauna body prisms | Creature locomotion moves prisms | live gameplay data |
| Cell swap drain / conveyor suction | Batched retire/relocate passes | must use clock-material suction/bloom for the *visuals*; the batch bookkeeping itself is one-shot per prism |

### 3.7 Full path inventory

The exhaustive per-path table (including flora growth, fauna wither, microscene
conveyor, cell swap, spawnables, projectiles/mines, test scaffolding) is appended by
the audit sweep — see the migration tracker in §5.

---

## 4. Target architecture

### 4.1 Shader side

Three graphs carry the animated states today; each gains clock inputs (all
Hybrid Per Instance):

| Graph | New per-instance properties | Behavior |
|---|---|---|
| `UnstablePrismGraph` (base prism) | `_GrowStartTime`, `_GrowRate`, `_GrowStartFrac` (scale fraction at t₀); `_ColorStartTime`, `_ColorDuration`, `_StartBrightColor`, `_StartDarkColor`, `_StartSpread` | Vertex: scale factor `s(t) = 1 − (1−s₀)·exp(−k·(t−t₀))` about the prism origin (the entity's `LocalToWorld` is at FINAL scale from the start). Fragment: colors = `lerp(start, material target, smoothstep((t−t₀)/dur))`. At rest (t ≥ end) both expressions equal the end state exactly — the settle swap merely clears the overrides. |
| `ExplodingBlockGraph` | `_ExplodeStartTime`, `_ExplodeSpeed`, `_ExplodeDuration` (keeps `_Velocity`) | Vertex: world offset `(t−t₀)·_Velocity`; `amount = _ExplodeSpeed·(t−t₀)`; `opacity = 1 − (t−t₀)/_ExplodeDuration`. Entity transform never moves. |
| `SuctionGraph` | `_SuctionStartTime`, `_SuctionDuration`, `_SuctionDirection` (grow=−1 / implode=+1), `_GrowDelay` (keeps `_Location`) | `progress(t)` computed in-shader; `_Location` stamped once (moving-target exception per §1 if retained). |

### 4.2 CPU side

- **Stamp APIs** on `PrismRenderService` (one `SetComponentData` bundle each):
  `StampGrow(handle, t0, rate, startFrac)`, `StampColorTransition(handle, t0, dur, startBright, startDark, startSpread)`,
  `StampExplosion(...)`, `StampSuction(...)` — plus MPB twins for the legacy path.
- **Analytic end times** computed at stamp: growth `t_end = t0 + ln(Δ₀/ε)/k`; color
  `t_end = t0 + dur`; etc.
- **`PrismAnimationScheduler`** — `PrismTimerManager` generalized to schedule the end
  swap (settle material/mesh, clear overrides, completion callbacks such as
  `ExecuteOnScaleComplete`'s shield/largest checks) at `t_end`.
- **Interruption = re-stamp**: the current visual value is analytic
  (`f(clock, stamp)`), so retargeting mid-flight computes the value on demand for the
  new stamp's start conditions. No per-frame "current color" tracking
  (`CurrentBrightColor` et al. become computed properties).
- **Gameplay finality at stamp**: collider enabled at authored size on `Initialize`;
  volume/`CachedVolume`/spatial shell stamped final; `IsGrowing`/`IsSettledForReveal`
  become clock predicates; `HoldColliderAtFullSize` deleted.
- **Retirements when migration completes**: `PrismScaleManager`,
  `MaterialStateManager`, `PrismEffectsManager`'s per-frame passes, and
  `AdaptiveAnimationManager`'s frame-skip machinery for these paths.

### 4.3 Pools

"The right prism from the right pool" = the pool hands out instances whose material
already accepts the stamp: prism pools keyed by prefab as today, with the material
chosen at stamp time from the theme set (domain × state × opacity) exactly as now —
the difference is that the material choice + override stamp happen ONCE, at
touchpoint 1, never per frame. Effect pools (explosion/implosion) likewise: the
pulled instance's material is the animated graph; the stamp is its initial
conditions; `OnEffectComplete` (the scheduled end) returns it to the pool.

---

## 5. Migration tracker

| # | Path | Status |
|---|---|---|
| 1 | Shader graphs: clock inputs (grow, color, explode, suction) | ☐ not started |
| 2 | `PrismRenderProperties` + stamp APIs + legacy MPB twins | ☐ not started |
| 3 | `PrismAnimationScheduler` (end-swap scheduling) | ☐ not started |
| 4 | Grow-in migrated; `PrismScaleManager` retired | ☐ not started |
| 5 | Color/state transitions migrated; `MaterialStateManager` retired | ☐ not started |
| 6 | Explosion/implosion migrated; `PrismEffectsManager` per-frame passes retired | ☐ not started |
| 7 | Shield morphs migrated (vertex-shader bloom/shatter) | ☐ not started |
| 8 | Gameplay-final-at-start (collider/volume/predicates; `HoldColliderAtFullSize` deleted) | ☐ not started |
| 9 | Docs locked (this file + CLAUDE.md anti-patterns) | ☐ in progress |

---

## 6. Enforcement

- **CLAUDE.md ▸ Anti-Patterns** carries the rule; any PR adding a per-frame prism
  visual write is rejected on review.
- Every new prism visual state MUST arrive as: pool material + stamp + clock shader +
  scheduled swap. If a state seems impossible to express that way, that is a design
  discussion (see §1 "animation vs. live gameplay data") — not a license for a
  per-frame loop.
- The three CPU animation managers carry header comments pointing here; new
  registrations into their per-frame passes are treated as regressions once their
  paths migrate.
