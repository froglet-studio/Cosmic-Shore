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
| Sparrow | Space→gun range (2.5) · Time→boost speed (1.5) · Mass→turret prism stretch (2.5) · Charge→skyburst blast (asset range 100→170) |
| Manta | Charge→overcharge detonation blast (1.75) · Mass→overcharge harvest capacity (1.75) · Space→Yawstery turn rate (1.6) |
| Dolphin | Charge→charge-boost peak (1.5) · Time→charge fill rate (1.5) |
| Rhino | Mass→trail slab max size (1.5) |
| Serpent | Time→boost duration (1.6) |
| Squirrel | **All four LIVE (approved + shipped, see §2 Squirrel)**: Charge→skim energy per prism hit (map 2.0, read in `SkimmerBoostPrismEffectSO`) · Mass→trail prism VOLUME (authored `trailVolume` ElementalFloat 1→2.5 on `VesselPrismController`, cube-root per axis) · Space→skimmer reach (authored skimmer `Scale` ElementalFloat 15→30) · Time→boost-ring cooldown (authored `cooldownMultiplierAtFullTime` 0.5 on `SquirrelTubeActionSO`; the generic map Time multiplier stays 1.0 because `VesselTransformer` consumes it for boost speed). The former Time→top speed mapping was REMOVED (prefab `ThrottleScalerMultiplier` disabled) — one parameter per element. |

## 2. Level-5 upgrade proposals (NOT implemented — mark up)

Ground rules used: reuse existing primitives (regular shield, piercing/stop-on-impact,
domain-sparing, steal, danger, the roll) — never SuperShield (no food-web sink), never
timers/decay, gate strictly in the acting system's layer.

### Manta — "Reaper Ray" (skim + harvest)

| Element | Quantitative (live) | Proposed L5 upgrade |
|---|---|---|
| Charge | overcharge detonation blast | **Domain-Safe Detonation** — the overcharge blast spares own-domain prisms (direct reuse of the Sparrow CHARGE-5 shape) |
| Mass | harvest capacity | **Deep Harvest** — overcharge collection also pops+collects *shielded* prisms (today the shield blocks collection) |
| Space | Yawstery turn rate | **Wide Wake** — near-field skimmer size class up while overcharged (reach/presence) |
| Time | *(open)* → propose: overcharge decay rate | **Held Charge** — overcharge no longer bleeds between skims (still spent on detonation) |

### Dolphin — "Darts" (charge and release)

| Element | Quantitative (live) | Proposed L5 upgrade |
|---|---|---|
| Charge | charge-boost peak | **Shockwave Release** — a full-charge release emits the spherical AOE explosion at the release point (reuses the skyburst spherical prefab, charge-scaled) |
| Mass | *(open)* → propose: trail prism scale while discharging | **Solid Wake** — discharge trail arrives shielded (Sparrow MASS-5 shape) |
| Space | *(open)* → propose: skimmer scale | **Slipstream** — skim energy multiplier vs any trail while discharging |
| Time | charge fill rate | **Instant Draw** — collecting any crystal completes the current charge instantly |

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
| Charge | *(open)* → propose: boost stack potency | **Venom Wake** — boost trail becomes a danger trail for the boost duration (reuses the overheat danger-trail machinery; dangerous to everyone incl. self, per the locked law) |
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
