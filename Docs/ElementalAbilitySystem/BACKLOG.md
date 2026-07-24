# Elemental Ability Upgrades — Sequenced Backlog

Order matters: Phase 0 makes the Sparrow *work*, Phase 1 makes elements *do something*
quantitatively, Phase 2 adds the level-5 qualitative tier, Phase 3 is presentation. Each item
carries its evidence pointer in `AUDIT.md`.

## Phase 0 — Repair the foundation (guns + plumbing) — no new features

| # | Item | Source |
|---|---|---|
| 0.1 | **Cherry-pick `f1f278ab4`** (`origin/claude/fix-gun-focus-vvYxp`) — DI-inject pool-replenished projectiles. THE gun fix. | AUDIT §2#1 |
| 0.2 | **Fix skyburst dud colliders**: re-enable the root collider on pool `Get` (in `ProjectilePoolManager`/`Projectile.OnEnable`) so `DisableColliderNow` can't poison reuse; add a flight-generation guard so the detonator's delayed `ReturnToFactory` can't steal a re-issued projectile. | AUDIT §2#2, #3 |
| 0.3 | **Cherry-pick/adapt `dad9f2ffc`** — fire along the muzzle's forward, not the Gun component's. | AUDIT §2#6 |
| 0.4 | **Kill the shared-SO state**: move `SparrowModeSwitchingFireSO`'s `_active/_registry/_isHeld` into a per-vessel executor (or clone stateful SOs for humans as AIPilot already does); add vessel identity to the `stationaryModeChanged` consumption so another vessel's stance toggle can't flip your held fire. | AUDIT §2#4, #5 |
| 0.5 | **Gun hygiene**: fix the `_onCooldown` latch; read domain live (drop the `Initialize` snapshot); detach full-auto bullets via the world-anchor pattern the skyburst already uses. | AUDIT §2#7 |
| 0.6 | **Cherry-pick `7924a4e4`** — the five fleet runtime bug fixes (all verified still live). | AUDIT §6 |
| 0.7 | **Sparrow prefab repair**: rewire `elementBars` (rename orphan — the elemental HUD is dead without it), delete the null-factory Gun + orphaned ElementPips GO, fix the Overheating turn-end event wiring, fix `TrailScaleModulator.controller`, point `SparrowPrismController.skimmer` at the active skimmer, fix `defaultAmmoIndex`. | AUDIT §5 |
| 0.8 | **Turret prism pipeline**: route turret-fired prisms through `TargetScale` + `Prism.Initialize` (spatial-index registration, Cell binding, bloom-in). Prereq for all Mass work; fixes a live continuity-law violation. | AUDIT §4-Mass |
| 0.9 | ~~Skyburst ammo economy~~ — RESOLVED as audit false positive: crystal restock was already wired (`SparrowVesselChangeResourceByCrystalEffect` refills Missiles to full on crystal impact). No change needed. | AUDIT §2#7 |

**Exit criterion:** hold-fire for 60 s in a busy scene with 2 human + 2 AI Sparrows: no NREs, no
runaway fire, no duds, bullets go where aimed, turret prisms bloom and register.

## Phase 1 — Quantitative layer (the fundamental, fleet-wide shape) — SHIPPED

All six items landed (1.3 shipped with the recommended default: resting 0 everywhere; 1.6's
edit-mode tests remain open — folded into Phase 2.6 verification). Deltas: the handler is
lazily self-initializing via VesselStatus.ElementalAbilityHandler; maps load from
Resources/ElementalAbilityMaps/{VesselClassType}. Charge→blast ships as charge01 through the
detonator with authored ranges 100→170 on the four skyburst effect assets.

| # | Item |
|---|---|
| 1.1 | Add `ElementalScaling` (cherry-pick + `IsQualitativeUnlocked`), `ElementalAbilityMapSO`, `R_VesselElementalAbilityHandler` + `IVesselStatus` property (ARCHITECTURE §3). |
| 1.2 | **Fix the comeback clobber**: route comeback bonuses through the modifier layer (or deltas), never `SetElementLevel` on the base — crystals must be able to progress a vessel to level 5 during a match. |
| 1.3 | **Initial-levels policy** (open decision below) + implement: MP spawn path gets explicit levels; SP default reconsidered. |
| 1.4 | Sparrow quantitative wiring per ARCHITECTURE §5: Space→range, Time→boost, Mass→stretch, Charge→blast (replace the literal `0`, author real min/max on the skyburst assets). |
| 1.5 | Cherry-pick the branch's non-Sparrow executor hunks (Manta/Dolphin/Rhino/Serpent) onto the new config home; author their `ElementalAbilityMapSO` assets. |
| 1.6 | Edit-mode tests: petal-math ↔ threshold consistency; every flyable's map resolves 4 entries; comeback-vs-crystal compositing. |

## Phase 2 — Level-5 qualitative tier — SHIPPED (2.1–2.5; 2.6 verification pending)

