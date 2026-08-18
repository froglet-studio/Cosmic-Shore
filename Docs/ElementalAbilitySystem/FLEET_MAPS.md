# Fleet Elemental Ability Maps — status + level-5 upgrade proposals

**Status of this doc:** the quantitative layer and the flower display are LIVE fleet-wide
(see §1). Four vessels have an **APPROVED + SHIPPED** map with all four level-5 upgrades
implemented — **Sparrow, Dolphin, Squirrel, Urchin**; their rows below are the record, not a
proposal, and are not to be re-litigated from the superseded tables kept beside them. The level-5
upgrades for **Manta, Rhino and Serpent** are still **PROPOSALS for Garrett to mark up** — none
are implemented. Approve/edit per row; implementation follows the Sparrow pattern (per-shot/per-use
snapshot, gated on `IsUpgradeActive(element)` in the executor, replicated unlock bits, no new
fundamentals).

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
| Sparrow | Space→gun range (9.0) · Time→boost speed (1.5, now on an **indefinite** boost — see §2 Sparrow) · Mass→turret prism stretch (2.5) **+ in-flight round growth (3× at rest → 6× at Mass 10, authored on `FullAutoAction.asset`)** · Charge→skyburst blast (asset range 100→170) |
| Manta | Charge→overcharge detonation blast (1.75) · Mass→overcharge harvest capacity (1.75) · Space→Yawstery turn rate (1.6) |
| Dolphin | Charge→blast capsule THICKNESS (0.75× at rest → 1.5× at level 10) + the Echo Sight on RT · Mass→crystal-seeding recharge (0.5) · Space→blast reach (2.0) · Time→charge fill rate (1.5) |
| Rhino | Mass→trail slab max size (1.5) |
| Serpent | Time→boost duration (1.6) |
| Urchin | **All four LIVE (approved + shipped 2026-08-15, re-cut 2026-08-18, see §2 Urchin)**: Charge→the whole spike weapon — cascade DEPTH (`UrchinSpikeActionSO.ResolveGenerations` reads `GetLevel(Element.Charge)` directly) × spike REACH (map 2.5, carried down every generation via `Projectile.ChainRangeScale`) · Space→projected track LENGTH (authored `lengthMultiplierAtFullSpace` 2 on `UrchinTrackActionSO`; map multiplier pinned 1.0) · Mass→volume grown per prism ridden (authored `growthAmount` ElementalFloat 0.6→1.2 on `GunVesselTransformer`) · Time→Slip ghost duration (authored `ghostSecondsAtRestingTime/AtFullTime` 0.6→1.6 on `UrchinSlipActionSO`) |
| Squirrel | **All four LIVE (approved + shipped, see §2 Squirrel)**: Charge→skim energy per prism hit (map 2.0, read in `SkimmerBoostPrismEffectSO`) · Mass→trail prism VOLUME (authored `trailVolume` ElementalFloat 1→2.5 on `VesselPrismController`, cube-root per axis) · Space→skimmer reach (authored skimmer `Scale` ElementalFloat 15→30) · Time→boost-ring cooldown (authored `cooldownMultiplierAtFullTime` 0.5 on `SquirrelTubeActionSO`; the generic map Time multiplier stays 1.0 because `VesselTransformer` consumes it for boost speed). The former Time→top speed mapping was REMOVED (prefab `ThrottleScalerMultiplier` disabled) — one parameter per element. |

### Flight model (not an elemental mapping, but it changes what the Time rows *feel* like)

`VesselTransformer` carries two movement models since 2026-08-15, selected per vessel by
`vectorFlightModel` (default **off**). The scalar model integrates a speed **scalar** along
`Course`, so during a drift the throttle pushes along the SLIDE — squeezing mid-drift digs you
deeper into it. The vector model integrates a world-space velocity and applies thrust along the
**NOSE**. Outside a drift the two are provably the same computation (proof + numeric verification:
`_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_DRIFT.md` §3.2), so the flag changes behaviour
only inside the drift window.

