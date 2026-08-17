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
| 2.5 | ~~**TIME-5 barrel roll**~~ — SHIPPED, then **RE-SCOPED (2026-08)**: the roll is no longer an upgrade at all. `BarrelRollController` ships it as BASE kit (left stick at perimeter + boost, one roll per press, ungated), and TIME-5 is now **Elemental Ward** — elemental-debuff immunity while boosting, built as the general `ResourceSystem.SetElementalDebuffImmunity` state + the shared `VesselElementalImmunity` driver (the Serpent holds the same state while stopped). Overheat is deleted; the boost is indefinite. See `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md`. Still open from the original item: **AI trigger synthesis** (autopilot produces no stick input, so AI never rolls). |
| 2.6 | In-editor verification pass per upgrade (repro steps + MPPM two-client check for the replicated bits). |

## Phase 3 — Presentation

Petal flare on unlock via `OnUpgradeStateChanged` (juice in `ElementalBarsConfigSO`); ability-icon
row only in the branch's final view-binding shape with authored sprites; unlocked-state icons;
Sparrow HUD indicators (~~roll armed~~ SHIPPED — the boost icon's ring is now the roll-charge pip, `SparrowHUDView.SetRollCharge`; still open: shielded turret, piercing, domain-safe). Clean up dead code
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

## Four-icon ability row — follow-ups (from the `vessel-ability-icons` branch)

The row contract, the level-5 upgrade signal, the ability-bound control hints and the fleet
auditor shipped. Squirrel and Sparrow are compliant. What is left, in rough priority order:

### Blocked on design (cannot be wired until someone authors the map)

1. **Author the open `ElementalAbilityMapSO` slots** for Manta, Dolphin, Rhino and Serpent. Each
   still has `(open design slot)` entries with `Input = 0` and **no `UpgradeLabel` on any element**.
   Proposals live in `FLEET_MAPS.md` §2 and are un-approved. Until the element→ability→input
   mapping exists, an icon row cannot be bound — do not guess it to satisfy the auditor.

### Wiring, once the maps land

2. **Rhino already has four icons** — `LaserTargeting`, `Crystal`, `ForceField` (all three are
   vessel-prefab objects parented into the HUD instance) plus `BoostContainer` from the HUD variant.
   They sit at x 1288.6 / 1461.6 / 1639.6 / 1814.6, y 116.7, 99.8×99.8 — a real row needing only
   ~3 px of pitch evening. Bind + reorder once the Rhino map is authored.
3. **Re-survey Dolphin and Manta at the vessel level.** The Rhino's icons were missed because the
   first survey only read HUD prefabs; three of its four icons live in the vessel prefab. Assume the
   same may be true of Dolphin (1 icon found) and Manta (0 found) until checked the same way.

### Independent of the maps

4. **Add an `InputDeviceIconSetSwitcher` to the Sparrow HUD.** It has four Xbox + four PlayStation
   `ControllerIcon` glyphs but no switcher, so (a) both sets render simultaneously, and (b)
   `BindHintsToAbilities` never runs there — its glyphs are currently placed *statically* and will
   strand again if the row is reordered. This is the fix that makes them self-placing.
5. **Sparrow glyph art is wrong** independently of position: the Xbox set uses `R1 Active` where the
   control is the right TRIGGER and `R2 Active` where it is the LEFT trigger; the PlayStation set uses
   `triangle` where the control is ✕ and `square` where it is R2. `Buttons/XBOX/` contains **no
   left-trigger art at all** — this needs an artist, not a wiring change.
6. **`ControllerButtonIconReferences` is vestigial and destructive.** Zero callers in the codebase;
   its only runtime effect is `Awake()` doing `_img.sprite = inactiveIcon` unconditionally, which
   stomps the authored per-side sprite. On the Squirrel all four pad glyphs share one `inactiveIcon`
   (`XBOX/R1.png`), so both the left and right glyph render as R1. Either delete the component or
   make it non-destructive and author per-side sprites.
7. **Author `upgradedSprite` art** for the wired vessels. The sprite-swap layer of the upgrade signal
   is in place but no vessel authors upgraded art, so only the element badge and the scale bump are
   visible today. Note two Sparrow icons (`missileIcon`, `weaponModeIcon`) have their sprite driven by
   gameplay and start disabled — the badge must carry the signal there regardless.
8. **Squirrel hint labels say `L1`/`R1`** in the inspector while the bindings are triggers (LT/RT).
   Designer notes only — used by `SetHintActive(label, …)` for unbound hints — but misleading.
9. **Author an impact-effect/skimmer container auditor** (`FrogletTools > Vessels`, modeled on
   `VesselAbilityRowAuditor`). This is the vessel contract's least-guarded clause — null containers,
   unwired skimmer stacks, orphaned effect assets, and stale serialized blocks have only runtime
   symptoms today (`.claude/skills/vessel/references/CONTRACT.md` §9 catalogues the live
   misconfigurations: Serpent's dead VacuumSkimmer, Sparrow's all-empty containers, the five
   unregistered hulls' null nested-skimmer containers).

---

## Dolphin follow-ups (opened by `claude/dolphin-energy-crystal-cooldown-zpvc07`)

Mechanics reference: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

10. **Serpent's skimmer is dead.** `_nearFieldSkimmer` resolves to a `VacuumSkimmer` whose
    GameObject is INACTIVE *and* which carries no `SkimmerImpactor`/container. Same class of
    fault the Dolphin had; deliberately left untouched by that branch (different vessel).
    Confirm with `FrogletTools > Vessels > Audit Vessel Skimmers`.
11. **`ExplosionImpactor.OnBlastResolved` is a static C# event.** CLAUDE.md's anti-patterns
    forbid static events for cross-system communication. It is presentation-only, has one
    self-filtering listener (the Dolphin HUD's prism tally), and subscribes/unsubscribes
    symmetrically — but it is a deviation. Convert it to a SOAP channel the moment a second
    consumer appears, or if the reviewer wants it converted now: the payload needs the firing
    vessel plus the count, so it is a new `ScriptableBlastResult` type (struct + event +
    listener), which is why it was not minted for one HUD tally.
12. ~~**The jaw gape is a linear approximation of the cone's half-angle.**~~ **RESOLVED.** Both
    the hull and the HUD icon now call `RiptideAnimation.GapeAngleAt(t, min, max)`, which lerps
    the TANGENTS of the two authored angles and takes the arctangent — exact at every charge,
    because `tan(angle(t)) = lerp(min, max, t) / (2 × height) = lerp(tan(minAngle), tan(maxAngle), t)`.
    The feared `RiptideAnimation` → impact-effect dependency was never needed: the identity holds
    with nothing but `MinJawAngle` / `MaxJawAngle`. The empty end reads its real 4.76° gape rather
    than a shut jaw. See `DOLPHIN_ENERGY_ECONOMY.md` §3.
13. **The Dolphin's Space icon is still placeholder art** (`ConeBlastIcon-PLACEHOLDER.png`,
    accepted by Garrett as "the blast seems fine"). The other three slots use shipped art (the
    vessel's own jaw silhouettes, the omni crystal, the authored boost ring).
14. **The Dolphin HUD has no `InputDeviceIconSetSwitcher`**, so `BindHintsToAbilities` never runs
    there and its control hints are unbound — same gap as the Sparrow (item 4).

## Dolphin follow-ups (opened by `claude/dolphin-echobliteration-capsule-a0vs26`)

15. **The rendered cone widens with the capsule's length, by construction.** `_maxExplosionScale`
    is BOTH the capsule's length and the cone mesh's base diameter, because the capsule's tips ride
    the visible base circle — that coupling is what keeps the damage volume inscribed in what the
    player sees. Taking the length to 130% therefore widened the full-charge visual (base diameter
    1600 → 2080) even though the blast destroys *less* mass than before off the gape axis. If the
    visual reads too wide once observed in context, the fix is a decision, not a bug: either accept
    it, retune the length, or decouple the mesh from the capsule and accept tips that reach past
    the drawn cone. Do not silently do the third. `DOLPHIN_ENERGY_ECONOMY.md` §1.
16. **The blast's vessel-impact volume is still one leading cross-section, not the swept solid.**
    Prisms go through the exact Burst sweep, but explosion→vessel effects resolve through the
    trigger collider riding the leading base plane — so a vessel the wavefront already passed is
    only hit on the frame the plane reached it. The capsule change fixed the collider's SHAPE
    (it now matches the sweep instead of contradicting it) but not its coverage in depth. Full
    statement and why fixing it is a gameplay change needing its own branch:
    `Docs/SPATIAL_INDEX.md` § Known limitations.
17. **Only the Dolphin's crystal-blast asset carries `_coreExplosionScale`.** The other four
    (`Manta`/`Rhino`/`Serpent`/`Squirrel`) will serialize it as `0` the next time Unity re-saves
    them, which is the intended fallback (core = min = the plain circular cone) — but if one of
    those vessels ever moves to the conic prefab, it needs the field authored or its blast rests
    as a sphere.

## Dolphin follow-ups (opened by `claude/dolphin-speed-boost-tuning-qgnojw`)

18. **`maxBoostMultiplier` is applied TWICE, so the authored peak is squared.**
    `VesselTransformer.CurrentBoostAmount()` multiplies `BoostMultiplier` (rewritten every
    discharge tick as it decays toward 1) by `ChargedBoostCharge` (pinned at the value the
    charge ended on) — both derive from the same `BoostMultiplierFrom`, so a full meter yields
    `maxBoostMultiplier²`. The design doc described a single factor for the ability's whole life;
    the code has always squared it. This is **shipped behaviour on both `ChargeBoostActionExecutor`
    and the legacy `ChargeBoostAction`**, so it was documented rather than changed — a tuning
    branch is the wrong place to halve a vessel's boost. Deciding it: either declare the square
    intentional and rename the field to say so, or collapse it to one factor and re-tune
    `maxBoostMultiplier` to `2.259² = 5.103` to hold the current feel. Do not change it silently
    in either direction. `DOLPHIN_ENERGY_ECONOMY.md` §2.
19. **`ChargedBoostCharge` is never cleared when a discharge ends** — only its gate
    (`IsChargedBoostDischarging`) is. Harmless today because every read is behind that gate, but
    it means the field holds a stale multiplier for the rest of the vessel's life, and any future
    reader that forgets the gate silently inherits a free boost. Clear it alongside the flag in
    `DischargeRoutineAsync`'s tail and in `VesselStatus`'s reset if this area is touched again.
20. **The Dolphin's speed retune has not been flown.** 60 → 78 cruise and 210 → 357 boost are
    arithmetic, not feel. 357 is a large jump and the speed tunnel amplifies how it reads — expect
    a balancing pass. Steps + knob table: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Skim-visual follow-ups (opened by `claude/dolphin-skim-effect-7sd2w1`)

21. **The Squirrel still runs BOTH skim visuals.** `SquirrelSkimmerImpactorDataContainer` holds
    `SkimmerFXPrismEffect` (the `[Obsolete]` per-prism beam) *and*
    `SkimmerForcefieldCracklePrismEffect` (its replacement) — the same doubled state the Dolphin
    was just cleaned out of. It was left alone deliberately: the Dolphin's removal was a
    playtest call on one vessel, and the Squirrel's beam may be reading as intentional on a
    vessel whose whole loop is trail-riding. Decide it explicitly — either retire the beam
    fleet-wide and delete `SkimmerFXPrismEffectSO` with it, or state in the SO's summary that
    the two are meant to compose and drop the `[Obsolete]`. Do not leave it as an accident.
22. **The Dolphin prefab carries three DEAD prefab-instance overrides** on its inactive nested
    legacy `Skimmer.prefab` instance (`m_IsActive: 0`), writing
    `skimmerPrismEffectsSO.Array.{size,data[0..2]}` — a field that is **commented out** on
    `SkimmerImpactor`, so Unity retains the modifications forever without ever resolving them
    (the same never-pruned-override pattern CLAUDE.md documents for `Cell`). One of them still
    references the beam asset, which is why a GUID sweep finds `SkimmerFXPrismEffect` in
    `Dolphin.prefab` after the container was cleaned. Harmless on three independent counts
    (dead field, inactive GameObject, `_nearFieldSkimmer` points at `EnergySkimmer`) — but it is
    a false positive for the next person who greps. Sweep it with the dead-override tooling
    rather than by hand-editing prefab YAML.

## Dolphin prefab rot (observed while shipping `claude/cell-data-not-found-p62gm9`)

23. **The Dolphin's `ElementalBarsController` is unwired and carries stale YAML keys.** Its
    component on `Dolphin.prefab` serializes `vesselPrismController` / `driftTrailAction` /
    `config` / `energyResourceIndex` / `view` — field names **no script in the project
    declares any more** (`ElementalBarsController` declares only `elementBars`, which is
    therefore unassigned, so the Dolphin shows **no elemental petal bars**). `view:` also
    points at fileID `257326519381942953`, which exists nowhere in the prefab — the one
    dangling local reference in the file, present since before this branch. Same
    never-pruned-override rot CLAUDE.md documents for `Cell`: Unity retains modifications
    whose `propertyPath` no longer resolves. Fix by running
    **FrogletTools > Vessels > Wire Elemental Petal Bars** on the Dolphin and re-saving the
    prefab, then re-check with the ability-row audit. Pre-existing; NOT caused by the shard
    toggle removal (verified: the base revision has the same dangling reference).
24. **`ActionExecutorRegistry._executors` on the Dolphin holds an empty slot** (`{fileID: 0}`
    as its first entry). Benign today — `InitializeAll` filters with `.Where(e => e)` — but it
    is the same authoring hole that produced this branch's `NullReferenceException` in the
    impact-effect dispatch, in a list that happens to filter. Clear the slot when the prefab is
    next opened.
