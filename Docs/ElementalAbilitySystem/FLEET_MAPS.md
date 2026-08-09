# Fleet Elemental Ability Maps — status + level-5 upgrade proposals

**Status of this doc:** the quantitative layer and the flower display are LIVE fleet-wide
(see §1). The level-5 qualitative upgrades for the non-Sparrow vessels are **PROPOSALS for
Garrett to mark up** — none are implemented. Approve/edit per row; implementation follows the
Sparrow pattern (per-shot/per-use snapshot, gated on `IsUpgradeActive(element)` in the
executor, replicated unlock bits, no new fundamentals).

## 1. What is live on every vessel now

- **Display (required, structural):** any vessel without an authored `ElementalBarsView`
  auto-creates one on its HUD canvas (`ElementalBarsController.CreateDefaultElementBars`), and a
  view with no authored bindings self-populates the standard four flowers
  (`ElementalBarsView.Build`). Placement is stamped fleet-wide from `ElementalBarsConfig`
  (the Squirrel's reference spot). A vessel literally cannot ship without the display.
- **Level economy (vessel-agnostic):** element levels rise by collecting the elemental
  crystals dropped by lifeforms (the adjust effect rides the crystal side), plus comeback
  bonuses. No per-vessel wiring involved.
- **Comeback (REQUIRED in every party game):** `ElementalComebackSystem` is auto-created by
  `MultiplayerMiniGameControllerBase` when a scene lacks one. ALL FOUR elements rise EQUALLY
  by `deficit × SO_ArcadeGame.ComebackRatePerScoreDeficit` (the per-game dial, synced to
  clients via `GameDataSO`; deficit = first-place team aggregate minus yours in the mode's
  scoring stat). The comeback layer can never lift an element above level 10
  (`ResourceSystem.ComebackCeiling`) — earned progression alone reaches the overcharge band.
  The old per-vessel/per-element profile weights are retired; the profile only seeds
  optional initial levels.
- **Quantitative scaling (map-driven):** every executor call site now reads
  `ElementalAbilityHandler.Multiplier(element)`, tuned by the vessel's
  `Resources/ElementalAbilityMaps/{Vessel}.asset`. The former hardcoded `atFull` literals
  moved into the maps at identical values — feel unchanged.

| Vessel | Live quantitative entries (map value) |
|---|---|
| Sparrow | Space→gun range (2.5) · Time→boost speed (1.5, now on an **indefinite** boost — see §2 Sparrow) · Mass→turret prism stretch (2.5) · Charge→skyburst blast (asset range 100→170) |
| Manta | Charge→overcharge detonation blast (1.75) · Mass→overcharge harvest capacity (1.75) · Space→Yawstery turn rate (1.6) |
| Dolphin | Charge→charge-boost peak (1.5) · Time→charge fill rate (1.5) |
| Rhino | Mass→trail slab max size (1.5) |
| Serpent | Time→boost duration (1.6) |
| Squirrel | **All four LIVE (approved + shipped, see §2 Squirrel)**: Charge→skim energy per prism hit (map 2.0, read in `SkimmerBoostPrismEffectSO`) · Mass→trail prism VOLUME (authored `trailVolume` ElementalFloat 1→2.5 on `VesselPrismController`, cube-root per axis) · Space→skimmer reach (authored skimmer `Scale` ElementalFloat 15→30) · Time→boost-ring cooldown (authored `cooldownMultiplierAtFullTime` 0.5 on `SquirrelTubeActionSO`; the generic map Time multiplier stays 1.0 because `VesselTransformer` consumes it for boost speed). The former Time→top speed mapping was REMOVED (prefab `ThrottleScalerMultiplier` disabled) — one parameter per element. |

## 2. Level-5 upgrade proposals (NOT implemented — mark up)

Ground rules used: reuse existing primitives (regular shield, piercing/stop-on-impact,
domain-sparing, steal, danger, the roll) — never SuperShield (no food-web sink), never
timers/decay, gate strictly in the acting system's layer.

### Sparrow — shooter (guns + turret + rockets + afterburner) — SHIPPED

The Sparrow's map has been live since the system landed; only its **TIME** row changed in the
boost redesign (2026-08). Mechanics detail, tuning knobs and the in-editor verification table live
beside the code: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` (TIME) and
`SPARROW_TURRET_STANCE.md` (MASS).

| Element | Quantitative (LIVE) | L5 upgrade (LIVE) |
|---|---|---|
| Charge | skyburst blast radius (authored on the skyburst effect assets, 100→170) | **Domain-Safe Skybursts** — explosions spare your own domain's prisms |
| Mass | turret-fired prism stretch (2.5) | **Shielded Prisms** — turret prisms arrive shielded |
| Space | gun range (2.5) | **Piercing Bullets** — bullets pierce prisms instead of stopping at first impact |
| Time | boost SPEED (1.5), consumed by `VesselTransformer.CurrentBoostAmount()` | **Elemental Ward** — while boosting, negative `ApplyElementalEffect` calls are dropped (`ResourceSystem.IsElementallyImmune`) |

**TIME row, changed 2026-08 — do not restore the old design:**

- **Overheat is removed.** The heat resource, `OverheatingActionSO`, `OverheatingActionExecutor`,
  the legacy `OverheatingAction`, and `VesselStatus.IsOverheating` are all deleted; input 7 binds
  straight to the shared `BoostAction.asset`. The boost is now unlimited in duration.
- **The strafing roll dropped to BASE kit** (was the TIME-5 upgrade). `BarrelRollController` lost
  its `IsUpgradeActive(Element.Time)` gate. Still one roll per boost press.
- **The roll also works in the stationary stance** (2026-08, a later branch). It lost its
  `IsTranslationRestricted` gate too: stopped, the boost gives no speed but the roll still arms on
  the press and still strafes — the stopped Sparrow's dodge. The displacement survives the
  restriction through a narrow per-modifier opt-in
  (`ShipVelocityModifier.ignoresTranslationRestriction`, default false; only the roll sets it), and
  the same stance triples pitch/yaw (`VesselTransformer.restrictedTurnMultiplier`). Neither touches
  the element map. Detail: `R_VesselActions/SPARROW_AFTERBURNER.md` §2.1–2.2.
- **TIME-5 is now Elemental Ward**, and the immunity behind it is a **platform state, not a Sparrow
  feature** — `ResourceSystem.SetElementalDebuffImmunity` / `IVesselStatus.IsElementallyImmune`,
  driven declaratively by the shared `VesselElementalImmunity` component. The **Serpent holds the
  same state while stopped, ungated** (`WhileTranslationRestricted`). Any vessel or mode can hold
  it; grants are source-keyed so holders can't clear each other.
- **The danger-trail machinery survives.** `VesselPrismController.EnableDangerMode` /
  `DisableDangerMode` lost their only caller with the overheat executor. Keep them — the Serpent's
  proposed "Venom Wake" below is exactly that machinery reused.

**MASS row, clarified 2026-08 — the element map is unchanged, the stance beneath it is not:**

The turret stance is now defined as *"a turret shot IS a bullet — you just see a prism flying, and
where the bullet would have been destroyed the prism stays"*. That parity is structural:
`FullAutoBlockShootActionSO` holds a reference to `FullAutoActionSO` and **adopts** its fire rate,
muzzle speed (SPACE-scaled, via the shared `FullAutoActionSO.ResolveSpeed`) and flight time rather
than authoring its own. It had drifted to 14 shots/s at 150 u/s against guns at 30 shots/s at
1500 u/s. **Pierce is the bullets' SPACE-5 gate, on both modes** — below it the shot stops at the
first prism it hits and anchors there, at 5+ it pierces to the end of its path and anchors there;
piercing is not a turret perk. Turret shots also run the bullets' own `ProjectileDamagePrismEffect`;
the self-destroying `DomainCheckProjectilePrismHitEffectSO` that used to sit on that path is deleted.

Two things worth knowing beyond the element map. First, the stance had been firing **invisible**
prisms: the path never called `Prism.Initialize`, so `IsCreationComplete` stayed false,
`BeginGrowthAnimation` early-returned, and every shot lived at `localScale` zero — no visual, and a
zero-volume collider that could not register a hit. Second, the flight is now GPU-side
(`Docs/PRISM_ANIMATION.md` §5 C5 — SHIPPED): the prism is stamped at its end point and the vertex
stage walks it in from the muzzle, while the prism's *carried* `Projectile` does the travelling and
the colliding. MASS itself is untouched: quantitative stretch on the prism's long axis, L5
*Shielded Prisms* — now applied as a pre-`Initialize` flag so the shield is part of the prism's
birth rather than a morph on arrival. Budget note: the cadence fix roughly doubles anchored mass to
~60 prisms/s while held. Detail: `R_VesselActions/SPARROW_TURRET_STANCE.md`.

### Manta — "Reaper Ray" (skim + harvest)

| Element | Quantitative (live) | Proposed L5 upgrade |
|---|---|---|
| Charge | overcharge detonation blast | **Domain-Safe Detonation** — the overcharge blast spares own-domain prisms (direct reuse of the Sparrow CHARGE-5 shape) |
| Mass | harvest capacity | **Deep Harvest** — overcharge collection also pops+collects *shielded* prisms (today the shield blocks collection) |
| Space | Yawstery turn rate | **Wide Wake** — near-field skimmer size class up while overcharged (reach/presence) |
| Time | *(open)* → propose: overcharge decay rate | **Held Charge** — overcharge no longer bleeds between skims (still spent on detonation) |

### Dolphin — "Darts" (charge and release) — APPROVED + SHIPPED

The proposal table below was superseded by Garrett's design; the shipped map is
`Assets/Resources/ElementalAbilityMaps/Dolphin.asset`. **The asset is the record — do not
re-litigate from the superseded proposal.**

Mechanics detail (energy economy, drift boost, the four gauges, the skimmer traps, and the
in-editor verification table) lives beside the code:
`_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

The Dolphin's spine is an ENERGY economy: skimming banks energy, hitting a prism halves it,
and hitting a crystal spends it ALL at once to release a blast. Energy sets the blast's GAPE,
and the hull's jaws open to that same angle so the blast is readable before it fires.

The blast's destruction volume is a **capsule sweep**, not a circular cone: its cross-section is
a stadium whose radius is fixed (the width across the beam) and whose LENGTH is what energy
buys, extended along the very axis the jaws open across. So a charged blast is a **fan** — wide
in the jaw plane (4.76° → 23.43°), narrow across it (3.81°) — and the hull's silhouette is
literally the blast's silhouette in that plane, at every charge. Geometry, numbers and the
exact jaw-angle curve: `DOLPHIN_ENERGY_ECONOMY.md` §1 and §3.

| Element | Quantitative (LIVE) | L5 upgrade (LIVE) |
|---|---|---|
| Charge | team-crystal recharge ×0.5 at level 10 (`DeployTeamCrystalActionSO.cooldownMultiplierAtFullCharge`, floored by `minCooldown`) | **Twin Seed** — carry TWO team crystals instead of one (`upgradedCharges`), so two can be planted back to back |
| Mass | drift prism VOLUME (`trailVolume` ElementalFloat 1→2.5 on `VesselPrismController`, cube-root per axis) | **Hard Wake** — drift prisms arrive shielded, gated on `IsDrifting` (`massUpgradeShieldsTrail`, the Squirrel's Heavy Trail machinery) |
| Space | crystal-impact blast SIZE ×2 at level 10 (`VesselExplosionByCrystalEffectSO._heightMultiplierAtFullSpace`). Scales the blast **self-similarly** — reach, capsule length AND capsule diameter together — because the angles ARE those over height, and energy owns the gape | **Clean Blast** — the blast spares the pilot's own domain (`_spaceUpgradeSparesAllies` → `InitializeStruct.AffectSelfOverride`). Below the unlock the cone is indiscriminate, which is what makes sparing allies worth earning |
| Time | boost charge RATE while drifting ×1.5 at level 10 (`ChargeBoostActionSO.chargeRateMultiplierAtFullTime`) | **Live Current** — skimming a DANGER prism grants 3× energy (`SkimmerChangeResourceByPrismEffectSO._dangerBonusElement/_dangerBonusMultiplier`; the Squirrel's Live Wire shape — the risk was always there, the reward is now earned) |

All four map `MultiplierAtFullLevel` are pinned to **1** — every scaling above is authored on its
own SO field. That is not cosmetic: `ChargeBoostActionExecutor` was already consuming the generic
Charge multiplier for the boost peak and the generic Time multiplier for the charge rate, while
`VesselTransformer` consumes generic Time for boost SPEED. Reading the map's generic multiplier for
the new abilities would have driven two unrelated parameters off one element. The boost peak is now
flat (Charge was reassigned to crystal seeding); give it its own element + field if it should scale
again.

Superseded proposal (kept for the record): Charge→charge-boost peak / "Shockwave Release",
Mass→trail scale while discharging / "Solid Wake", Space→skimmer scale / "Slipstream",
Time→charge fill rate / "Instant Draw".

### Rhino — "Bulldozer" (slabs + forcefield + ram)

| Element | Quantitative (live) | Proposed L5 upgrade |
|---|---|---|
| Charge | *(open)* → propose: forcefield shrink rate (the authored-but-dead `GrowSkimmerAction.shrinkRate` Charge mapping, 6→2) | **Unyielding Field** — forcefield no longer shrinks on prism hits, only on crystal timeout |
| Mass | trail slab max size | **Armored Slabs** — grown slabs arrive shielded (Sparrow MASS-5 shape) |
| Space | *(open)* → propose: forcefield max size | **Breaker** — ramming destroys shielded prisms in one hit (devastate on ram) |
| Time | *(open)* → propose: slab growth rate | **Fast Pour** — slab growth continues while boosting |

### Serpent — "Wall-Weaver" (boost + wall)

| Element | Quantitative (live) | Proposed L5 upgrade |
|---|---|---|
| Charge | *(open)* → propose: boost stack potency | **Venom Wake** — boost trail becomes a danger trail for the boost duration (reuses `VesselPrismController.EnableDangerMode`, now caller-less since the Sparrow's overheat was removed; dangerous to everyone incl. self, per the locked law) |
| Mass | *(open)* → propose: wall prism scale | **Fortified Wall** — woven wall prisms arrive shielded |
| Space | *(open)* → propose: skimmer scale | **Coil Reach** — skim energy from own wall at double rate |
| Time | boost duration | **Endless Coil** — consuming a boost charge while boosting chains without the reload pause |

### Squirrel — racer (drift + tube) — APPROVED + SHIPPED

The original proposal table below was superseded by Garrett's markup; the shipped design:

| Element | Quantitative (LIVE) | L5 upgrade (LIVE) |
|---|---|---|
| Charge | skim energy per prism-skimmer collision (map 2.0, `SkimmerBoostPrismEffectSO`) | **Live Wire** — danger prisms grant the 10× energy bonus (the bonus was always-on before; it is now EARNED — below Charge 5 danger prisms pay base energy) |
| Mass | trail prism VOLUME (`trailVolume` ElementalFloat 1→2.5, cube-root per axis) | **Heavy Trail** — trail prisms arrive shielded ONLY while drifting (`massUpgradeShieldsTrail` + `IsDrifting` gate on `VesselPrismController`) |
| Space | skimmer reach (skimmer `Scale` ElementalFloat 15→30 — this mapping predates the doc and was restored to the record). BASE joust (ungated): jousting any lifeform's embedded crystal while moving FASTER than it withers opposing-domain lifeforms (`ILifeFormEntity.Jousted`; rooted flora sit at speed 0 so they're trivially joustable) | **Shepherd** — jousting an OWN-domain lifeform's crystal levels it up (`ILifeFormEntity.LevelUp`, the lifeform elemental contract — see `Docs/ECOSYSTEM.md §3`) |
| Time | boost-ring cooldown ×0.5 at level 10 (`SquirrelTubeActionSO.cooldownMultiplierAtFullTime`) | **Twin Rings** — the tube deploys a second ring (baseline reduced 2→1 ring; `upgradeExtraRings`) |

Removed: Time→top speed (prefab `ThrottleScalerMultiplier` disabled — one parameter per element).
HUD: the shared upgrade-highlight system (`VesselHUDView.abilityIcons` + base
`VesselHUDController` subscribing `OnUpgradeStateChanged`) is wired on the Squirrel's four
icons (boost gauge / drift / impact / tube); other vessels adopt by filling their view's
`abilityIcons` bindings — no code.

## 3. Implementation notes for approved rows

- Executor-side gate on `IsUpgradeActive(element)` at use time; per-use snapshot; AI gets it
  free through the same executors.
- Shield grants: regular shield only, flag-before-`Initialize` or `ActivateShield()` at rest.
- Domain-sparing: explosion/collection layer only — never `Prism.Damage`, never danger effects.
- Fill in each map's `UpgradeLabel`/`UpgradeDescription` when a row is approved; the HUD reads
  the map.
- The `Input` fields in the non-Sparrow maps are 0 (unset) — fill during HUD icon work.