| Vessel | Model | Drift throttle policy |
|---|---|---|
| **Squirrel** | vector | **Live** — thrust along the nose; aiming out of a slide and squeezing recovers |
| **Dolphin** | vector | **Locked** — no acceleration while drifting; with its authored grip 0 the velocity vector freezes outright, so entering a drift at speed costs nothing (`DOLPHIN_ENERGY_ECONOMY.md` §2a) |
| **Scarab** | vector | Live, own policy (integrator + hard ceiling + Snap Dash) |
| everyone else | scalar | — (bit-identical to before the flag existed) |

Relevant to this document because three Time rows are speed rows: the Squirrel's Time→top-speed
mapping is retired (`ThrottleScalerMultiplier` disabled), the Dolphin's Time reaches speed only via
`CurrentBoostAmount`, and the Scarab's Time IS its throttle ceiling. None of those mappings changed
here — only the direction thrust is applied in.

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
| Mass | turret-fired prism stretch (2.5) | *(open again — Shielded Prisms moved to Space 5, 2026-08 round 4)* |
| Space | gun range (steepened: base halved twice, atFull 9 — SPACE 15 unchanged) | **Piercing Bullets** — shots pierce, and turret prisms arrive SHIELDED with a wider hit sphere (moved from Mass 5, 2026-08 round 4) |
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

Mechanics detail lives beside the code, in two files:
`_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` (energy economy, drift
boost, the four gauges, the skimmer traps) and `DOLPHIN_CRYSTAL_SEEDING.md` (the passive seeding
and the Echo Sight, added 2026-08-14 when the right trigger was freed).

