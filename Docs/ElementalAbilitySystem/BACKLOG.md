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

1. **Author the open `ElementalAbilityMapSO` slots** for Rhino and Serpent (Manta shipped
   2026-08-26 via the spec remake — see FLEET_MAPS.md §2 Manta and `MANTA_STING_KABLOOM.md`;
   Dolphin shipped earlier). Each
   still has `(open design slot)` entries with `Input = 0` and **no `UpgradeLabel` on any element**.
   Proposals live in `FLEET_MAPS.md` §2 and are un-approved. Until the element→ability→input
   mapping exists, an icon row cannot be bound — do not guess it to satisfy the auditor.

### Wiring, once the maps land

2. **Rhino already has four icons** — `LaserTargeting`, `Crystal`, `ForceField` (all three are
   vessel-prefab objects parented into the HUD instance) plus `BoostContainer` from the HUD variant.
   They sit at x 1288.6 / 1461.6 / 1639.6 / 1814.6, y 116.7, 99.8×99.8 — a real row needing only
   ~3 px of pitch evening. Bind + reorder once the Rhino map is authored.
3. **Re-survey Dolphin at the vessel level.** The Rhino's icons were missed because the
   first survey only read HUD prefabs; three of its four icons live in the vessel prefab. Assume the
   same may be true of Dolphin (1 icon found) until checked the same way. (Manta is resolved:
   the 2026-08-26 remake authored its four-icon row into Manta.prefab at the wirer bands.)

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
20. **The Dolphin's speed retune has not been flown.** 60 → 68 cruise and 210 → 347 boost are
    arithmetic, not feel. 347 is a large jump and the speed tunnel amplifies how it reads — expect
    a balancing pass. (The 78/357 figures that stood here came off `DefaultMinimumSpeed` 10, which
    is **0** since `claude/dolphin-minimum-speed-59q8ay` — throttle-off is now a real stop, and the
    floor no longer pads either number.) Steps + knob table: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

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

## Danger-prism consequence drift (observed while shipping `claude/dolphin-time5-debuff-immunity-kn4vmy`)

25. **CLAUDE.md's danger-prism paragraph asserts an "input mute" that nothing wires.** The
    locked-design paragraph lists a danger prism's costs as "slow + all-element debuff +
    boost reset", and the elemental-immunity sentence says the immunity "denies ONLY the
    elemental drain: the slow, the input mute and the boost reset still land". Verified at
    ship time: `SparrowDebuffByRhinoDangerPrismEffect.asset` — the only effect that mutes an
    input — is referenced by **no `VesselImpactorDataContainerSO` at all**, so the input mute
    happens on no vessel. "Boost reset" is also imprecise: the live effect is
    `VesselChangeBoostByPrismEffectSO`, a `retainedFraction` **halving** on any prism ram, and
    it deliberately skips its pinned-snapshot correction while the vessel is DRIFTING. The
    same doc-vs-producer gap CLAUDE.md already records for the per-vessel speed effect.
    Left alone here to keep this branch's diff scoped to the Dolphin; the fix is a re-audit of
    that whole paragraph against the shipped containers, per-vessel, not a wording tweak.

26. **`SkimmerChangeResourceByPrismEffectSO`'s danger-bonus fields now have zero users.**
    `_dangerBonusElement` / `_dangerBonusMultiplier` were introduced for the Dolphin's Time-5
    "Live Current"; that upgrade was re-scoped to Drift Ward and the asset set back to
    `None`/`1`. The machinery is generic, documented and correctly gated on `IsUpgradeActive`,
    so it is kept as a reusable surface rather than deleted — but it is unreferenced today.
    Delete it if no vessel claims it within a release or two, and note that it is a DIFFERENT
    effect from `SkimmerBoostPrismEffect.dangerEnergyMultiplier` (the platform's 10× danger
    bonus): different resource (energy vs boost), different gate (per-asset element vs
    hardcoded Charge). The two are easy to confuse from an ability map's prose alone.

