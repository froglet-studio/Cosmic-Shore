# PRISM ANIMATION — the clock-material law (LOCKED)

> **The law:** No prism may ever need multiframe CPU updates in order to animate.
> Prism animation is: **pull** the right prism from the right pool (its material already
> accepts initial conditions), **stamp** the initial conditions once, let the **GPU run the
> course off the clock** with zero further CPU writes, and **swap** to a prism in the
> end state at the pre-computed end frame. **Colliders — and gameplay state generally —
> go to the FINAL state at the START of the animation.** Only photons animate.

This is a required practice, platform-wide, and it cannot be broken by future work.
**STRICT MODE (locked by the prompter, 2026-08-01): there is NO legacy fallback.**
The clock-material path is the only prism animation path — no toggle, no
per-material fallback tier, no CPU animation managers. A prism whose graph is not
wired (§4.4) does not fall back: gameplay state still goes final at the stamp
(law-correct), the visual snaps to its end state, and the stamp site logs a loud
error naming exactly what to wire (`PrismClockDiagnostics`, per the project's
fail-loud policy). If a graph is ever unwired (a revert, a new prism graph),
spawns/transitions/effects visibly snap — that is the intended forcing function,
not a bug to paper over with a fallback. (All four §4.4 phases are wired
in-branch as of 2026-08-02 — see `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md`.)

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
| The clock | The **`_PrismClock` global uniform**, published once per frame by `PrismClock`'s publisher from the SAME value the stamps use (`Time.time`) — CPU and GPU read the same number BY CONSTRUCTION (O(1) per frame, the law's allowed clock publisher). Do NOT use the shader Time node: URP feeds `_Time` from a different clock domain than naive CPU stamps (scaled time since startup, not since level load), which made every bloom evaluate as already finished — prisms popped in fully grown, silently. `Time.time` is scaled, so hitstop/pause freeze prism animation for free — desired. |
| GPU-driven animation precedent | Blend shapes are already converted to textures for shader-driven animation with no controller scripts (CLAUDE.md ▸ Shader & Visual Development); the explosion/implosion shaders are already parametric in an "amount" — the CPU merely feeds them per frame today. |

---

## 3. The audit — every way prisms are updated (2026-07-31)

An 11-agent exhaustive sweep (10 lenses + a completeness critic) inventoried
**162 path entries: 94 violations, 68 conforming**. §3.1–3.6 summarize the seven
core paths; §3.7 is the complete per-lens inventory (lenses overlap deliberately —
a path can appear under more than one; **§5 is the deduplicated work list**);
§3.8 records latent bugs the sweep surfaced and the verified-clean absences.

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
| Octahedron engage bloom | `PrismStateManager.ApplyShieldState` → `PrismOctahedronShield.Engage` | ONE stamp (`StampShieldMorph`); the vertex stage blooms the 8 faces out of their centroids off the shader clock, on the cache-shared settled mesh | ✅ shipped 2026-08-15 (was: per-frame CPU morph-mesh rebuild on the un-batched GameObject renderer) |
| Shatter overlay (disengage) | `Disengage` | ONE batched pure-entity spawn per frame (`PrismShieldShatter` → `SpawnShieldShatterBatch`) carrying one stamp each; retired by a flat time sweep | ✅ shipped 2026-08-15 (was: a per-prism child GameObject rebuilding a shatter mesh every frame) |
| Stella engage/disengage | `ActivateSuperShield` → `PrismStellatedOctahedronShield` | Same two paths, same shader function — 24 faces instead of 8 | ✅ shipped 2026-08-15 |
| Settled shield state | Engage (t = 0, not morph completion) | Swap to cache-shared mesh (`GetSharedShieldMesh`) + return to instanced path — now applied at the START, since the morph runs on that same shared mesh | ✅ — the reference end-swap, retimed to the stamp |

### 3.5 Spawn / pool / trail paths

| Path | Mechanism today | Verdict |
|---|---|---|
| Pool pull + `Initialize` | `ResetState` + property stamps, then `CreateBlockCoroutine` (spawn-stagger wait + per-frame creation-budget polling loop, `Prism.cs:667-758`) | ⚠ the budget loop is scheduling, not animation — but under the law it collapses to a stamp with a staggered t₀ |
| Spawn stagger (`waitTime`) | `WaitForSeconds` per prism | ⚠ same — becomes part of the stamped start time |
| Creation-completion budget | Static per-frame counter + retry loop | ⚠ same |

*(Trail lay, boost rings, segment/track spawners, spawnable shapes, painting regrow,
conveyor recycle — all inventoried in §3.7 lenses A, F, G, J, K.)*

### 3.6 Movers (gameplay data — not animation)

| Path | Mechanism | Classification |
|---|---|---|
| Gyroid bonding movers | Steer transform + `NotifyPositionChanged` | live gameplay data — out of the law's scope, keeps its contracts |
| Fauna body prisms | Creature locomotion moves prisms | live gameplay data |
| Cell swap drain / conveyor suction | Batched retire/relocate passes | must use clock-material suction/bloom for the *visuals*; the batch bookkeeping itself is one-shot per prism |

### 3.7 Full path inventory (all 10 lenses + critic)

<!-- AUDIT_TABLE_START -->
#### A. Grow-in / scale

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| PrismScaleManager sliced grow-in pass (the shared CPU engine) | per-frame CPU | ❌ | `Controller/Managers/PrismScaleManager.cs:54-196` | Replace with a clock material on the companion entity: at spawn write per-instance _GrowStartTime (t0), _GrowRate (k, computed once from GrowthRate exactly as line 146), and rely on the entity's LocalToWorld already holding FINAL scale; vertex shader scales positions by clamp01(1−exp(−k·(t−t0))) — i… |
| PrismScaleAnimator trigger surface + end-of-animation side effects | per-frame CPU | ❌ | `Controller/Environment/Prisms/PrismScaleAnimator.cs:92-124` | Keep as the data holder (TargetScale/GrowthRate/min-max are the initial conditions the pool-pull loads into the material). Move ExecuteOnScaleComplete to spawn: UpdateVolume already reads TargetScale, so the SOAP raise + IsLargest/IsSmallest checks are start-safe verbatim. |
| Universal pool-spawn bloom: Prism.Initialize → CreateBlockCoroutine | per-frame CPU | ❌ | `Controller/Vessel/Prism.cs:534-555` | Pool-pull configures everything at spawn in one pass: transform.localScale = TargetScale (final), collider enabled at authored size (world footprint final at start — one scheduled callback at t0+waitTime only if the 0.6s no-collide spawn window must survive), spatial Register + RefreshVolumeCache im… |
| Vessel trail lay (all 11 vessels) | per-frame CPU | ❌ | `Controller/Vessel/VesselPrismController.cs:215-303` | No caller change beyond the engine migration: TargetScale remains the initial condition the pool-pull loads into the clock material. CreateBlock's computed scale becomes the final transform write at spawn. Nothing else moves. |
| Environment lay: PrismTrailBuilder + spawnable consumers | per-frame CPU | ❌ | `Controller/Environment/Spawning/PrismTrailBuilder.cs:65-89` | Inherits the engine migration wholesale. ConfigureLaid's TargetScale write becomes the final-transform + clock-material configure. The arena-ready gate simplifies: IsSettledForReveal = IsCreationComplete && now >= t0+settleTime, and behind the veil the builder can stamp t0 = now − settleTime to rend… |
| Load-gate force settle (CompleteGrowthImmediately) | one-shot | ✅ | `Controller/Environment/Spawning/PrismTrailBuilder.cs:327-357` | Becomes a single t0 rewrite per prism (stamp the clock into the past) or disappears entirely — with GPU-clocked growth a 25k cohort settles with zero CPU regardless of frame budget, so the load-gate grower/creation boosts and the settle sweep are all deletable. |
| Boost ring / Squirrel tube: BoostRingBuilder + HoldColliderAtFullSize | per-frame CPU | ❌ | `Controller/Environment/Spawning/BoostRingBuilder.cs:83-120` | With transform final at spawn the entire coroutine deletes: collider enabled at authored size = full-size world footprint from frame 0 with zero per-frame work. |
| Boost pool spawn config (PrismFactory.SpawnBoostPrism) | one-shot | ✅ | `Controller/Prisms/PrismFactory.cs:208-230` | Unchanged in spirit: SetGrowthRate becomes 'write k into the clock material at pull'. The dedicated-pool pattern (per-behavior pools carrying their own initial conditions) is exactly what the target architecture generalizes. |
| Assembler-driven growth (WallAssembler / GyroidAssembler) | per-frame CPU | ❌ | `Controller/Assemblers/WallAssembler.cs:270-326` | Grow-on-bond and ConvertBlock retargets become one-shot clock-material retargets (write new _TargetScale/_StartScale + restamp t0; gameplay volume snaps to new target immediately via one RefreshVolumeCache). |
| Flora leaf growth (growPeriod loop → new health prisms) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Flora.cs:94-98` | Inherits the engine migration: each new leaf pulls with leafSize as its clock-material target, volume final at start (flora mass counts in Cell.LiveVolume the moment the leaf is laid — re-baseline any phase tuning that implicitly relied on ramp-in). |
| Fauna body-prism shaping + Boid feeding mass transfer (Grow ±) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Fauna.cs:340-357` | Retarget = one write: _StartScale := current displayed scale (computed analytically from the old (t0,k,start,target) — no transform read), _TargetScale := new target, t0 := now. |
| Turret prism bloom (FullAutoBlockShootActionExecutor) | per-frame CPU | ❌ | `Controller/Vessel/R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs:128-147` | Engine migration covers it: pull with blockScale as clock target, transform final at spawn (the projectile system keeps writing position — unrelated to scale). The raw-localScale fallback branch (:146) becomes the only branch and is a legal one-shot. |
| Fired trail-block projectile (FireTrailBlockActionExecutor) | per-frame CPU | ❌ | `Controller/Vessel/VesselActions/FireTrailBlockActionExecutor.cs:49-94` | Engine migration for the bloom; move to the factory/pool channel with ProjectileScale as an initial condition; replace the Destroy timer with the projectile-end effect chain (or a scheduled swap to an end-state), per the continuity law. |
| AOE double-growers: AOERadialBlocks + AOEDangerHemisphereBlocks GrowToScale | per-frame CPU | ❌ | `Controller/Projectiles/AOERadialBlocks.cs:200-222` | Immediate pre-migration cleanup: delete both GrowToScale loops and the localScale=0 rewrites (the engine already grows from zero), replace the growthRate field writes with prism.SetGrowthRate(config.GrowthRate), replace r.material with the kind/state pipeline. |
| Clean AOE / painting / microscene one-shot lays | per-frame CPU | ❌ | `Controller/Projectiles/AOEBlockCreation.cs:104-141` | Engine migration only. Microscene's recycle becomes the canonical 'pull from pool with initial conditions': one configure call (pose + kind + domain + clock target, t0=now) with no ordering trap, since the clock material has no disabled-animator state to re-arm. |
| Prism.Restore (destroyed → live reappear) | one-shot | ✅ | `Controller/Vessel/Prism.cs:1089-1128` | Keep the one-shot gameplay writes exactly as-is (already final-at-start); add a bloom for continuity by stamping the clock material (t0=now, start=0, target=retained scale) in the same write — zero CPU follow-up, and the disabled-animator special case (CurrentVolume fallback, ResetState re-arm) diss… |
| Pre-spawn scale parameterization (writes vessel state, never live prisms) | one-shot | ✅ | `Controller/Vessel/TrailPassives/ScoutTrailPrismScaler.cs:86-148` | None required. Under the target architecture these remain the producers of the initial conditions each pool-pull loads. |