**2026-08-14 — the two abilities swapped which one carries an input.** Charge's crystal seeding
became PASSIVE (a cooldown loop that seeds team crystals into the cell's cytoplasm), which freed
the right trigger for the **Echo Sight**: hold it and every prism inside the blast's current
destruction volume lights up (a zoomed first-person view shipped alongside it and was cut the same
day — the highlight alone carries the ability and it leaves the speed tunnel untouched).
Rationale, placement rules and the FOV-vs-speed-tunnel resolution: `DOLPHIN_CRYSTAL_SEEDING.md`.

**2026-08-17 — the map was re-cut so each element owns one DIMENSION of the one weapon.** The
Dolphin has essentially a single offensive act (bank energy by skimming, fly into a crystal,
release a cone), so the elements were re-assigned to the orthogonal axes of that act rather than
to four loosely-related mechanics:

- **Charge took the Echo Sight AND the blast's THICKNESS** — the capsule's diameter across the
  beam, 0.75× the authored core at the resting level rising to 1.5× at level 10
  (`_coreMultiplierAtRestCharge` / `_coreMultiplierAtFullCharge`). Note this is the fleet's first
  use of `ElementalScaling.MultiplierFromRest`: the pair does NOT anchor at 1 at rest, so the
  authored core is what a MID-charge Dolphin fires and a fresh pilot's beam is deliberately
  thinner. Sight and thickness share the slot because the profile you are widening is the profile
  the sight draws. Its L5 became **Pilot Echo** (vessels inside the volume light up in their own
  domain colour), replacing Twin Seed.
- **Mass took crystal seeding** from Charge — the recharge multiplier moved with it
  (`cooldownMultiplierAtFullMass`, `[FormerlySerializedAs]` on the old Charge name). Its L5 is
  **Claimed Seed**: below it the seed is a free-for-all OMNI crystal wearing the lime CTA (your
  own ammunition, standing in open space, for whoever reaches it first); at Mass 5 it lands
  TEAM-locked. **Twin Seed is retired** — the yield is one crystal per cycle at every level.
- **Mass gave up the trail entirely.** `trailVolume` is disabled and `massUpgradeShieldsTrail` is
  off on `Dolphin.prefab`; the Dolphin no longer grows its drift prisms or shields them. (The
  machinery stays — it is the Squirrel's Heavy Trail — it is simply no longer wired here.)
- **Space narrowed to REACH only.** It still scales the blast self-similarly through
  `_heightMultiplierAtFullSpace`; what changed is that Charge now moves the capsule diameter on
  top of that, so the three elements own three orthogonal dimensions and none can steal what
  another bought: **energy → gape · Charge → thickness · Space → reach**.
- **Time is unchanged** (charge fill rate, Live Current).

The HUD row was re-cut to match — see the table below and `DOLPHIN_CRYSTAL_SEEDING.md`.

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
| Charge | crystal-blast capsule **THICKNESS** — the width across the beam, `0.75×` the authored `_coreExplosionScale` at the resting level rising to `1.5×` at level 10 (`VesselExplosionByCrystalEffectSO._coreMultiplierAtRestCharge/_coreMultiplierAtFullCharge`, floored by `_minCoreMultiplier`). Total extent across the gape is set by ENERGY, so Charge does not enlarge the blast — it redistributes that extent, trading a long thin beam for a fat round one. Carries the **Echo Sight** on the right trigger (`EchoSightActionSO`) | **Pilot Echo** — the sight lights up VESSELS caught in the same volume, each brightened in its own domain's colours (`EchoSightVesselHighlighter` drives `_ColorMultiplier` on `VesselGraph`; `BlastVolume.Contains` is the CPU transcription of the same predicate the sweep job and the prism shader run) |
| Mass | crystal-seeding recharge ×0.5 at level 10 (`DeployTeamCrystalActionSO.cooldownMultiplierAtFullMass`, floored by `minCooldown`). The ability is **PASSIVE** — no input; it seeds into the cell's cytoplasm on a loop, so this multiplier sets the seeding tempo and therefore the blast's tempo | **Claimed Seed** — the seed lands TEAM-locked instead of as a free-for-all omni crystal (`upgradedCrystalPrefab` = `TeamCrystal.prefab`, plus the `ownDomain` stamp that IS `Crystal.CanBeCollected`'s gate). Below it your ammunition is anyone's |
| Space | crystal-impact blast **REACH** ×2 at level 10 (`VesselExplosionByCrystalEffectSO._heightMultiplierAtFullSpace`). Scales the blast self-similarly (reach and base diameter together) because the half-angle IS baseRadius/height; Charge's thickness multiplier composes on top of it and moves only the capsule diameter | **Clean Blast** — the blast spares the pilot's own domain (`_spaceUpgradeSparesAllies` → `InitializeStruct.AffectSelfOverride`). Below the unlock the cone is indiscriminate, which is what makes sparing allies worth earning |
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
| Mass | trail slab max size | **Armored Slabs** — grown slabs arrive shielded (the "arrive shielded" shape; on the Sparrow this lives on **MASS** 5 — it spent 2026-08 rounds 4–6 on Space 5 and was returned by sign-off on 2026-08-13) |
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

### Urchin — chain spikes + trail rider — APPROVED + SHIPPED (2026-08-15, RE-CUT 2026-08-18)

The Urchin's revival. Both of its signature mechanics survived in the tree **unwired rather than
deleted**, and the map was approved this session against what they actually do. The shipped map is
`Assets/Resources/ElementalAbilityMaps/Urchin.asset` — **the asset is the record.**

Mechanics detail lives beside the code, in two files:
`_Scripts/Controller/Vessel/R_VesselActions/URCHIN_CHAIN_SPIKES.md` (the cascade, its three brakes,
the 2023 historical record, the determinism fix, the collider budget) and `URCHIN_TRAIL_RIDER.md`
(attach/ride/slip, and the platform attach-guard it required).

The Urchin's spine is **conversion, never destruction**: a spike embeds in a prism, *steals* it
(domain flip, mass conserved), then fires its own `LoadedGun` out of the converted mass — and the
ride does the same thing on contact, growing your own trail under you and stealing an enemy's as
you pass. So SPACE and CHARGE are the two axes of the cascade (how far it reaches, how deep it
runs), MASS is what a ridden prism gains, and TIME is the escape.

**The 2026-08-18 re-cut.** The two spike abilities were MERGED onto one trigger (a tap is the
aimed shotgun, a hold-and-release is the omni burst, sized by the hold), which freed the left
trigger for a new ability — the **Track Projector**, a straight stretch of rideable trail laid in
front of the nose so the Urchin has something to grind in open space. Element ownership moved with
them: CHARGE now owns the whole weapon (a charge-up mechanic on the charge element, with the reach
dial it inherited from SPACE), and SPACE — reach — owns the track's LENGTH. Mass and Time are
unchanged.

| Element | Ability (Input) | Quantitative (LIVE) | L5 upgrade (LIVE) |
|---|---|---|---|
| Charge | **Chain Spikes** (`RightStickAction` 1) | the WEAPON — cascade **DEPTH** (`UrchinSpikeActionSO.ResolveGenerations`, linear in `GetLevel(Element.Charge)` between the asset's authored pair, clamped `[0, 4]`) and spike **REACH** (muzzle speed × the map multiplier, 2.5 at level 10, floor 0.4, carried down the whole cascade by `Projectile.ChainRangeScale`) | **Overcharge** — the cascade gains a generation (`chainsOnChargeUpgrade`) AND `ChainRangeFalloff` becomes 1, so it runs deeper and stops losing reach as it spreads. The merge merged the upgrades too: this is the old CHARGE-5 "Overcharge" and SPACE-5 "Deep Cascade" as two halves of one idea |
| Mass | **Trail Rider** (`Input 0` — **PASSIVE**, contact-driven) | volume each friendly prism gains as you ride over it (`GunVesselTransformer.growthAmount`, `ElementalFloat` 0.6→1.2, read with `EvaluateLive`) | **Reinforced Wake** — prisms grown while riding arrive **shielded**; since a shielded prism pays double ride-ammo, a fortified lap funds the next one |
| Space | **Track Projector** (`LeftStickAction` 2) | track **LENGTH** — world units of single-lane trail laid ahead of the nose (`UrchinTrackActionSO.ResolveLength`, authored 100 u × `lengthMultiplierAtFullSpace` 2 at level 10, floored 0.4). The map's generic Space multiplier stays **1.0** to avoid double-dipping | **Long Haul** — the projection runs a further authored 100 u (`upgradeExtraLength`). A longer rail is a longer grind: more ammo recharged, more prisms grown, more speed carried off the far end |
| Time | **Slip** (`Button2Action` 7) | ghost duration — how long the hull phases out after letting go (`UrchinSlipActionSO`, 0.6 s→1.6 s, extrapolated across `[-5, 15]`) | **Slipstream** — hostile trail is ridden at **friendly** speed. `Urchin.prefab` authors `FriendlyTerrainSpeed 150` against `HostileTerrainSpeed 10`, so this is the largest single number in the vessel |

Notes that matter when retuning:

- **Trail Rider is bound to no input event on purpose.** It fires on contact, so
  `R_VesselActionHandler.CollectBoundActions` can never resolve it — the same lesson the Dolphin's
  passive Charge seeding records from the other side. Its behaviour lives in `GunVesselTransformer`
  and `VesselAttachPrismEffectSO`, which the vessel holds directly.
- **CHARGE's depth does NOT come off the map multiplier.** It reads the integer level; the map's
  2.5 is the REACH multiplier. Two dials on one element is a deliberate exception the merge
  forced — the weapon is one ability now, and "how far it reaches" and "how deep it runs" are the
  two coordinates of one cascade, not two unrelated parameters. Retune them apart: depth on the
  asset's authored pair (`generationsAtRestingCharge` / `generationsAtFullCharge`), reach on the
  map entry.
- **CHARGE's reach is the only quantitative value that has to travel.** A cascade can outlive the
  pilot who started it (dead, respawned, across the cell), so the reach is stamped onto the gun →
  onto each projectile → onto that spike's own `LoadedGun`. Nothing in a cascade ever looks back up
  at the vessel. (It was read off SPACE until the merge; the read moved, the plumbing did not.)
- **The two spike abilities are now ONE asset** (`UrchinSpikeAction.asset`). `UrchinSpikeVolleyAction`
  and `UrchinSpikeBarrageAction` are retired — the same SO type still carries both patterns, but the
  tap pattern and the charged pattern are two fields of one ability rather than two assets bound to
  two triggers.
- **The track's cooldown is deliberately NOT elemental.** TIME is Slip's on this vessel, and a
  second Time consumer is the double-dip the convention exists to prevent. It is authored 20 s —
  the Squirrel boost ring's cooldown, matched so the fleet's two "place a structure" abilities
  share one cadence.
- **All four L5 gates read `IsUpgradeActive(element)`** — the replicated unlock bit. Every one of
  them changes the prismscape (reach, depth, shielded prisms, ride speed), so a local level read
  desyncs it.

Shipping it also required one **platform** change, recorded here because it is not Urchin-only:
`VesselDamagePrismEffectSO` declines while `IVesselStatus.IsAttached` (`skipWhileAttached`,
default on). Riding a trail and ramming it are the same collision through one flat effect list, so
any attaching vessel destroys the prism it latched onto — the 2023 "urchin destroys the first
block" bug. See `URCHIN_TRAIL_RIDER.md` § "The platform change".

### Scarab — the Rocket League vessel (throttle + drift + dash + ball/switch economy) — AUTHORED (2026-08-15)

`VesselClassType.Scarab = 12` exists and `Assets/Resources/ElementalAbilityMaps/Scarab.asset` is
**authored** — this table is the shipped map, not a proposal. Full design — controls, the
player-generated multi-ball model, the switch, the crystal→ball economy, the four-lane
"quadrality" rationale, ecology retune and registration checklist — lives in
`_Scripts/Controller/Vessel/R_VesselActions/SCARAB.md`. Rows come from Garrett's markup of
2026-08-15. Map multipliers pinned to 1 wherever an authored field carries the scaling (the
Dolphin no-double-dip pattern); **Space is the exception** — there is no authored ball scale, so
the map's own `MultiplierAtFullLevel` is the carrier.

| Element | Quantitative | L5 upgrade |
|---|---|---|
| Charge | cavitation-blast **cooldown** (`ScarabCavitationBlast.cooldownSeconds 2.5` × `cooldownMultiplierAtFullCharge 0.5` at L10 — the authored-cooldown idiom) | **Cavitation Shear** — the blast destroys SHIELDED prisms outright instead of only shedding shields (`AOEExplosion.InitializeStruct.DevastatingOverride`, per-use snapshot) |
| Mass | switch structure size — ring aperture + interior fill span (`switchScale` ElementalFloat 1→2.5) | **Armored Switch** — the switch is built from SHIELDED prisms, snapshotted at placement, so an opposing ball caroms off and sheds one shield per prism |
| Space | forged **ball size**, ×1 → **×4 at L10** (`MultiplierAtFullLevel: 4` on the map itself; stamped once at forge time) | *(open — the notes name no Space upgrade; do not invent one)* |
| Time | top speed of the throttle ramp (`ThrottleScalerMultiplier` ElementalFloat 1→1.5 — the existing dormant `VesselTransformer` field, enabled) | **Snap Dash** — double-tap the THROTTLE (RT) for a burst gap closer (detected off the RT `RightStickAction` edges, no new input plumbing) |

**The right-stick dash is base kit and has no cooldown** — it is not a map row. Only the
cavitation blast riding it is paced, which is the Charge row. Snap Dash is the *throttle's*
upgrade, not the dash's; do not conflate them.

Superseded passes (kept for the record): the vessel was "Mantis", Astro-League-only, with a single
mode ball launched by a cavitation cone and a braking wall on the A button — Surgical Strike /
Ablative Wake / Deep Wall / Hair Trigger. A second pass proposed Charge = ball-generation energy
with **Split Shot**, Mass with **Second Pass**, and Space = juke reach. **The 2026-08-15 markup is
the record; do not re-litigate from a superseded pass.**

## 3. Implementation notes for approved rows

- Executor-side gate on `IsUpgradeActive(element)` at use time; per-use snapshot; AI gets it
  free through the same executors.
- Shield grants: regular shield only, flag-before-`Initialize` or `ActivateShield()` at rest.
- Domain-sparing: explosion/collection layer only — never `Prism.Damage`, never danger effects.
- Fill in each map's `UpgradeLabel`/`UpgradeDescription` when a row is approved; the HUD reads
  the map.
- The `Input` fields are filled on Sparrow (4/4), Dolphin (3/4), Squirrel (2/4) and Urchin (3/4 —
  Trail Rider is deliberately 0 because it is **passive**, not because it is unset). Manta, Rhino
  and Serpent are 0 across the board — fill during HUD icon work. A genuine 0 and a passive
  ability are indistinguishable in the asset, so say which it is in the row.