27. **The Rhino's `GrowTrailAction` has never run — its grow loop is unreachable.**
    `GrowTrailActionExecutor.Begin` sets `_growing = true` and then calls `End()`, which sets
    it back to `false`, so `LoopAsync`'s `while (_growing)` never executes a single step and it
    falls straight through to a shrink loop that has nothing to shrink. The ordering is simply
    inverted; `_growing = true` belongs *after* the `End()` that cancels the previous run.
    It is bound both to the Rhino's own `_inputEventShipActions[InputEvent 0]` and to its
    `AIPilot.abilities`, so it is dead for the human pilot and the AI alike.

    **Consequence, and why it surfaced:** without the grow, the Rhino only ever lays its
    RESTING trail — `BaseScale (3, 3, 0.5)` with an authored `Gap: 2` gives two rails of
    `3×1/2 − 1 = 0.5` width, i.e. **0.5 × 3 × 0.5 = 0.75 volume per prism**, laid every 5
    world units (`initialWavelength == minWavelength == 5`, so the spacing is speed-invariant)
    20 units behind the hull. That is 4× smaller than the next-smallest trail in the fleet
    (Squirrel 3.09, Serpent 3.00, Manta 5.00, Dolphin 12.00) and at any distance it reads as
    *no trail at all* — which is how it was found, via an AI Rhino released by the freestyle
    Spawn Matrix's hangar.

    **Do not fix the ordering on its own — it would make the Rhino worse.** `Step` clamps
    `XScaler`/`YScaler`/`ZScaler` against `maxSize` but never clamps `Gap`, and
    `AnyAboveMin`'s gap branch (`if (so.WGap > 0f)`) is unreachable for the asset's authored
    `GapWeight: -1`, so a live grow loop would open the hole without bound and never restore
    it — rails inverting to zero width and flying out sideways. The spawner now refuses to lay
    a degenerate rail (`VesselPrismController.ClampHalfGap`, shipped on the toy branch, no-op
    against every authored config), but that is a floor, not the fix.

    **The design fork to settle first:** the executor's own restore branch was written for a
    POSITIVE `GapWeight` — growth pulls the hole closed, the shrink puts it back — which yields
    a solid blade (`XScaler`/`YScaler` at `MaxSize 4` → `6 × 12 × 0.5` ≈ **36 volume**, a 48×
    jump). The asset authors `-1`, which inverts it into the runaway-open case. Whichever
    reading is intended, the volume change lands directly on **Cleave** and **Astro League**
    (both Rhino-only) and their `PhaseThresholds` would need re-deriving against the grown
    slab — see CLAUDE.md, "a cell whose prisms are not nominal must author its volume ladder".
    That is why this is its own branch and not a toy fix.

---

## ✅ CLOSED (claude/scarab-vessel-polish-k9mds6) — Scarab juke root-roll bank cancellation (opened by `claude/sparrow-spin-cooldown-p8agtv`)

`ScarabJukeController` is the structural twin of `BarrelRollController` — same perimeter trigger
(`stick.magnitude >= perimeterThreshold`), same `rollSign = stick.x >= 0 ? +1 : -1`, same visual
360° smoothstep, same `rootRollDegrees` (15) applied through `VesselTransformer.ApplyRotation`
about `transform.forward` — and it therefore carries the same defect the Sparrow branch fixed:

- **The bank cancels it.** `ScarabVesselTransformer.Roll()` is `-EasedLeftJoystickPosition.x × (…)`
  about the same axis, so the two rotations add and the trigger (a full stick deflection) is
  precisely when the bank is at maximum, pointing the other way. The camera reads the ROOT's up,
  so the pilot's horizon tilts against the juke rather than with it.
- **The roll is linear, the animation is smoothstep.** `rollSign * rootRollDegrees *
  (Time.deltaTime / jukeDurationSeconds)` drifts across the dash at a constant rate instead of
  easing with it, and summing `dt / duration` overshoots on the frame that ends the loop.