#### B. Color / domain / state

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| UpdateMaterial → MaterialStateManager fused color lerp (the central violation) | per-frame CPU | ❌ | `Controller/Environment/Prisms/MaterialPropertyAnimator.cs:159-223` | Add per-instance transition inputs to the prism shader + entity overrides: _FromBrightColor/_FromDarkColor/_FromSpread + _TransitionStartTime + _TransitionDuration. |
| Domain paint at spawn / recolor — ChangeTeam → HandleTeamChange | per-frame CPU | ❌ | `Controller/Managers/PrismTeamManager.cs:54-60` | Split spawn from mid-life recolor. SPAWN: the prism grows in from scale zero, so no color transition is visible or needed — make the spawn paint a pure one-shot: SetMaterial(domain material, refreshColors:true) with zero-duration (add an UpdateMaterial fast-path or a SetTeamImmediate that skips the… |
| Steal / capture repaint | per-frame CPU | ❌ | `Controller/Managers/PrismTeamManager.cs:62-96` | Already 'collider/gameplay final at start' — gameplay needs zero change. Visual: SetMaterial(new domain's state-appropriate end material) + one BeginColorTransition(from = old domain colors) write; GPU clock runs the 0.8s blend. No pool swap needed (same prism, same mesh). |
| MakeDangerous repaint | per-frame CPU | ❌ | `Controller/Managers/PrismStateManager.cs:51-79` | SetMaterial(GetTeamDangerousBlockMaterial one-shot) + BeginColorTransition(from = current colors). For spawn-time danger (Prism.Initialize / PrismKinds.Apply on fresh spawn) skip the transition entirely — prism is at scale zero; pull from a danger-state pool or immediate paint. |
| ActivateShield / ApplyShieldState repaint + octahedron engage | per-frame CPU | ❌ | `Controller/Managers/PrismStateManager.cs:81-92` | Color: as path 1 (shielded end material one-shot + GPU from→to). Morph: bake box→octahedron as a vertex-shader morph (two vertex streams or a morph texture) driven by per-instance _EngageStartTime on the INSTANCED path — no exotic-visual handoff during engage, prisms stay batched; schedule ONE callb… |
| ActivateSuperShield repaint + stellated engage (self-Update violation) | per-frame CPU | ❌ | `Controller/Managers/PrismStateManager.cs:94-121` | Same as path 5: one-shot opaque end material + GPU color clock; stellation morph as GPU vertex morph off _EngageStartTime; scheduled single callback swaps to StellatedOctahedronMeshGenerator.GetSharedShieldMesh. |
| DeactivateShields / ApplyNormalState repaint (+ PrismTimerManager scheduled path) | scheduled | ❌ | `Controller/Managers/PrismStateManager.cs:123-143` | Keep PrismTimerManager as the generic end-frame scheduler (rename/generalize: it is exactly the 'at the right frame, swap for the end-state prism' mechanism). |
| SetTransparency — one-shot material swap (CONFORMING) | one-shot | ✅ | `Controller/Environment/Prisms/MaterialPropertyAnimator.cs:225-233` | None required. (If a fade-in/out is ever wanted for continuity-of-existence polish, do it as a GPU clock via the same _TransitionStartTime inputs — not by reintroducing a lerp manager.) |
| ClearPrisms per-physics-tick _Alpha MPB fade (rogue, and blind on the instanced path) | per-frame CPU | ❌ | `Controller/Vessel/ClearPrisms.cs:117-127` | Pure shader solution, zero per-prism writes: publish the camera→vessel line (two float4 globals) once per frame via Shader.SetGlobalVector; the prism shader computes distance-from-line per vertex/pixel and derives _Alpha itself. |
| MaterialBlendUtility.BeginBlend — rogue per-object coroutine blend (overheat danger trail + skim overcharge ma… | per-frame CPU | ❌ | `Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs:31-128` | Delete MaterialBlendUtility. Overheat: VesselPrismController._dangerMode already sets IsDangerous — route the visual through prism.MakeDangerous()/PrismStateManager so it inherits the migrated GPU transition (and spawn-time danger needs no transition at all — the prism grows from zero). |
| Initial domain paint via Prism.Domain setter (SetInitialTeam) | per-frame CPU | ❌ | `Controller/Vessel/Prism.cs:129-136` | Fold into the pool-pull contract: Initialize/factory pull writes the final domain material one-shot (refreshColors:true) before the prism becomes visible — no transition, since the grow-in from scale zero already satisfies continuity. |
| ThemeManager domain material set generation (end-state material pool source) — CONFORMING | one-shot | ✅ | `Controller/Managers/ThemeManager.cs:14-110` | Extend, don't change: the BaseMaterialSet block shaders gain the transition inputs (_FromBright/_FromDark/_FromSpread/_TransitionStartTime/_TransitionDuration, defaulting to no-op), making every generated end-state material also a transition-accepting material — no new material count, batching prese… |
| PrismFactory.ConfigureForTeam — effect-prism team colors — CONFORMING | one-shot | ✅ | `Controller/Prisms/PrismFactory.cs:293-334` | None for the color write — it already matches the target pattern (pull from pool, load initial conditions once). |

#### C. Instanced render path (PrismRenderService)

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| Companion entity creation (prototype instantiate + initial stamp) | one-shot | ✅ | `Controller/ECS/Rendering/PrismRenderService.cs:391-413` | This IS the pool-pull primitive. Extend: Create() gains the animation-stamp payload (or a follow-up Stamp* call in the same frame) — grow/color clock components live in the prototype archetype so the stamp stays non-structural; RenderBounds parameter added for animations that displace geometry beyon… |
| Grow-in transform sync (PrismScaleManager → SetTransform) | per-frame CPU | ❌ | `Controller/Managers/PrismScaleManager.cs:165-172` | Stamp LocalToWorld ONCE at FINAL scale at creation; add _GrowStartTime/_GrowRate/_GrowStartFrac to the Prism override set (prototype archetype); BlockGraph vertex stage computes s(t)=1−(1−s₀)·exp(−k·(t−t₀)) about the prism origin; analytic t_end = t0+ln(Δ₀/ε)/k scheduled on PrismAnimationScheduler f… |
| Color/state transition lerp (MaterialStateManager → SetColors) | per-frame CPU | ❌ | `Controller/Managers/MaterialStateManager.cs:84-117` | Stamp {_ColorStartTime, _ColorDuration, _StartBrightColor, _StartDarkColor, _StartSpread} once (color-space-converted at the service boundary); target = the swapped-to material's authored constants; fragment lerp on smoothstep((t−t₀)/dur); scheduled SetMaterial(refreshColors:true) at t0+dur is the s… |
| Explosion per-frame parameter feed (PrismEffectsManager.ProcessExplosions) | per-frame CPU | ❌ | `Controller/Managers/PrismEffectsManager.cs:296-307` | Every output is f(t, stamp): stamp {_ExplodeStartTime, _Velocity, _ExplodeSpeed, _ExplodeDuration} once at TriggerExplosion (which already writes the exact initial-condition set — extend it); entity LocalToWorld stamped once at p₀; vertex shader offsets by (t−t₀)·_Velocity; RenderBounds expanded at… |
| Implosion per-frame parameter feed (PrismEffectsManager.ProcessImplosions) | per-frame CPU | ❌ | `Controller/Managers/PrismEffectsManager.cs:346-431` | Stamp {_SuctionStartTime, _SuctionDuration, _SuctionDirection, _GrowDelay} once; progress computed in-shader; _Location snapshotted at stamp time. The MOVING convergence target is the audit's one documented-exception candidate (live gameplay data per PRISM_ANIMATION.md §1): if play-testing shows tra… |
| Effect initial-condition stamp (TriggerExplosion / ApplyInitialVisualState) | one-shot | ✅ | `Utility/Effects/PrismExplosion.cs:208-224` | Extend in place: add the clock params (start time/speed/duration/direction/delay) to this same stamp; expand RenderBounds here; make the effect visible immediately (correct params exist at t0). This becomes the complete touchpoint-1 for effects. |
| Base material swap (SyncRenderMaterial → SetMaterial) | one-shot | ✅ | `Controller/Vessel/Prism.cs:476-496` | Keep as the scheduled settle swap's engine. The refreshColors gate becomes the clock predicate (now < colorEndTime); the scheduled end-swap calls SetMaterial(refreshColors:true) AND re-stamps clock overrides to rest values so the material constants display verbatim. |
| Mesh swap for settled shields (SetRenderMeshOverride / ClearRenderMeshOverride → SetMesh) | one-shot | ✅ | `Controller/Vessel/Prism.cs:443-462` | Unchanged; when shield morphs migrate to vertex-shader bloom/shatter (tracker item 7) this same SetMesh is the scheduled end swap, and the exotic GameObject fallback for shields can retire. |
| Visibility — immediate (SetVisible) | one-shot | ✅ | `Controller/ECS/Rendering/PrismRenderService.cs:419-433` | Keep for handoffs; the per-frame EnableVisual caller in ProcessExplosions disappears under stamps. New code should prefer QueueVisible unless same-instant matters. |
| Visibility — batched queue + LateUpdate flush (QueueVisible / FlushVisibility) | one-shot | ✅ | `Controller/ECS/Rendering/PrismRenderService.cs:442-509` | Unchanged — this is the model the design keeps: stamps are non-structural and need no batching; visibility remains the only structural op and is already amortized. The GPU-clock end swap (in-place re-stamp + SetMaterial/SetMesh) adds zero structural changes. |
| Exotic-visual handoff color continuity (SyncRenderColorsFromAnimator) | one-shot | ✅ | `Controller/Vessel/Prism.cs:503-529` | Under GPU-clock stamps this becomes either unnecessary (the entity's stamped clock overrides survive the handoff untouched — the animation was never CPU-tracked) or an analytic evaluation f(clock, stamp) for the MPB twin on the GameObject side. |
| Movers — NotifyPositionChanged → SyncRenderTransform (gameplay data, law-exempt) | per-frame CPU | ✅ | `Controller/Vessel/Prism.cs:1142-1160` | No migration — keeps the existing NotifyPositionChanged contract. Boundary rule for reviewers: if a mover's path IS expressible from initial conditions (a scripted conveyor glide along a fixed curve), it is animation and must move to a stamped clock path instead of riding this exemption. |
| Pool-return / teardown lifecycle (OnDisable hide, OnDestroy destroy) | one-shot | ✅ | `Controller/Vessel/Prism.cs:1162-1203` | Unchanged, plus one addition: pool return must also cancel any outstanding PrismAnimationScheduler entry for the instance (or the scheduler re-validates the handle + a generation counter at fire time) so a scheduled end swap can never fire on a reused prism. |
| Load-gate snap (PrismScaleAnimator.CompleteImmediately → SyncRenderTransform) | one-shot | ✅ | `Controller/Environment/Prisms/PrismScaleAnimator.cs:143-161` | Becomes trivial under the stamp architecture: behind the veil the stamp simply sets t₀ in the past (or duration 0), so the shader is already at rest — the snap and the load-gate grower-budget raise (PrismScaleManager.cs:90-92) both dissolve. |
| Dev-only zombie audit (periodic entity-visibility sweep) | scheduled | ✅ | `Controller/Managers/PrismEffectsManager.cs:186-246` | Keep; under the scheduler design its detection predicate changes from IsActive flags to 'now > scheduled end + grace', and it doubles as the watchdog for missed scheduled swaps. |
| Stress harness churn (PrismRenderStressTest) | per-frame CPU | ❌ | `Controller/ECS/Rendering/PrismRenderStressTest.cs:238-283` | Keep as the measurement tool, and ADD a third mode: stamp-once GPU-clock animation over the new clock components with zero Update writes — the A/B (per-frame churn vs stamped clock at equal visual motion) is the acceptance benchmark proving the infrastructure and validating the Hybrid-Per-Instance c… |

#### D. Shields (exotic geometry)

> **All four ❌ rows below SHIPPED 2026-08-15** — see §4.8 for the design as built and
> §5 B4 for the record. The table is left as the 2026-07-31 audit found it, including the
> migration sketches, because two of them were superseded in a way worth keeping visible:
> no separate "anim variant" mesh was needed (the settled shared mesh carries the
> centroids, so it IS the morph mesh), and no scheduled end-callback was needed (the
> shader clamps at t = 1, which is the settled shield).

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| Octahedron shield engage bloom (per-face morph) | per-frame CPU | ❌ | `Controller/Vessel/PrismOctahedronShield.cs:259-287 (Engage)` | Bake the bloom into a vertex shader on the SHARED cached mesh: extend OctahedronMeshGenerator.GetSharedShieldMesh to emit one 'anim' variant per quantized geometry carrying per-vertex face-centroid in TEXCOORD1 (normals are already flat per-face). |
| Octahedron shield shatter overlay (disengage) | per-frame CPU | ❌ | `Controller/Vessel/PrismOctahedronShield.cs:294-330 (Disengage)` | Same shared anim mesh as the engage bloom, second shader path (or second material) driven by _ShatterStartTime: t = saturate((_Time.y - _ShatterStartTime)/_ShatterDuration); pos = centroid + (1-t)*(v - centroid) + t*_ShatterMaxOffset*normal. |
| Stellated (super-shield) engage bloom — plus a persistent idle per-prism Update() | per-frame CPU | ❌ | `Controller/Vessel/PrismStellatedOctahedronShield.cs:255-280 (Engage)` | Same GPU-clock migration as the octa bloom (shared anim mesh with per-vertex face-centroid in TEXCOORD1, _EngageStartTime per-instance property, shader clamp to settled shape, optional scheduled swap to the plain settled stellation from StellatedOctahedronMeshGenerator.GetSharedShieldMesh). |
| Stellated (super-shield) shatter overlay | per-frame CPU | ❌ | `Controller/Vessel/PrismStellatedOctahedronShield.cs:287-320 (Disengage)` | Identical to the octa shatter migration: pooled shatter ghost + shared anim mesh + _ShatterStartTime/_ShatterMaxOffset per-instance properties, one scheduled pool-return callback at t=1. |
| Instant engage / instant disengage (instant:true or duration<=0) | one-shot | ✅ | `Controller/Vessel/PrismOctahedronShield.cs:274-279, 314-317` | No change needed for correctness. Under the target architecture this becomes 'write _EngageStartTime = _Time.y - _EngageDuration' (already-settled clock value) — same one-shot write as the animated case, unifying the two code paths. |
| Settled-state shared-mesh swap (ApplyShieldedPose → SetRenderMeshOverride handoff) — ALREADY the 'swap to end-… | one-shot | ✅ | `Controller/Vessel/PrismOctahedronShield.cs:422-457 (ApplyShieldedPose)` | Keep this code nearly verbatim; retime its trigger. Under GPU-clocked bloom, schedule ApplyShieldedPose's render-handoff portion via a single callback at engageStart+engageDuration (PrismTimerManager already provides exactly this scheduled-callback shape for shield deactivation — reuse it), or elimi… |
| SetExoticVisualActive render-path handoff + color-continuity flush | one-shot | ✅ | `Controller/Vessel/Prism.cs:503-517 (SetExoticVisualActive)` | Under the GPU-clock migration the bloom runs on the companion entity itself (shared anim mesh + per-instance _EngageStartTime), so the shield never leaves the instanced path and these calls are deleted from the shield code. |
| Gameplay/collider/flag state on shield transitions | one-shot | ✅ | `Controller/Managers/PrismStateManager.cs:145-162 (ApplyShieldState — flags + spatial index at engage START)` | Move the rb.mass = _shieldMass write from ApplyShieldedPose into Engage() (both shield classes), next to the flag writes — gameplay fully final at t=0. Everything else in this path is already correct and becomes the template for the migrated engage: one synchronous gameplay commit, then a fire-and-f… |
| Shield material swaps (state-change material feed + shield material override) | per-frame CPU | ❌ | `Controller/Managers/PrismStateManager.cs:63-66,105-108,150-153,168-171 (materialAnimator.UpdateMaterial on every state change)` | When MaterialStateManager migrates to GPU-clocked color transitions (per-instance _ColorLerpStartTime + from/to colors in the existing _BrightColor/_DarkColor/_Spread sink family), the shield system needs no change beyond passing the target material/colors once — the one-shot UpdateMaterial call sha… |
| Pool-return / disable snap-to-clean | one-shot | ✅ | `Controller/Vessel/PrismOctahedronShield.cs:171-194 (OnDisable — Unregister + ApplyUnshieldedPose + StopShatter + ClearRenderMeshOverride + SetExoticVisualActive(false))` | No change. Under GPU-clock the cleanup shrinks: no ticker to unregister, no morph mesh to reset — just ClearRenderMeshOverride + resetting the per-instance _EngageStartTime/_ShatterStartTime properties to the sentinel 'no animation' value on pool reuse (fold into Prism.Initialize alongside the exist… |
| Test-harness toggles (triggers only, not prism update paths) | per-frame CPU | ✅ | `Controller/Vessel/PrismOctahedronShieldTester.cs:41-62 (Update polling Space / auto-toggle timer)` | No migration needed; they exercise whatever the shields do. After the GPU-clock migration they double as the in-editor verification rig for the shader bloom/shatter. |

#### E. Destruction / restoration

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| Explosion debris flight + shatter + fade (ProcessExplosions) | per-frame CPU | ❌ | `Controller/Managers/PrismEffectsManager.cs:172-184` | Pool-pull is already right (dedicated PrismExplosionPoolManager, 64 prewarm). Change TriggerExplosion into a pure stamp: write {_ExplodeStartTime = Time.timeSinceLevelLoad, _Velocity (already exists), _ExplodeSpeed, _ExplodeDuration = 5} as per-instance overrides on the companion entity (PrismRender… |
| Implosion / consume suction (ProcessImplosions, 0→1) | per-frame CPU | ❌ | `Controller/Managers/PrismEffectsManager.cs:346-431` | Stamp {_SuctionStartTime, _SuctionDuration = 2, _SuctionDirection = +1, _Location} once at StartImplosion (SuctionGraph computes progress in-shader — Docs/PRISM_ANIMATION.md §4.1); one scheduled pool-return callback at t0+2s. |
| Grow (reverse suction, StartGrow 1→0 with 0.25s delay) — currently DORMANT | per-frame CPU | ❌ | `Utility/Effects/PrismImplosion.cs:234-266` | Same SuctionGraph stamp with _SuctionDirection = −1 and the 0.25s growDelay folded into the stamp (t0 = now + growDelay — the shader clamps progress to 1 before t0, so the delay costs zero CPU frames). |
| Effect initial-condition stamp + team colors + scale | one-shot | ✅ | `Utility/Effects/PrismExplosion.cs:75-80` | Keep as-is; under the clock shader the stamp also sets visibility immediately (t=t0 evaluates to the unexploded state by construction), deleting the deferred EnableVisual contract. SetTeamColors/_pendingTeamColors machinery is untouched — it is already the per-instance-override pattern. |
| Destruction gameplay state (SetupDestruction) | one-shot | ✅ | `Controller/Vessel/Prism.cs:890-929` | No change. This is the reference for the swap discipline; the migration only replaces the VFX twin's driver (paths above). |
| Restoration (Prism.Restore) | one-shot | ✅ | `Controller/Vessel/Prism.cs:1089-1134` | Keep gameplay finality exactly as-is (collider on, index restored, volume reweighed at frame 0). Add a grow stamp at the reveal: StampGrow(handle, t0 = now, rate, startFrac = 0) so the shader blooms the visual from zero while the collider is already live — one extra one-shot write, no scheduler entr… |
| Per-frame VFX spawn caps + concurrency ceiling | one-shot | ✅ | `Controller/Prisms/PrismFactory.cs:32-38` | After the clock migration the marginal cost of a live effect is zero CPU, so MAX_ACTIVE_EFFECTS stops being a CPU guard — the remaining bound is pool size / instance count (GPU instancing absorbs thousands). |
| PrismImplosion wall-clock watchdog (per-instance Update) | per-frame CPU | ❌ | `Utility/Effects/PrismImplosion.cs:120-124` | Retire with the manager pass: under the target architecture the scheduled end callback at t0+dur IS the single completion authority, and the failure modes this watchdog hunts (state-reset bugs starving the polled completion) cannot occur. |
| Zombie-VFX safety audit (editor/dev builds only) | per-frame CPU | ✅ | `Controller/Managers/PrismEffectsManager.cs:92-97` | Retires naturally with paths 1-3: once completion is a scheduled callback keyed to a stamp, 'zombie with enabled renderer but no manager entry' has no mechanism to occur. Keep during the transition; delete (with the EnabledInstances registries) when the manager passes go. |
| Effect pool lifecycle + scene teardown | one-shot | ✅ | `Utility/Effects/PrismExplosionPoolManager.cs:12-55` | Unchanged except the caller of OnEffectComplete moves from the per-frame completion queue to the PrismAnimationScheduler entry created at stamp time. Prewarm sizing can be revisited once the 64/frame cap is lifted (path 7). |
| Event routing: OnBlockImpactedEventChannel → PrismFactory (context, one-shot) | one-shot | ✅ | `Controller/Vessel/Prism.cs:56` | Unchanged. PrismEventData is already the stamp payload; if the moving-sink buffer option is chosen for implosions, TargetTransform maps to a sink index allocated per eater at this seam. |
| Wither/devour prism-side routing (cross-reference) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/LifeForm.cs:269-295` | Prism side: inherited automatically from paths 1-2. Spindle fade: same clock recipe on the spindle material — stamp {_DeathStartTime, _DeathDuration} once (the shader already animates off _DeathAnimation; make it compute _DeathAnimation = saturate((t−t0)/dur) instead) + one scheduled callback for Di… |

#### F. Spawn / pooling / trail lay

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| Pool pull + placement (factory dispatch) | one-shot | ✅ | `Controller/Prisms/PrismFactory.cs:40-56 (one InteractivePrismPoolManager per PrismType: dolphin/serpent/sparrow/manta/squirrel/rhino/interactive/boost)` | The pull itself is already one-shot and stays. To reach 'right prism with the right material from the right pool': either widen pool keying to (type × domain-material × kind) — the Boost pool at PrismFactory.cs:48-52 is the shipped precedent for a state-dedicated pool — or keep type-keyed pools and… |
| Vessel trail-lay spawn contract (CreateBlock) | one-shot | ❌ | `Controller/Vessel/VesselPrismController.cs:172-199 (SpawnLoopAsync: UniTask loop, wavelength/speed delay)` | Keep the loop and the one-shot writes, change what they mean: pull from a (type × domain × kind) pool or stamp per-instance properties at spawn — _BrightColor/_DarkColor sinks already exist (PrismRenderService.SetColors) — plus a _SpawnTime/_BloomDuration property; GPU shader scales/blooms off the c… |
| Spawn-time domain recolor (ChangeTeam → 0.8s CPU color lerp) | per-frame CPU | ❌ | `Controller/Managers/PrismTeamManager.cs:42-52 (SetInitialTeam), 54-60 (ChangeTeam), 98-128 (HandleTeamChange → per-state material pair)` | Pool-pull with the material already right (domain-keyed pools), or one-shot: at spawn write start=previous-life colors (or target colors directly for fresh spawns — nothing was visible yet, no continuity to preserve), target=domain colors, plus _TransitionStart/_TransitionDuration per-instance prope… |
| Grow-in scale animation feed (TargetScale → PrismScaleManager sliced pass) | per-frame CPU | ❌ | `Controller/Vessel/Prism.cs:175-183 (TargetScale setter → SetTargetScale + BeginGrowthAnimation), 235-242 (ChangeSize), 250-254 (SetGrowthRate), 592-596 (ResetState re-zero scale on pooled reuse)` | One stamp at spawn: transform.localScale = TargetScale immediately (physics/gameplay final at start — colliders, spatial index, volume cache all correct from frame 0, killing RefreshVolumeCache-during-growth and the O(growing) cell churn); per-instance properties _GrowStartTime/_GrowDuration/_GrowOr… |
| CreateBlockCoroutine spawn window (waitTime stagger + creation budget + deferred collider/renderer enable) | per-frame CPU | ❌ | `Controller/Vessel/Prism.cs:31 (waitTime 0.6s default), 36-37 + 680-685 (cached WaitForSeconds), 648 (MaxCreationCompletionsPerFrame=6), 655 (LoadGateCreationCompletionsPerFrame=512), 667-758 (coroutine: stagger → budget spin-wait → SetRenderVisible(true) + blockCollider.enabled=true → BeginGrowthAnimation → SOAP created raise → spatial Register + collider-LOD notify)` | Under the law, collider and gameplay state go FINAL at spawn: enable the collider at authored size on the spawn frame (owner-clearance via layer/ignore-collision instead of a timer, or at most ONE scheduled enable callback at waitTime — no coroutine polling), register with the spatial index immediat… |
| HoldColliderAtFullSize per-frame collider compensation | per-frame CPU | ❌ | `Controller/Vessel/Prism.cs:272-322 (HoldColliderAtFullSize + coroutine: per-frame BoxCollider.size = authored*target/current inverse-compensation, per-frame localScale floor at 1% target, restore authored size on settle, onGrown callback)` | The whole coroutine evaporates under the law: transform goes to final scale at spawn (collider at authored size is automatically full world size, zero writes), the bloom is a GPU vertex-scale off the clock. |
| Boost prism pool overrides (SpawnBoostPrism) | one-shot | ✅ | `Controller/Prisms/PrismFactory.cs:48-52 (dedicated boost pool rationale), 58-64 (boostPrismGrowthRate=8, pinned to PrismScaleManager's clamp ceiling), 208-230 (SpawnBoostPrism: waitTime=0, SetGrowthRate, kind-flag leak clear)` | Keep the dedicated pool. GrowthRate becomes a per-instance _GrowDuration material property (e.g. 0.15s fast bloom vs 0.8s trail bloom) — freeing the value from PrismScaleManager's [0.05,0.1]/frame clamp so 'fast' is an authored duration, not a saturated rate. |
| BoostRingBuilder ring lay + deferred shield-kind engage | scheduled | ❌ | `Controller/Environment/Spawning/BoostRingBuilder.cs:55-76 (LayRing geometry), 83-120 (LayOne: pool spawn → ChangeTeam:99 → TargetScale:101 → Initialize:105-106 → immediate Danger kind:111-113 → HoldColliderAtFullSize(deferredKind ? apply-shield-onGrown : null):115)` | With transform-at-final-scale-from-spawn, the octahedron MeshCollider is full-size at frame 0, so PrismKinds.Apply runs for ALL kinds inline in LayOne — shield state, spatial-index shell registration, and materials final at start. |
| Environment trail lay (PrismTrailBuilder LayOne/LaySync/LayGradual/LayBatched/LayBudgetedAsync) | per-frame CPU | ❌ | `Controller/Environment/Spawning/PrismTrailBuilder.cs:44-57 (LayOne: raw Object.Instantiate — NOT pooled), 65-89 (ConfigureLaid: ChangeTeam → pose → TargetScale:73 → Initialize:77 → PrismKinds.Apply:80 → WatchForReveal:88), 107-120 (LaySync), 125-136 (LayGradual: WaitForSeconds interval coroutine), 140-152 (LayBatched: N/frame UniTask), 427-473 (CloneBatchAsync: InstantiateAsync 256-batches + stall watchdog), 488-558 (LayBudgetedAsync: shared per-frame ms budget, 250ms slice under load gate:574)` | Convert LayOne to a pool pull through the same factory channel the vessel path uses (PrismType.Interactive or a dedicated environment pool), with domain-material + kind + _GrowStartTime stamped at Get — 'right prism, right material, right pool' for the environment too, and the Blue→domain recolor le… |
| Arena-ready gate: reveal watch, poll, force-settle (grow-in compensation layer) | per-frame CPU | ❌ | `Controller/Environment/Spawning/PrismTrailBuilder.cs:98-103 (WatchForReveal list), 239-254 (SetLoadGateHolding), 264-312 (PollArenaReady: stall cap, all-clear hold), 327-357 (SettleGrowWatch: CompleteGrowthImmediately snaps, 2000/poll), 364-377 (SweepGrowWatch)` | Under GPU-clocked grow-in, reveal-readiness is arithmetic, not observation: arena ready = all lays drained (existing counters) AND now >= max(_GrowStartTime + _GrowDuration) — one comparison against a running max stamped at lay time, no per-prism watch list, no force-settle pass (or trivially: stamp… |
| Danger-trail overheat material blend (MaterialBlendUtility) | per-frame CPU | ❌ | `Controller/Vessel/VesselPrismController.cs:79-82 (_dangerMode fields), 272-285 (CreateBlock danger branch: IsDangerous flag + BeginBlend or direct sharedMaterial swap), 312-329 (Enable/DisableDangerMode)` | Danger blocks pull from the pool already wearing the danger state: set IsDangerous pre-Initialize (already done) and let PrismStateManager/PrismTeamManager select the danger material pair as the SPAWN material (one sharedMaterial write + SyncRenderMaterial). |
| SegmentSpawner / SpawnableBase orchestration (+ super-shield diagnostic) | one-shot | ✅ | `Controller/Environment/MiniGameObjects/SegmentSpawner.cs:132-187 (Initialize: seeded selection, per-domain cycling, SpawnAndLayout), 272-288 (SpawnAndLayout → spawnable.Spawn → LayoutSegment), 316-325 (NukeTheTrails: Destroy container — despawn path, no animation), 214-245 (SuperShieldSpawnedPrisms diagnostic: AddComponent + shield.Engage(instant or bloom) + flag pokes, bypassing PrismStateManager per PrismKinds.cs:19-20 note)` | No change needed for the orchestration itself. When paths 3/4 migrate, this file is untouched — it inherits conforming lays through PrismTrailBuilder. Route the diagnostic through PrismKinds.Apply/ActivateSuperShield so it exercises the same state machine gameplay uses, and give NukeTheTrails a pool… |
| Trail.TrailRenderer visual (vestigial) + Trail list bookkeeping | one-shot | ✅ | `Controller/Vessel/Trail.cs:12 (public TrailRenderer field), 39-46 (Clear → TrailRenderer.Clear()), 25-37 (Add), 58-176 (LookAhead/Project/GetBlock — read-only queries over prism transforms)` | None required. If the field is confirmed dead (no prefab wires it), delete it and the two Clear() call sites to shrink the pooled-reuse path. |
| Destruction handoff: prism to final state one-shot + pooled VFX prism (the law's model, already shipped) | one-shot | ✅ | `Controller/Vessel/Prism.cs:890-929 (SetupDestruction: scale animator OFF, collider OFF, render OFF, spatial MarkDestroyed, volume zeroed — ALL final state on the destruction frame), 955-1000 (Explode/Implode raise factory event)` | Keep the handoff shape as the template for SPAWN: mirror it so birth = pull display prism with initial conditions (timepoint, colors, scale) exactly as death already pulls debris. The VFX component's own CPU animation migrates in the effects area; once it is GPU-clocked, delete the per-frame caps. |
| Pool buffer maintenance / async incubated refills | per-frame CPU | ✅ | `Utility/PoolsAndBuffers/GenericPoolManager.cs:56-63 (inactive incubator staging), 223-244 (Prewarm), 296-343 (BufferMaintenanceAsync: EarlyUpdate loop, rate-controlled), 354-421 (RefillAsync: InstantiateAsync batches, deactivate-before-reparent), 251-264 (CreateFunc/CreateInstance miss attribution)` | No change required by the law. If pools become (type × domain × kind)-keyed, the maintenance loop generalizes per keyed sub-pool; alternatively per-instance stamping at Get keeps a single pool per type and this file fully unchanged. |
| Spawn-parameter easing lerps (XScaler / danger scale multipliers) | per-frame CPU | ✅ | `Controller/Vessel/VesselPrismController.cs:154-163 (SetNormalizedXScale), 201-212 (LerpXScalerAsync: per-frame XScaler lerp over 1.5s), 332-358 (LerpScaleMultipliers: per-frame X/Y/ZScaler lerp for danger-mode enter/exit)` | None required. Optionally replace the two ad-hoc lerp tasks with an analytic evaluation at spawn time (value = f(now - rampStart)) to drop the always-running tasks, but this is hygiene, not law. |

#### G. Ecosystem movers

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| Fauna locomotion body-prism movement (movers contract) | per-frame CPU | ✅ | `Controller/Environment/FloraAndFauna/LightFauna.cs:910-929` | No migration required — this is live gameplay data, explicitly out of the law's scope (Docs/PRISM_ANIMATION.md §1 'Animation vs. live gameplay data', §3.6). The value each frame depends on live steering/physics and could not have been computed at a start stamp. |
| Fauna level-up body bloom (GrowToScale root-scale lerp) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Fauna.cs:287-291` | Stamp per body prism: at level-up, write gameplay state final (root localScale to target immediately, spatial index shell/occupancy/volume re-stamped once), and stamp each body prism's per-instance _GrowStartTime/_GrowRate/_GrowStartFrac so the vertex shader scales the visual from oldScale/newScale→… |
| Fauna wither-from-extremities (starvation/joust death) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/LightFauna.cs:179-189` | All-stamps-at-death: at Die, compute each spindle's ring index by distance once, stamp its renderer material (or per-instance override) with _DeathStartTime = now + ringIndex*interval and _DeathDuration; the shader runs the whole cascade off the clock with zero further CPU writes. |
| Fauna devour / no-spindle wither — suction-to-mouth consume loops | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/LightFauna.cs:253-278` | Stamp _SuctionStartTime/_SuctionDuration/_Location per implosion instance and let the shader compute progress — retires the per-frame _State write. The moving mouth is the documented exception candidate (Docs/PRISM_ANIMATION.md §1): first try snapshotting the mouth position at bite time (bites are 2… |
| Boid starvation fade-out (root scale to zero) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Boid.cs:520-536` | Gameplay final at death (prism MarkDestroyed/collider off/volume zero at t0 — a dying boid should not be edible/collidable anyway), stamp a per-instance shrink (_GrowStartFrac inverted: scale 1→0 over 0.4s, or reuse the SuctionGraph toward the boid centre), schedule the husk Destroy at t0+0.4s via t… |
| Herbivore grazing / forager consumption (the ecosystem's bulk suction channel) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/LightFauna.cs:724-843` | Same as the devour entry (it is the same PrismImplosion path — one migration fixes both): SuctionGraph stamp with start time/duration/sink; sink snapshotted at bite (the creature already brakes-to-hover and holds facing for consumeHoldSeconds ≈ the suction duration, so a snapshot is likely visually… |
| Flora growth (grow tick + paced instantiation drain) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Flora.cs:100-128` | Flora-side: nothing structural — the decision tick and drain pacing survive as-is, but the drain's Instantiate becomes pool-pull + stamp (grow params _GrowStartTime=now, rate; collider/index/volume final at stamp — TryReserve already claims the site up-front, which is exactly gameplay-final-at-start… |
| Gyroid bonding movers (mound knitting — steered prisms) | per-frame CPU | ✅ | `Controller/Assemblers/GyroidAssembler.cs:377-386` | Out of the law's scope today — keep the NotifyPositionChanged contract. Opportunistic future migration IF the parent structure is verifiably static for the pull duration: snapshot bondSite at PrepareMate, stamp an exponential-approach clock animation (analytic: p(t)=target−(target−p₀)·e^−t), set col… |
| Wall bonding movers (drift-course wall assembly) | per-frame CPU | ✅ | `Controller/Assemblers/WallAssembler.cs:330-357` | Same as gyroid movers: keep the contract; if/when wall roots are static, a MoveTowards at fixed speed from a snapshot is exactly linear-in-t (analytic arrival time = dist/speed) — stamp start/velocity, gameplay state (index position, collider, Steal) final at stamp, schedule the snap. |
| Microscene conveyor recycle — container suction-out / bloom-in (Wanderway) | per-frame CPU | ❌ | `Controller/Toys/Microscene.cs:113-140` | Purest win in the area: (1) suction = per-prism SuctionGraph stamp (_SuctionStartTime=now, _SuctionDuration, _Location=container anchor) written once — colliders/index go final at stamp (unregister or move to destination immediately; the scene is off-screen and logically in transit, matching gamepla… |
| Batched lay + bloom-in (microscene first population, cell environment build) | per-frame CPU | ❌ | `Controller/Toys/Microscene.cs:84-102` | Keep the per-frame instantiation budget only until prisms are pool-pulled; then a lay is N stamps with staggered start times (t₀ᵢ = now + i·Δ) issued in one or few frames — the veil/load-gate fast-grow special case disappears (stamp t₀ in the past = already settled). |
| Cell swap — retiring-world suction (single root scale) | per-frame CPU | ❌ | `Controller/Environment/Cell.cs:1237-1273` | Per-prism suction stamp at retire time: the same walk that re-parents (or the GetComponentsInChildren the drain already does) writes each prism's _SuctionStartTime/_SuctionDuration/_Location=cell centre once — GPU runs the collapse; fixes the instanced-path gap by construction (the stamp IS the enti… |
| Cell swap — hidden drain (500 destroys/frame) + pooled returns | one-shot | ✅ | `Controller/Environment/Cell.cs:1345-1361` | Keep the slicing. Improvement aligned with 'pull from the right pool': environment prisms are Instantiated/Destroyed today — making them pool-resident turns the drain into pool returns and the rebuild into pool pulls + stamps, removing the 35k Instantiate on every swap. |
| Worm colony locomotion (kaiju rebuild — the legacy make-room shift is DELETED) | per-frame CPU | ✅ | `Controller/Environment/FloraAndFauna/WormFauna.cs` (Update → SyncBodyPrismsToIndex) | RESOLVED as option (a), Aug 2026: the legacy `Worm.cs` (its `LerpUtilities` make-room shift and dormant `MoveWorm`) was deleted with the worm-colony rebuild. The new `WormFauna` drives follow-the-leader gameplay motion under the standard `NotifyBodyPrismsMoved` movers contract — same class as fauna locomotion; segment insertion is absorbed by the follow springs (no bespoke shift animation exists to migrate). | |
| Prism.NotifyPositionChanged sink (the movers contract itself) | one-shot | ✅ | `Controller/Vessel/Prism.cs:1142-1160` | None — this is the sanctioned mechanism. Post-migration it remains the gameplay-mover contract (fauna locomotion, bonding steering); visual-transition callers (Microscene suction) stop calling it per-frame because their gameplay state goes final at stamp. |

#### H. Timers / coroutines / tweens

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| PrismTimerManager scheduled shield deactivation (THE swap-scheduler primitive) | scheduled | ✅ | `Controller/Managers/PrismTimerManager.cs:56-121` | Keep and PROMOTE: this is exactly the 'swap at the right frame' primitive. Generalize TimerAction beyond DeactivateShield (creation-complete, projectile-anchor, morph-settled, end-state pool swap); replace the O(n) per-frame scan with a min-heap on EndTime if timer counts grow. |
| Prism.CreateBlockCoroutine — per-prism spawn-hold + creation-budget spin | scheduled | ❌ | `Controller/Vessel/Prism.cs:551` | Pool-pull writes initial conditions once: _SpawnTime, _GrowDuration/rate, target scale, colors into per-instance properties (extend the SetColors sink); GPU clock runs invisible→bloom with zero further writes. |
| Prism.HoldColliderAtFullSizeCoroutine — per-frame collider inverse-compensation | per-frame CPU | ❌ | `Controller/Vessel/Prism.cs:272-322` | Dissolves completely under the target architecture: with GPU-clocked growth the TRANSFORM never animates (it sits at final scale from frame 0; the shrink lives in the shader), so the authored collider is already full-size at spawn — enable it and do nothing. |
| MaterialBlendUtility.BlendRoutine — per-frame material lerp on prism renderers | per-frame CPU | ❌ | `Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs:31-128` | Delete the utility for prisms. The per-instance color sink already exists (SetColors: _BrightColor/_DarkColor/_Spread): one-shot write of {fromColor, toColor, _BlendStartTime, _BlendDuration} instance properties, shader lerps off the clock; end state needs no swap because the target colors are alrea… |
| FireTrailBlockActionExecutor — per-frame projectile-prism movement + timed Destroy | per-frame CPU | ❌ | `Controller/Vessel/VesselActions/FireTrailBlockActionExecutor.cs:38-47` | Straight-line constant speed is the ideal GPU-clock case: pool-pull with instance props {origin, direction, speed, _SpawnTime}; shader displaces the rendered instance, zero CPU writes in flight. |
| FullAutoBlockShootActionExecutor.MoveAndAnchorAsync — per-frame MoveTowards to anchor | per-frame CPU | ❌ | `Controller/Vessel/R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs:277-346` | Deterministic flight (fixed dir, speed, travelDistance chosen at fire time): instance props {muzzlePos, dir, speed, _FireTime, stopDistance}, GPU animates the flight. |
| AOERadialBlocks.GrowToScale — detached fallback grower racing the real grow path | per-frame CPU | ❌ | `Controller/Projectiles/AOERadialBlocks.cs:214` | Delete GrowToScale outright — Prism.Initialize already owns grow-in; there is no path where the prism 'doesn't auto grow'. Under the target architecture the whole question disappears: growth is a per-instance initial condition (_SpawnTime, duration) and no spawner ever needs a fallback animator. |
| AOEDangerHemisphereBlocks.MakeDangerousAsync + GrowToScale — deferred restyle + duplicate grower | per-frame CPU | ❌ | `Controller/Projectiles/AOEDangerHemisphereBlocks.cs:205-258` | Set IsDangerous/IsShielded BEFORE Initialize (the ResetState contract explicitly supports spawner-requested pre-Initialize state — Prism.cs:579-580), killing the one-frame defer; pull from a danger-styled pool (PrismKinds already maps kind→state, PrismKinds.cs:37) so the danger palette is an initial… |
| Octahedron shield engage/shatter morph — centrally ticked per-frame CPU mesh rebuild | per-frame CPU | ❌ | `Controller/Vessel/PrismOctahedronShield.cs:259-330` | The morphs are closed-form in t: face-vertex = centroid + (v-centroid)*faceScale (+ normal*offset for shatter). Bake face centroid + face normal into the SHARED octahedron mesh (color/UV2 channels), add _MorphStart/_MorphDuration/_MorphMode instance props; a vertex shader runs the bloom/shatter off… |
| Stellated super-shield engage/shatter morph — per-prism Update() | per-frame CPU | ❌ | `Controller/Vessel/PrismStellatedOctahedronShield.cs:336-401` | Identical GPU vertex-morph migration as the octahedron shield (shared mesh + per-face centroid/normal attributes + _MorphStart/_MorphDuration instance props, PrismTimerManager one-shot at settle for the shared-mesh handoff). |
| SkimmerOvercharge BlowUpPrismsOverTime — staggered detonation ripple | scheduled | ✅ | `Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs:174-195` | Already cadence-conforming. Optional hardening: schedule the N destruction callbacks through a generalized PrismTimerManager (t0 + 0.1·i) instead of a live await-loop on an SO (survives scene teardown races, no allocation). |
| Shield/danger trigger sites — one-shot state writes (complete conforming inventory) | one-shot | ✅ | `Controller/Vessel/Prism.cs:875-888` | No cadence change. When the shield morphs go GPU-clocked, these calls become: one-shot instance-prop write (_MorphStart=now + state palette) + optional PrismTimerManager settle/deactivation callbacks — same call sites, same signatures. |
| Next-frame deferral shims (SeedAssembler bonding, AssembledArchBurst scale enforce) | scheduled | ✅ | `Controller/Vessel/VesselActions/SeedAssemblerConfigurator.cs:60-71` | Both shims dissolve when initial conditions move into pool-pull material properties (nothing zeroes the transform anymore, nothing needs a next-frame rewrite). |
| AOE spawn-stagger loops (spawn scheduling, one-shot per prism) | scheduled | ✅ | `Controller/Projectiles/AOEBlockCreation.cs:56-85` | Keep as spawn scheduling. Under the target architecture the de-spike motivation weakens (spawn = a few instance-prop writes, no coroutine, no mesh work), so several of these staggers can collapse to single-frame batch spawns with staggered _SpawnTime props — the visual stagger moves onto the GPU clo… |
| Spawner-parameter lerps (never touch spawned prisms) | per-frame CPU | ✅ | `Controller/Vessel/TrailScaleModulator.cs:33-61` | No migration required by the prism law (prisms receive only initial conditions). Optional tidy-up: evaluate scaler(t) analytically at spawn time instead of running a lerp loop, which also removes the fire-and-forget async on the controller. |
| Skimmer.DrawCircle sweet-spot markers (prism-adjacent, prisms read-only) | scheduled | ✅ | `Controller/Vessel/Skimmer.cs:195-247` | Out of scope for prism updates. If the shards ever become prisms, the 8s release must become a suction/fade with a scheduled end-swap. |

#### I. Shaders & materials (GPU side)

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| BlockGraph — the live-prism rest-state shader (static, clockless) | one-shot | ✅ | `_Graphics/Materials/Graphs/BlockGraph.shadergraph:626 (_Spread, hlslDeclarationOverride:3 = Hybrid Per Instance)` | This is the graph to extend for the clock-material architecture. Add DOTS-instanced (Hybrid Per Instance) properties: _AnimStartTime f1, _AnimDuration f1, _StartBrightColor f4, _StartDarkColor f4, _StartSpread f3, _StartScale f1 (or f3). |
| MaterialStateManager per-tick color/spread lerp → BlockGraph (the flagship violation this area enables) | per-frame CPU | ❌ | `Controller/Managers/MaterialStateManager.cs:84-85 (per-tick progress advance), :108 (PrismRenderService.SetColors per animated frame, entity path), :116 (MeshRenderer.SetPropertyBlock per animated frame, legacy path), :11-17 (header already declares this a KNOWN VIOLATION of the clock-material law)` | Pure GPU-clock replacement using the BlockGraph extension above: at transition start do ONE write — stamp _Start* = currently displayed colors (already tracked as MaterialPropertyAnimator.CurrentBrightColor/CurrentDarkColor/CurrentSpread for interruption support), _AnimStartTime = now, _AnimDuration… |
| ExplodingBlockGraph — parametric shatter shader, CPU-ticked parameters | per-frame CPU | ❌ | `_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph:686,1268,2008,2351,2510,2625 (_DarkColor/_ExplosionAmount/_Velocity/_BrightColor/_Opacity/_Spread all Hybrid Per Instance :3); :823,1533,1816 (_ExplosiveRotation/_ExplosiveSpead/_SqrDistance material constants, not instanced)` | Closest-to-done migration in the project. Add instanced _AnimStartTime f1, _Speed f1, _AnimDuration f1 (keep _Velocity, already instanced). In-graph: e = max(0, Time − _AnimStartTime); _ExplosionAmount := _Speed*e; _Opacity := saturate(1 − e/_AnimDuration); translation moves into the vertex stage: p… |
| SuctionGraph — parametric implosion/growth shader, CPU-ticked _State + per-frame moving-sink _Location | per-frame CPU | ❌ | `_Graphics/Materials/Graphs/PrismGraphs/SuctionGraph.shadergraph:1260,1289,1458,2138,2192 (_State/_DarkColor/_Location/_BrightColor/_Spread Hybrid Per Instance :3); :1318 (_Move bool, not instanced); :2402 (_SqrDistance constant)` | Two-part. (1) _State goes GPU-clock exactly like the explosion: instanced _AnimStartTime/_AnimDuration/_GrowDelay/_Direction; in-graph _State := direction-signed saturate((Time − _AnimStartTime − _GrowDelay)/_AnimDuration). |
| UnstablePrismGraph — the existing GPU-clock prism precedent (flicker runs with ZERO CPU updates) | GPU clock | ✅ | `_Graphics/Materials/Graphs/PrismGraphs/UnstablePrismGraph.shadergraph — node census: 1 TimeNode, 1 SineNode, 1 VoronoiNode, 3 BlendNodes; edge chain: Time → Add → Sine → Multiply(SqrDistance) → Voronoi(angle offset) → Blend(_UnstableColor over bright/dark) → PrismSubGraph → BaseColor + vertex Position` | Already conforming as animation. Two hygiene items when touched: (1) its 6 properties need hlslDeclarationOverride:3 if overcharged prisms should ever draw through the companion entity with per-instance colors (today a GameObject-renderer material append on an entity-rendered prism is also exposed t… |
| DOTS per-instance property plumbing — PrismRenderProperties + PrismRenderService (the stamp carrier) | one-shot | ✅ | `Controller/ECS/Rendering/PrismRenderProperties.cs:19-35 ([MaterialProperty] _BrightColor f4 / _DarkColor f4 / _Spread f3), :42-58 (_Velocity f3 / _ExplosionAmount f1 / _Opacity f1), :64-74 (_State f1 / _Location f3)` | To add {_AnimStartTime, _AnimDuration, _StartScale, _StartBright, _StartDark, _StartSpread}: (1) declare each as an exposed Shader Graph property with hlslDeclarationOverride:3 (pattern-match the working _Velocity per Docs/PRISM_ECS_MIGRATION.md §7, then Reimport so DOTS_INSTANCING_ON variants recom… |
| ThemeManager runtime material generation — the domain × state material census | one-shot | ✅ | `Controller/Managers/ThemeManager.cs:14-31 (Awake: 4 domain sets), :33-110 (GenerateDomainMaterialSet — new Material() clone of all 18 set entries + one-shot SetColor of _BrightColor/_DarkColor per domain)` | No behavioral change needed — this IS the 'right pool with the right material' half of the target architecture: pools keyed (domain × state × transparency) over these 36. |
| SpreadFresnelShader / TriangleFresnelShader HLSL family — legacy static prism-look shaders (non-instanced) | one-shot | ✅ | `_Graphics/Materials/Shaders/SpreadFresnelShader.shader:48-71 (plain CGPROGRAM/UnityCG: _Spread vertex displacement + fresnel lerp of _BrightColor/_DarkColor; properties NOT in a UnityPerMaterial CBUFFER → not SRP-Batcher compatible; no DOTS instancing; no _Time)` | Do not extend. If DartBlock/TriBlock/TriangleBlock prefabs are still spawnable prisms, rebase their materials onto BlockGraph theme materials during the migration so they inherit the clock properties; otherwise mark the family decor-only. Delete the dead PrismShader.shader stub. |
| Spindle phase-variant materials + _DeathAnimation fade (adjacent flora path — both the best precedent and a ma… | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Spindle.cs:37-38,103-125 (8 quantized _Phase variant materials bucketed by world-position hash — sway desync with SHARED materials, zero per-renderer state: the exact 'material that accepts the initial conditions' pattern), :189-250 (violation: condense/evaporate coroutines tick _DeathAnimation 0↔1 via MPB every frame for ~1s)` | Same recipe as prisms: add _FadeStartTime/_FadeDirection (instanced, or quantized shared fade materials as the file suggests); in-graph _DeathAnimation := direction-signed saturate(Time − _FadeStartTime). |
| Wider GPU-clock precedent inventory (crystal/effect graphs) — patterns ready to copy | GPU clock | ✅ | `36 shader graphs contain a TimeNode (full list from grep): notably _Graphics/Materials/Graphs/ShepardGraph.shadergraph (Time→Modulo(_Period)→looping vertex ripple bounded by _Start/_Stop, with _Ease/_velocity — a material-parameterized, endlessly-looping clock animation with zero CPU writers: no script in the repo sets _Start/_Stop/_Period), CrystalGraph.shadergraph and SkimmerGraph.shadergraph (TimeNode AND already Hybrid Per Instance — cited as the pattern source in Docs/PRISM_ECS_MIGRATION.md:369), ExplodingCrystalGraph, AnimatedSpindleGraph, ForceFieldGraph, RippleGraph, LaserGraph, WispGraph, SkyBoxGraph, + 13 Lifeform_World graphs` | No action; reference material. ShepardGraph is the copy-paste template for windowed clock behavior (start/stop/period params), CrystalGraph/SkimmerGraph for Hybrid-Per-Instance flags, Spindle phase buckets for quantized initial-condition materials when a per-instance prop is not warranted. |

#### J. Discovery sweep

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| ClearPrisms camera-occlusion transparency | per-frame CPU | ❌ | `Controller/Vessel/ClearPrisms.cs:84-105` | This is view-dependent (live camera+vessel line) so it can never be stamp-once per prism — but it needs ZERO per-prism CPU: write the camera→vessel segment + capsule radius as 2-3 GLOBAL shader uniforms once per frame and compute the occlusion alpha in the prism shader from world position vs the lin… |
| MaterialBlendUtility per-renderer coroutine blends | per-frame CPU | ❌ | `Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs:31-70` | Delete the utility. Overcharge mark: stamp per-instance blend params through the existing PrismRenderService color sinks (_BrightColor/_DarkColor/_Spread) — {_BlendStartTime, from/to colors} — and let the shader lerp off the clock; at blend end the prism is already in the end-state colors (no swap n… |
| VesselPrismController danger-material stamp on spawn | one-shot | ❌ | `Controller/Vessel/VesselPrismController.cs:272-285` | One-shot in shape but wrong in mechanism: a bare sharedMaterial swap without Prism.SyncRenderMaterial() is invisible under the companion-entity path (the documented renders-nothing anti-pattern), and it bypasses PrismStateManager.MakeDangerous (which owns danger state + shield mutual-exclusion). |
| CloakSeedWall cloak/uncloak (Serpent) | one-shot | ❌ | `Controller/Vessel/R_VesselActions/Executors/CloakSeedWallActionExecutor.cs:360-398` | The triggers are one-shot but they feed the multi-frame CPU color-lerp manager. Migrate the blend itself to the clock-material: stamp {_CloakStartTime, _CloakDirection, from/to color pairs} per instance via PrismRenderService sinks, GPU runs the dissolve; at the scheduled end swap the prism to the t… |
| WallAssembler / GyroidAssembler magnet-steering of mate prisms | per-frame CPU | ❌ | `Controller/Assemblers/WallAssembler.cs:330-357` | Once a mate is chosen the trajectory is deterministic: reserve the bond site immediately (PrismSpatialIndex.TryReserve — occupancy/gameplay to FINAL state at start), stamp {startPose, targetPose, _StartTime, duration} into per-instance params and let the GPU interpolate the matrix (entity-side, same… |
| Sparrow FullAutoBlockShoot turret prisms (launch + anchor) | per-frame CPU | ❌ | `Controller/Vessel/R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs:136-147` | The flight is a pure function of time: p(t) = muzzle + dir·min(speed·t, stopDistance). Stamp {_StartTime, _Velocity, _StopDistance} per instance; the GPU moves the visual and eases the grow-in on the same clock (no per-frame CPU position writes). |
| AOERadialBlocks bespoke parallel grower | per-frame CPU | ❌ | `Controller/Projectiles/AOERadialBlocks.cs:200-221` | Delete the fallback GrowToScale entirely — Prism.Initialize's CreateBlockCoroutine + scale animator already own grow-in (the 'fallback' actively races the manager and stomps its slice writes). |
| AOEDangerHemisphereBlocks danger prisms (material clone + bespoke grower) | per-frame CPU | ❌ | `Controller/Projectiles/AOEDangerHemisphereBlocks.cs:200-216` | Three fixes in one: (1) state via PrismStateManager.MakeDangerous (owns danger/shield mutual exclusion + routes material with SyncRenderMaterial — the raw .material clone is both a leak and likely invisible under the entity path); (2) delete the bespoke GrowToScale — grow-in belongs to the (future G… |
| Microscene conveyor suction/bloom recycle (Wanderway) | per-frame CPU | ❌ | `Controller/Toys/Microscene.cs:112-132` | Scale-about-a-pivot is a pure function of time: stamp {_PivotWorldPos, _TransitionStartTime, _Duration, _FromScale, _ToScale} per instance (or one shared per-scene constant block) and compute the collapsed matrix in the shader — zero per-frame CPU, no per-frame entity matrix writes. |
| Cell.RequestCellSwap world suction + sliced drain | per-frame CPU | ❌ | `Controller/Environment/Cell.cs:1262-1273` | Same pivot-collapse-on-the-clock as the Microscene: stamp {_SuctionCenter, _SuctionStartTime, _SuctionDuration} once (a per-cell shader constant or per-instance stamp walked once) and let the GPU collapse every instance; one CPU write total. |
| ShapeDrawingManager environment shrink-to-outline (Phase 2, dormant) | per-frame CPU | ❌ | `Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs:427-461` | Stamp per-instance {startPose, targetPose, _StartTime, _Duration}; GPU interpolates both position and scale off the clock; spatial index goes to the final outline position at start (or simply unbinds — the environment is 'nuked' for the drawing mode anyway). |
| Fauna level-up body growth (parent-scale over body prisms) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Fauna.cs:302-315` | Stamp {_GrowStartTime, _FromScale, _ToScale, _PivotWorldPos(=fauna origin at stamp)} on the body prisms' instances; GPU scales about the pivot on the clock; body-prism colliders + spatial index go to the final scale/position at start (fauna colliders are already coarse); one scheduled callback settl… |
| Boid despawn shrink + boid prism Grow feeders | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Boid.cs:565-576` | Shrink-out: stamp {_ShrinkStartTime, duration} and let the GPU run it; scheduled callback at the end pool-returns the boid (Destroy in a gameplay loop is already an anti-pattern). |
| TrailViewer sliding transparency window (dormant legacy) | per-frame CPU | ✅ | `Controller/Vessel/TrailViewer.cs` (DELETED, D2 2026-08-02 — component excised from Urchin.prefab) | If the feature returns: pass the attachment world position + window radius as global shader uniforms and fade in the prism shader — zero per-prism CPU, no material churn. |
| TrailBlockBufferManager pre-instantiation buffer (dormant legacy) | one-shot | ❌ | `Controller/Projectiles/TrailBlockBufferManager.cs:63-76` | Delete. PrismFactory's pools + team material sets already own this; any resurrection must pull pooled prisms whose domain material is the pooled initial condition (sharedMaterial + SyncRenderMaterial, never .material). |
| GunVesselTransformer slide Grow/Steal trigger | one-shot | ✅ | `Controller/Vessel/GunVesselTransformer.cs:91-103` | Trigger is already one-shot and event-driven — conforming. It inherits whatever the Grow/Steal pipelines become: Grow restamps {_GrowStartTime, from→to scale}, Steal restamps the color-blend clock params (see PrismTeamManager entry). |
| PrismStateManager / PrismTeamManager state + team changes (the MaterialStateManager feeder mouths) | one-shot | ❌ | `Controller/Managers/PrismStateManager.cs:63` | These are the highest-value migration point: the entire color animation is start-color→target-color over a fixed duration — a textbook clock material. Stamp {_BlendStartTime, _FromBright,_ToBright,_FromDark,_ToDark,_FromSpread,_ToSpread} per instance at the trigger (the UpdateMaterial call site beco… |
| Prism.SetTransparency instant swap | one-shot | ✅ | `Controller/Vessel/Prism.cs:1055` | Already the target shape (pool-state material + one-shot swap + entity sync). Under the full law, callers that want a VISIBLE fade (continuity) pair it with a stamped dissolve (see CloakSeedWall entry) and keep this as the end-state swap. |
| PrismColliderLodManager (collider LOD, gameplay state) | per-frame CPU | ✅ | `Controller/Managers/PrismColliderLodManager.cs:27-31` | Not an animation path — the locked decision's collider clause ('gameplay state may go to final state at start') is orthogonal to and compatible with LOD culling. No migration; just ensure future animation migrations set collider state once at animation start and let LOD own it thereafter. |
| PrismTimerManager scheduled expiries | scheduled | ✅ | `Controller/Managers/PrismTimerManager.cs:20` | Already conforming — and it is exactly the 'at the right frame, seamlessly swap to the end-state prism' scheduling primitive the target architecture needs. Reuse it as the swap scheduler for every migrated path. |
| SkimFxRunner ship→prism stretch beam | per-frame CPU | ❌ | `Controller/ImpactEffects/EffectsSO/Helpers/SkimFxRunner.cs:35-69` | The prism end is static and the ship end is LIVE data — the sanctioned pattern is a pooled beam whose shader reads the vessel position from a per-vessel GLOBAL uniform (one CPU write per vessel per frame, shared by all beams) with {_PrismPos, _StartTime, _Duration} stamped per instance; pool the bea… |
| Spindle evaporate fade (flora/fauna limb rods — prism-adjacent) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/Spindle.cs:189-209` | Textbook clock material: stamp _DeathStartTime (+ speed) once and compute deathAnimation = saturate((time-start)*speed) in the spindle shader; one scheduled callback despawns/pool-returns at completion. Same fix pattern as the prism paths even though it's outside the prism instanced pipeline. |
| Fauna variant tuning spindle material swap + body prism retarget | one-shot | ✅ | `Controller/Environment/FloraAndFauna/Fauna.cs:353-368` | Already conforming (initial conditions at spawn). Under the GPU-grow migration, TargetScale retarget becomes part of the spawn stamp. |
| Flora/assembler spawn feeders (AssembledFlora, BranchingFlora, WallAssembler conversion) | one-shot | ✅ | `Controller/Environment/FloraAndFauna/AssembledFlora.cs:253-273` | Spawn-time writes are the conforming half; they inherit the GPU grow-in when the spawn pipeline migrates. The non-conforming halves are itemized separately (assembler steering). |
| PrismFactory team-color MPB stamp (spawn path detail) | one-shot | ✅ | `Controller/Prisms/PrismFactory.cs:312-334` | Already the target pattern (initial conditions loaded at pull). Ensure the entity path mirrors via PrismRenderService color sinks (it does — SetColors). |
| PaintingRunner toy-station transitions (prism-material geometry, not prisms) | per-frame CPU | ❌ | `Controller/Toys/PaintingRunner.cs:278-313` | Optional under the prism law but same recipe: stamp {_TransitionStartTime, from/to scale} and run the ease in-shader (the prism material already ships per-instance color properties; adding the clock scale would cover all toy geometry), or accept as UI-class animation if toys are explicitly out of sc… |
| Test/editor scaffolding (classify-only) | per-frame CPU | ✅ | `Controller/ECS/Rendering/PrismRenderStressTest.cs:238-286` | Exempt (test scaffolding; never ships in gameplay scenes). Keep the stress test as the regression harness FOR the migration: its 'movingFraction' mode measures exactly the per-frame-write cost the clock-material work eliminates. |

#### K. Completeness-critic additions

| Path | Cadence | Verdict | Where | Migration |
|---|---|---|---|---|
| GAP: SpawnableCord cord-wave prism mover (SeaweedFlora) | per-frame CPU | ❌ | `Controller/Environment/FloraAndFauna/SpawnableCord.cs:206-230 (Update: driven-vertex sine + energized-queue propagation, up to 300 vertex checks/frame)` | This is exactly the SpindleGraph precedent: bake the sway into a GPU clock material (per-instance _Phase/_Amplitude/_Axis stamped once at lay from the cord parameterization, TimeNode-driven sine in the shader). Colliders stay at rest pose (gameplay state = final state at start). |
| GAP: SpawnableFlower bespoke branch lay (AOEFlowerSpawner) | one-shot | ❌ | `Controller/Projectiles/SpawnableFlower.cs:101-119 (CreateBlock: raw Instantiate + ChangeTeam + TargetScale + Initialize — bypasses PrismTrailBuilder and pooling)` | Same recipe as the other environment lays: pool-pull via the PrismFactory channel (BoostRingBuilder.LayOne or PrismTrailBuilder.LayOne) with a bloom-clock material stamped with spawn timepoint + target scale + domain colors; collider enabled at final size on frame 0; scheduled swap to the rest-state… |
| GAP: SpawnableDartBoard ring lay | one-shot | ❌ | `Controller/Environment/MiniGameObjects/SpawnableDartBoard.cs:60-96 (per-ring Instantiate loop: ChangeTeam(Jade/Ruby alternation) + TargetScale + Initialize + WatchForReveal, bypasses PrismTrailBuilder.LayOne per its own comment)` | Identical to SpawnableFlower: route through the shared conforming lay primitive (pool pull + initial-conditions bloom material + end-state swap). The WatchForReveal load-gate integration already models 'snap to end state' — under the new architecture the gate simply stamps the material clock to its… |
| GAP: AstroLeague ball per-tick prism sweep (shield / unshield / eat) | per-frame CPU | ✅ | `Controller/Arcade/AstroLeague/AstroLeagueBall.cs:570-640 (ProcessPrismInteractions: PrismSpatialIndex.QuerySphere sweep over the segment travelled each physics tick, on EVERY peer)` | No change to the ball itself — gameplay state already goes final immediately. The load it generates migrates for free once shield engage/disengage becomes a material-clock swap: ActivateShield = pool-swap (or mesh+material override) to a shielded prism whose engage-bloom clock starts at the stamp; D… |
| GAP: AstroLeague arena edge lining lay + field-reset devastate sweeps | one-shot | ✅ | `Controller/Arcade/AstroLeague/AstroLeagueArena.cs:149-220 (RebuildEdgeLining: super-shielded Blue lining laid per peer via BoostRingBuilder.LayOne on the PrismFactory channel)` | Lay side: inherits the BoostRingBuilder migration (pool-pull, full-size collider frame 0 — already the law's collider model — bloom on the material clock). |
| GAP: NudgeShard cytoplasm steal trigger | one-shot | ✅ | `Controller/Environment/Cytoplasm/NudgeShard.cs:28-47 (OnTriggerEnter: Squirrel vessel → foreach prism in Prisms: prism.Steal(player, domain))` | Nothing to change here — one-shot state write. It conforms fully once the repaint it triggers becomes a color-transition clock material (stamp old+new domain color pairs + transition start time, GPU lerps, swap to the flat end-state material on the scheduled tick). |
| GAP: SquirrelTubeActionExecutor — tube lay trigger + pool-return pop-out teardown | one-shot | ❌ | `Controller/Vessel/R_VesselActions/Executors/SquirrelTubeActionExecutor.cs:141-151 (SpawnTubeAsync: rings-per-frame UniTask loop into BoostRingBuilder.LayRing — Boost pool, danger kind)` | Lay inherits the BoostRingBuilder migration. Teardown: swap each tube prism to a pooled fade/suction prism (SuctionGraph-style clock material stamped with start time + sink), disable the collider immediately (gameplay to final state at start), schedule one callback (PrismTimerManager) to pool-return… |
| GAP-DETAIL: ShapeDrawingManager captured-trail shrink bypasses the render bridge (+ pool-detach and event-driv… | per-frame CPU | ❌ | `Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs:384-461 (ShrinkPrismsIntoShape: per-frame transform.position + localScale Lerp on captured PLAYER trail prisms — no SyncRenderTransform / NotifyPositionChanged, so the instanced companion never sees the move)` | When Phase 2 is ported: the shrink-into-outline is a per-prism start-pose → target-pose interpolation with a known duration — ideal for a clock material with per-instance start/end transforms (stamp both, GPU interpolates, swap to a static miniature at the end tick). |
| GAP-MINOR: HealthPrism Explode/Implode overrides + effect-SO trigger mouths not enumerated | one-shot | ✅ | `Controller/Environment/HealthPrism.cs:73-98 (Explode/Implode overrides: spindle + LifeForm bookkeeping wrapped around base — one-shot, conforming)` | No behavioral change. Migration caveat: the end-state-swap primitive must dispatch through the prism's virtual Explode/Implode (or replicate HealthPrism's unhook-before/notify-after ordering) so lifeform bookkeeping and the LifeFormCrystal drop guarantee survive the swap. |
<!-- AUDIT_TABLE_END -->

### 3.8 Latent bugs & findings surfaced by the sweep

Fix these DURING the migration (most disappear by construction under stamp+clock):

1. **Cell swap suction is invisible on the instanced path** — `Cell.RequestCellSwap`'s
   1.1 s retiring-world suction scales a root transform but never syncs child prisms'
   companion entities (zero `PrismRenderService`/`NotifyPositionChanged` references in
   `Cell.cs`), so entity-rendered prisms stand at full size then vanish at the drain.
   The per-prism suction stamp fixes this by construction. (`Microscene.AnimateScaleAsync`
   shows the expensive-but-correct per-frame notify alternative — both extremes collapse
   into the stamp.)
2. **Rogue color writers are blind on the instanced path**: `ClearPrisms`' per-physics-tick
   `_Alpha` MPB fade, `MaterialBlendUtility`'s `_Color`/`_EmissionColor` coroutine blends
   (also the wrong property names for the prism shader), and bare `sharedMaterial` swaps
   without `SyncRenderMaterial` in `VesselPrismController`/`TrailViewer` — all write the
   disabled MeshRenderer.
3. **AOE double-growers**: `AOERadialBlocks.GrowToScale` + `AOEDangerHemisphereBlocks.GrowToScale`
   run bespoke per-frame scale loops RACING `PrismScaleManager` on the same prisms, without
   `SyncRenderTransform`/`RefreshVolumeCache` — their writes never reach the screen between
   manager steps on the instanced path. Also: `growthRate` FIELD writes there are dead on
   pooled prisms (`SetGrowthRate` exists for this), and `AOEDangerHemisphereBlocks` uses
   `renderer.material` (banned clone).
4. **Stellated shield idles a per-prism `Update()` forever** — `PrismOctahedronShield`
   was migrated to the central `PrismOctahedronShieldManager` ticker (registered only
   while morphing) but `PrismStellatedOctahedronShield` still self-ticks; a super-shielded
   track carries thousands of standing early-return Updates. Cheapest interim fix in the
   area even before the GPU migration.
5. **`FireTrailBlockActionExecutor`** bypasses pooling, moves its projectile prism per
   frame, and `Destroy()`-timers it — a clock-law violation AND an imposed-despawn
   ecosystem-law violation.
6. **`PrismImplosion` wall-clock watchdog** — per-instance `Update()` for a 4 s timeout;
   belongs on the scheduler.
7. **Orphans**: `TrailBlockBufferManager` — confirmed unreferenced, DELETED
   2026-08-01. `TrailViewer` — the sweep's "no references" claim was WRONG:
   `Urchin.prefab` carried it (GUID check). Component excised from the prefab by
   fileID + file DELETED (D2, 2026-08-02).
8. **Dormant**: `PrismType.Grow` / `PrismImplosion.StartGrow` has no live raiser;
   `ShapeDrawingManager` shrink-to-outline (Phase 2, dormant) also bypasses the render
   bridge and spatial index.
9. **Spindles** (flora/fauna limb rods — prism-adjacent, same law family): per-frame MPB
   `_DeathAnimation` fade breaks SRP batching mid-fade; migrate alongside with clock
   inputs on the spindle material.
10. **✅ FIXED 2026-08-02 — the shield engage-morph ate the grow stamp of every
    shielded environment prism** (the C13 live repro: `[PrismClock] STRICT MODE: no
    companion render entity to stamp (grow:SpawnablePrism (Clone))`). Two independent
    defects compounded, both now closed — see §4.5:
    - `Prism.ApplyRenderPath` only created the companion entity on the frame it wanted
      to SHOW the prism, and refused while `_exoticVisualActive`. A shield `Engage()`
      runs a 0.35 s per-face bloom, but `CreateBlockCoroutine` reveals the prism after
      `waitTime` = 0.1 s (**one frame** under the load gate) — so the reveal landed
      *inside* the exotic window, `EnsureRenderEntity` was skipped, and the ONE-SHOT
      grow stamp had no target. Deterministic, not a race.
    - Nothing suppressed the morph at spawn. Every `ShieldedSpawnablePrism`
      (`prismProperties.IsShielded` baked true → `Prism.Initialize` calls
      `ActivateShield()`) and every environment prism carrying `PrismKind.Shielded` /
      `SuperShielded` via `PrismKinds.Apply` (Yggdra, Orrery, Zephyr, Caldera, Geode,
      Atlantis, the Wanderway conveyor's palette) hit it — i.e. the HexRace track and
      the freestyle six.
    Collateral now gone with it: those prisms drew from the un-batched GameObject
    MeshRenderer for the whole morph, each registered with
    `PrismOctahedronShieldManager` and rebuilt a per-prism morph mesh **every frame**
    during the heaviest frames of an arena build, and each fired a `ShieldActivate`
    SFX at lay time.

**Verified clean (no prism update path — do not re-audit)**: Rewind system, warp/flow
fields, `SkimmerAlignPrismEffectSO` (reads prisms, writes the vessel), network sync
(prisms are per-peer local), PhotoBooth/Recording tools, mines, cell phase transitions
(no recolor), DOTween (zero prism usage — all UI), Animator/Animation components on
prism prefabs (none).

---

## 4. Target architecture

### 4.1 Shader side

Four Shader Graphs own the prism look today (audit lens I):

- **`BlockGraph`** — ALL live-prism rest states (normal opaque, shielded ×2,
  super-shielded ×2, danger ×2, cloak). Static — no Time node. Its vertex stage
  already runs `SpreadSubGraph` (object-scale-compensated normal offset), so a
  clock scale multiplier slots in cleanly.
- **`ExplodingBlockGraph`** — explosion VFX pool AND (quirk) the transparent live
  prism (`TransparentPrismMaterial` rests at `_ExplosionAmount = 0`).
- **`SuctionGraph`** — implosion/growth VFX pool.
- **`UnstablePrismGraph`** — overcharge flicker: **the existing GPU-clock precedent**
  (Time → Sine → Voronoi, runs with ZERO CPU updates). The law asks every animated
  state to work the way this one already does.

(Plus a legacy hand-HLSL family — `SpreadFresnelShader`/`TriangleFresnelShader`, 14
materials on old trail prefabs — and a dead stub `BlockMaterials/PrismShader.shader`.)

Each animated graph gains clock inputs, all **Hybrid Per Instance**
(`hlslDeclarationOverride: 3` — the §7 recipe in `Docs/PRISM_ECS_MIGRATION.md`):

| Graph | New per-instance properties | Behavior |
|---|---|---|
| `BlockGraph` | `_GrowStartTime`, `_GrowRate` (k), `_GrowStartFrac` (scale fraction at t₀); `_ColorStartTime`, `_ColorDuration`, `_StartBrightColor`, `_StartDarkColor`, `_StartSpread` | Vertex: scale factor `s(t) = 1 − (1−s₀)·exp(−k·(t−t₀))` about the prism origin (the entity's `LocalToWorld` is at FINAL scale from the start). Fragment: colors = `lerp(start, material's authored values, smoothstep((t−t₀)/dur))` — **the target is the bound material's authored colors, so no `_Target*` properties are needed** and the settle swap is just `SetMaterial` (already one-shot). Authored defaults (`_GrowStartFrac = 1`, `_ColorDuration = 0`) make every existing material render the settled end state unstamped. |
| `ExplodingBlockGraph` | `_ExplodeStartTime`, `_ExplodeSpeed`, `_ExplodeDuration` (the flight velocity is the graph's existing world-space `_Velocity` — ONE stamped vector shared with the shatter-spin axis chain) + the grow trio and color five, because transparent LIVE prisms rest on this graph: they bloom via the same `PrismGrowScale` cluster and fade colors via the same `PrismColorLerp` cluster as BlockGraph (bright/dark intercepted at the Prism Sub Graph feeds, spread at the explosion spread-chain Add) | Vertex: object-space offset computed **on the GPU inside `PrismExplosionClock`** as `mul((float3x3)GetWorldToObjectMatrix(), _Velocity·(t−t₀))` — a raw, unnormalized inverse-model multiply. **Never Shader Graph's Direction-mode Transform node**: it emits `TransformWorldToObjectDir`, which NORMALIZES — magnitude destroyed, direction re-skewed by the prism's non-uniform scale (the wrong-vector bug). No CPU-side matrix math on the animation path. `amount = _ExplodeSpeed·(t−t₀)`; `opacity = 1 − (t−t₀)/_ExplodeDuration`. Entity transform never moves after the stamp; **`RenderBounds` are reset to the mesh then expanded at stamp time to cover the whole flight envelope** (`ResetBoundsToMesh` + `ExpandBoundsForClockAnimation` — bounds are the one CPU-side consumer, because frustum culling itself runs on the CPU; without this, debris culls against the unexploded box). Defaults render the rest state (the transparent-prism quirk keeps working unstamped). |
| `SuctionGraph` | `_SuctionStartTime`, `_SuctionDuration`, `_SuctionDirection` (grow=−1 / implode=+1), `_GrowDelay` (keeps `_Location`) | `progress(t)` computed in-shader; `_Location` stamped once (moving-target exception per §1 if retained). |

Implementation constraints (verified by the capability audit):

- New `[MaterialProperty]` structs must be added to the **prototype archetypes** in
  `PrismRenderService.GetPrototype` so every stamp stays non-structural
  `SetComponentData` — `AddComponentData` on a live entity is a per-prism structural
  change, the exact cost the prototype pattern kills. Fold the clock params into the
  three EXISTING override sets (Prism / Explosion / Implosion).
- float/float4 sizes are safe; float3 works today (`_Spread`/`_Velocity`/`_Location`)
  but `PRISM_ECS_MIGRATION.md` §7 documents the size-mismatch fallback to float4.
- Instanced rendering is ON in the shipped config
  (`Assets/Resources/PrismRenderConfig.asset`, `useInstancedRendering: 1`); per-instance
  values live in Entities Graphics' persistent GPU buffer — a stamp is genuinely
  write-once.
- **The clock path rides the instanced renderer, with NO fallback (strict mode)**: a
  prism without a usable companion entity (no ECS world / tool scenes / pre-first-show
  / exotic-morph windows) gets its one-shot gameplay-final state and NO visual
  transition — silent where that is a normal transient (fresh spawn paint, morph
  overlay windows), screaming via `PrismClockDiagnostics.WarnNoRenderEntity` where the
  render path is genuinely down. There are no MaterialPropertyBlock twins and no CPU
  animation tier; the former managers are DELETED (tracker D2, 2026-08-02).
- Currently NON-instanced properties that must not be driven per-instance without
  flipping the flag first: `_SqrDistance`, `_Alpha` (BlockGraph),
  `_ExplosiveRotation`/`_ExplosiveSpead` (ExplodingBlockGraph), `_Move` (SuctionGraph),
  and ALL of UnstablePrismGraph.

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
  become clock predicates; `HoldColliderAtFullSize` deleted. `PrismScaleAnimator.
  ExecuteOnScaleComplete` (volume SOAP raise + IsLargest shield engage) moves to the
  stamp — `UpdateVolume` already computes from `TargetScale`, so it is start-safe
  verbatim. **PhaseThresholds must be re-baselined** against volume-final-at-spawn
  (today volume ramps in over the bloom; see `Docs/ECOSYSTEM.md` §18's measuring tool).
- **Three whole compensation subsystems exist ONLY to patch CPU-clocked grow-in and
  delete under the law**: `HoldColliderAtFullSize` (per-frame collider inverse-resize;
  sole caller `BoostRingBuilder`), the `CreateBlockCoroutine` waitTime/creation-budget
  window (colliders disabled 0.6 s, 6 completions/frame — a spawn becomes ONE stamp,
  with a staggered t₀ where the stagger was aesthetic), and the arena-ready gate's
  per-prism reveal watch + force-settle sweep (+ its three load-gate boost knobs) —
  settling becomes a pure clock predicate.
- **The shipped structural template**: `SetupDestruction` + pooled VFX twin is
  already exactly the law's shape — gameplay state one-shot final at frame 0, a
  pooled effect prism seeded with initial conditions, swap at completion. Cite it;
  copy it.
- **Retirements when migration completes**: `PrismScaleManager`,
  `MaterialStateManager`, `PrismEffectsManager`'s per-frame passes,
  `AdaptiveAnimationManager`'s frame-skip machinery for these paths, and the
  `MAX_ACTIVE_EFFECTS`/64-per-frame VFX caps (they exist purely to bound the
  per-frame CPU apply; the 64/frame skip is itself a continuity-law breach under
  burst load that the migration removes).

### 4.3 Pools

"The right prism from the right pool" = the pool hands out instances whose material
already accepts the stamp: prism pools keyed by prefab as today, with the material
chosen at stamp time from the theme set (domain × state × opacity) exactly as now —
the difference is that the material choice + override stamp happen ONCE, at
touchpoint 1, never per frame. Effect pools (explosion/implosion) likewise: the
pulled instance's material is the animated graph; the stamp is its initial
conditions; `OnEffectComplete` (the scheduled end) returns it to the pool.

Audit findings to fix here: pools are keyed by prism TYPE only (one
`InteractivePrismPoolManager` per vessel type + Interactive + the Boost pool) —
"right material from the right pool" is currently true only when a pooled prism is
reused into the same domain (`ChangeTeam` no-ops); every cross-domain reuse runs the
0.8 s CPU repaint today. Under the law that repaint becomes a stamped clock lerp
(or an instant swap where no transition is wanted — spawn-paint of a fresh prism is
a *creation*, not a recolor of existing mass, so the grow-in bloom alone can carry
the continuity and the domain material can be final from frame 0). Also:
`PrismTrailBuilder.LayOne` uses raw `Object.Instantiate`, not pools, and starts
`Domains.Blue` — environment lays should pull pooled prisms with the final domain
material.

---

### 4.4 Phase A infrastructure + in-editor wiring protocol

Shipped 2026-07-31; **made STRICT (always-on, no toggle, no fallback) 2026-08-01 at
the prompter's direction.** `ClockAnimationEnabled` is constant `true`; the former
`UseClockAnimation` opt-in was removed. The pieces:

- **`Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl`** — the four Custom
  Function bodies: `PrismGrowScale`, `PrismColorLerp`, `PrismExplosionClock`,
  `PrismSuctionClock`. Every function treats rate/duration ≤ 0 as "unstamped" and
  returns the settled end state or the legacy CPU-fed parameter, so wiring the graphs
  changes nothing until a stamp arrives.
- **`PrismRenderProperties.cs`** — the 15 clock override structs (Prism / Explosion /
  Implosion sets), added to the **prototype archetypes** in
  `PrismRenderService.GetPrototype` when the toggle is on (stamps stay non-structural).
- **`PrismRenderService`** — `ClockAnimationEnabled` (+ `SetClockAnimationOverride`
  A/B hook, clears the prototype cache), and the touchpoint-1 stamp APIs:
  `StampGrow`, `StampColorTransition`, `StampExplosionClock`, `StampSuctionClock`,
  `ClearPrismStamps`. All return false when dark → callers keep the legacy path.
- **`PrismClock`** (`_Scripts/Utility/`) — the single clock: stamps read `Now`
  (`Time.time`) and a hidden publisher writes the same value to the `_PrismClock`
  global uniform every frame (the graphs' Clock input — never the Time node),
  plus `SettledPast` for behind-the-veil instant settling.
- **`PrismTimerManager.ScheduleAction` / `CancelScheduledActions`** — the generalized
  touchpoint-3 scheduler (flat list, one delegate per animation event).
- No MPB stamp twins, by decision (§4.1): the clock path rides the instanced
  renderer; GameObject-fallback prisms keep the legacy CPU managers until those
  retire.

**Phase B1/B2 call sites (LIVE — strict, the only path):**

- **Grow-in (B1)** — `PrismScaleAnimator` stamps instead of engaging
  `PrismScaleManager` when the clock path is live: transform/entity matrix/volume go
  FINAL at the stamp, completion side effects (`ExecuteOnScaleComplete`: volume SOAP
  delta + IsLargest shield) run at the START, settledness is the analytic
  `t₀ + ln(d₀/ε)/k` clock predicate (`IsVisuallyGrowing` / `IsVisuallySettled`, read
  by `Prism.IsGrowing` / `IsSettledForReveal`), retargets re-stamp from the
  analytically-current per-axis fraction (shrinks included — fractions above 1
  converge down), and `CompleteGrowthImmediately` becomes a stamp rewrite into
  `PrismClock.SettledPast`. Pre-creation growth requests defer to the
  creation-complete stamp — a **known visual delta**: prisms bloom from zero at
  reveal instead of inheriting the legacy 0.6 s invisible head start (more
  continuity-law-correct, slightly later apparent growth). Created/delta SOAP volume
  accounting is preserved exactly (the created event captures its volume before the
  stamp mutates it).
- **Explosion/implosion (B3)** — `PrismExplosion.TriggerExplosion` stamps
  `{t₀, speed, MaxDuration, velocity, velocityOS}` and shows the entity immediately
  (the stamp IS the correct initial state — no unanimated-mesh flash to hide); the
  entity transform holds the initial pose forever; ONE scheduled `OnEffectComplete`
  returns it to the pool. Two playtest-found fixes are part of the contract:
  the world→object conversion of the flight velocity happens **on the GPU inside
  `PrismExplosionClock`** as a raw `(float3x3)GetWorldToObjectMatrix()` multiply —
  never Shader Graph's Direction-mode Transform node, whose
  `TransformWorldToObjectDir` NORMALIZES (magnitude destroyed, direction re-skewed
  by non-uniform scale — the wrong-vector bug); the CPU stamps ONE world-space
  `_Velocity` shared by the flight offset and the shatter-spin axis chain. And
  `RenderBounds` are **reset to the mesh then expanded to the full flight envelope**
  at the stamp (`ResetBoundsToMesh` + `ExpandBoundsForClockAnimation` — bounds are
  the one legitimate CPU-side computation, because frustum culling runs on the CPU;
  the entity matrix never moves, so unexpanded bounds frustum-cull the debris
  against the unexploded box; reset-before-expand keeps pooled reuse from
  compounding). `PrismImplosion.StartImplosion/StartGrow` stamp `{t₀, duration, ±direction,
  growDelay, location}`; the **moving convergence target stays live** as the §1
  documented exception — `PrismEffectsManager.clockConvergenceTracking` refreshes
  `_Location` only (one float3 per frame per implosion, self-dropping when the target
  dies; the suction then freezes at the last stamped point while the GPU finishes the
  animation). Suction bounds get the same envelope treatment as explosions:
  `ResetBoundsToMesh` + `EncapsulateBoundsPoint` (mesh ∪ convergence point) at the
  stamp, and the location refresh re-encapsulates a wandering sink — a no-op while
  the point stays inside the envelope, so the per-frame cost stays read-mostly.
  Pool return / re-trigger cancels the scheduled completion and retires
  the stamp (`ClearExplosionClockStamp`/`ClearSuctionClockStamp`) so a legacy-path
  reuse of the same entity can never replay a stale clock animation.
- **Batched pure-entity debris (B3 mass-death carrier, 2026-08-02)** — prism-death
  explosion VFX no longer check out a pooled GameObject at all: `PrismFactory.
  SpawnExplosion` routes to **`PrismDebris`**, which queues each death and, once per
  frame at LateUpdate, spawns the whole burst via `PrismRenderService.
  SpawnExplosionDebrisBatch` — ONE `em.Instantiate(prototype, N)`, ONE batched
  `DisableRendering` strip, per-entity `SetComponentData` stamps (pose, colors,
  explode clock, flight-envelope `RenderBounds`), all at **full `DefaultDuration`**
  (no pressure shortening — a live entity effect costs zero per-frame CPU).
  Retirement is a flat time-ordered sweep (`PrismDebris.Sweep`) ending in ONE
  batched `DestroyEntity` — never a per-effect timer entry. Why: profiled on a 30³
  lattice with throttles lifted, 2,408 deaths in one frame were 2,408 pool misses
  and `PrismExplosion.OnDisable` alone cost **1,863 ms** (its `EnabledInstances`
  `List.Remove` was an O(n) scan; now O(1) swap-remove for the surviving pooled
  uses). The pooled `PrismExplosion` path remains ONLY as the fallback when the
  render service is off (and for implosions — their batch port is the Prompt 9
  remainder). Clamp semantics (`minSpeed`/`maxSpeed`/`DebrisSpeedLimit` override)
  are read off the pool prefab so both carriers ship identical debris.
- **Color/state transitions (B2)** — `MaterialPropertyAnimator.UpdateMaterial` binds
  the END-STATE material immediately (its authored values are the lerp targets),
  stamps `{t₀, duration, start colors}` (start = analytic current of any in-flight
  clock transition, else the outgoing material's colors), and schedules ONE settle
  via `PrismTimerManager.ScheduleAction` (clears the stamp — invisible, since at
  t ≥ end the shader already equals the bound material — then fires the caller's
  completion). Every feeder (`SetInitialTeam`, `Steal`/`ChangeTeam`, `MakeDangerous`,
  shield repaints) rides this with zero caller changes. Exotic handoffs pin the
  analytic current colors (`FlushDisplayedColorsToRenderer`); pool return cancels the
  scheduled settle and clears stamps.

**In-editor wiring (REQUIRED for correct visuals — until it lands, every prism
spawn/transition/effect snaps and logs; do all three graphs):**

1. **BlockGraph** — add properties with these EXACT reference names, all marked
   **Hybrid Per Instance** (Node Settings ▸ Shader Declaration; verify
   `hlslDeclarationOverride: 3` lands in the file — the `PRISM_ECS_MIGRATION.md` §7
   recipe): `_GrowStartTime` Float 0 · `_GrowRate` Float 0 · `_GrowStartFrac` Vector3
   (1,1,1) · `_ColorStartTime` Float 0 · `_ColorDuration` Float 0 · `_StartBrightColor`
   Color (1,1,1,1) · `_StartDarkColor` Color (1,1,1,1) · `_StartSpread` Vector3 (0,0,0).
   (`_GrowStartFrac` is **Vector3 (1,1,1)** — per-axis start fraction, so anisotropic
   `Grow()` retargets stay continuous.)
   Vertex stage: Custom Function node (Source = `PrismClockAnimation.hlsl`, Name =
   `PrismGrowScale`, Clock ← the **`_PrismClock` property node** — the unexposed
   global the publisher drives; NEVER the Time node) → multiply the object-space
   vertex position componentwise by the `Scale` (Vector3) output, upstream of the
   existing `SpreadSubGraph` offset.
   Fragment: Custom Function `PrismColorLerp` with `Target*` inputs fed by the
   existing `_BrightColor` / `_DarkColor` / `_Spread` property nodes; route its
   outputs everywhere those properties fed before.
2. **ExplodingBlockGraph** — add `_ExplodeStartTime` / `_ExplodeSpeed` /
   `_ExplodeDuration` (Float 0, Hybrid Per Instance). Custom Function
   `PrismExplosionClock` with `Velocity` ← the existing world-space `_Velocity`
   property (the same node feeding the shatter-spin chain — ONE stamped vector),
   `LegacyAmount` ← `_ExplosionAmount` property and `LegacyOpacity` ← `_Opacity`
   property; its `Amount`/`Opacity` outputs replace the direct property uses;
   `ObjectOffset` **adds directly to the object-space vertex position**. The
   world→object conversion lives INSIDE the HLSL function (raw
   `(float3x3)GetWorldToObjectMatrix()` multiply, unnormalized) — **never a
   Direction-mode Transform node**, whose `TransformWorldToObjectDir` normalizes
   and skews the vector under non-uniform prism scale. The stamp site also
   resets + expands `RenderBounds` to the flight envelope
   (`PrismRenderService.ResetBoundsToMesh` / `ExpandBoundsForClockAnimation`).
3. **SuctionGraph** — add `_SuctionStartTime` / `_SuctionDuration` /
   `_SuctionGrowDelay` (Float 0) and `_SuctionDirection` (Float 1), Hybrid Per
   Instance. Custom Function `PrismSuctionClock` with `LegacyState` ← `_State`
   property; its `State` output replaces the `_State` uses.
4. Save/reimport. There is nothing to enable — the clock path is always on.

**Fail-loud wiring diagnostics (STRICT MODE — no fallback):** every stamp site
checks whether the material actually being bound declares its clock property
(`HasProperty(_GrowStartTime / _ColorStartTime / _ExplodeStartTime /
_SuctionStartTime)`) and whether a companion entity exists to stamp. Neither check
gates behavior — there is nothing to fall back to. They exist to SCREAM
(`PrismClockDiagnostics.WarnUnwiredMaterial` / `WarnNoRenderEntity`, once per
offender) with the exact graph, property name, and doc reference, while the visual
snaps to its end state. Graphs can still be wired **one at a time** — each material
family goes smooth the moment its graph reimports (BlockGraph's grow trio first:
trail/ring/gyroid growth), and the errors enumerate exactly what remains. Note the
transparent-prism quirk: transparent live prisms rest on ExplodingBlockGraph, so
their grow bloom and color fades need the grow trio + color five (and the
`PrismGrowScale` / `PrismColorLerp` clusters) added there too — both are wired
in-branch — or they snap (loudly) while opaque prisms animate.

**Verification protocol (strict mode):**

- BEFORE wiring: every prism spawn/steal/danger/shield transition and every
  explosion/implosion SNAPS to its end state, and the console carries one
  `[PrismClock] STRICT MODE` error per unwired material naming the missing property.
  That is the expected pre-wiring state, not a regression.
- AFTER wiring each graph: its material family animates GPU-smooth (per-vertex,
  per-frame, regardless of CPU load) and its errors disappear. All three graphs
  wired = console clean.
- If something still snaps or looks chunky after wiring, the diagnostics say exactly
  which material/property is wrong (exact reference name + Hybrid Per Instance flag
  are the usual culprits).
- DiagnosticsHUD "Animators" rows (`PrismScaleManager` / `MaterialStateManager`)
  must read **0 active / 0 reg is not required — registered is fine — but 0 ACTIVE
  always**, in every scene, under any load: nothing may engage the retired passes.
- Colliders: a just-laid ring must collide at full size from the frame the collider
  enables, while the visual is still blooming.
- Hitstop/pause: prism animations freeze with `Time.timeScale` (the clock is scaled
  time) — expected and desired.

### 4.5 Two corollaries of "the stamp is one-shot" (shipped 2026-08-02)

A stamp is not retried and not polled: it writes initial conditions at ONE instant and
the GPU runs the rest. Everything upstream of that instant is therefore load-bearing,
and §3.8 #10 is what happens when it isn't. Two rules now hold the line.

**(a) Companion-entity EXISTENCE is independent of which path DRAWS.**
`Prism.ApplyRenderPath` used to create the entity only when it was about to *show* the
prism, and never while `_exoticVisualActive` (the shield morph's per-prism geometry
owns the GameObject renderer). Creation and visibility are now separate concerns: the
entity is created as soon as the prism is renderable at all, hidden if something else
is drawing, and queued visible when the instanced path takes over. Consequences to
respect when adding any future exotic visual:

- `Prism.EffectiveRenderMesh()` is the single definition of a prism's *batchable*
  geometry — settled shield override, else the live mesh, else `_authoredMesh` (cached
  at `Awake`). **Never register `meshFilter.sharedMesh` while an exotic visual is
  animating**: that is transient per-prism morph geometry, and registering it mints a
  unique `BatchMeshID` per prism (a draw-call storm plus a registration leak).
- `Prism.SyncRenderMesh()` is the only writer of the entity's mesh (cached, so the
  no-change case is free) and runs whenever the instanced path re-engages — which also
  closes the case where a shield disengages without ever having set an override, so
  `ClearRenderMeshOverride` had nothing to push.
- Every clock stamp site gets ONE self-heal: `Prism.TryEnsureRenderEntityForStamp()`
  before the strict-mode scream (`PrismScaleAnimator.StampClockGrowth` does this).
  When it still fails, `Prism.DescribeRenderEntityState()` names the exact gate — the
  diagnostics report a *fact*, not the old list of suspects.

**(b) Creation-time state is not a transition — the birth rule.**
`PrismStateManager.IsBirthTransition` (`!prism.IsCreationComplete`) makes a shield
engaged/disengaged during a prism's creation SNAP: no per-face bloom, no shatter
overlay, no state SFX. This is the continuity law applied correctly, not an exemption
from it — the prism has never been on screen, and the grow-in bloom that follows *is*
its transition into existence. It is the same reading `MaterialPropertyAnimator`
already applies to spawn-paint ("a creation, not a recolor"). `ApplyShieldedPose` /
`ApplyUnshieldedPose` correspondingly stop force-enabling the BoxCollider during that
window (`Prism.Initialize` deliberately holds it off until reveal; the non-instant path
already respected this via `KeepGameplayColliderDuringMorph`).

A shield engaged **later**, on live mass — a skim, a steal, an ability — still blooms
its faces exactly as before. `SegmentSpawner.SuperShieldSpawnedPrisms`, which pokes the
shield component directly rather than going through `PrismStateManager`, honours the
same rule explicitly.

### 4.6 The batched pure-entity debris carrier (both death visuals, shipped 2026-08-02/04)

The clock law says an effect's animation is a stamp plus a scheduled end. It does not
say the *carrier* of that stamp has to be a GameObject — and once the animation stopped
costing per-frame CPU, the carrier was the entire remaining cost. A pooled effect
charges `Instantiate`-or-pool-miss, `OnEnable`/`OnDisable` registry churn, a Transform,
a per-effect `PrismTimerManager` entry, and (for implosions) a per-instance MonoBehaviour
`Update` watchdog — all to hold **one pose and one clock stamp**.

**Both death visuals now spawn as batched entities.** `PrismDebris` accumulates a frame's
deaths and, in `LateUpdate` at execution order 29000 (after gameplay has queued them,
before the render service's visibility flush at 30000, so a prism hidden in `Update` has
its debris drawing the *same* frame), issues:

| family | batch spawn | per-frame CPU while live | retirement |
|---|---|---|---|
| explosion | `PrismRenderService.SpawnExplosionDebrisBatch` — ONE `em.Instantiate(prototype, N)` + ONE batched `RemoveComponent<DisableRendering>` | **zero** | time-ordered sweep → ONE `DestroyEntity` batch |
| suction / implosion | `SpawnImplosionDebrisBatch` — same shape, Implosion override set | ONE `float3` per live effect (the §1 exception) | same sweep |

Why the implosion needs a record and the explosion does not: the suction converges on a
**moving** target. Every implosion in the game comes from `Prism.Consume` → `Implode`,
and all eight `Consume` call sites are fauna passing a live creature `Transform` (the
eater, or a predator's `mouth`) — AOE, projectiles, skimmers and vessel rams all route to
`Damage` → `Explode` instead. So there is no fixed-point majority to batch separately:
`PrismDebris` keeps an `ImplosionRecord` per live suction carrying the target Transform,
the (fixed) world→object matrix, and a **CPU mirror of the object-space culling
envelope**. The refresh then costs one `_Location` write per effect per frame, with a
`RenderBounds` write only when the point wanders outside the stamped envelope — the
mirror exists precisely so the refresh never reads a component back per entity. A target
that dies mid-suction is real-null'd and the sink freezes at its last known point, the
same degradation the pooled path always had (starvation and predation outlive the VFX).

Rules for anything added to this carrier:

- **The moving target is genuinely moving — never "optimize" it into a snapshot.**
  The feed tuning brakes a grazing eater to a hover for exactly the suction duration
  (`consumeHoldSeconds` 2 s = `PrismImplosion.implosionDuration`), which invites the
  conclusion that the eater is stationary enough to snapshot the convergence point.
  It is not. The hover decays from `maxSpeed`, and `LightFaunaDataSO`'s `6f` is a
  **stale field initializer that both shipped assets override**:
  `MassBrittleStarFaunaDataSO` authors **25**, `MassSharkFaunaDataSO` **35** (neither
  authors `feedingBrakeSharpness`, `feedingClusterRadius` or `consumeHoldSeconds`, so
  those do take the 4/s, 12 u, 2 s initializers). Residual drift for the braking
  grazer is ~`v0/k` ≈ **6.25 world units** — about half the 12-unit feeding cluster
  radius. The predation path is worse by construction: `DevouredCoroutine` passes the
  predator's `mouth` while it keeps swimming, with no brake at all, so a 35-speed
  shark can carry the convergence point tens of units during one suction. Two
  consequences: a snapshot would visibly suck mass toward where the creature *was*,
  and the culling envelope's growth write is a few-times-per-effect event rather than
  the rare case the pooled path's comment implied.
- **Uniform durations keep the sweep O(retired), not O(live).** Append order is expiry
  order, so the sweep only inspects the head. Per-spawn durations may vary, but a
  shorter-lived entry queued behind a longer one is destroyed late (harmless — opacity
  is already 0), bounded by the spread.
- **Epoch-tag every batch.** `PrismRenderService.CurrentEpoch` at spawn; a mismatch at
  sweep time means the world died and took the entities with it, so records are dropped
  without a destroy.
- **A failed batch spawn SUSPENDS the path** for 5 s rather than silently accepting and
  dropping the next requests — that is what routes them to the pooled fallback.
- **No pressure shortening.** The pooled path squeezes effect duration under load to
  bound pool size and per-instance churn; an entity has neither, so batched effects
  always animate at full length. Continuity of existence is *stronger* here, not weaker.
- **`PrismType.Grow` has no producer** anywhere in the project, so `PrismFactory.SpawnGrow`
  and its `OnGrowCompleted` per-effect callback are unreachable. That is why the batch
  carries no completion-callback machinery: fire-and-forget is not a limitation here, it
  is the whole live contract. The stamp still carries `GrowDelay` so the shader contract
  stays complete if a grow producer ever lands.
- **A death visual wears the palette of the TIER the prism was wearing, not just its
  domain** (2026-08-13). The dying prism's `PrismKind` rides `PrismEventData.Kind`
  (stamped in `Prism.Explode` / `Implode` from `PrismKinds.Of` *before* the destruction
  pass, so no later step can rewrite the flags out from under it) and both
  `PrismFactory.TryGetTeamColors` and `ConfigureForTeam` resolve their pair from
  `SO_ColorSet.GetPrismKindColors` — the same composition `ThemeManager` paints the live
  prism with. Before this, debris was always tinted at the PLAIN tier, so a danger prism
  shattered into ordinary domain-coloured debris (`Docs/PALETTE.md §2.1`). **This is free
  on the batch**: colour is already a per-entity override, so a mixed-tier burst stays one
  `em.Instantiate` and one draw — the tier must never become a reason to split a batch or
  to swap the material, which would cost a prototype per tier. Danger also carries a
  DYNAMICS difference, `PrismExplosion.DetonationGain` (authored on the pool prefab as
  `dangerDetonationMultiplier`), applied identically on both routes; it scales the debris
  speed, the shatter rate and the clamp band together, because those are one quantity on
  this contract.

**The death path is now split by markers.** `AOE.ResolveDamage` wraps a whole drain, so
everything a death did landed in one unattributable self-time bucket. `Prism` now emits
`Prism.Destroy.Setup` / `.SpatialIndex` / `.StatRaise` / `.SFX` / `.EffectRequest`, which
is what makes "re-profile the lifted-throttle blast" a measurement rather than a guess.
Alongside them, the per-death allocations and redundant interop that the split exposed
are gone: `PrismEventData` is a **struct** (it was a class allocated per raise — i.e. per
kill), `Cell`'s `Domains[3]` literals in `AddBlock`/`RemoveBlock`/`DominantDomain` are
hoisted to statics, `GameDataSO.FindByName`/`FindByTeam` are index loops instead of
`FirstOrDefault(lambda)` (three allocations per call, and `StatsManager.PrismDestroyed`
calls it **twice per death**), `HealthBlockTracker`'s cell-forwarding `RemoveWhere`
predicate is cached, the density grids take a `Vector3` instead of re-reading
`transform.position` once per grid, and a death reads its own pose once instead of four
times.

#### 4.6.1 Retiring the pooled effect path — assessed 2026-08-04, NOT YET

The obvious next step is to delete the pooled `PrismExplosion` / `PrismImplosion`
spawn path now that both families batch. The audit says the *behavioural* case is
already made and the *mechanical* case is not. Both halves matter, so both are
recorded here rather than left to be re-derived.

**Why it is already dead weight, not a safety net.** The comment that used to call
it a "fallback for a disabled render service" was wrong, and that mattering is the
point of writing this down:

- With `PrismRenderService.Enabled` false, `Create` returns an invalid handle, and
  `PrismExplosion.TriggerExplosion` disables its renderer *unconditionally* before
  the branch — so a pooled explosion in that world renders **nothing** and logs.
  A pooled implosion fares slightly better and still fails: `ApplyInitialVisualState`
  re-enables the renderer, but `StampClockStrict`'s no-entity branch only warns, so
  it draws a **static, un-animated block** for its full duration. Strict clock mode
  has no CPU animation tier by design (§4.4) — "loud and frozen" IS the contract.
- `PrismRenderService.SetRuntimeOverride` has **no caller anywhere in Assets**, and
  the shipped `Resources/PrismRenderConfig.asset` has `useInstancedRendering: 1`.
  The legacy A/B variant in `Docs/PRISM_EXPLOSION_BENCHMARK.md` is produced by
  checking out a different *branch*, not by flipping this toggle. There is no
  shipping configuration in which the pooled path is what the player sees.
- No consumer needs a GameObject back. `Prism.Explode` / `Prism.Implode` are the
  only raisers of `PrismType.Explosion` / `.Implosion` and both discard the
  channel's `PrismReturnEventData`; `PrismFactory` is the only caller of
  `TriggerExplosion` / `StartImplosion` / `StartGrow`; `StopEffect()` has zero
  callers. `PrismType.Grow` has no producer at all.

**Why it is a refactor, not a deletion — the three real dependencies.**

1. **The pool prefabs are the CONFIG SOURCE for the batched path.**
   `PrismDebris.Configure` / `ConfigureImplosion` read the mesh, material, layer,
   debris clamp band and suction duration straight off `explosionPool.Prefab` /
   `implosionPool.Prefab`. That is deliberate — it is what guarantees both paths
   ship identical debris — but it means deleting the pooled path first requires
   deciding where that authored data lives (an SO, or the prefabs demoted to pure
   config assets that are never spawned).
2. **The dev-build zombie audit** in `PrismEffectsManager` walks
   `PrismExplosion.EnabledInstances` / `PrismImplosion.EnabledInstances` and pokes
   `Renderer` / `IsActive` / `UsesEntityRenderPath`. With nothing pooled it audits
   an empty set — harmless, but it stops being the safety net it was written to be,
   and the batched carrier has no equivalent (its records ARE the live set).
3. **`GameLoadSampler`** folds `EnabledInstances.Count` into its benchmark metrics.
   The debris counters exist (`PrismDebris.LiveDebrisCount`,
   `LiveImplosionDebrisCount`) but the sampler must be re-sourced or its numbers
   silently drop to the batched half only.

**The gate.** Do not retire until the implosion batch has been *measured*, not just
shipped: an in-editor playtest of fauna feeding (suction converges on the moving
eater, no stuck `imp` count on the harness HUD) plus a benchmark pass per
`Docs/PRISM_EXPLOSION_BENCHMARK.md`. Retiring in the same change that introduces
the batch would remove the ability to A/B it by flipping the config asset — which
is exactly the tool that would diagnose a regression.

### 4.7 The camera↔vessel occlusion corridor — a PLATFORM LAW (shipped 2026-08-04, C1)

> **The law:** prisms between the player's camera and the player's vessel go see-through so
> the ship is never hidden. It is **not a feature a vessel or a game mode may choose.** It
> must not be possible to author a vessel, a prism, or a minigame in which it is off.
>
> The previous implementation was per-vessel opt-in — a `ClearPrisms` component present on
> three of eleven vessels, with a dead `IVessel.AllowClearPrismInitialization()` gate — and
> it had been silently dead on all three for a long time. Opt-in is what made that possible,
> so the restoration removed the ability to opt in at all.

**How the law is made un-authorable** (four layers; the first two make it structural, the
last two make a violation loud):

| Layer | Mechanism | What it forecloses |
|---|---|---|
| The **shader** half lives in the prism graphs | `PrismOcclusionFade` is spliced into `SurfaceDescription.Alpha` on **every graph a live prism can render with** (BlockGraph, ExplodingBlockGraph) | A new prism, trail, or environment lay inherits the corridor by construction. There is no per-prism, per-material or per-instance switch to forget. |
| The **target** binds at the universal vessel entry point | `VesselController.Initialize` under `IPlayer.IsLocalPilot` — the one method every vessel must call to become a player's vessel (single-player spawn, multiplayer spawn, menu autopilot, runtime swap) — **plus `ChangePlayer`**, which hands a LIVE vessel to a different player and never reaches `Initialize` | A new vessel needs no component and no prefab wiring. A new minigame needs no scene wiring, and cannot dodge it by using the non-networked spawn path (that is what `IsLocalPilot` covers over `IsLocalUser`). The `ChangePlayer` arm closed a real seam found 2026-08-05: the Cellular Duel round-boundary ownership swap (`GameDataSO.SwapVessels`) left the corridor bound to the hull the AI inherited, so for the whole next round the cone cut its hole around the AI's ship while the local pilot's own ship could sit hidden behind prism mass — the exact condition the law exists to prevent. |
| **Runtime** fail-loud | `PrismOcclusionDiagnostics.VerifyCorridorCapable`, called from `Prism.SyncRenderMaterial` — every material a prism ever binds passes through it. One error per offending material, naming it | A prism on an unwired shader, or an opaque material without alpha test, screams instead of silently staying solid. |
| **Asset** gate | Edit-mode test `PrismOcclusionCoverageTests` (graphs wired · every material on them dissolvable · **every prefab carrying a `Prism` renders on a wired graph**) + FrogletTools > Ecology > Prism Animation > **Validate Occlusion Corridor** | New prism content authored outside the corridor fails a test, not a playtest. All three gates share ONE rule (`PrismOcclusionDiagnostics.IsCorridorCapable`) so they cannot drift. |

The **one** sanctioned hold is `PrismOcclusionCorridor.SetSuppressed`, used by exactly one
caller — `CameraManager`'s manual replay camera, a broadcast vantage that is not looking at
the local ship, where a camera→ship capsule would cut a hole through unrelated mass. It is
symmetric (`RestoreGameplayCamera` lifts it) and it is a HOLD, not an opt-out: the vessel
binding survives it, so nothing has to remember to re-point the corridor afterwards. That lift
is **unconditional and first**, above `RestoreGameplayCamera`'s own follow-target early return
(fixed 2026-08-05): a replay can finish a frame after its scene tore down, when the follow
target is a destroyed Transform, and returning before the lift latched the hold on for the rest
of the session — `_suppressed` is otherwise reset only by the `RuntimeInitializeOnLoadMethod`
installer, once per app launch, so every subsequent match ran with the corridor off.

**Deliberate exclusions, named rather than hidden.** `SuctionGraph` renders a prism DURING
consumption (a sub-second implode of mass being removed), never standing mass that can
occlude. Four legacy prism prefabs on pre-corridor shaders — `GreenDartBlock`,
`TriangleBlock` (the SpreadFresnel family §3.7 I says not to extend, referenced only by the
Recording Studio scenes) and `TrailRing`, `TrailPentagon` (referenced by nothing at all) —
are listed by name in the validator and the test. If any is revived as live gameplay mass,
**rebase it onto a wired prism graph; do not grow the exclusion list.**

---

§1 draws a line between **animation** (a pure function of the clock and stamped initial
conditions) and **live gameplay data** (a value that depends on the running simulation).
Camera-relative occlusion is squarely the second kind: whether a given prism sits between
the camera and the ship changes every frame, for every prism, as both ends move. It can
never be a per-prism stamp — and that is not a licence for a per-frame per-prism write.

**The sanctioned shape is a GLOBAL uniform: ONE O(1) publish per frame that every prism
reads, with zero per-prism CPU.** §1 already lists it as conforming ("a single O(1) global
uniform write per frame (a clock publisher) — it is not per-prism"). The corridor is the
reference implementation, and the same shape is the prescribed migration for every other
view-dependent prism effect in §3.7 (`SkimFxRunner`'s live ship end, the retired
`TrailViewer` window).

| Piece | Where |
|---|---|
| Publisher (2 `Shader.SetGlobalVector` per frame, `LateUpdate`, self-installing like `PrismClock`) | `Utility/PrismOcclusionCorridor.cs` |
| Target binding (the platform-law choke point) | `Controller/Vessel/VesselController.cs` `Initialize` / `OnDestroy`, on `IPlayer.IsLocalPilot` |
| Runtime fail-loud | `Utility/PrismOcclusionDiagnostics.cs`, called from `Prism.SyncRenderMaterial` |
| Automated asset gate | `Tests/EditMode/PrismOcclusionCoverageTests.cs` |
| Tuning (radius SCALES / core alpha / sanity band) | `ScriptableObjects/PrismOcclusionConfigSO.cs` → `Resources/PrismOcclusionConfig.asset` |
| GPU test + dither | `_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl` |
| Live design surface (dials · preview · measure · bake) | FrogletTools > Ecology > Prism Animation > **Occlusion Dither Lab** — `Editor/PrismOcclusionDitherLab.cs` + `_Graphics/Materials/Graphs/PrismOcclusionDitherPreview.shader` |
| Graph wiring (idempotent, validate-before-write) | `Tools/Shaders/wire_prism_occlusion_corridor.py` |
| Material opaque+clip contract (idempotent fixer) | `Tools/Shaders/enable_prism_alpha_clip.py` |
| Debris erosion splice (idempotent) | `Tools/Shaders/wire_prism_explosion_erosion.py` |
| Erosion CDF re-fit (after lattice retune) | `Tools/Shaders/fit_prism_erosion_cdf.py` |
| Interactive gate | FrogletTools > Ecology > Prism Animation > **Validate Occlusion Corridor** |

The two globals are `_PrismOcclusionTarget` (the vessel's world position) and
`_PrismOcclusionParams` (`outerRadius, innerRadius, coreAlpha`). The **near** end of the
corridor is never published: the shader reads `_WorldSpaceCameraPos`, so it is always
exactly the camera that is rendering. `outerRadius <= 0` is the off sentinel and is the
shader's very first branch, so a disabled corridor costs a compare.

**The profile (retuned 2026-08-04).** Alpha is **exactly `coreAlpha` = 0** inside
`innerRadius` — fully tapered to nothing, so no dithered ghost survives anywhere the ship
can be — and **exactly 1** at and beyond `outerRadius`.

**The shape is a BARE CONE — the minimal volume that can occlude the ship (2026-08-04).**
It is a *point* at the lens, widening to the circle that circumscribes the hull, and it
ends **at the vessel's plane** — no cap at either end. Nothing outside the
eye→silhouette cone can be in front of the ship, and nothing level with or behind it can
either, so the corridor never dissolves a prism it does not have to.

Two lines make it: `t` is left **unclamped** and the cone is bounded by rejecting
`t ∉ (0,1)`, then `outerAtT = outerRadius * t` tapers the radius. Saturating `t` instead —
the earlier version — pinned the closest point to the vessel past `t = 1`, which turns the
metric there into distance-to-the-ship-point: that is precisely the hemispherical cap the
rejection now removes.

**The base is graded too, on a derived band.** The bare cone initially ended in a hard cut
at the vessel's plane — a prism spanning it was faded on the camera side and solid on the
far side, which reads as a crisp semicircular edge on any large plate at that depth. A
second, *axial* clearance term now closes it: 1 up to the base band, 0 at the vessel's
plane.

Its thickness is **derived, not authored** — it is the radial shell's own world thickness
(`outerRadius − innerRadius`) expressed in units of `t`. That makes the gradient shell
**isotropic**: the same thickness across the base as around the sides, so the whole
boundary fades at one rate and there is no seam anywhere on it. It self-scales too (a long
corridor gets a proportionally short axial band), and it adds no config field — the
`float3` params are untouched, so no graph surgery.

The two clearances are combined by **product, not `min()`**: multiplying two C2 curves
stays C2, whereas `min()` would crease wherever they cross — precisely the artefact the
grading exists to remove.

**Why not the capsule it replaced:** the constant radius was an artefact of the retired
`ClearPrisms` `CapsuleCollider`, carried into the first shader version unexamined. A fixed
world radius subtends a *huge* solid angle near the camera, so a capsule massively
over-clears there. Tapering makes the cleared region a **constant angular size** — exactly
the ship's own silhouette, at every depth.

**The corridor is SHIP-SIZED, not world-sized.** The two radii are not authored in world
units at all: they are multiples of the vessel's own **circumscribing radius**, measured
from its hull by `PrismOcclusionCorridor.MeasureCircumscribedRadius` at bind time. The
authored defaults put the gradient's **outer edge exactly on the circle that circumscribes
the vessel** (`outerRadiusScale = 1`) and its **fully-clear core at half that**
(`innerRadiusScale = 0.5`). A new vessel of any size therefore gets a correctly-scaled
corridor with nothing to author — the same "no per-vessel wiring" property the rest of the
platform law rests on. Note the consequence, which is deliberate and is sharpened by the
cone: the cleared disc is now *exactly* the ship's screen silhouette, with the fully-opaque
edge on it and zero margin around it. `outerRadiusScale` is the one dial if that reads too
tight.

`innerRadiusScale` is **0.25** — deliberately much narrower than the outer edge, so three
quarters of the cone's cross-section is gradient and only a small centre is hard-clear. The
dissolve reads as a soft column rather than a hole with a rim. 0 would make the whole cone
a gradient, clear only on the axis itself.

The measurement is **rotation-invariant** (max distance from the vessel origin to the mesh
bounds' corners in world space — a rigid rotation preserves those distances, whereas
`Renderer.bounds`, a world AABB, would swing with attitude and size the corridor differently
on every spawn), and it is **hull-only**: anything under a `Skimmer` is excluded, because a
skimmer is a field volume deliberately far larger than the ship (the shared Skimmer prefab is
scaled 15× around a 0.5 sphere — a 7.5-unit world radius) and would peg every vessel's
corridor to the skimmer instead of the hull. Because size is now derived, a bad measurement
is not cosmetic — too small hides the ship anyway, too large dissolves the world in front of
it — so measured radii are clamped into a config sanity band and both the clamp and an
unmeasurable hull scream once, naming the vessel and the number.

The band between the two radii is deliberately **short**, and the two things that keep a
short band from reading as an edge are both continuity choices:

- **Quintic smootherstep**, not cubic smoothstep: value, first AND second derivatives are
  zero at both ends (C2). Cubic zeroes only the first, which leaves a faint crease exactly
  where the band starts and stops — invisible over a wide band, obvious over a narrow one.
- **A dither kernel that keeps coverage honest.** Whatever pattern the screen door uses, the
  number that decides whether a *short* band reads as a fade or as an edge is
  |coverage − alpha| — how closely the kept-fragment fraction tracks the alpha at every point
  in the band. Measured in situ (kept fraction per alpha bin over a rendered prism wall):
  **0.0021 (IGN) · 0.0042 (spiral) · 0.0048 (Worley, CDF-remapped) · 0.038 (perlin) ·
  0.097 (hex) · 0.100 (halftone) · 0.128 (rings×IGN) · 0.132 (quasicrystal) ·
  0.140 (Worley, raw) · 0.212 (concentric rings)**. A kernel is admitted to the file only
  under ~0.01; the rest buy their look by trading that away, which is why the twelve
  candidates rendered on 2026-08-04 reduced to the three below. Worley appears twice on
  purpose — it is the one candidate a cheap monotonic remap moves from one side of the
  admission line to the other.

  That list is the **2026-08-04 in-situ pass**, and every number in it is on that harness.
  The 2026-08-06 hard-edge pass (SHARD, below) re-measured on a rebuilt harness that reads
  the same shipped Worley at 0.0073/0.0117 — ~1.55× stricter — so its numbers are quoted
  with the kernel they belong to and are **not** merged into this list. Compare within a
  harness, never across one.

  `PRISM_OCCLUSION_KERNEL` in `PrismOcclusionCorridor.hlsl` selects between the survivors.
  All are procedural — no texture, no sampler, no asset — and all cost less than the
  corridor test itself.

  **Choose the look in the Lab, not by editing `#define`s.** FrogletTools > Ecology >
  Prism Animation > **Occlusion Dither Lab** drives the kernel and every scale dial as
  shader globals **live, including in play mode** — which is the only place a dither can
  actually be judged, because it has to be read in motion against real trail mass. Three
  things make it more than a slider panel:

  - **The preview is the shipped GPU code.** `PrismOcclusionDitherPreview.shader` includes
    the corridor's own HLSL and calls the same `PrismOcclusionDitherThreshold` dispatch,
    reading the same globals. A C# re-implementation could drift from the game; this
    cannot, and a kernel added to the corridor shows up in the Lab for free.
  - **Measure runs the admission rule**, not a proxy: it renders threshold+alpha to a float
    target, reads it back and computes |coverage − alpha| over real rendered output — the
    same methodology as the in-situ numbers above — and measures the **shipped Worley
    baseline in the same pass**, so the verdict is a ratio and cannot be flattered by
    anything about the harness. Sliders that let someone silently break a platform law
    would be a worse tool than no sliders.
  - **Bake writes the values back** into the constants and flips design mode off, so nobody
    hand-transcribes numbers out of a screenshot. Every rewrite is anchored and must match
    exactly once, or the bake refuses; the trailing comments (which carry the measured
    windows) survive it.

  Design mode is the `PRISM_OCCLUSION_LIVE_TUNING` gate, and it is **not free** — it
  compiles all five kernels into every prism shader and allocates registers for the
  largest, which costs occupancy on exactly the draw class this game has most of. At 0 the
  file compiles as though none of it existed: one kernel, no branch, no uniforms. It is
  fail-safe in both directions — with nothing published every dial falls back to its
  constant, so design mode with the Lab closed renders identically to shipped mode
  (verified by compiling both modes and diffing the output).

  - **3 — screen-space SHARD (carried, 2026-08-06).** Worley with the METRIC changed and
    nothing else: same lattice, same Hoskins hash, same orbiting feature points, same 3×3
    search, same CDF remap, but distance is measured with the **gauge of an equilateral
    triangle** (`g(q) = max(q.y, 0.866·|q.x| − 0.5·q.y)`) instead of the Euclidean length.
    Level sets of a gauge are its own polygon, so the flecks become **triangles with hard
    straight edges** while the arrangement the eye reads as organic flecking is untouched.

    **Why the shape is a design surface, not a dither detail.** The unit shape is the
    smallest piece of the game the player sees, repeated thousands of times right beside
    their ship. The house motif is **soft-hard-soft** — bloom (soft) around low-poly
    prisms (hard) along a smooth flight curve (soft), and the UI borders doing the same
    thing, graded at both ends but taking hard turns in their pathing: rigid geometry
    sandwiched between the ambiguous. A circle is the one shape that cannot participate —
    it is soft, with a soft gradient either side of it (soft-SOFT-soft), so Worley's round
    flecks read as foam against everything else in frame. Triangles restore the sandwich:
    hard unit shape, ambiguous placement, soft corridor profile around it.

    **The area normalisation is load-bearing, twice.** The gauge is scaled by **1.28607**
    so the triangle at a given threshold has the same AREA as the circle it replaces
    (`r = d·√(π/3√3)`). That is what makes it "triangles of the same size" — the dissolve's
    ink density at every alpha is unchanged — *and* it lands the distance distribution on
    Worley's own measured CDF, so **one fitted remap serves both cellular kernels**
    (the constants are `PRISM_OCCLUSION_CELL_CDF_*`, renamed from `..._WORLEY_CDF_*` to say
    so). Independently re-fitting under the triangle metric lands at 0.0118/0.8775 — within
    noise of the shipped 0.011/0.873. Change the area constant and both must be re-fitted.

    **It is very slightly CHEAPER than Worley**: a gauge is homogeneous of degree 1, so the
    `min` is taken on it directly and the final `sqrt` disappears; per cell it trades a
    `mul/add` for an `abs/mul/max`.

    **Fidelity: 0.0074 uniform / 0.0145 corridor**, against the shipped Worley's 0.0073 /
    0.0117 measured in the same harness — so about **24% more corridor error** for the
    shape, comfortably inside the admission rule, and phase-stable (0.0073–0.0074 across
    t = 0…400s). Note the harness is ~1.55× stricter than the original in-situ pass that
    produced the numbers in this section, so compare **within a harness, not across**.
    Temporal coherence 0.64% of band pixels per 60fps frame (Worley 0.50%, ceiling ~1.45%).

    **`PRISM_OCCLUSION_SHARD_ORIENT`** picks how the triangles are turned: `FIXED` (default
    — all one heading, 0.0074/0.0145), `FLIP` (up/down for one free negation,
    0.0066/0.0126), `SPIN` (per-cell rotation off the orbit phase, one extra `cos`,
    0.0070/0.0129). The scattered ones measure marginally *better* — a uniformly oriented
    gauge is more spatially correlated — so the default is a **look** call: FIXED is the
    one whose shape is nameable at a glance, which is the entire point of the change.

    **The 3×3 search is measured, not argued, under this metric.** Because an equilateral
    triangle's circumradius is twice its inradius, a feature point outside the neighbourhood
    can in principle beat one inside it (Euclidean distance forbids that). Against an
    exhaustive 5×5: 0.216% of pixels differ at all, mean threshold delta **1.5e-5**. A 5×5
    triples the hash count to buy back one part in 10⁵ — not taken.

    **The lattice pitch is a free dial inside a measured window, and the old "re-fit the
    CDF" warning was wrong.** The distance is measured in *cell* units, so its distribution
    does not move with the pitch: re-fitting anywhere from 3 to 15 px lands within noise of
    the shipped constants and buys nothing measurable (at 15 px a bespoke re-fit takes the
    sweep from 0.0062 to 0.0059 and leaves the corridor error at 0.026 untouched). The "~19×
    degradation" the file used to threaten is what dropping the remap **entirely** costs
    (raw F1 = 0.140), not what moving the pitch costs. What actually bounds it is **sampling
    at both ends, and neither end is fittable**: 3 px puts the shape under the pixel floor
    (0.013 either way), and past 11 px too few cells span the gradient band, so corridor
    error climbs — 0.0193 at 11 px, **0.0248 at 15 px, which reads as a chunky edge rather
    than a fade**. Usable window **4.5–11 px, sweet spot 6–8** (8 px measures identically to
    the shipped 6 and is the most legible *as a triangle*).

    A triangular **tessellation** (simplex grid, per-facet phase, facets filling as nested
    triangles) was measured in the same pass, passed the number at 0.0009/0.0056 and is
    **not carried**: it dissolves into thin strokes at mid alpha and reads as scratchy
    crosshatch rather than as facets, and with the per-facet stagger removed it measures
    0.164 and is the literal wallpaper the Bayer grid was dropped for. **Passing the number
    is necessary, not sufficient.**

  - **5 — WORLD-SPACE SHATTER3D (carried — REJECTED ON LOOK 2026-08-10, the day it
    shipped).** SHATTER lifted into the world: Voronoi POLYHEDRA cut by crack planes,
    world-anchored — true parallax between stacked layers, no strobe at speed (a
    world-anchored pattern's optical flow IS the scene's), screen-pixel fidelity held
    by a power-of-two octave ladder of world cell sizes with a jittered rung boundary.
    Every number passed: **0.0006 uniform / 0.0031 in-situ across a 30× depth range**
    through a clang-compiled build of the shipped file, at cost parity with 2D. On
    real trail mass it read as **glitchy clipping in a ring around the vessel**, and
    the failure is geometric: a volumetric crack PLANE lying near-parallel to a
    viewed SURFACE intersects it in a region whose ramp is nearly constant, so a
    face-sized plate shares one threshold and flips at one alpha — a plate-flash, not
    a dither. The 2D kernel cannot produce this (its band direction always lies in
    the screen plane), and the uniform sweep, the in-situ bin, and the flat preview
    slice are all structurally blind to it — none samples the field off
    surface-glancing geometry. **Passing the number is necessary, not sufficient —
    the tessellation candidate's lesson, paid a second time, this time from a kernel
    that SHIPPED for hours.** Carried (not deleted) because the anchoring insight
    stays right if the glancing-plane failure is solved — e.g. filling polyhedra by
    distance-to-owner (a 3D SHARD: level sets are closed surfaces, never
    near-parallel to a face across a whole plate) instead of parallel planar cuts.
    Do not re-ship as-is. Dials: `PRISM_OCCLUSION_SHATTER3D_CELL` / `..._WALL`
    (ratio), live in the Lab, which shows the same warning.

  - **4 — screen-space SHATTER (CURRENT — shipped 2026-08-06 at polygon 16.26 px /
    wall 20 px; briefly displaced by SHATTER3D on 2026-08-10 and restored the same
    day when the 3D kernel was rejected on look).** The other way to get a hard-edged unit shape: instead of growing a polygon around a point, take the **Voronoi
    cell itself** — an irregular convex polygon, nothing but straight edges — and fill it
    between two parallel straight lines from a hashed phase and a hashed band direction.
    Neighbouring cells are independent, so their boundaries are always visible and the
    pattern reads as a **cracked lattice of walls** rather than as scattered flecks. It is a
    different proposition from SHARD, not a variant: SHARD hardens the fleck, SHATTER makes
    the *negative space* the motif. Both are legitimately soft-hard-soft, so which belongs
    next to the ship is a look call that can only be made in motion — hence carried rather
    than described.

    **Two independent dials**, the only kernel here where wall thickness is authorable
    separately from cell size: `PRISM_OCCLUSION_SHATTER_CELL` (polygon px) and `..._WALL`
    (band repeat px — at alpha `a` the dark wall is `(1−a)·WALL` wide, so it is literally
    "how thick the walls get as the corridor closes").

    **The wall window is RELATIVE, not absolute — corrected 2026-08-06**, and the
    correction came from a setting chosen by eye in the Lab that the first window wrongly
    flagged as a failure. The original sweep held the polygon at a fixed 11 px, which made
    a flat "wall 4–11 px" look like the rule. It is not: what fails is a wall wide relative
    to *its own* polygon, because there is no lattice left to crack. Measured by ratio —
    0.75× → 0.0063, 1.00× → 0.0094, **1.23× → 0.0102 (shipped)**, 1.30× → 0.0162,
    1.64× → 0.0173. Read it as **polygon 8–20 px, wall up to ~1.25× the polygon**, and
    measure past that rather than assuming in either direction.

    The shipped 16.26 / 20 holds **0.0102–0.0128 across t = 0…400s** — at or inside the
    Worley baseline's 0.0117, and better than SHARD's 0.0145.

    **No CDF fit and none needed** — `frac` of a hash is uniform by construction, so
    fidelity is exact in the large and there is no remap to keep in sync. Most expensive
    kernel in the file: Worley's nine hashes and sines, plus a tenth hash for the owning
    cell and one sin/cos for the band direction.

  - **2 — screen-space Worley (the calibration reference).** Distance to the nearest jittered lattice point
    over the 3×3 neighbourhood that can contain it. Reads as **organic flecking** — irregular
    blobs with visible cell structure — rather than IGN's even stipple or the spiral's
    standing bands; screen-anchored, so prisms dissolve through it. The most expensive kernel
    carried (9 cells × one float-only Hoskins `hash22` each, ~18 hashes, vs IGN's one
    frac-chain and the spiral's zero), though still ALU-only and still paid on corridor
    fragments only. **Its CDF remap is load-bearing, not polish**: raw F1 distance clusters
    around 0.43 with nothing at either extreme, so a plain `F1 / max` threshold measures
    0.1401 — outside the admission rule. A `smoothstep` fitted to the measured F1 CDF
    (`0.02 → 0.83`) takes it to 0.0048, a 19× improvement for one instruction, and because
    the remap is monotonic the cell boundaries and the whole look are unchanged — only the
    rate at which cells fill in as alpha sweeps, which is the part that was wrong. Note the
    remap is what is load-bearing, **not** its coupling to `PRISM_OCCLUSION_CELL_SIZE`: that
    coupling was re-measured on 2026-08-06 and does not exist (see the size window under
    kernel 3). Dropping the remap costs 19×; moving the pitch inside its window costs
    nothing.
  **The morph axis.** `PRISM_OCCLUSION_MORPH_RATE` (cycles/sec, default 0.12 — one cycle
  per ~8s; 0 freezes it) evolves whichever kernel is selected, so the stipple is never the
  same twice. It is an axis rather than a fourth kernel because each kernel interprets it
  natively: Worley's feature points **orbit inside their own cells**
  (`0.5 + 0.5·sin(2π·hash + t)` per axis — bounded to the cell, which is what keeps the 3×3
  search exhaustive; a `frac(hash + t)` drift is cheaper and wrong, the point teleports
  across the cell every cycle), and the spiral drifts its band phase, which for a sheared
  Archimedean spiral is a slow rotation. Time is `_Time.y`, so morphing costs one MAD per
  fragment and **zero CPU** — no per-prism state, no publisher change, no new uniform; the
  same initial-conditions-plus-a-clock shape the law asks for everywhere else.

  Three things make it safe, and one makes it impossible for IGN:

  - **Exposure is bounded by the profile.** The pattern is only visible where alpha is
    strictly between 0 and 1 — the narrow gradient shell — since the core clips regardless
    of threshold and the exterior clips nothing. An evolving threshold can only flip pixels
    inside that band.
  - **0.69% of band pixels change state per 60fps frame** at the default rate, which reads as
    the pattern flowing. Past ~0.25 cycles/sec (1.45%) it reads as noise; treat that as a
    ceiling. Coverage fidelity is **independent of the rate** — 0.0065–0.0070 measured across
    0.04 through 0.25 — so the rate is purely a motion dial and cannot break the fade.
  - **Worley uses the sin-orbit jitter at EVERY rate, including 0.** The orbit's marginal is
    arcsine rather than uniform, so it shifts the F1 CDF: feeding the old static constants
    (0.02/0.83) to moving points measures 0.0238, straight back out of the admission rule.
    One jitter function means one fit covers both, verified phase-stable at **0.0068** from
    rate 0 through t = 400s. The constants moved to **0.011/0.873** for this.
  - **IGN cannot morph.** It is a hash, not a field — no continuity in any input — so
    advancing it resamples the pattern per pixel per frame rather than moving it. That is
    full-amplitude shimmer. Only kernels that are continuous in position can be continuous
    in time.

  Perlin was re-examined here specifically because continuous morphing is its selling point,
  and it still does not qualify: the CDF remap that rescued Worley only takes 2-octave value
  noise from 0.036 to **0.0252**, because a bell-shaped distribution does not flatten under a
  single smoothstep the way a cell-distance one does. Its temporal coherence also turned out
  to be **indistinguishable** from Worley's (0.17% vs 0.19% of pixels flipping per frame at
  matched rates), so it offers nothing the admitted kernels do not already provide.

  **The layered beat, and the two dials that answer it** (2026-08-10, resolved
  2026-08-11). Every screen-anchored kernel is a pure function of the screen pixel, so
  two surfaces stacked along one camera ray — a prism's own back face showing through
  its clipped front face, or two parallel walls of trail mass — read the IDENTICAL
  threshold at every pixel while their alphas differ only slightly. Their clip contours
  are then near-identical line sets a pixel or two apart, which is the textbook moiré
  condition; and because the alpha field rides the GEOMETRY while the threshold rides the
  SCREEN, camera motion slides the pair at slightly different rates and the interference
  beats. SHATTER shows it worst (parallel straight walls, the shallowest gradient here).

  **REJECTED — the depth-parallax domain shear** (`pixel += depth · gain · dir`, shipped
  2026-08-10, reverted 2026-08-11). It decorrelated the layers as designed and read as a
  LARGER flicker than the beat it fixed: translating the domain moves the ENTIRE lattice,
  so the pattern's screen velocity is `gain × depth-change-per-frame` — tens of pixels
  per frame of *coherent* crawl at flight speed, and coherent motion is the most salient
  thing the eye can be shown. **The lesson generalises: a fix that moves the pattern
  globally cannot win against speed.**

  What replaced it is two LOCAL dials, independently switchable, attacking the beat's two
  separate preconditions:

  - **`PRISM_OCCLUSION_SHATTER_DEPTH_PHASE`** (SHATTER only) — adds the depth term inside
    the kernel's final `frac()` rather than to its domain, so the Voronoi lattice does not
    move at all and only each cell's WALL slides within its own cell. Coverage-neutral by
    the frac-of-uniform argument. **Shipped at 0**, because the measurement (clang build of
    the shipped file: rate | delta at 2u | delta at 12u | band pixels flipping per frame at
    300 u/s) says it cannot do the job — `0.002` → 0.004 / 0.024 / **2.0%**; `0.020` →
    0.040 / 0.240 / **17.9%**; `0.050` → 0.100 / 0.400 / **37.2%**. Useful separation of a
    prism's own two faces (~2u apart) needs ~0.075+, while the flicker ceiling (~1.45%,
    the morph note's own number) allows ~0.0015 — the two requirements are **~50× apart**,
    the same conflict that killed the shear, because both are depth-driven. Carried as a
    Lab dial because it is one MAD and provably coverage-neutral, so seeing the conflict
    costs nothing.
  - **`PRISM_BACKFACE_POWER`** (`PrismBackFaceFade`, spliced after the corridor by
    `Tools/Shaders/wire_prism_backface_fade.py`) — attacks the OTHER precondition, and is
    the only fix that REMOVES the interference rather than scrambling it: a beat needs both
    layers at similar mid-band alpha *simultaneously*, so sharpening the far surface
    (`alpha^power`) drops the interior out of the band while the exterior is still
    dissolving. **No temporal cost at all** — it does not depend on depth or time. Prisms
    render two-sided (`_Cull: 0`), which is why the usual second layer is the prism's own
    interior. Facing comes from the world NORMAL (`dot(N, camera − position) < 0`) rather
    than `SV_IsFrontFace`, because Shader Graph only exposes that semantic through an Is
    Front Face node and this project has none to donor-clone, while it has 36 NormalVector
    nodes. Measured both-in-band range: `1.0` (off) 0.09–0.92 · `2.0` 0.28–0.92 ·
    **`3.0` (shipped) 0.44–0.92** · `4.0` 0.54–0.92. It **must** sit after the corridor —
    in the gradient band the graph's own alpha is 1 and only the corridor's fade is
    fractional, so sharpening earlier would square a 1 and do nothing. The stated trade is
    a look change: interiors vanish earlier, so a mid-fade prism reads as a thinner shell;
    `1.0` disables it without touching the graph.

  - **1 — corridor-relative spiral.** An Archimedean spiral in the corridor's own
    polar frame (9 bands across the cone radius, sheared 3 turns per revolution), so the
    pattern is anchored to the *corridor*: it stands still and the world travels through it,
    which reads as an **iris around the ship** rather than a dissolve. Cheapest of the set —
    both coordinates are already paid for (the radial ratio is the profile's own, the angle
    comes from the perpendicular vector the distance came from), so it costs two dots, an
    `atan2` and a `frac`, with no hash. Two invariants: the arm count **must stay an integer**
    (`atan2`'s ±π seam jumps the phase by exactly one turn, which `frac` erases only for an
    integer count — a fractional one leaves a radial scar), and the angle is measured in the
    **camera's** right/up frame, because any basis built from the corridor axis alone has to
    pick a reference vector and visibly snaps the whole spiral around when the axis swings
    past it.
  - **0 — screen-space interleaved gradient noise.** A low-discrepancy screen-space hash with
    no repeating tile, anchored to the screen so prisms dissolve *through* it. The ordered 4×4
    Bayer matrix it replaced read as what its name says — a regular grid. IGN also beats plain
    white noise, which is motlier still but clumps, and clumping over a narrow band is a ragged
    edge; irregular *and* even is the combination that works.

Four properties of the design worth preserving if it is ever touched:

- **Nothing is per-prism.** No trigger volume, no tracked set, no material swap, no
  per-instance override. Widening the corridor costs nothing.
- **The shape is chosen, not inherited.** The capsule was a leftover from a physics
  collider; a shader is free to describe any field, so it describes the right one.
- **Coverage is the point.** A prism material that cannot fade is an *invisible hole* in the
  corridor — no error, no visual tell, nothing to notice until someone says they can't see
  their ship. That is how the old system stayed broken; every gate above exists to make that
  state impossible to reach silently.
- **Prisms stay in the opaque queue — ALL of them, for every transparency effect
  (2026-08-10).** The fade is screen-door (a dither threshold fed into
  `SurfaceDescription.AlphaClipThreshold`), not blending, and the threshold now engages for
  ANY fragment whose final alpha lands below 1 — not only inside the corridor. That one
  rule made the dither **THE prism transparency mechanism**: the corridor fade, the
  exploding-debris fade-out (`PrismExplosionClock`'s Opacity ramp), and the cloak family's
  authored near-zero alpha all ride the same screen door, with the same depth parallax,
  composing in COVERAGE (alphas multiply before one threshold compare — a debris prism
  fading inside the corridor is one consistent pattern, not two stacked transparencies).
  Consequently there are **no transparent prism materials any more**: the seven that
  blended (ExplodingBlockMaterial, CloakedPrismMaterial, TransparentPrismMaterial, the
  Transparent Shielded/SuperShielded/Danger/Jade variants) were converted to opaque +
  `_ALPHATEST_ON` with their authored `_Alpha`/`_Opacity` preserved as dither coverage —
  the "Transparent*" names survive as the cloak-state bind targets, but nothing blends.
  `enable_prism_alpha_clip.py` enforces and converts; `PrismOcclusionDiagnostics` faults a
  transparent prism material at runtime; the coverage test fails on one in CI. (One stale
  value surfaced by the conversion: `MazeDangerBlockMateral` — live prisms on
  ExplodingBlockGraph — carried a dead `_Opacity 0` that would have become "invisible";
  it is now 1. The tool prints every material's authored coverage so the next stale value
  is visible at conversion time.)
- **The corridor STOPS SHORT of the ship — `PRISM_OCCLUSION_NOSE_CLEARANCE`
  (2026-08-11).** The cone used to run all the way to the vessel's ORIGIN plane with the
  axial gradient still in progress when it arrived, so a prism the ship was flying into
  was still partly dematerialised at the moment of contact — and an impact you cannot see
  land does not read as an impact. The fade must now be COMPLETE one hull radius short of
  the vessel plane, measured in hull radii because that is the length the corridor already
  knows and the one that scales fleetwide with nothing authored (the hull radius bounds
  every part of the ship about its origin, so a clearance of 1 means "solid from a
  ship's-length out, through the nose and past it"). Measured on-axis through a clang
  build at the Sparrow's 12.32 hull radius with a 30 u camera: cleared 22–28 u out,
  fading 20→14 u, and **fully solid from 12.3 u all the way through the vessel plane**.
  The trade is stated and is the point: mass inside that buffer is solid and CAN occlude
  the ship at contact range — dial toward 0.5 if that starts to bite, 0 restores the old
  flush-to-the-plane behaviour. A camera closer than the clearance switches the corridor
  off entirely, which is correct rather than dangerous: inside one hull radius there is no
  room for occluding mass to hide behind.
- **The corridor test is per-fragment**, from the Position(World) node — the same
  post-vertex-animation position the rasterizer used. A per-object test would make a large
  environment plate flip wholesale between solid and dissolved.
- **`_Alpha` is multiplied, not replaced.** The graph's own alpha source (BlockGraph's
  `_Alpha`, ExplodingBlockGraph's clock Opacity) feeds the corridor node's BaseAlpha, so
  authored and clock-driven alpha are first-class dither inputs: the corridor only scales
  them, and whatever the product is renders as coverage.
- **A fading prism outside the corridor never pops, on any kernel.** The four
  screen-anchored kernels work anywhere; the SPIRAL is corridor-anchored (no polar frame
  exists outside the cone), so the dispatch takes a `polarValid` flag and swaps it for IGN
  on out-of-corridor fades rather than letting a whole prism vanish at one alpha.
- **The exploding prism's FADE is its own dither — body-anchored, never a function of
  the view: ONE WIPE PER FACE, anchored to UV0 (2026-08-10; re-anchored 2026-08-11 —
  the first cut carved the body into Voronoi chunks and read as the prism being
  EATEN from many points per face; the second anchored to body POSITION with
  dominant-axis face classification, which the per-face shatter SPIN breaks:
  fragments migrate across dominance boundaries as pieces rotate, so wipes jumped
  face frames mid-tumble — reported as "the normals stop updating as the pieces
  spin").** `PrismErosionFade` (same HLSL file) sweeps ONE jagged erosion front
  across each face as the clock Opacity runs 1 → 0. **UVs are mesh attributes — no
  vertex animation (flight, spin, scale) can move them** — so the front is glued to
  the face under any motion and any camera, and the whole flight-undo matrix ride
  was deleted with the problem (the function is three hashes, a projection, and a
  1D value noise — simpler AND cheaper). Faces share the wipe's UV-space direction,
  but each face's UV frame is oriented differently on the box, so world-space
  fronts still differ per face; the stamped `_Velocity` seeds each prism's
  direction and jag so no two chunks peel alike. **Soft-hard-soft**: Survival is
  a HARD edge (`PRISM_EROSION_FRINGE` 0, 2026-08-11). It briefly led the front with
  a dithered fringe, on the reading that soft-hard-soft wanted a soft trailing
  component; in motion that was wrong, because the debris edge then dissolved in the
  SAME visual language as the corridor it flies through and the two read as one
  confused surface instead of "a prism breaking up" inside "the world going
  see-through". The motif's soft component here is the unbroken face the front eats
  into and the irregular JAG of the front itself. Removing the fringe also made the
  fade curve essentially exact — the smear WAS the coverage error, 0.0296 → **0.00068**
  mean against the margin-compressed ramp. **The wipe
  finishes early by design**: thresholds are compressed above
  `PRISM_EROSION_END_MARGIN` (0.15), so every fragment is gone 15% of the fade
  before the batch retires — closing the "pieces vanish before the wipe finishes"
  race structurally — and the fade itself was extended 1.5×
  (`PrismExplosion.DefaultDuration` 5 → 7.5, `MinPressuredDuration` 0.22 → 0.33).
  Spliced by `Tools/Shaders/wire_prism_explosion_erosion.py` (which MIGRATES the
  old position-anchored wiring in place) between the explosion clock and the
  corridor node; live prisms stay exact pass-throughs via the ≥1/≤0 early-outs,
  and a wiped-away fragment takes the corridor's alpha≤0 fast out. The wipe
  coordinate carries a CDF remap fitted over the uniform UV square
  (`Tools/Shaders/fit_prism_erosion_cdf.py`; re-run if `WIGGLE`/`WIGGLE_FREQ`
  move — `END_MARGIN`/`FRINGE` sit outside the fitted quantity and tune freely),
  validated against a clang build of the file itself; the ASCII render of the
  compiled function shows one connected hard front per face at every alpha. The guard is
  `PrismOcclusionCoverageTests.ExplodingGraph_CarriesTheObjectAnchoredErosion`.

**Cost, stated:** per fragment, for solid mass outside the corridor — one compare against
the off sentinel, ~10 ALU of segment-distance, two compares, done (no dither, no texture).
The kernel is paid only by fragments whose final alpha is fractional: the corridor's
gradient shell, mid-fade debris, cloaked prisms. Draw calls, batches, render queue and
collider count are unchanged; every prism stays in the same instanced batch (and the
ex-transparent materials now WRITE DEPTH and skip sorting, which is a small win, not a
cost). The one non-zero structural cost is unchanged: `_ALPHATEST_ON` on every prism
material makes prism fragments alpha-tested, which forfeits early-Z rejection for those
draws on tile-based GPUs. Prisms are unlit boxes with a trivial fragment shader, and the
alternative (per-prism transparent material swaps) is strictly worse, but it is a real
trade and it is the thing to measure if prism fill cost regresses. Reverting the corridor
alone is no longer one command — the fade-out and cloak paths now DEPEND on the clip
(their materials no longer blend), so a revert means re-converting those seven materials
to transparent as well.

**What it replaced.** `ClearPrisms` (deleted, with its prefab and the dead
`IVessel.AllowClearPrismInitialization()` opt-out gate): a per-vessel kinematic
Rigidbody + `CapsuleCollider` trigger that swapped each entered prism's `sharedMaterial` to
the team transparent material and wrote a `MaterialPropertyBlock` per tracked prism per
physics tick. It had been dead for a long time in three independent ways — the MPB never
reaches the instanced batch (§3.8 #2), its capsule sat on layer `TrailBlockOcclusion` while
prisms sit on `Default` so the collision matrix never paired them, and the Rhino prefab
still carried an override for a `prismLayer` field the script no longer had. Removing it
also removes 3 trigger colliders + 3 kinematic Rigidbodies from the vessel fleet
(Rhino/Dolphin/Serpent) and the `OnTriggerStay` traffic they generated against every prism
they overlapped.

### 4.7.1 The second citizen of §4.7 — the Dolphin's Echo Sight (shipped 2026-08-14)

The corridor is a platform LAW; this is not. It is one vessel's ability, held on a trigger and off
for everyone else. It is recorded here because it is the **second** use of §4.7's sanctioned shape,
and it demonstrates the shape generalises: a view-dependent prism visual that is a *feature* rather
than a law still gets exactly one global-uniform publisher and zero per-prism CPU.

While the Dolphin's pilot holds the sight, every prism standing inside the volume its next crystal
blast would sweep lights up. `PrismDestructionSight` publishes five globals per frame (apex, sweep
axis, gape axis, `(height, coreRadiusPerUnitDepth, halfLengthPerUnitDepth)`, strength);
`PrismDestructionSight.hlsl` runs the containment test per fragment, spliced into BlockGraph and
ExplodingBlockGraph by `Tools/Shaders/wire_prism_destruction_sight.py` — the same census the
corridor covers, for the same reason (a prism material that cannot light up is a hole in a
targeting aid).

Three properties worth carrying to the next one:

- **The tested volume is not re-derived.** `ExplosionHelper.TryResolveConicVolume` builds it from
  the authored scales, the live energy read and the Space multiplier the *detonation* uses, and the
  HLSL transcribes `AOEConicSweepQueryJob.Execute` literally, capsule-segment clamp included. A
  preview carrying its own copy of that arithmetic drifts the first time anyone retunes a scale, and
  a targeting aid that lies is worse than none.
- **It ADDS light, it does not tint.** Replacing colour lands in the domain palette's space and
  reads as "this prism changed team"; adding reads as "this one is lit up", which no tier's palette
  means. The prism graphs are Unlit and carry no Emission block, so additive-into-BaseColor is how
  emission is expressed — which is why it splices exactly like `PrismOcclusionFade`, taking the
  graph's own value in and handing the composed value back rather than overwriting.
- **It composes with the corridor for free.** The corridor dissolves *coverage*, not colour, so a
  highlighted prism standing in the corridor thins out like its neighbours instead of punching
  through the ship. Two §4.7 consumers on the same graph do not interact, because they write
  different channels of the same surface description.

Mechanic and tuning: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md`.

### 4.8 The shield morph — the last CPU ticker (shipped 2026-08-15, B4)

Both shield tiers animated their per-face **engage bloom** and **disengage shatter** by
rebuilding a mesh on the CPU every frame, driven by `PrismOctahedronShieldManager` — the
one remaining sanctioned per-frame prism ticker, and the last ❌ in Phase B. Both are now
`f(clock, stamp)`; the manager is **deleted**.

**The one thing a vertex shader cannot derive is which face a vertex belongs to.** That is
the whole problem, and the whole fix: `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator`
now bake each vertex's own **face centroid** into **TEXCOORD1** (`FaceCentroidUVChannel`,
one constant shared by both tiers). With it, both animations are the same two-term
expression the CPU used, evaluated per vertex:

```
engage  (Direction >= 0):  p = centroid +      t  * (v - centroid)
shatter (Direction <  0):  p = centroid + (1 - t) * (v - centroid) + t * Offset * n
t = smoothstep(0, 1, saturate((clock - StartTime) / Duration))
```

Five properties carry it, all Hybrid Per Instance: `_ShieldMorphStartTime`,
`_ShieldMorphDuration` (0 = unstamped = render the mesh as authored),
`_ShieldMorphDirection`, `_ShieldMorphOffset`, and — since **§4.8.1** — the breaking
impulse `_ShieldMorphVelocity`, which adds a drift and a per-face tumble to the shatter
expression above (zero is the identity, so the two-term form is still exactly what a
direction-less disengage renders). `PrismShieldMorph_float` lives in
`PrismClockAnimation.hlsl`; `Tools/Shaders/wire_prism_shield_morph.py` wires **both**
live-prism graphs (BlockGraph + ExplodingBlockGraph — the same census as the corridor and
the flight clock, because `TransparentPrismMaterial` rests on the latter).

Five properties of the result are worth carrying to the next migration:

- **The settled mesh IS the morph mesh, so the shield never leaves the instanced path.**
  The audit sketched a second "anim variant" mesh per quantized geometry; none is needed,
  because at `faceScale = 1` the expression is the identity. That collapses the whole
  exotic-visual window: `SetRenderMeshOverride(sharedMesh)` at engage is now the *only*
  render call, `SetExoticVisualActive` is driven to **false** and never to true, and a
  batch of same-size shields stays ONE draw through the entire animation instead of each
  prism minting a unique mesh (and therefore a unique draw call) for 0.35–0.7 s. The
  handoff contract itself is unchanged and still load-bearing — a bare `MeshFilter` swap
  still renders nothing.
- **Everything is final at t = 0.** `Engage` applies the whole shielded pose — collider,
  mass, material, shared mesh, render handoff — and only then stamps. There is no
  scheduled completion callback at all: the shader clamps at `t = 1`, which is exactly the
  settled shield, so a finished bloom needs nothing done to it. The stamp is only ever
  *cleared*, and only where it must be — at disengage (the entity has gone back to the
  prism's own box mesh, which carries no centroids, so a live stamp would collapse that box
  toward the object origin) and on pool reuse (`ClearPrismStamps`).
- **The shatter is batched pure-entity debris, not a child GameObject** — and since
  §4.8.1 it flies along the force that broke the shield. The prism itself
  is back on its box the instant the shield drops, so the shards are necessarily a separate
  visual — and a separate visual with one pose, one stamp and one retirement is the §4.6
  carrier's exact shape. `PrismShieldShatter` queues a frame's disengages and spawns them
  through `SpawnShieldShatterBatch`, grouped by `(mesh, material, layer)` because — unlike
  the death debris — shields vary in size and domain. Shards ride the **Prism** override
  set on the prism's own material, so they share a batch with every settled shield like
  them.
- **A shatter is never cancelled.** The old `StopShatter()` deleted visible, mass-shaped
  geometry mid-flight on a re-engage — the thing "nothing pops out of existence" forbids.
  Re-engaging now lets the old shards finish while the new shield blooms.
- **`AnimationCurve` retired — exact for the fleet, a deliberate change on two prefabs.**
  `AnimationCurve.EaseInOut(0,0,1,1)` is a Hermite with **zero** end tangents, which IS
  `3t² − 2t³` = `smoothstep`. That is not an assumption: Unity's own serialization of the
  same constructor writes `inSlope/outSlope: 0` (cross-checked against
  `SpaceCrystalAnimator.shrinkCurve` on `MassSharkFauna` / `MassBrittlestarFauna`). Every
  shield whose component is added at runtime by `PrismStateManager.Awake` — i.e. every
  prism in the game except the two below — is therefore reproduced **exactly**.
  **The exception, stated rather than glossed:** `BlueBlock.prefab` (live in three
  multiplayer scenes + both Recording Studios) and `OctahedronShieldTest.prefab` serialize
  a HAND-ALTERED curve with end tangents **2** — `2t³ − 3t² + 2t`, fast-slow-fast, the
  opposite shape, deviating from `smoothstep` by up to **0.192** at t ≈ 0.2/0.8. Those two
  now ease like the rest of the fleet. Keeping them would have meant a per-instance curve
  parameter for one prefab's drift; and since the C# fields are deleted, the authored
  deviation would not have survived the next save of either prefab anyway. `smoothstep`
  is also what every other clock transition already uses (`PrismColorLerp`).

Gate: `PrismShieldMorphTests` (edit-mode, assets only) checks the baked centroids against
the retired CPU formula, the Hybrid-Per-Instance wiring and UV channel on both graphs, that
the ticker file is gone, and that neither shield has grown an `Update` / coroutine / tween
or a CPU mesh rebuilder. `FrogletTools > Ecology > Prism Animation > Validate Clock Wiring`
covers the same properties from the compiled-material side.

---

### 4.8.1 The shatter is the prism explosion, applied per face (shipped 2026-08-19)

A shield does not fall apart on its own — **something breaks it** — and until now the
disengage overlay could not tell you what. Every face shrank to its centroid and slid out
along its own normal, so a Rhino sword swing, a rammed hull, a herbivore stripping armour
and a shield timer expiring all produced the *same symmetric puff*, viewed from any
direction. The prism explosion has always taken the one initial condition that fixes this:
a **velocity**. The shatter now takes the same one, and reproduces the explosion's motion
model on the shield's faces.

```
tS   = smoothstep(0, 1, saturate((clock - t0) / Duration))     // shape, as before
tSec = clamp(clock - t0, 0, Duration)                          // physical seconds

p  = centroid + (1 - tS)*(v - centroid) + tS*Offset*n          // contract + fly out (unchanged)
p  = centroid + R(p - centroid)                                // tumble  <- RotateFacesAlongAxis
p += velocityObj * tSec                                        // drift   <- PrismExplosionClock
     R = Rodrigues about normalize(cross(velocityObj, n)),
         angle = PRISM_SHIELD_SHATTER_SPIN * |Velocity| * tSec
```

**The tumble is ALWAYS ON; the impulse only steers and accelerates it.** This is the
correction that took three passes to find, and the reasoning is worth keeping: the first
version made the rotation *proportional to the breaking impulse*, so a shatter with no
impulse — or with one that never reached the GPU — had no rotation whatsoever and rendered
as the pre-change fly-out, pixel for pixel. That is indistinguishable on screen from "the
feature is not wired", which is exactly how it was reported, twice. `PRISM_SHIELD_SHATTER_TUMBLE`
(rad/second) now depends on **nothing but the clock and the mesh's own face normal**, both of
which are proven to arrive — the fly-out along `Normal` is visibly correct, so `Normal`,
`Direction`, `ShatterOffset`, `StartTime` and `Duration` all demonstrably reach the shader.
`PRISM_SHIELD_SHATTER_SPIN` (rad per world unit the impulse travels) rides on top, so a hard
hit still spins harder than a soft one, and the axis becomes `cross(v, n)` — the explosion's
own axis — when there is an impulse to take it from. **General rule: an effect's headline
motion must not be gated on the newest, least-proven input in the chain.** Put the new input
on the *refinement* and let the motion stand on what already works.

The fallback axis is the face's own in-plane tangent (the branchless Duff basis
`PrismJiggleBasis` already in this file). It matters that it is *in-plane*: the face tips
away from where it was pointing rather than spinning about its own normal like a plate on a
stick, and because the object-space normal IS the face id on these hard-edged meshes, the
eight faces never tumble in lockstep.

**The pivot is the face's OWN centre, which means splitting the face in two.** The base
explosion material does this explicitly — translate the face's centre to the origin, rotate,
translate back — and the shatter now builds itself the same way:

```
faceCenter = FaceCentroid + offset*Normal      // where the face's centre ends up
rel        = faceScale * (Position - centroid) // the face's geometry, centred on ITSELF
p          = faceCenter + R(rel) + drift
```

Rotating `MorphedPosition − FaceCentroid` instead — which is what the first tumbling version
did — pivots the face around its *baked* centroid, a point it has already flown up to
`ShatterOffset` units away from. The face then orbits that point rather than spinning about
its centre of mass, and reads as "not really rotating". The split also makes the drift
immune to the tumble for free: it is added to the CENTRE, never rotated with the geometry.
`verify_prism_shield_shatter.py` asserts both halves — that the distance to the face's own
centre is preserved by the rotation, and that the distance to the baked centroid is *not*
— and the second assertion is mutation-tested against the old pivot.

**Removal is the EROSION, not a shrink.** The shatter used to scale each face to a point,
which reads as the shield *deflating*; debris does not shrink, it erodes. The faces now stay
at **full size** for the whole shatter and are taken away by `PrismErosionFade` — the same
hard, jagged, UV0-anchored front the exploding prism uses (§ "THE EROSION" in
`PrismOcclusionCorridor.hlsl`), so no amount of flight or spin can slide it.

Three pieces make that work, and each is worth carrying:

- **The shield meshes grew UV0** (`Octahedron`/`StellatedOctahedronMeshGenerator.ErosionUVChannel`).
  The erosion's anchor is a *mesh attribute* by design, and the shield meshes had only UV1
  (the face centroids). Each face now maps to the same isoceles triangle in the unit square;
  identical UVs do not make the faces peel alike, because the wipe's direction and jag are
  hashed **per face** from the centroid. This was safe to add only because BlockGraph reads
  UV0 nowhere else — the wiring tool asserts that before it splices.
- **`PrismShieldMorph` gained an `Opacity` output** — 1 unless shattering, then a *linear*
  `1 − p` (the erosion's thresholds are CDF-fitted against a linear alpha ramp, not the eased
  `t`). It is appended as slot 10, so migrating the previous signature renumbers nothing.
- **BlockGraph MULTIPLIES rather than replaces.** `_Alpha × Survival → PrismOcclusionFade.BaseAlpha`.
  A prism that is not shattering has Opacity 1, `PrismErosionFade` returns Survival 1 outright
  at `BaseOpacity >= 1`, and `Alpha × 1` is `Alpha` — bit-for-bit the old chain, **including the
  cloak family's authored near-zero alpha**. Feeding the erosion the material's alpha directly
  would have put a wipe pattern on every cloaked prism; that is the trap the multiply avoids.

`Tools/Shaders/wire_prism_shield_erosion.py` owns this splice and deliberately does **not**
share a tool with `wire_prism_explosion_erosion.py`: same HLSL function, but two graphs, two
drivers (the explosion clock vs. the shield morph) and disjoint asset sets, so neither can
regress the other. Known gap, stated rather than glossed: a prism that is BOTH transparent and
shielded renders its shards on ExplodingBlockGraph, where the erosion is driven by the
explosion clock — which is unstamped for a shard, so `TransparentPrismMaterial`'s resting
`_Opacity` of 0 makes those shards invisible. That predates this pass and is unchanged by it.

**Duration is the base explosion's.** Both tiers default `shatterDuration` to
`PrismExplosion.DefaultDuration` rather than a number of their own: a shield coming apart and
a prism coming apart are the same event class, and at different lengths they read as
different effects (the shatter shipped at 0.6 s against the explosion's 7.5 s and looked
truncated next to it). The tumble is a RATE, so lengthening the shatter spins the faces
further — `PRISM_SHIELD_SHATTER_TUMBLE` was re-scaled 4.0 → 1.2 rad/s for the longer life.
Note the shatter's removal mechanism is the face CONTRACTING to a point, not the explosion's
opacity fade, so the contraction is now correspondingly slow; that is the knob to watch if
the effect ever reads as sluggish.

**Tumble first, drift second — the order is load-bearing.** Rotating a position that
already carries the drift rotates the DRIFT ITSELF: `rel` becomes the ~12 world units the
shard has travelled instead of the face's own ~1 unit of extent, so the face swings on a
wide arc instead of spinning in place, and at the angles this reaches (up to ~3 rad) the
shard ends up travelling back toward the blow. Shipped that way in the first pass and
fixed; the verifier now asserts the composition rather than transcribing it. The tumble is
also CONDITIONAL (a degenerate normal, or an impulse straight down the face normal, has no
axis) while the drift is not — so neither case may return early, or a face struck dead-on
stops moving altogether.

**Two clocks, deliberately.** The SHAPE terms ride the normalized eased `tS`; drift and
tumble ride `tSec`, real seconds, because they are physical quantities (units/second,
radians/unit) and must not be reshaped by the easing that governs a face's contraction.
That is also what makes a hard hit throw the shards further and spin them harder — the
same `speed × seconds` gain the explosion's `_ExplosionAmount × _ExplosiveRotation`
carries.

**Zero velocity is the identity for both new terms**, which is the whole compatibility
story: a shield timer expiring, an arena teardown, a domain change, `MakeDangerous`,
`ActivateSuperShield`, and a herbivore grazing armour off all still render exactly the
puff they always did. `Prism.Consume` is deliberately in that set — it carries no impact
vector, only a suction sink, the same reasoning `AbsorbSuperShieldHit` already applies to
the deflection wobble. What *does* carry a direction: `Prism.Damage`'s impact vector (the
main path — a shielded prism hit by anything sheds along the blow), the Rhino sword's
`SkimmerSwingKinematics` contact velocity, and the overcharge skimmer's flight velocity.

Five things are worth carrying forward:

- **The magnitude is clamped on the CPU, and that clamp is the tuning dial.** Impact
  vectors are not comparable across the two damage paths — the legacy inertia gain and the
  true-velocity (`proportionalDebris`) one differ by orders of magnitude for the same blow,
  which is exactly why `PrismExplosion.TriggerExplosion` carries its own clamp note. Only
  the DIRECTION is reliably meaningful, so `PrismShieldMorph.ClampBreakVelocity` caps the
  speed at the shield component's authored `shatterDriftSpeedCap` (octahedron 20, stella
  25 u/s) and the GPU stays a pure function of what it is handed. Because the tumble angle
  rides the same clamped speed, that ONE number is how violently a shield comes apart. Set
  it to 0 and the tier reverts to the pre-2026-08-19 puff.
- **It re-expresses `RotateFacesAlongAxis` in HLSL rather than reusing it, for two
  concrete reasons** — the same call §4.9 made. That subgraph is not on **BlockGraph**,
  where a shielded prism's own material lives; and it rotates about the object ORIGIN off a
  **TANGENT** vector the shield meshes do not carry (`OctahedronMeshGenerator` /
  `StellatedOctahedronMeshGenerator` author positions, normals and TEXCOORD1 only), so a
  zero tangent turns its second rotation into a `cos(angle)` scale pulse. Routing the
  shards through the real explosion path instead — spawning them as `ExplosionDebrisSpawn`
  entities on the shield mesh — was considered and **rejected on that tangent dependency**
  plus the subtractive graph migration it would have needed to retire the shatter branch.
- **The rotation runs in the locally-ISOTROPIC frame** (`position * objectScale`), §4.9's
  correction: prisms are non-uniformly scaled, and an object-space rotation seen through
  that scale is a shear that wags the long axis far more than the others. Invisible on a
  cube, obvious on a trail slab — which is why the verifier asserts it explicitly.
- **The tumble moves positions only — the normal does NOT follow it, and that is a known
  limitation, not an oversight.** Carrying the normal through the morph was built, shipped,
  and reverted the same day. It rotated the vertex normal by the same rotation and spliced
  in front of `PrismJiggleClock.Normal`; on **ExplodingBlockGraph** the only acyclic source
  for an incoming normal is `RotateFacesAlongAxis`' output — and that subgraph is fed BY
  this node's position output, so routing its normal back in made the two nodes a **cycle**.
  ShaderGraph rejects a cyclic graph outright, so **every material on ExplodingBlockGraph
  went magenta and every prism explosion in the game broke.** The lesson generalises past
  this node: *splicing a node in FRONT of something already DOWNSTREAM of it closes a loop*,
  and a per-node structural validator cannot see it — `wire_prism_shield_morph.py` asserted
  every local invariant (slot ids, types, one feeder per input, the splice anchors) and
  passed. It now asserts **acyclicity over the whole graph**, which is the check that was
  missing. Doing the normal properly needs a SECOND custom function downstream of both
  nodes, not a wider signature on this one; the shard shrinks to nothing in 0.6–0.7 s, so
  stale shading is a small price next to that risk.
- **A property NODE's output slot carries its property's concrete type.** The same pass
  cloned a `Vector1MaterialSlot` onto the new Vector3 `_ShieldMorphVelocity` property node,
  so no vector could reach the shader and the shatter rendered as though the stamp had never
  arrived — a *silent* failure, visible only as "the old animation". The validator now
  checks this for every Vector1–4 property node in the graph, not just the one being wired.
  Both defects came from the same blind spot: a tool that proves what it built, but not what
  it built it INTO.
- **The culling envelope grew the drift term.** `SpawnShieldShatterBatch` re-centres the
  AABB on `ObjectDrift/2` and extends it by `|ObjectDrift/2|`, exactly like the explosion
  debris' envelope; the tumble adds nothing, because a face rotating about its own centroid
  stays inside its own circumradius of it. The object-space drift is computed at the
  request site (`PrismShieldMorph.RequestShatter`), which already holds the Transform —
  deriving it in the spawner would mean inverting a matrix per shard.

One new Hybrid-Per-Instance property carries it, `_ShieldMorphVelocity` (float3, WORLD
space; the shader does the world→object conversion with a raw unnormalized inverse-model
multiply — never a Direction-mode Transform node, which normalizes). The spin gain is a
shape constant in the HLSL (`PRISM_SHIELD_SHATTER_SPIN`, 0.25), the same place §4.9 keeps
its decay and cone constants, because the per-shield dial that matters is the speed cap.

Gates, three of them, each covering what the others structurally cannot:
`Tools/Shaders/wire_prism_shield_morph.py --check` proves the graph wiring — including,
since the reverted normal pass, that the graph is ACYCLIC and that every property node's
slot type matches its property (and the tool MIGRATES a graph carrying the old 9-slot
signature, so it is also the merge-conflict resolver); `PrismShieldMorphTests.EveryWiredGraph_MatchesTheHlslSignatureExactly` proves
the node's slots still describe the function's parameters — ShaderGraph passes slots
positionally, so a signature change on one side and not the other silently shifts every
argument with no error anywhere; and `Tools/Shaders/verify_prism_shield_shatter.py`
compiles the SHIPPED `.hlsl` through an HLSL→C++ shim and proves the arithmetic (ten
properties, including that a zero velocity is byte-identical to the pre-change shatter at
every t, that a face struck dead-on is pushed rather than tumbled, and that a NaN velocity
falls back to the plain puff). The last one is mutation-tested: dropping the isotropic frame or
moving the drift onto the eased `t` both fail it.

---

### 4.9 The super-shield deflection jiggle (shipped 2026-08-15, C14)

Super-shielded mass is **fully invulnerable** — `Prism.Damage`, `Prism.Consume`, the Burst AOE
resolve and the physics-fallback AOE all bail on it, and `devastate: true` does not override that
(it only bypasses the *shielded* tier). The consequence was that a hit on super-shielded mass had
**no visual consequence of its own**: the impactor's sparks and SFX fired, the prism did not move,
and the deflection read as the shot having missed. This is that deflection, made legible.

**It is §1 ANIMATION, so it is a per-instance STAMP — not §4.7's global uniform.** The distinction
is the one §4.7 draws in its own words: a corridor fade is *view-dependent*, changing every frame
for every prism as the camera and ship move, and therefore can never be a stamp. A deflection is
the opposite — it is fully determined at the instant of the hit, so it belongs to the §1 animation
category and rides `_PrismClock` + three Hybrid-Per-Instance properties. The precedents to copy
here are C5 (`PrismFlightClock`) and B1 (`PrismGrowScale`), never `PrismOcclusionFade`.

**The motion is a struck body's free precession, applied per FACE.** Each face rotates about the
prism's **object origin** — so the stella's outer spike tips wag far while the core barely moves,
which is what makes it read as jiggly rather than as a rigid nudge — by an angle `amplitude ×
envelope(t)`, about an axis that lies on a cone about that face's own normal. The axis
**precesses** around the normal and **nutates** (the cone half-angle breathes between 0 and π/2),
at deliberately non-commensurate rates, so the face alternates between an in-plane twist and a
maximum tip while the tip direction revolves, and the pattern never repeats inside one deflection.

**Three things about it are worth not re-deriving:**

- **The randomness needs no stamped seed and no mesh channel.** Prism meshes are hard-edged — the
  box is 24 verts / 6 distinct normals, the super-shield stella 72 / 24, because
  `StellatedOctahedronMeshGenerator` splits per face for its own normals — so the object-space
  **normal IS the face id**. The object-to-world translation is a free per-prism seed (so a track
  lining under one blast does not shimmer in lockstep), and `StartTime` re-rolls every hit. This
  matters because **the stella carries neither tangents nor UVs**: the tangent basis is built
  branchlessly from the normal alone (Duff et al. 2017), never read from the vertex stream, where
  it would be zero.
- **The rotation happens in the locally-ISOTROPIC frame** (`position × objectScale`), because
  prisms are non-uniformly scaled — a trail slab is long and thin — and an object-space rotation
  seen through that scale is a shear that wags the long axis far more than the others. The normal
  is carried through the same frame *inverted*, because a normal transforms by the inverse
  transpose. This is the same correction `RotateFacesAlongAxis` already applies to the shatter
  spin on ExplodingBlockGraph, and the effect composes with it: on that graph the jiggle takes the
  shatter's rotated position and normal as its inputs rather than replacing them.
- **The envelope reaches EXACTLY zero at `t = Duration`** (the `(1 − u)` factor is what guarantees
  it), so the scheduled `ClearJiggleStamp` is invisible and a stamp that never gets cleared is a
  permanent no-op rather than a prism stuck mid-wobble.

**One gate, not four.** The `IsSuperShielded` early-return previously existed as four independent
copies — `Prism.Damage`, `Prism.Consume`, `PrismSpatialIndex.ResolveExplosionHit` and
`ExplosionImpactor.ExecuteCommonPrismCommands`. All four now route through
**`Prism.AbsorbSuperShieldHit(impactSpeed)`**, which returns true when the prism absorbs the hit.
A per-call-site copy is a rule you can forget to apply at the next damage source; route every new
one here. Note the AOE path still keeps its own `PrismFlags.IsSuperShielded` read, because that
flag is also what sets `shouldContinue = false` and stops the blast expanding past the layer.

**Gameplay is untouched.** No collider, volume, spatial registration, shell state, domain or state
flag changes — which is exactly why it is safe to fire from *inside* an invulnerability gate.

**Two scope calls, recorded so they are not mistaken for omissions.** (1) `PrismTeamManager.Steal`
carries a fifth `IsSuperShielded` early-return and is deliberately NOT routed here: a steal is a
different verb — an attempt to change ownership, not a hit that could have destroyed the mass — and
on every vessel whose skimmer container carries `SkimmerDamagePrismEffectSO` the same contact
already reaches `Prism.Damage`, so routing it too would only double-fire into the rate limit.
(2) The AstroLeague ball/field sweeps and `Fauna.IsShieldedMass` read the flag to *skip*
super-shielded mass before any hit is dispatched; those are filters, not gates, and there is no
deflection to show.

**A SKIM never deflects, on any vessel — verified by playtest and re-verified after the energy
sword landed.** Two independent reasons, and both are wiring rather than design:

1. Four of the five skimmer containers (Dolphin, Manta-overcharge, Sparrow, Squirrel) carry **no
   prism-damage effect at all**, so they never call `Prism.Damage` and cannot reach the gate.
2. The fifth — `RhinoForceFieldSkimmerImpactorDataContainer` — carries
   `RhinoSkimmerDamagePrismEffectSO`, which handles super-shielded contact in its own branch
   (`PopSuperShield` on an energized blade, `NotifyPopDenied` + `BounceBack` otherwise) and
   **returns before any `Damage` call** either way.

Note what case 2's *denied* path is: a blade striking hardened mass it cannot break — textbook
"hit but not destroyed". It does not currently deflect, because the sword ships its own denial
feedback (a recoil plus a denied cue). Whether those should compose is a design question for
whoever owns the sword, not something to wire in from this side.

This is worth stating explicitly because the opposite reads as obviously true: an effect SO that
funnels into `Prism.Damage` with no shield check does exist, so "a skim is a hit" is a plausible
inference — and it was wrong twice, once about the fleet and once again after a merge replaced the
generic effect asset with a branching one. **Whether a given contact deflects is a question about
that container and that effect SO's branches, never about this feature.** It is the same shape
CLAUDE.md already records for the prism-collision slow, which three docs asserted fleet-wide for
vessels that had no speed effect wired. Check the producer before repeating any such claim.

**Costs.** Zero per-frame CPU: one stamp per hit (rate-limited per prism, default 0.12 s — a swept
piercing projectile re-dispatches the same prism every frame it overlaps, a drone swarm re-queues
its meal every behaviour tick, and N concurrent blasts each resolve independently, so without the
gate a prism under fire would restart its envelope before it could visibly move and read as
*frozen*), one scheduled clear, and one `RenderBounds` reset+expand.

**The culling envelope carries the prism's SCALE RATIO, and that is not a detail.** Padding is
`radius × maxTilt × (max(lossyScale)/min(lossyScale)) × 1.25` (`PrismSuperShieldJiggle.CullingPadding`).
Sizing it off `radius × maxTilt` alone — the obvious formula, and what the first cut shipped —
under-covers every anisotropic prism, because the rotation happens in the world-proportioned
frame and is mapped back through `1/scale`. Measured against the shipped HLSL with a clang
harness, peak displacement as a multiple of `radius × tilt`: **0.98× at uniform scale, 2.73× at
(3,3,10), 4.64× at (12,2,2), 15.97× at (1,1,20)** — bounded by the scale ratio in every case.
The under-covered prism frustum-culls away mid-wobble at the screen edge, which reads as the
mass blinking out. The four rows are pinned by `PrismSuperShieldJiggleTests` so the formula
cannot quietly regress to the uniform one.

**The settle is guarded, not cancelled.** `PrismTimerManager.CancelScheduledActions` is a linear
scan of the shared list, so cancelling per stamp would make one blast over N super-shielded
prisms O(N²) — exactly the case that stamps N prisms in a frame. Instead the callback carries the
stamp time it was scheduled for and no-ops unless `Prism.LastSuperShieldJiggleTime` still matches,
which is O(1) and also invalidates a settle left over from a previous life (a pooled prism is
deactivated, not destroyed, so the scheduler's own null-owner sweep never drops it —
`Prism.ResetState` clears the stamp time).

**Tuning** is `PrismSuperShieldJiggleConfigSO` (`Resources/PrismSuperShieldJiggleConfig`): duration,
the tilt floor/ceiling and the impact speed that reaches the ceiling, both wobble rates, and the
spam gate. The *curve shape* stays in the HLSL, matching how C5 splits feel from easing.

---

## 5. Migration tracker (the deduplicated work list)

> **Every ☐ / ◐ row below has a ready-to-paste branch prompt in
> `Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md`** — scoped, priority-ordered, and
> re-audited against the tree on 2026-08-15. Start there rather than
> re-deriving a row's scope from this table; several rows are narrower than
> they read (C7 is done by construction, C8 shipped, and the C6 remainder is
> two parent-transform scale animations). When a row here changes status, that
> doc is the other half of the update.

Phase A — infrastructure (everything else rides on it):

| # | Item | Status |
|---|---|---|
| A1 | Shader graphs: clock inputs (BlockGraph grow+color, ExplodingBlockGraph, SuctionGraph) — Hybrid Per Instance, settled-state authored defaults | ✅ ALL FOUR PHASES WIRED PROGRAMMATICALLY IN-BRANCH + PLAYTEST-CONFIRMED 2026-08-02 (donor-clone JSON synthesis, machine-validated; `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` phases 1–4): grow ✅, colors ✅, explosions ✅ (GPU-side conversion + bounds envelope), suction ✅. Plus the transparent-prism grow + color clusters on ExplodingBlockGraph (steal/repaint fades — wired, pending playtest) |
| A2 | `PrismRenderProperties` clock structs + prototype-archetype additions + `PrismRenderService` stamp APIs | ✅ shipped 2026-07-31; STRICT (always-on, no toggle) 2026-08-01. No MPB twins by design — no fallback tier exists |
| A3 | Swap scheduler: generalize `PrismTimerManager` (ScheduleAction / CancelScheduledActions) | ✅ shipped 2026-07-31 |

Phase B — migrate the engines (each retires a per-frame pass):

| # | Item | Status |
|---|---|---|
| B1 | Grow-in → clock (all ~12 feeder paths ride the one engine); gameplay-final-at-start (volume/spatial stamps, clock predicates, `ExecuteOnScaleComplete` → start) | ✅ LIVE (strict, the only path) 2026-08-01 — `PrismScaleManager` DELETED (D2, 2026-08-02). Graph wiring ✅ (playtest-confirmed smooth). Pending: `HoldColliderAtFullSize` deletion, `CreateBlockCoroutine` window simplification, arena-gate simplification, PhaseThresholds re-baseline |
| B2 | Color/state transitions → clock lerp (start colors + t₀; target = material authored; end-state material bound at START, settle scheduled) | ✅ LIVE (strict, the only path) 2026-08-01 — `MaterialStateManager` DELETED (D2, 2026-08-02). Graph wiring ✅ (playtest-confirmed smooth on BlockGraph; the transparent-prism color cluster on ExplodingBlockGraph is wired too — 2026-08-02 — so transparent steals/repaints fade instead of snapping) |
| B3 | Explosion/implosion → clock (stamp `{t₀, velocity, speed, duration}` / `{t₀, duration, direction, delay, location}`) | ✅ LIVE (strict, the only path) 2026-08-01 — moving-target DECIDED as the §1 exception (a snapshot would suck prisms toward where the fauna WAS): progress rides the clock, `PrismEffectsManager` refreshes `_Location` only (one float3/frame) while the target lives. Animation passes + Burst jobs DELETED (D2, 2026-08-02 — the manager keeps only convergence refresh + zombie audit). Graph wiring ✅ both graphs, PLAYTEST-CONFIRMED 2026-08-02: explosions ✅ (GPU-side world→object conversion inside `PrismExplosionClock` — raw inverse-model multiply, never the normalizing Direction-mode Transform — + flight-envelope bounds) and suction ✅ (`EncapsulateBoundsPoint` envelope). **Mass-death carrier upgraded 2026-08-02**: prism-death explosions spawn as BATCHED PURE-ENTITY debris (`PrismDebris` + `PrismRenderService.SpawnExplosionDebrisBatch` — no GameObject/pool/per-effect timer; full duration always); pooled path = fallback only. **Implosion batch port shipped 2026-08-04** (`SpawnImplosionDebrisBatch` + `RefreshImplosionDebrisBatch`): suctions ride the same carrier, and the moving-target §1 exception moved onto it as a per-record `_Location` refresh with a CPU-mirrored culling envelope — see §4.6 for the carrier's rules and the death-path marker split that shipped with it |
| B4 | Shield morphs → GPU (vertex-shader bloom/shatter from per-vertex face data + t₀; settled shared-mesh swap already conforms) | ✅ **SHIPPED 2026-08-15 — Phase B is complete and the LAST sanctioned CPU ticker is DELETED** (`PrismOctahedronShieldManager` + `IPrismShieldMorphTicker`; its active set is empty by construction because no shield registers any more, and an edit-mode test fails if the file returns). Both tiers' engage bloom and disengage shatter are now `f(clock, stamp)` via `PrismShieldMorph_float` + four Hybrid-Per-Instance properties, wired into **both** live-prism graphs by `Tools/Shaders/wire_prism_shield_morph.py`. The mesh generators bake each vertex's FACE CENTROID into TEXCOORD1, which makes the cache-shared **settled** mesh also the morph mesh — so no "anim variant" mesh was needed (the audit's sketch), no scheduled end-callback was needed (the shader clamps at t = 1, which IS the settled shield), the exotic-visual window collapses entirely (`SetExoticVisualActive` is now only ever driven FALSE; `SetRenderMeshOverride` still carries the handoff), and same-size shields stay in ONE batch through the whole animation instead of each minting a unique mesh + draw call. The shatter overlay's per-prism child GameObject is replaced by batched pure-entity debris on the §4.6 carrier (`PrismShieldShatter` → `SpawnShieldShatterBatch`, grouped by mesh × material × layer since shields vary in size and domain) and is no longer cancellable on re-engage — deleting shards mid-flight was a continuity-law breach. `AnimationCurve.EaseInOut(0,0,1,1)` == `smoothstep` (zero end tangents — verified against Unity's own serialization of that constructor), so every runtime-added shield is reproduced EXACTLY; the two prefabs that serialize a hand-altered curve (`BlueBlock`, `OctahedronShieldTest`, end tangents 2) now ease like the fleet, a stated deviation of up to 0.192. Curve fields retired. Design + the five carryable properties: §4.8. **Follow-up 2026-08-19 (§4.8.1):** the shatter now takes the prism explosion's own initial condition — a WORLD-space velocity — so the shards DRIFT along the blow and each face TUMBLES about `normalize(cross(velocity, faceNormal))`, the `RotateFacesAlongAxis` model re-expressed in HLSL (that subgraph is not on BlockGraph and needs a TANGENT the shield meshes do not carry). One new Hybrid-Per-Instance property (`_ShieldMorphVelocity`) and a CPU-side speed clamp (`shatterDriftSpeedCap`) that is the single per-tier dial. The tumble deliberately does not carry the vertex normal — a first attempt at that made `PrismShieldMorph` and `RotateFacesAlongAxis` a CYCLE on ExplodingBlockGraph and turned every explosion magenta; the wirer now asserts acyclicity and property-node slot types (§4.8.1). Zero velocity is the identity, so every direction-less disengage is byte-identical — proven by `Tools/Shaders/verify_prism_shield_shatter.py`, which compiles the shipped HLSL and checks it |

Phase C — rogue paths & ecosystem visuals (each is standalone):

| # | Item | Status |
|---|---|---|
| C1 | `ClearPrisms` `_Alpha` fade → GLOBAL-uniform shader corridor, **promoted to a platform law** | ✅ SHIPPED 2026-08-04 — `ClearPrisms.cs` + its prefab DELETED and excised from Rhino/Dolphin/Serpent (−3 trigger colliders, −3 kinematic Rigidbodies), along with the dead `IVessel.AllowClearPrismInitialization()` opt-out. Replaced by `PrismOcclusionCorridor` (2 globals/frame) + `PrismOcclusionCorridor.hlsl` wired into **every graph a live prism can render with** (BlockGraph + ExplodingBlockGraph), bound at `VesselController.Initialize` so no vessel or mode can omit it, with runtime fail-loud (`PrismOcclusionDiagnostics`) and an edit-mode coverage test. Full design, the four enforcement layers, and the stated cost: §4.7 |
| C2 | `MaterialBlendUtility` (overheat danger trail + skim overcharge) → the one color pipeline | ✅ shipped 2026-08-01: utility DELETED. Overheat danger trail: the redundant direct blend removed — `IsDangerous` pre-`Initialize` already runs `MakeDangerous()` through the pipeline (per-domain danger material, clock or legacy transition); `EnableDangerMode`'s material param is legacy-ignored. Skim overcharge: rides `MaterialPropertyAnimator.UpdateMaterial(overchargedMaterial, …)` — visible on both render paths; the multi-material append semantic retired |
| C3 | AOE double-growers (`AOERadialBlocks`, `AOEDangerHemisphereBlocks`) → single engine stamp; fix dead `growthRate` field writes + `renderer.material` clone | ✅ shipped 2026-08-01: both bespoke `GrowToScale` loops deleted (growth = the one engine via `TargetScale` + `SetGrowthRate`); `MakeDangerousAsync` deleted — danger/shield now ride the pre-`Initialize` flag contract so `PrismStateManager` applies the proper per-domain theme materials (the `renderer.material` clone and the instanced-path-blind restyle are gone); hemisphere prisms now get the firing vessel's Domain like the radial sibling |
| C4 | `FireTrailBlockActionExecutor` → pooled + mover-contract or stamped ballistic clock; remove `Destroy()` timer (ecosystem law) | ✅ 2026-08-07: **resolved by deletion**, the C10 outcome. `FireTrailBlockActionExecutor` + `FireTrailBlockActionSO` (and their metas) are gone. They were unreachable — a repo-wide GUID sweep found neither script on any prefab, scene or `.asset`, no `FireTrailBlockAction` asset was ever created from the `[CreateAssetMenu]`, and no C# referenced them outside their own pair. Migrating a path nothing can execute would have shipped an untested one; deleting removes four latent bugs instead: the raw `Instantiate` (line 65, commented `// ADDED TO REMOVE POOL`), **two** racing `Destroy` timers on a visible prism (the deferred `Destroy(go, ProjectileTime)` and `MoveBlockForward`'s tail — the imposed death `Docs/ECOSYSTEM.md` §0 forbids), a per-frame `tf.position +=` with no `NotifyPositionChanged`, so a wired-up version would have drawn at the muzzle for its whole flight and been invisible to `PrismSpatialIndex`, and authored defaults where `friendlyFire = true` → `MakeDangerous` silently clears its own `shielded = true`. The turret path below is the pattern to author from if the ability is ever wanted |
| C5 | `FullAutoBlockShoot.MoveAndAnchorAsync` turret anchor flight → stamped clock translation + one anchor callback | ✅ SHIPPED 2026-08-07 — `MoveAndAnchorAsync` DELETED. New `PrismFlightClock` (HLSL) + `_FlightStartTime`/`_FlightDuration`/`_FlightVelocity` (Hybrid Per Instance, wired into **both** live-prism graphs by `Tools/Shaders/wire_prism_flight_clock.py`) + `PrismRenderService.StampFlight`/`ClearFlightStamp`. The prism is spawned at the flight's **END POINT** with everything final and the vertex stage walks the visual in from the muzzle; zero CPU writes between the stamp and the anchor. **The open question in the prompt is answered: gameplay DOES collide mid-flight**, and it is the prism's *carried `Projectile`* that does it — detached at the muzzle, flown by the bullets' own `LaunchProjectile`, which is a projectile and keeps the ordinary gameplay-transform contract. That split is what lets the prism's transform be final at the destination. A stopping impact (SPACE < 5) re-stamps: one `NotifyPositionChanged` to the impact point, then `ClearFlightStamp`. The easing is the BULLETS' `cos(t·π/2T)`, so a turret prism and a bullet released together stay abreast. Also fixed here: the path never called `Prism.Initialize`, so every turret prism lived at `localScale` zero — invisible, with a zero-volume collider. Detail: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md` |
| C6 | Fauna visual transitions → clock: level-up bloom, wither-from-extremities (staggered t₀ ring stamps — pacing already analytic), devour/graze suction, boid starvation fade | ☐ not started |
| C7 | Flora growth tick / paced instantiation → stamped blooms (spawn scheduling stays CPU; visuals ride clock) | ☐ not started |
| C8 | Microscene conveyor recycle + first-population bloom → suction/bloom stamps (kills the per-frame notify storm) | ✅ shipped 2026-08-02 with the Wanderway grand-scale upgrade. `Microscene.AnimateScaleAsync` DELETED: the recycle is now (1) one grow-clock re-stamp per prism toward the animator min scale (budgeted, GPU runs the shrink), (2) `Prism.HideForTransport` + ONE container transform write, (3) a budgeted re-pose whose blooms are the standard creation stamps. The per-frame `NotifyPrismPositions` sweep is gone — it existed only because a container scale is invisible on the instanced path unless every child entity is re-synced every frame (§3.8 #1's failure, paid for rather than fixed). First population moved from `LayBatched` to `LayBudgetedAsync` so it rides the arena gate behind an `EnvironmentLoadVeil`. New: `Prism.BeginBulkTransport`/`EndBulkTransport` raises the creation-completion budget while transported mass re-enters |
| C9 | Cell swap retiring-world suction → per-prism suction stamps (fixes instanced-path invisibility, §3.8.1) | ☐ not started |
| C10 | Worm segment make-room shift → stamped slide (locomotion stays mover-contract) | ✅ 2026-08-02: resolved by deletion — legacy `Worm.cs` removed in the worm-colony kaiju rebuild; `WormFauna` locomotion rides the mover contract (Docs/ECOSYSTEM.md §23) |
| C11 | Spindle `_DeathAnimation` fade (prism-adjacent) → clock inputs on spindle material | ☐ not started |
| C12 | `PrismImplosion` watchdog → scheduler; orphan cleanup; `SkimFxRunner` stretch beam review; `CloakSeedWall` dead code removal | ◐ 2026-08-01: `TrailBlockBufferManager` deleted; `TrailViewer` removed from Urchin.prefab + deleted (D2, 2026-08-02); watchdog / SkimFxRunner / CloakSeedWall pending |
| C13a | Environment-laid prisms miss the clock path (the live repro: `grow:SpawnablePrism (Clone)`) | ✅ FIXED 2026-08-02 — root cause was NOT the raw-`Instantiate` lay: the shield engage-morph held `_exoticVisualActive` across the creation reveal, so `EnsureRenderEntity` was skipped at the exact instant the one-shot grow stamp fired. Fixed by §4.5 (a) entity existence ⊥ visibility + stamp-site self-heal + fact-based diagnosis, and (b) the birth rule (spawn-time shields snap). §3.8 #10 has the full anatomy. Pooling is orthogonal — a pooled prism with a `Shielded` kind failed identically; `BoostRingBuilder` only escaped because it defers shield kinds to `onGrown` |
| C13b | Environment lay pooling: `PrismTrailBuilder.LayOne` → pooled pull with final domain material (kills the `Domains.Blue` → domain spawn repaint) | ☐ not started — still worth doing on its own merits (spawn repaint, alloc churn), but it is NOT a clock-path fix. Note the pools are `maxSize`-bounded and environment mass is never released, so a naive pool-through would either destroy conserved mass on release or instantiate forever; it needs its own environment-prefab pool design |
| C14 | Super-shielded prisms absorb hits SILENTLY — a deflection reads as a miss | ✅ SHIPPED 2026-08-15 — new `PrismJiggleClock` (HLSL) + `_JiggleStartTime`/`_JiggleDuration`/`_JiggleParams` (Hybrid Per Instance, wired into **both** live-prism graphs by `Tools/Shaders/wire_prism_jiggle_clock.py`) + `PrismRenderService.StampJiggle`/`ClearJiggleStamp` + `PrismSuperShieldJiggle` (the stamp site) + `PrismSuperShieldJiggleConfigSO` (the feel). Each FACE wobbles about the prism's object origin on an axis that PRECESSES about that face's own normal and NUTATES, decaying to exactly zero at `Duration` so the scheduled clear is invisible. Per-face and per-prism randomness is derived on the GPU from the face normal and the object-to-world translation — no seed stamped, no mesh channel authored, which matters because the super-shield stella carries neither tangents nor UVs (the tangent basis is built from the normal alone). **Not** the §4.7 global-uniform shape: this is §1 animation, not a view-dependent value. The four invulnerability gates that used to each carry their own `IsSuperShielded` early-return now route through ONE `Prism.AbsorbSuperShieldHit`. Design + the measured envelope: §4.9 |

Phase D — lock-in:

| # | Item | Status |
|---|---|---|
| D1 | Docs locked (this file + CLAUDE.md anti-pattern + manager banners + cross-refs) | ✅ shipped (2026-07-31) |
| D2 | Delete the retired classes + scene components (`PrismScaleManager`, `MaterialStateManager`, `PrismEffectsManager`'s animation passes + Burst jobs, `AdaptiveAnimationManager` frame-skip machinery, retired animator fields, `TrailViewer`) | ✅ DONE 2026-08-02, programmatically: classes deleted; components excised from `PrismManagers.prefab` + `Urchin.prefab` by fileID (machine-verified reference-free); `PrismEffectsManager` slimmed to convergence refresh + zombie audit; animator dead surface stripped (`IsAnimating`/`IsScaling`/registration/…); `GameLoadSampler` re-sourced to `PrismSpatialIndex.LiveCount` + effect `EnabledInstances`; `AdaptivePerformanceSetting` documented INERT. Remaining in-editor: PhaseThresholds re-baseline (needs the measuring tool) |
| D3 | In-editor verification pass (all migrated paths, both render paths, load-gate + hitstop + pause) | ☐ not started |
| D4 | Retire the pooled `PrismExplosion` / `PrismImplosion` spawn path | ☐ **gated, not blocked** — the behavioural case is made (§4.6.1: no GameObject consumers, no working visual fallback, no runtime-override caller, shipped config instanced-ON), but it is a refactor not a deletion: the pool prefabs are the batched path's CONFIG source, and the zombie audit + `GameLoadSampler` read `EnabledInstances`. Gate = measured implosion parity (fauna playtest + a benchmark pass); do not do it in the same change as the batch |

---

## 6. Handoff — the in-editor gate (REQUIRED, do this next)

> **The step-by-step, self-verifying checklist lives in
> `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md`** — phases 1–7 with exact clicks, the
> validator (`FrogletTools > Ecology > Prism Animation> Validate Clock Wiring`),
> the play-mode smoke test, and a troubleshooting table. This section is the
> summary.

**STRICT MODE is live: the clock path is the only animation path, and the graphs
are not wired yet.** Until this session happens, every prism spawn/transition/
effect snaps to its end state and the console screams one `[PrismClock]` error per
unwired material. That is the intended forcing function. The session:

1. Wire the graphs per §4.4 — **BlockGraph's three grow properties + the
   `PrismGrowScale` node are the 15-minute change that makes ring / gyroid / trail
   growth GPU-smooth**; then BlockGraph's color inputs, ExplodingBlockGraph,
   SuctionGraph. Each material family goes smooth (and silent) the moment its graph
   reimports; the remaining errors enumerate what's left.
2. Run the §4.4 verification protocol. (The retired CPU animation managers are
   deleted, so "0 active animators" holds by construction — verification is now
   about zero `[PrismClock]` errors and visual smoothness.)
3. ✅ DONE (2026-08-02, programmatically): the D2 physical deletion pass — retired
   manager classes deleted, their prefab components excised by fileID, the
   `PrismEffectsManager` animation passes/Burst jobs removed (the class stays for
   the §1 convergence refresh + zombie audit), `AdaptiveAnimationManager` deleted.
4. The remaining C-phase items (C6–C10 ecosystem visual transitions, C4/C5
   projectile teardown fixes) land branch-by-branch on the wired graphs, following
   the B1/B3 templates (suction = `StampSuctionClock` + scheduled retire; blooms =
   the grow engine, already migrated).
5. `TrailViewer` ✅ removed from `Urchin.prefab` + file deleted (D2, 2026-08-02).
   Remaining in-editor chore: re-baseline PhaseThresholds
   (volume-final-at-spawn — FrogletTools > Ecology > Measure Cell Environment
   Baselines).

## 7. Enforcement

- **CLAUDE.md ▸ Anti-Patterns** carries the rule; any PR adding a per-frame prism
  visual write is rejected on review.
- Every new prism visual state MUST arrive as: pool material + stamp + clock shader +
  scheduled swap. If a state seems impossible to express that way, that is a design
  discussion (see §1 "animation vs. live gameplay data") — not a license for a
  per-frame loop.
- The three CPU animation managers carry header comments pointing here; new
  registrations into their per-frame passes are treated as regressions once their
  paths migrate.
