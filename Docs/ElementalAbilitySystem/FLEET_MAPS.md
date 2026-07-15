# Fleet Elemental Ability Maps — status + level-5 upgrade proposals

**Status of this doc:** the quantitative layer and the flower display are LIVE fleet-wide
(see §1). The level-5 qualitative upgrades for the non-Sparrow vessels are **PROPOSALS for
Garrett to mark up** — none are implemented. Approve/edit per row; implementation follows the
Sparrow pattern (per-shot/per-use snapshot, gated on `IsUpgradeActive(element)` in the
executor, replicated unlock bits, no new fundamentals).

## 1. What is live on every vessel now

- **Display (required, structural):** any vessel without an authored `ElementalBarsView`
  auto-creates one on its HUD canvas (`SilhouetteController.CreateDefaultElementBars`), and a
  view with no authored bindings self-populates the standard four flowers
  (`ElementalBarsView.Build`). Placement is stamped fleet-wide from `ElementalBarsConfig`
  (the Squirrel's reference spot). A vessel literally cannot ship without the display.
- **Level economy (vessel-agnostic):** element levels rise by collecting the elemental
  crystals dropped by lifeforms (the adjust effect rides the crystal side), plus comeback
  bonuses. No per-vessel wiring involved.
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
| Squirrel | Time→top speed (authored `ThrottleScalerMultiplier` 1→2.5 on the transformer, now evaluated LIVE via `ElementalFloat.EvaluateLive` — the unified read for per-vessel component floats; generic map multipliers stay 1.0 to avoid double-dipping) |

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

### Squirrel — racer (drift + tube)

| Element | Quantitative (proposed) | Proposed L5 upgrade |
|---|---|---|
| Charge | danger-ring potency | **Ring Master** — danger ring also grants the 10× skim bonus to the owner (risk/reward symmetric) |
| Mass | trail prism volume | **Heavy Trail** — drift trail prisms arrive shielded |
| Space | drift trail width (xShift/gap) | **Wide Line** — double trail while drifting |
| Time | top speed (LIVE — see §1) | **Barrel Roll** — direct reuse of the Sparrow TIME-5 controller (it is vessel-agnostic) |

## 3. Implementation notes for approved rows

- Executor-side gate on `IsUpgradeActive(element)` at use time; per-use snapshot; AI gets it
  free through the same executors.
- Shield grants: regular shield only, flag-before-`Initialize` or `ActivateShield()` at rest.
- Domain-sparing: explosion/collection layer only — never `Prism.Damage`, never danger effects.
- Fill in each map's `UpgradeLabel`/`UpgradeDescription` when a row is approved; the HUD reads
  the map.
- The `Input` fields in the non-Sparrow maps are 0 (unset) — fill during HUD icon work.