**The mechanism is already landed.** `VesselTransformer.BankIntoTurnSuppressed` is honoured in
`ScarabVesselTransformer.Roll()` as of that branch, so the fix is: set it around the juke routine
(clearing it in the tail AND in `OnDisable`), and advance the root roll by the delta of the
animation's own smoothstep. `BarrelRollController.RollRoutine` is the reference implementation.

**Deliberately NOT done on the Sparrow branch.** The Scarab is a different vessel with its own
play-tested feel, and removing its bank mid-juke is a change nobody has judged on screen. It wants
its own branch and its own playtest — a Scarab pilot should confirm the juke reads better, not
merely differently. Verify in **Scarab Scramble**: juke left and right, confirm the horizon tilts
the same way the model spins and that the dash still turns at full rate.

**CLOSED by the scarab-polish branch, exactly per the prescription above**: the owner path sets
`BankIntoTurnSuppressed` for the dash (cleared in the routine's tail AND `OnDisable`; the
replica's cosmetic roll passes a null transformer and never touches it), and the root bank
advances by the delta of the same smoothstep the spin uses. The playtest demanded above is still
owed — it is a numbered step in the branch's `UNITY_VERIFICATION_CHECKLIST.md` entry.

## Serpent fuel-pellet follow-ups (opened by `cece/epic-planck-1snjtc`)

Measured 2026-09-25 while restoring Solid Fuel Pellets (`R_VesselActions/SERPENT_FUEL_PELLETS.md`).
Logged, not acted on — none of these is the subject of that branch.

- **Dead legacy `VesselActions/ConsumeBoostAction.cs`** (`class ConsumeBoostAction : ShipAction`,
  guid `c8f865735b87b6a43a367be3280d8332`) — the old magazine version of this ability. **0** asset
  references to its guid across `.prefab`/`.asset`/`.unity`, **0** code references to the type.
  Salvage check before deleting: it carries nothing the new executor lacks. Proposal: delete it and
  its `.meta`.
- **Orphaned Seed Wall readout** — `SerpentVesselHUDView.shieldIcon` / `shieldIconsByCount` /
  `SetShieldCount` and the controller's shield-resource path now have no target on any shipped HUD
  (`shieldIcon` nulled on `Serpent.prefab` so the lockup would not resurrect the old art). Decide
  when the Mass slot is designed: delete, or re-home if the wall returns.
- **`ConsumeBoostActionExecutor.boostChanged` is unwired** on `Serpent.prefab`
  (`boostChanged: {fileID: 0}`), so `RaiseBoostChanged` is a no-op there. Either wire it (if a HUD
  wants the multiplier) or remove the field. Not changed: it was already unwired before the branch.
- **Time L5 is an open design slot** for the Serpent — needs design markup, nothing invented.

## Phase 5 — Element scaling unification (SHIPPED 2026-09-18)

Full record: **`ELEMENT_SCALING_UNIFICATION.md`**. The generic per-element multiplier
(`ElementalAbilityMapSO.MultiplierAtFullLevel` / `MinMultiplier` +
`R_VesselElementalAbilityHandler.Multiplier(Element)`) is REMOVED; all ten live multipliers moved to
an `ElementalFloat` on whatever owns the number, bit-identically. Two undeclared double-applications
of Time were fixed (Rhino ramp ceiling, Serpent boost speed) and four defensive `1.0` pins deleted.

| # | Item | Status |
|---|---|---|
| 5.1 | Retire the generic channel; migrate 10 call sites; author the 9 asset-hosted floats + 2 prefab floats | **SHIPPED** |
| 5.2 | Rhino/Serpent playtest — each loses an undeclared Time application (ramp ceiling ÷2.5, boost speed ÷1.6 at Time 10). Neither number was ever authored as a design | **NEEDS PLAYTEST** |
| 5.3 | **THREE `ElementalFloat`s were authored `Enabled` and never evaluated** — `GrowTrailActionSO.maxSize` (Mass 4→8), `GrowSkimmerActionSO.shrinkRate` (Charge 6→2) and `FullAutoActionSO.speedValue` (Space 375→4875), all read via `.Value` on a ScriptableObject, which nothing binds. Authored `Enabled: 0`; runtime behaviour byte-identical. Turning any of them ON is a BALANCE change and needs a design call | **SHIPPED (data honest; the ramps remain a design question)** |
| 5.3b | **`Tools/Build/check_elemental_floats.py`** — the standing gate. Fails on any ElementalFloat authored `Enabled` with `Min != Max` on a ScriptableObject nothing evaluates. Keyed on the asset's own `m_Script` guid (a field NAME is not an identity), states how many blocks it scanned, `--self-test` with four negative controls, proven against the real tree in both directions | **SHIPPED** |
| 5.4 | **Legacy bound path: the stated hazard was WRONG and is retracted.** `ElementalShipComponent.BindElementalFloats` reflects over MonoBehaviour fields ONLY, so no ScriptableObject-hosted ElementalFloat is ever bound and none can be dirtied — the vessel-contract rule-1 concern does not apply. What shipped instead: `ElementalFloatBinder` deleted (dead, and broken — it set a nonexistent `"Ship"` property and its "clone" dropped Min/Max/element/Enabled), `Skimmer`'s redundant bind removed (it reads live), `AOERadialBlocks.depthScale` deleted (unserialized, permanently 1, compounding on pool reuse) | **SHIPPED** |
| 5.4b | **The live surface was TWO fields, and one of them was not behaviour-neutral.** Measured against the shipped prefabs: FOUR of the six sit on components no prefab, scene or asset references (`ConsumeBoostAction.boostMultiplier`, `GrowActionBase.maxSize`/`shrinkRate`, `ZoomGrowRateDistributeAction.sharedRate`) — converting them is dead work, so they moved to 5.4c. `FullAutoAction.speed` is authored `Enabled: 0` on both Falcon and Shrike, so its conversion is a no-op by construction. `FireGunAction.ProjectileTime` (Urchin ×2 guns, `Enabled: 1`, 4 → 8 on **Space**) is the only field with live behaviour — and it is the field **`EnergizeAction` used as a WRITABLE channel** (`ProjectileTime.Value = x` on start, restore on stop), so converting the read alone would have made that write a no-op and silently switched off the Urchin's energize. The write got a channel of its own first: a FLOOR (`FireGunAction.RaiseOutputFloors` / `ClearOutputFloors`), composed as `Mathf.Max(element, floor)`. Identical at the authored numbers, and it removes three defects the write-and-restore shape carried (a level change mid-energize made `ScaleValueWithLevel` overwrite the raise and drop it; the restored "default" was captured from `fireActions[0]` and written to EVERY gun; that default was the pre-scaling authored `Value` 5, so a restore replaced the element-scaled lifetime with a constant until the next level event) | **SHIPPED** |
| 5.4c | **Five dead pre-`R_` `VesselActions/` components, proposed for deletion, not deleted.** `ConsumeBoostAction`, `GrowActionBase` + its two subclasses `GrowTrailAction`/`GrowSkimmerAction`, `ZoomGrowRateDistributeAction`, and `ToggleProjectileActionWrapper`. Evidence: a guid sweep of each script's `.meta` guid across all of `Assets` (excluding `.cs`/`.meta`) returns **zero** asset referrers for every one, the only C# references are within the dead cluster itself plus one `[Tooltip]` STRING in `SyncActionWrapper`, and every one has a live `R_VesselActions` successor. Not deleted here because `LAUNCH_BLOCKER_INDEX.md`'s salvage-before-delete gate is a human verdict and "referenced by nothing" means nothing is USING it, not that it contains nothing — `GrowActionBase` is the only `IScaleProvider` implementor `SyncActionWrapper`'s tooltip names, so check what that wrapper is for before removing its example. Note `ToggleProjectileActionWrapper` writes another action's public fields (`wrappedAction.Energy`, `wrappedAction.projectileTime`) — the same shape 5.4b just replaced with a floor, one class over | **OPEN — needs a human verdict** |
| 5.5 | **`ResourceSystem.GetLevel` is NOT off by one — REFUTED, measured.** The arithmetic is float32: `0.7f * 10` rounds to exactly `7f`, and crystal progression's `+= 0.1f` drifts upward, away from the boundary. All 26 cases land on their integer, verified by running real C#. Locked as a standing refutation in `ElementalScalingUnificationTests` | **CLOSED — not a defect** |
| 5.6 | `element_ability_table.py` could not see `ScarabBallForge.BallSizeScale` (a C# `static readonly`). The tool now reads C# field initializers, which also covers the rule-4-i case of an asset written before the field existed | **SHIPPED** |
| 5.7 | **Nine SO-hosted `ElementalFloat`s become plain `float`s — the type is the claim.** An ElementalFloat on a ScriptableObject read as `.Value` can never scale, so on these nine the type was a promise the build could not keep. `check_elemental_floats.py` gates the DANGEROUS case (an authored ramp that never runs) and structurally cannot gate a misleading type. Full row, including the two things this was wrong about, in `FLEET_GAPS.md` §3 | **SHIPPED** |
| 5.8 | **`element_ability_table.py` was wrong about SEVEN of its twelve reported gaps.** Fixed: comment/string blanking in `follow_static_calls` (a doc comment hung a Scarab gate on three hulls); namespace-qualified `Element.X` args (the Manta's two L5 gates read as unimplemented); nested-prefab-instance `m_Modifications` floats (six of twelve vessel prefabs invisible); C# field initializers; and `guard_state` reading a serialized bool's C# default (an unauthored `false` counted as a live gate on ten of twelve hulls). 12 disagreements → 5 | **SHIPPED** |
| 5.9 | **The Scarab's map declared a retired upgrade.** "Armored Switch" was retired with the switch's prism fill on 2026-08-24; the map went on naming it, so every surface that asks the map reported a wired Mass 5. Entry corrected to an `(open design slot)` that records the retirement, and its `AbilityDescription` corrected (Mass scales the RING RADIUS, not a fill that no longer exists) | **SHIPPED** |
| 5.10 | The remaining gaps are DESIGN gaps, not wiring. Five when this row was written; **three** after the scope + rifle branch merged and filled the Serpent's Charge and Space: **Rhino Charge + Space, Serpent Mass**. Report: **`FLEET_GAPS.md`** — and re-run `element_ability_table.py --gaps` rather than trusting either count | **OPEN — design** |
| 5.11a | **A shared `ShipActionSO` mutated its own serialized field at runtime — and the dangerous copy was dead code.** `GrowSkimmerActionSO.ApplyMaxSizeDebuff` writes `maxSize.Value = original * multiplier`, awaits, then writes it back — on a SHARED asset, so in multiplayer two Rhinos debuffed at overlapping times race on one number and the second restore writes the FIRST one's already-multiplied value back as "original". This is the exact last-initializer-wins hazard `ARCHITECTURE.md §2(a)` and the vessel contract both name as their cautionary tale, and it predates this branch — found by D1 while proving the `Enabled: 0` flip on that same field is a no-op. Measured: **nothing calls it.** There are TWO methods by that name — the live caller (`VesselChangeSkimmerSizeByProjectileEffectSO`) holds a `ShieldSkimmerScaleConfigSO`, a different class whose version writes a private runtime `_maxScaleMultiplier` and never touches a serialized field. The uncalled one is deleted, which also unblocked 5.7's `maxSize`. **Sequel (Sep 2026):** the live one is deleted too, with the control-theft tier (`Docs/ELEMENTAL_ECONOMY.md §9`) — and it shared the hazard as well as the name, since ONE `ShieldSkimmerScaleConfig.asset` drives every Rhino, so writing runtime state on it still let one hit shrink every Rhino's blade. Runtime state made it safer, not safe | **SHIPPED** |
| 5.11b | **CLOSED by the control-theft tier (Sep 2026).** What survived 5.11a was `ShieldSkimmerScaleConfigSO`'s own debuff latch and multiplier on the SHARED asset, so two Rhinos shared one debuff and the second press was swallowed by `if (_isMaxSizeDebuffed) return`. Its only caller — `VesselChangeSkimmerSizeBySparrowFullAutoProjectileEffect` — is deleted with the control tier (`Docs/ELEMENTAL_ECONOMY.md §9`), so `ApplyMaxSizeDebuff`, `_isMaxSizeDebuffed` and `_maxScaleMultiplier` are gone and `MaxScale`/`PrismMaxScale` are the authored values. The playtest this row asked for is moot: the answer to *"does the debuff change anything on screen"* is now **nothing does** | **CLOSED — the mechanic was removed rather than fixed** |

## Rhino follow-ups (opened by `cece/serene-goodall-338ctt`)

- **Rhino has no `VesselChangeSpeedByPrismEffectSO` in its prism container** (CLAUDE.md's danger
  prism section already names it the open item with Serpent). The new sword BIND slows ROTATION
  inside super-shielded mass and deliberately touches nothing about speed; a hull ram into ordinary
  mass still costs the Rhino no speed. Measurement: grep `Rhino` vessel impactor container for the
  effect type — zero entries.
- **Pre-retune tables survive as history in `RHINO_RAMP_BOOST.md` and `HEADLONG.md`.** Both carry a
  "RETUNED 2026-09-25" callout above the old curve/ladder tables rather than rewriting them; the old
  rows are labelled but a reader skimming a table can still quote a 1200 u/s top speed. Rewrite the
  tables in place once the retune is play-tested and the old numbers stop being a useful A/B.
- **The sword bind is inert on the legacy box path** — it reads `PrismShellContactManager`'s live
  pair set, so under `ForceLegacyBoxInteraction` the Rhino gets the entry beat (jiggle + thud) and
  no sustained drag/grind. Acceptable while the shell tier is the shipped path; revisit if that
  flag is ever flipped on in a build.

## Elemental economy follow-ups (opened by `cece/sweet-planck-1apw8u`)

Logged, not acted on — each carries the measurement, per `/refactor`'s rule that a row with no
measurement attached is worse than no row.

- **`SlowExplosionImpactorDataContainer` is now EMPTY, so three abilities have no vessel-facing
  effect.** Measured: `vesselExplosionEffects: []` and `explosionPrismEffects: []`, referenced by
  `AOESlowExplosion.prefab` (the Rhino's sword crystal burst + the Rhino's vessel crystal blast)
  and `AOEShieldedRingSpawner.prefab` (the Squirrel's vessel crystal blast). It held exactly one
  effect (`VesselChangeSpeedByExplosionEffect`, an input mute) and that effect broke the
  control-theft law, so emptying it was correct — but a blast that reaches a pilot and does nothing
  is a hole, not a neutral outcome. The sanctioned filling is a Debuff-class
  `VesselCombatHitByExplosionEffectSO` plus a drain priced through
  `Tools/Build/author_combat_debuff_magnitudes.py`, which is a Broadside **pricing** decision, not
  a wiring change. The container and both prefabs are deliberately KEPT: a dangling container
  reference is worse than an empty one.
- **`ScriptableEventSkimmerDebuffApplied` / `SkimmerDebuffPayload` have no producer.** Measured: the
  only raiser was `VesselDamageBySkimmerEffectSO` (deleted); the only consumer is
  `RhinoVesselHUDController.ShowDebuffTimer`, whose readout is ALREADY dark for an unrelated reason
  (the ability lockup's retire sweep switched the Rhino's whole root-level status cluster off —
  `Docs/ABILITY_LOCKUP.md`). Kept as a wire rather than deleted: it is the generic *"a skimmer
  debuffed you"* vehicle and the next Debuff-class skimmer effect is its producer. Deciding between
  "wire the next producer" and "delete channel + handler + payload" is a design call, not a cleanup.
- **`ShieldSkimmerScaleConfigSO.prismMaxScale` / `PrismMaxScale` have no reader.** Measured: a
  project-wide grep finds the `[SerializeField]`, the accessor, and nothing else (the unrelated
  `BreakwaterStationBuilder.PrismMaxScale` is a different constant). Its only consumer was the
  retired max-size debuff. Kept as serialized data rather than dropped inside a removal commit, so
  the value is recoverable if the blade ever regains a prism-growth cap; delete it in a pass that
  can also drop the key from `ShieldSkimmerScaleConfig.asset`.
- **Six of `Rhino.prefab`'s eight `SkimmerImpactor` overrides are INERT and one of them is mine.**
  Measured on the merged tree: that instance overrides `skimmerImpactorDataContainer` (the ONLY
  field the script still declares), plus `skimmerPrismEffectsSO.Array.size`/`data[0..2]`,
  `vesselSkimmerEffectsSO.Array.size`/`data[0]` and `skimmerPrismStayEffectsSO.Array.size` — and all
  four of those field names are COMMENTED OUT in `SkimmerImpactor.cs` (three inline effect lists
  plus a `// TODO -> Add to the container` stay-list). This branch removed the three entries that
  pointed at a deleted effect; the remaining six are the same class and still read as wiring. Note
  the surviving `vesselSkimmerEffectsSO.data[0]` points at the haptics effect, which IS live — via
  the CONTAINER, not via this override. Removing them is a prefab-YAML edit with no behaviour to
  change, and the real fix is to finish the container migration the comments describe.
- **Two mode generators are red and were red before this branch** — proven by running both at
  `origin/bleeding-edge`: `author_dogfight_assets.py --check` fails its asset-key validation on
  `CallToActionTargetType` (a field the call-to-action retirement deleted from `SO_ArcadeGame`, so
  re-running it would re-introduce a retired key), and `author_wildlife_liberation_assets.py`
  aborts on the spent one-shot `controller field block not found in donor scene`. Both are already
  recorded in CLAUDE.md as part of the six-red-generator finding. This branch touched both files
  (a comment correction; and re-pointing the `Runtime Cell Data.asset` anchor off the removed
  `OnFaunaHeartsChanged` onto `OnFaunaWaveSpawned`, which the abort still runs past) and does not
  widen the failure.
- **`check_using_directives.py` reports a false positive on a member named `Element`.**
  `LifeformHeartSizeTests.cs` declares `public int Element;` in a nested struct and the gate reads
  it as an unqualified use of `CosmicShore.Data.Element`. Proven pre-existing: the identifiers are
  byte-identical at the base tip and the file compiles today, so a genuine missing `using` would be
  a standing editor error. It surfaced only because a one-line prose edit pulled the file into the
  gate's changed-file scope. This is the false-positive class CLAUDE.md already documents for that
  gate (`Key`, `Direction`, `Frame`, `Stats`); the fix is to make the gate skip identifiers in a
  declarator's NAME position, not to add a `using` that nothing needs.

## From the Squirrel omni-crystal card branch (2026-09-26)

- **The three sibling `*SignalColor` accessors still hand a LINEAR value to GAMMA consumers.**
  `GetShieldedSignalColor` was corrected this branch (`Docs/PALETTE.md §2.9`); its three siblings
  were deliberately left alone and are a real, measured exposure. Counts and measurements, so the
  next pass starts from evidence rather than from this row:
  `GetDomainSignalColor` **16** call sites, `GetDangerSignalColor` **5**, `GetCtaSignalColor` **4**
  (`grep -rl <name> Assets/_Scripts/`). The error's SIZE scales with how far apart a colour's
  channels are, which is why it was invisible on the siblings and 7° on the one that broke: Jade's
  shielded base face shifts **210.3° vs 217.3°** under the conversion, while Jade's
  `TrailHighlightColor` shifts **176.6° vs 176.4°** — 0.2°, i.e. nothing. **Not a blanket fix.**
  These accessors' job is an unmistakable SIGNAL rather than a match to something in the world, and
  their shipped appearance was judged by eye; converting them would move the Echo Sight, the vessel
  vision band and every domain-tinted HUD slot at once. The work is per-consumer: for each of the
  25, decide whether it is depicting WORLD MASS (wants the conversion) or naming a TEAM (may keep
  the normalised signal, and should say so). Note `GetCtaSignalColor` and `GetDangerSignalColor`
  take no domain, so they are 9 sites of a single decision each.
  *Shape (`/refactor` §3): a read whose writer is elsewhere — the space a value is in is decided by
  the consumer, and nothing in the accessor's signature says which one it is written for.*

- **Seven of eight vessels render a LOCKED omni crystal card.** This is **INCOMPLETENESS, not
  debt** — filing it as cleanup would invite somebody to invent eight icons. Measured: exactly
  **1** HUD variant authors `omniAbilitySprite` (the Squirrel's shielded ring); the other seven
  draw the shared emblem above a locked plate, which is the honest state and what a locked card is
  for. What a crystal DOES is a property of the hull, so each icon is a design decision for that
  vessel, and the owner has said they will take a pass. No action until then.

## From the Dolphin omni-tally branch (`cece/tender-brown-eu5bdr`, 2026-09-26)

- **Correction to the row above: SIX of eight vessels now render a LOCKED omni card, not seven.**
  The Dolphin's omni card is GENERATED (its blast's prism tally, centred, held until the next
  blast) by `DolphinVesselHUDView.EnsureGeneratedAbilityIcons`, which binds an invisible anchor
  icon so the card is not locked. The "exactly 1 HUD variant authors `omniAbilitySprite`" count
  still holds — the Dolphin authors none; it generates.

- **Latent orphan if the Dolphin ever authors `omniAbilitySprite`.** DEBT this branch CREATED.
  `AbilityLockupView.Build` calls `EnsureGeneratedAbilityIcons()` before `EnsureOmniCrystalCard()`,
  so the tally binds `CoreAbility.OmniCrystal` first; `BindCoreAbilityIcon` then returns on the
  second bind (`if (coreAbilities[i].icon) return;`, `VesselHUDView.cs:426`) — but
  `EnsureOmniCrystalCard` has ALREADY built and painted its `OmniCrystalHost/OmniCrystalIcon`
  (`VesselHUDView.cs:368-382`) before asking. Result: a visible, un-placed sprite at the view root.
  Not reachable today (`grep -n omniAbilitySprite` over `DolphinHUDVariant.prefab` and the Dolphin vessel prefabs → no key, so the field is its C# default, null).
  Fix when it becomes reachable: have `EnsureOmniCrystalCard` return early when an OmniCrystal
  binding already carries an icon, BEFORE building its host.
  *Shape (`/refactor` §3): two producers for one slot, arbitrated by call order.*

- **`VesselAbilityRowWirer` still authors `BlastCount` under the Space jaw container**
  (`Assets/_Scripts/Editor/VesselAbilityRowWirer.cs:264`, `:376`). Harmless — the view re-homes the
  text onto the omni card at runtime, keeping its font and material — but the authored position is
  now a lie a reader will believe. Either author it under a `BlastTallyButton` host at the view
  root, or leave it and say so in the wirer's comment. No prefab was edited on this branch.