Implementation notes: unlock bits ride an owner-write `NetworkVariable<byte>` on
`R_VesselActionHandler` (VesselStatus is deliberately a plain MonoBehaviour); non-owner peers
resolve `IsUpgradeActive` from the replicated bits, the owner/offline path from the locally
derived latch. Piercing ships as a per-shot `StopOnFirstPrismImpact` flag (default true below
Space-5) applied in `ProjectileImpactor`'s prism case; domain-sparing as a per-shot
`SpareOwnDomain` flag gating the direct-hit damage in `SkyBurstProjectileDamagePrismEffectSO`
(AOE already spared own domain); the steal→`UpdateDomain` gap is wired in
`Prism.HandleTeamChangedForCell`. MASS-5 shields apply at anchor (after collider re-enable +
index registration, so the Box→Mesh swap runs last and the index flags sync). The barrel roll
is `BarrelRollController` on the vessel root (visual-child roll, `ModifyVelocity` displacement,
`BlockRotationOverride` for travel-aligned bridging prisms, replicating via `n_BlockRotation`);
`676a8f994` was cherry-picked so gamepad+touch publish the radial stick vectors. REMAINING in
2.5: AI trigger synthesis (autopilot vessels produce no stick input, so the roll is inert for
AI), keyboard/mouse stick population, and an authored animator roll state if the transform roll
isn't juicy enough.

| # | Item |
|---|---|
| 2.1 | Unlock detection + latch policy (hysteresis 5/4, no mid-action interrupt) + `NetworkVariable<byte>` unlock bits on `VesselStatus` (server-write). |
| 2.2 | **SPACE-5 piercing**: implement destroy-on-first-prism-impact as the sub-5 default (per-shot flag through `Gun.FireGun → Projectile.Initialize`); L5 restores today's pierce-through. Revisit full-auto pool `bufferSizeTarget` (piercing raises concurrent live projectiles). |
| 2.3 | **MASS-5 shielded turret prisms**: `IsShielded` flag-before-Initialize; regular shield only. Collider-budget statement: RESOLVED — shields keep the authored `blockCollider` trigger (no convex MeshCollider), so shielded prisms are collider-LOD-cullable like any other. (Interaction is at authored box size; shape-precise shielded collision is the planned three-LOD follow-up.) |
| 2.4 | **CHARGE-5 domain-sparing skyburst**: prereq — wire steal → `PrismSpatialIndex.UpdateDomain` (stale-domain gap is documented in `Docs/SPATIAL_INDEX.md`); then gate the direct-hit damage per-shot. Keep the two AOE damage paths (Burst batch + physics fallback) in lockstep. |
| 2.5 | **TIME-5 barrel roll** (the largest item): publish `Left/RightNormalizedJoystickPosition` from all input strategies (adapt unmerged `676a8f994`); `BarrelRollActionExecutor` — perimeter detect (magnitude ≥ ~0.95 on the *radial* vector, never the eased one), CW/CCW by stick half, ramped `ModifyVelocity` orthogonal displacement (left-stick direction, rotation input attenuated during the roll), OrientationHandle/Animator visual roll (new animator state; note the prefab runs `MantaAnimationContoller`), `blockRotation` override for travel-aligned bridging prisms, AI trigger synthesis, camera check (per-frame delta < teleport-snap threshold). |
| 2.6 | In-editor verification pass per upgrade (repro steps + MPPM two-client check for the replicated bits). |

## Phase 3 — Presentation

Petal flare on unlock via `OnUpgradeStateChanged` (juice in `ElementalBarsConfigSO`); ability-icon
row only in the branch's final view-binding shape with authored sprites; unlocked-state icons;
Sparrow HUD indicators (roll armed, shielded turret, piercing, domain-safe). Clean up dead code
(`ElementPipsView`, `SparrowAnimationController` or adopt it properly, `AIGunner`,
`ExplodableProjectile`, `StopGunsAction`, `SparrowExhaustProjectile.prefab`).

## Open decisions (owner: Garrett)

1. **Initial element levels.** SP arcade spawns at level 5 in everything (`MiniGame.cs:61`) —
   all upgrades ON at spawn; MP spawns at 0 — upgrades unreachable without crystals.
   *Recommendation:* spawn at resting 0 (or captain-authored levels) everywhere; let crystals +
   comeback drive progression. This makes the unlock an earned mid-match power spike in every mode.
2. **Default bullet behavior inversion.** Spec: bullets destroy on first impact by default,
   piercing at Space-5. Today they already pierce — implementing the spec *nerfs* sub-5 Sparrows.
   *Recommendation:* follow the spec (the default+upgrade pair is what makes Space legible), tune
   fire rate/damage to compensate if needed.
3. **Charge-5 scope.** Minimal reading: gate only the direct-hit damage (AOE already spares own
   domain). Stronger reading: additionally flip AOE `affectSelf` to true below L5 so low-Charge
   skybursts are genuinely risky near your own structure. *Recommendation:* minimal first;
   revisit after playtest.
4. **Latch policy default.** Re-lock with 5/4 hysteresis (codebase-symmetric) vs latch-for-turn.
   *Recommendation:* re-lock; debuffs stripping your upgrade is legible elemental counterplay.
5. **`origin/claude/falcon-brittlestar-reintro-oyx1q1`**: if it is ever merged, it must not land
   without `f1f278ab4` (its prewarm cap makes the pool-injection NRE strictly worse — AUDIT §2#1).
