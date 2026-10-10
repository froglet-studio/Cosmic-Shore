# Rhino elemental ability map: the decision packet

**Measured 2026-10-10** on `cece/nice-brahmagupta-ojclij` (head `0e67df5b`), by running
`python3 Tools/Build/element_ability_table.py Rhino --verbose` and `--json` and reading every asset
and source line cited below. **Nothing here is authored.** `Assets/Resources/ElementalAbilityMaps/Rhino.asset`
is unchanged, and so is every prefab, SO and C# file. This is the proposal
`Docs/prompts/RHINO_ABILITY_MAP_PROMPT.md` asked for: one a designer can accept, edit or reject
row by row in one sitting, built from what the Rhino already does rather than from a blank page.
Un-approved until Garrett marks it up (`FLEET_MAPS.md` §2, the `/vessel` skill §3 gate).

Reading order if short on time: §2 (the table), §5 (the one recommendation), §6 (what to tick).

---

## 0. The live state, and what moved since the prompt was written

### 0.1 The tool's verdict (trimmed; the full print is one command away)

```
RHINO   abilities 2/4   scaling wired 3/4   L5 gates wired 0/4
  CHARGE  (open design slot)                 passive
          ! OPEN DESIGN SLOT - nothing authored
  MASS    Trail Slabs                        passive
          scale   1 -> 1.5 across L0..L10
                  massMaxSizeMultiplier (ElementalFloat)  <- GrowTrailAction.asset
          L5      -
                  gated at VesselPrismController.cs:506  <- Rhino.prefab   [OFF: turnUpgradeShieldsTrail (unauthored, C# default false) = 0]
  SPACE   (open design slot)                 passive
          ! OPEN DESIGN SLOT - nothing authored
  TIME    Ramp Spool                         passive
          scale   1 -> 2.5 across L0..L10
                  timeAccelerationMultiplier (ElementalFloat)  <- RhinoRampBoostAction.asset
          L5      -
4 rows, 2 with a declaration/wiring disagreement
```

A complete hull for comparison (same tool, same run):

```
SQUIRREL   abilities 4/4   scaling wired 4/4   L5 gates wired 4/4
  CHARGE  Crystal Joust   passive   stealScale 1 -> 2.5 <- VesselOvertakeBySkimmerEffect.asset   L5 Shepherd   gated at VesselWitherLifeformByCrystalEffectSO.cs:66
  MASS    Boost Ring      RT        x1 -> x0.5 <- SquirrelTubeActionExecutor.cs:106             L5 Twin Rings gated at SquirrelTubeActionExecutor.cs:164
  SPACE   Steal           passive   Scale 15 -> 30 (nested-prefab override) <- Squirrel.prefab   L5 Iron Grip  gated at SkimmerStealPrismEffectSO.cs:32
  TIME    Skimming        passive   energyMultiplier 1 -> 2 <- SkimmerBoostPrismEffect.asset     L5 Live Wire  gated at SkimmerBoostPrismEffect.cs:72

URCHIN     abilities 4/4   scaling wired 4/4   L5 gates wired 4/4
  CHARGE  Chain Spikes    RT / R-Shift   chargeRangeMultiplier 1 -> 2.5   L5 Overcharge
  MASS    Trail Rider     passive        growthAmount 0.6 -> 1.2          L5 Reinforced Wake
  SPACE   Track Projector LT / L-Shift   ProjectileTime 4 -> 8, x1 -> x2  L5 Long Haul
  TIME    Slip            B / R          0.6 -> 1.6 (absolute)            L5 Slipstream
```

"Complete" means every row names an ability, names the number the element reaches and the file
that owns it, and names a level-5 upgrade with the source line that gates it. The Rhino is missing
the first on two rows and the third on all four.

### 0.2 The text print hides two channels the walk found

Run with `--json`, the two "open" rows are not empty:

| Row | What the reference walk reaches | Live? |
|---|---|---|
| **Space** | `Scale` ElementalFloat on `Assets/_Prefabs/Spacevessels/Components/ForceFieldSkimmer Variant.prefab`: `Scale.Min 30`, `Scale.Max 50`, `Scale.Enabled 1`, `Scale.element 3` (Space), as `m_Modifications` on that variant. Read every frame through `Skimmer.LiveElementalScale` (`Skimmer.cs:54`, `EvaluateLive`) into `ShieldSkimmerScaleDriver.BaseScale` (`ShieldSkimmerScaleDriver.cs:100`) | **Yes. Space already sets the sword's resting length, 30 at rest to 50 at level 10.** The map does not say so. |
| Space | `VesselExplosionByCrystalEffectSO.cs:108` via `RhinoVesselExplosionByCrystalEffect.asset`, `_heightMultiplierAtFullSpace` 1.0 (code default), gate at `:208` behind `_spaceUpgradeSparesAllies` (unauthored, false) | Inert: authored x1, and the blast it scales spawns `AOESlowExplosion.prefab`, whose `SlowExplosionImpactorDataContainer.asset` is empty on both lists |
| **Charge** | `VesselExplosionByCrystalEffectSO.cs:166` via the same asset, `_coreMultiplierAtRestCharge` 1.0 / `_coreMultiplierAtFullCharge` 1.0 (code defaults) | Inert, same empty container |

The text renderer prints only the OPEN flag for an unnamed row; the channels are in the JSON. That
is worth a line in the `/element-ability-table` skill, but it is a tool note, not a Rhino decision.
The consequence for this packet is in §2: **the Space slot is open in the MAP only.** The design
exists, ships, and is played; the asset is the thing that does not know it. This is the mirror of
the Broadside lesson recorded on the Time row (`BROADSIDE.md` §3, "a capability live in code and
flat in data reads like one that does not exist"): a capability live in code AND data, undeclared,
reads as an open design slot.

### 0.3 Corrections to the prompt's table and to `STEAM_RELEASE_TASKS.md` R10

| Prompt / R10 says | Tree says today | Where |
|---|---|---|
| "one of four slots designed" (R10: "1 of 4") | **2 of 4**: Mass (Trail Slabs) and Time (Ramp Spool) | tool output above |
| Time `(open design slot)` | **Ramp Spool**, `timeAccelerationMultiplier` x1 -> x2.5 floored x0.5 on `Assets/_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset`; filled from Broadside's first playtest, deliberately with no L5 | `BROADSIDE.md` §3; `FLEET_MAPS.md` §2 Rhino Time row |
| Mass "x1 -> x1.5, floored x0.25" authored on the map | Same numbers, moved HOME to `GrowTrailAction.asset` `massMaxSizeMultiplier`; the map carries no numbers since 2026-09-18 | `ELEMENT_SCALING_UNIFICATION.md` |
| Space open | Open in the map; **live in data** (0.2) | `ForceFieldSkimmer Variant.prefab` |
| "the Rhino currently has no ability on any button" | True of the MAP's `Input` fields (all 0). False of the prefab: `Rhino.prefab` `_inputEventShipActions` binds event 0 (`FullSpeedStraightAction`) to `RhinoRampBoostAction` + `GrowTrailAction`, event 1 (`RightStickAction`, RT) to `RhinoShieldSwipeRightAction`, event 2 (`LeftStickAction`, LT) to `RhinoShieldSwipeLeftAction`. The sword is the most-bound ability in the fleet; it has no map row | `Rhino.prefab` lines 2718-2731 |
| "Peel the Cage" | Renamed **Cleave** (`GameModes.Cleave = 39`; Ribcage -> PeelTheCage -> Cleave) | `CLEAVE.md` naming note |
| "332 u at 1200 u/s down to 25 u at cruise", "115 u asymptote", `RotationThrottleScaler 0.5` | **Retuned 2026-09-25**: `maxBoostMultiplier` 24 -> **16.8** (top 1200 -> **840 u/s**), `RotationThrottleScaler` 0.5 -> **0.2**. Flat-out plateau radius **622 u** at 840, cruise **28.6 u**, asymptote **286.5 u**, flat-out (stick budget) radius **666.2 u** | `RHINO_RAMP_BOOST.md` retune note; `HeadlongCircuitSettings` |
| Trail Slabs is "the existing Mass scalar, and the reason Cleave's arena reads the way it does" | The scalar is live; **the grow loop it scales has never executed** (0.4). Cleave's arenas were measured against the RESTING trail prism (`BaseScale` 3 x 3 x 0.5, volume 4.5, `CLEAVE.md` "Stated cost") | `GrowTrailActionExecutor.cs`; `BACKLOG.md` 27 |

### 0.4 One shipped row is a scalar on a loop that never runs

`GrowTrailActionExecutor.Begin` (the file at
`Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs`) still reads

```csharp
_growing = true;
End();                       // sets _growing = false
_cts = ...; LoopAsync(so, _cts.Token).Forget();   // while (_growing) never enters
```

so the slab never grows and the Mass channel (`maxSize 4` x `massMaxSizeMultiplier` 1 -> 1.5) scales
a ceiling nothing reaches. `BACKLOG.md` item 27 records this and why the one-line fix is NOT safe on
its own (`GapWeight: -1` on `GrowTrailAction.asset` inverts the restore branch, so a live loop opens
the rail gap without bound; the design fork is whether growth closes the gap or opens it). Two more
facts the packet depends on:

- Even with the loop fixed, an **AI Rhino never grows a slab**: `Rhino.prefab` `AIPilot.abilities`
  lists `GrowTrailAction` with `Duration: 0`, and `AIPilot.UseAbilityCoroutine` runs
  `StartAction`, waits `Duration`, then `StopAction` (`AIPilot.cs:1126-1130`), and
  `GrowTrailActionSO.StopAction` is `End()`. The `/vessel` skill's rule 37 shape.
- The Rhino's trail today is therefore the resting one everywhere: `BaseScale (3, 3, 0.5)`,
  `Gap 2`, `offset 20`, `initialWavelength == minWavelength == 5` (`Rhino.prefab` VesselPrismController
  block, lines ~2509-2560).

**So "Trail Slabs" is not a complete row either.** It is a declared ability with a live scaling
channel and no effect. The packet keeps it where it is (it is Garrett's row, not an open slot) and
puts the precondition in §2b and §7.

---

## 1. What the Rhino demonstrably does today (the authored numbers)

Every number below is read from the file named, not from a doc. Derived values show their formula.

### 1.1 The ramp boost (`RHINO_RAMP_BOOST.md`)

| Quantity | Value | File |
|---|---|---|
| `DefaultThrottleScaler` / `DefaultMinimumSpeed` | **50** / **0** | `Assets/_Prefabs/Spacevessels/Rhino.prefab` |
| `maxBoostMultiplier` | **16.8** | `Assets/_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset` |
| `accelerationPerSecond` / `bleedPerSecond` / `returnPerSecond` | **220** / **300** / **120** | same asset |
| `straightnessGraceBand` | **1.0** (0.3 would restore the binary latch) | same asset |
| `timeAccelerationMultiplier` (Time, the Ramp Spool row) | `Min 1`, `Max 2.5`, `UseFloor 1`, `Floor 0.5`, `element 4` | same asset |
| `StraightLineGesture.EngageThreshold` | **0.3** | `Assets/_Scripts/Controller/IO/StraightLineGesture.cs:29` |
| `ThrottleScalerMultiplier` / `BoostSpeedMultiplier` | `Enabled: 0` / absent (C# default disabled): Time does NOT reach the ceiling | `Rhino.prefab`; `VesselTransformer.cs:923-930` |
| cruise / top | `50 x 1 + 0` = **50 u/s**; `50 x 16.8 + 0` = **840 u/s** | derived |
| build cruise -> top | `790 / 220` = **3.6 s** at Time rest; `790 / 550` = **1.4 s** at Time 10; `790 / 110` = **7.2 s** at the x0.5 floor | derived |
| coast top -> cruise after disengage | `790 / 120` = **6.6 s** | derived |
| `RotationThrottleScaler`; `PitchScaler`/`YawScaler`/`RollScaler` | **0.2**; **90 / 90 / 90** | `Rhino.prefab` |
| max turn rate | `speed x 0.2 + 90` (`VesselTransformer.cs:273`): **100 deg/s** at cruise, **258 deg/s** at top | derived |
| min turn radius | `v / omega`: **28.6 u** at cruise, **186.5 u** at top, asymptote `180 / (pi x 0.2)` = **286.5 u** | derived; `HeadlongCircuitSettings` doc comment |
| flat-out radius (no boost given up) | `186.5 / 0.28` = **666.2 u** | `HeadlongCircuitSettings.FlatOutRadius`; pinned by `HeadlongCircuitTests.cs:63` |

The graded ramp: `MultiplierFor(deviation) = lerp(1, 16.8, 1 - clamp01((deviation - 0.3) / 0.7))`
(`RampBoostActionSO.cs`), so a single-axis turn at full throttle at stick `s` sustains
`50 x MultiplierFor(s)` u/s. The retune note's live curve: stick 0.30 -> 840 u/s / 622 u,
0.50 -> 614 / 331, 0.70 -> 389 / 190, 1.00 -> 50 / 29.

### 1.2 The energy sword (`RHINO_SHIELD_SWIPE.md`, `RHINO_ENERGY_SWORD.md`, `RHINO_SWORD_COMBOS.md`)

| Quantity | Value | File |
|---|---|---|
| inputs | RT / LT analog (difference = yaw+roll to +-90 deg, sum = chop to 65 deg); both held = the energize stance; tap strings = combos | `Assets/_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset` (`swipeYawDegrees 90`, `swipeRollDegrees 90`, `chopPitchDegrees 65`, `swipeOutSeconds 0.18`, `swipeCooldownSeconds 0.35`, `stanceSumThreshold 1.5`, `stanceCenterEpsilon 0.4`) |
| resting length | `Skimmer.Scale` **30 -> 50 on Space** (0.2), `elongateYOnly 1`, `lengthScale 2` | `ForceFieldSkimmer Variant.prefab` |
| energy meter | the Shield resource (index 1), 0..1, no passive gain, no decay | `ShieldSkimmerScaleDriver.cs` |
| length from energy | world Y tweened from `BaseScale` (= the Space value) to `maxScale` **120**; `prismGrowSpeed 30`, `shrinkSpeed 10` | `Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset` |
| energy per kill | **0.04** per prism destroyed (25 kills to full), **0.12** per super-shielded pop | `Assets/_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset` |
| energize | hold the stance **1 s**, costs **0.1**, tail **5 s** after leaving the stance, cooldown **5 s**; `popRequiresEnergizedBlade 1` | `ShieldSkimmerScaleConfig.asset`; `RhinoSkimmerDamagePrismEffect.asset` |
| crystal burst | all three dimensions to `authored x lerp(1, 4, energy)`, hold **2.5 s**, grow **600**/return **150** u/s, drains energy to 0, spawns no explosion | `ShieldSkimmerScaleConfig.asset`; `RHINO_ENERGY_SWORD.md` |
| bind (cold blade in super-shielded mass) | turn rate eases to **0.3** (`bindTurnRateMultiplier`, C# initializer; the key is absent from the asset) | `ShieldSkimmerScaleConfigSO.cs:127` |
| contact velocity | the touched POINT's true velocity (`SkimmerSwingKinematics`), `swingVelocityScale 1`, `restitution 1/3`, `debrisSpeedLimit 200`, `proportionalDebris 1`, `sliceDestroyedPrisms 1` | `RhinoSkimmerDamagePrismEffect.asset`; `Assets/_Scripts/Controller/Vessel/SkimmerSwingKinematics.cs` |
| measured tip speed, swipe at 35 u/s cruise | **534 u/s** at length 30, **1219 u/s** at 120 (the blade is the lever arm; the measured table has no row at 50) | `RHINO_SHIELD_SWIPE.md` "Measured magnitudes" |
| the sword's vessel-facing effects | `VesselCombatHitBySword.asset` (`hitClass 5` Strike, `sameVictimCooldownSeconds 1.4`, `requireOwningMachine 1`, `requireFasterThanVictim 1`) and `RhinoSwordStealBySkimmerEffect.asset` (`debuffMagnitude -0.08`, `buffMagnitude 0.08`, `stealScale Enabled 0`, `requireOvertake 0`, `cooldown 1`): a Strike on a SLOWER rival steals 0.8 petal per element; the take runs only through the reporter (`VesselCombatHitBySkimmerEffectSO.cs`, the PvP-is-petals-only rule) | the two assets; `Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset` |
| combos | 12 sequences x 2 sets (base / energized), energized paths >= 1.2x base tip travel; variant chosen from local `IsEnergized` at `ShieldSwipeActionExecutor.cs:223-225` | `RhinoSwordComboLibrary.asset`; `RHINO_SWORD_COMBOS.md` |
| AI | never pulls a trigger: no swipe, no stance, no energize, no combo; a parked blade still cuts on contact (ungated) | `RHINO_ENERGY_SWORD.md`; `CLEAVE.md` "Nothing is shielded" |

Container audit (rule 22 of the `/vessel` skill, measured by guid across every
`Effect Containers/**/*.asset`): `RhinoSkimmerDamagePrismEffect`, `RhinoSwordStealBySkimmerEffect`,
`VesselCombatHitBySword` and `RhinoSwordCrystalBurstEffect` are each referenced by exactly ONE
container, the sword's. So every sword asset can be re-authored without forking. The hull's
container (`RhinoImpactorDataContainer.asset`) carries `VesselHapticsByPrismEffect`,
`VesselDamagePrismEffect`, `VesselElementalDebuffByDangerPrismEffect`, and the two crystal effects;
it carries **no** `VesselChangeSpeedByPrismEffectSO`, so on a Rhino a danger prism does exactly one
thing: drain petals (`BACKLOG.md` "Rhino follow-ups").

### 1.3 The trail (`VesselPrismController` on `Rhino.prefab`, `GrowTrailAction.asset`)

`BaseScale (3, 3, 0.5)`, `Gap 2`, `MinimumGap 1`, `offset 20`, wavelength 5 (speed-invariant);
`GrowTrailAction.asset`: `maxSize 4`, `growRate 3`, `shrinkRate 1`, `XWeight 1`, `YWeight 1`,
`ZWeight 0`, `GapWeight -1`, `massMaxSizeMultiplier 1 -> 1.5` floored 0.25 on Mass. The loop is
dead (0.4). `turnUpgradeShieldsTrail` is absent from the prefab (C# default false), and its driver
`SetTurnTrail` is called only by the Manta's executors (`MantaAnalogTurnBoostExecutor.cs:133`,
`YawsteryActionExecutor.cs:242`), so that Mass-5 gate is unreachable on a Rhino even if switched on.

---

## 2. The proposal table: the two open slots

Columns: the ability and the card it labels; the `Input` the map row would carry (for the HUD hint;
`0` = passive); what the element scales and over what range; the level-5 upgrade; the EXISTING
mechanic it is built from; and the code it costs. **Any row marked "C# touch" needs a gameplay
change and is a bigger conversation than a data edit**, exactly as the prompt asked.

| Element | Ability (card) | Input | Scales | Level-5 | Built from | Code |
|---|---|---|---|---|---|---|
| **Charge** | **Hot Edge**: the energy the blade banks per kill | `0` passive (the bank happens on contact) | `energyPerPrism` 0.04 and `energyPerSuperShieldedPrism` 0.12 x **1 at rest -> x2.5 at level 10, floored x0.5** (proposed endpoints; the fleet's Charge convention: Squirrel `stealScale` 1 -> 2.5, Urchin `chargeRangeMultiplier` 1 -> 2.5; the floor is Ramp Spool's). Per kill 0.04 -> 0.10: a full-length blade in **10 kills instead of 25**; a super pop 0.12 -> 0.30. At the floor, 0.02 (50 kills) | **Bright Edge**: at Charge 5 the ENERGIZED combo set plays on a cold blade too (the 12 upgraded flourishes, each with >= 1.2x the tip travel of its base). The super-shield pop stays behind the real energize ritual, untouched | the energy loop `RHINO_ENERGY_SWORD.md` calls "the intended emergent loop": length is the lever arm, energy is length, kills are energy. Scaling: a new `ElementalFloat chargeEnergyMultiplier = ElementalFloat.Multiplier(1, 2.5, Element.Charge, 0.5)` on `RhinoSkimmerDamagePrismEffectSO`, multiplied in at the two `AddEnergy` sites (`:147`, `:171`) through `EvaluateLive(status)`. L5: `ShieldSwipeActionExecutor.cs:223-224` picks the library set from `energized`; OR in `IsUpgradeActive(Element.Charge)` for the pick only | **C# touch, small**: one field + two reads for the scaling (the shape every migrated channel took in `ELEMENT_SCALING_UNIFICATION.md`); one line for the L5. No new mechanic, no new asset |
| Charge (alternative, zero code) | **Keen Strike**: petals stolen per sword Strike | `0` passive | `stealScale` on `RhinoSwordStealBySkimmerEffect.asset`: author `Enabled 1, Min 1, Max 2.5, element 1, UseFloor 1, Floor 0.5`. 0.8 petal per element per Strike at rest -> 2 petals at level 10. Read off the thief's REPLICATED level (`EvaluateReplicated`, `VesselOvertakeBySkimmerEffectSO.cs:191`) | *(none proposed; the Squirrel's Shepherd lives on a crystal effect the Rhino does not carry)* | the Squirrel's shipped Charge row verbatim (`FLEET_MAPS.md` §2 Squirrel, "The Rhino's sword shares the type and authors it disabled") | **Asset only** |
| **Space** | **Reach**: the sword's resting length. DECLARE what ships | `1` (`RightStickAction`, RT: the swipe; `InputHintBindingMap` already carries a binding for that event) | **30 at rest -> 50 at level 10**, already authored and live (`ForceFieldSkimmer Variant.prefab` `Scale`, read at `ShieldSkimmerScaleDriver.cs:100`). Because `MaxScale` is fixed at 120, a level-10 blade starts 20 u longer and the energy meter buys 70 u instead of 90 | **leave open** (honest gap). The one candidate from an existing shape is **Clean Edge** (the blade spares your own domain's prisms, the Dolphin's Space-5 "Clean Blast" shape), flagged in §2b | nothing to build: the map entry is the whole change | **Map asset only** for the row. Clean Edge would be a C# touch and a design-ruling question |

Why not the `FLEET_MAPS.md` §2 "Bulldozer" rows: the Charge proposal there ("forcefield shrink
rate, `GrowSkimmerAction.shrinkRate` Charge 6 -> 2") names a field that is a plain `float` since
`FLEET_GAPS.md` 5.7 on an action whose registry `Rhino.prefab` carries **inactive** (superseded by
`ShieldSkimmerScaleDriver`, `RHINO_SHIELD_SWIPE.md` "Sword dimensions"), and its L5 "Unyielding
Field" (no shrink on prism hits) describes a shrink-on-hit that no longer exists (the meter has no
decay; the shrink debuff went with the control-theft tier, `BACKLOG.md` 5.11b). The Space proposal
("forcefield max size") is a second dial on the same length the Space row ALREADY scales. Both are
superseded here, not re-litigated.

### 2b. The level-5 column, all four rows (0/4 authored today)

| Element | Row state | Proposed L5 | Built from | Code | Verdict |
|---|---|---|---|---|---|
| Charge | open (above) | **Bright Edge** (above) | combo library, `IsUpgradeActive` | 1 line | propose |
| Mass | Trail Slabs, dead loop (0.4) | **Armored Slabs** (grown slabs arrive shielded) is the right shape and is **blocked** three ways: the grow loop has never run (BACKLOG 27, with its `GapWeight` fork); no Rhino-reachable shield condition exists (`turnUpgradeShieldsTrail` is Manta-driven, `driftShieldsTrail` needs a drift the Rhino never enters), so it needs a new `IsBoosting` term in the `VesselPrismController.cs:503-506` OR chain; and the AI's `Duration: 0` ends the grow the frame it starts | `VesselPrismController` shield branch | C# touch + BACKLOG 27 first | **leave open; settle BACKLOG 27 first** |
| Space | live, undeclared (above) | **Clean Edge**: at Space 5 the blade does not cut prisms of the pilot's own domain (and so banks nothing off them). Shape: the Dolphin's Clean Blast (`_spaceUpgradeSparesAllies`), applied in the sword's own effect SO before `PrismEffectHelper.DamageProportional`. **Flag**: `RHINO_ENERGY_SWORD.md`'s follow-up on self-farming says "skip the ENERGY BANK, never the damage", and the v3 ruling forbids gates on ordinary cutting. A domain skip on damage is a gate on ordinary cutting. Design must rule, not a session | domain compare + `IsUpgradeActive(Element.Space)` in `RhinoSkimmerDamagePrismEffectSO.Execute` | C# touch, small | **candidate only; default is open** |
| Time | Ramp Spool, no L5 by design (Broadside) | **Ramp Ward**: while the ramp is engaged (`IsBoosting`, which `RampBoostActionExecutor.Begin` sets and `End` clears, including across the graded band), danger prisms do not drain petals. `VesselElementalImmunity` on the Rhino root: `condition WhileBoosting`, `upgradeGate Time`, `wardedSources DangerPrism`. The Dolphin's Drift Ward with the drift swapped for the ramp; wards the ARENA only, never a rival's Strike or blast | `Assets/_Scripts/Controller/Vessel/VesselElementalImmunity.cs` (its guid appears 0 times in `Rhino.prefab` today) | **zero code**: one component, three fields | propose, with the Cleave caveat in §3/§4 |

---

## 3. Mode consequences, per option

The three modes are balanced against the Rhino as it flies today. What each option does to each:

### 3.1 Astro League (`ASTROLEAGUE.md`; `GameModes.AstroLeague = 37`; card seats Rhino, Scarab, Squirrel; comeback rate **1 per goal**)

Ball physics through the swung blade: a contact resolves ON the blade (`bladeAwareStrikes`, C#
default true on `AstroLeagueSettingsSO`), takes `VelocityAt(contact)` from `SkimmerSwingKinematics`,
adds the arcade pop `hitBoostMultiplier 2.5` and `bladeTipStrikeBonus 1.35` lerped hilt -> tip
(`Assets/_SO_Assets/Games/AstroLeagueSettings.asset`). The court's edge lining is super-shielded
and an energized blade can carve it ("deliberately a paid, windowed act", `RHINO_ENERGY_SWORD.md`
"Binding").

| Option | Consequence |
|---|---|
| Charge: Hot Edge | A Rhino cutting enemy trail (the ball's own obstacle) banks energy up to 2.5x faster, so the blade reaches length and the tip reaches its 1219 u/s regime sooner. Harder shots earlier in a match, through the lever arm the mode already prices. No new force on the ball; `maxSpeed` still clamps the striker velocity (`AstroLeagueBall.cs:1808`) |
| Charge: Keen Strike | Nothing on the ball. More petals per Strike on a slower rival; Strikes score no goals, so it is theft only |
| Space: Reach (declare) | Nothing changes: this IS the shipped blade. A level-10 Rhino already swings 50 u at rest |
| L5 Bright Edge | More tip travel per flourish for human pilots. The ball cares about the point velocity, so a wider, faster flourish is a bigger strike window. Cosmetic-plus |
| L5 Clean Edge | A Rhino stops cutting its own domain's trail, which is the ball's shield and lane for that domain. Defensive, moderate |
| L5 Ramp Ward | No authored danger mass in the court (the doc mentions danger prisms only in the friendly-fire note at `ASTROLEAGUE.md:551`). Effectively inert here |

### 3.2 Cleave (`CLEAVE.md`; `GameModes.Cleave = 39`; Rhino only; comeback rate **0.0125 per prism**; targets **1200 / 1200 / 1500 / 1500**)

Scoring IS destruction. Nothing in any arena is shielded (asserted by `cleave_budget.verify`), so
energize is irrelevant and the AI can score. Danger traps: **511 / 399 / 428 / 228** prisms per
rung. The cell grows nothing (the 2026-08 fauna removal), so elemental crystals come from ejected
petals and the comeback is the main road to level 5.

| Option | Consequence |
|---|---|
| Charge: Hot Edge | The destruction-rate dial, directly: energy is length, length is swept width AND tip speed, and destruction is what scores. At x2.5 the blade is at 120 after 10 kills instead of 25. **This is the row with the largest Cleave consequence and it needs a playtest before the endpoints are final**; x2.5 is the fleet's number, not a Cleave-measured one |
| Charge: Keen Strike | Near-inert: a sword Strike on a rival Rhino moves petals, not prisms |
| Space: Reach (declare) | Nothing changes |
| L5 Bright Edge | Human-only; more sweep per combo. Rivals of a comboing human cut less per flourish than the human does, which is the point of a level-5 |
| L5 Clean Edge | Own trail never scores here ("Your own and your teammates' trails never score"), so this changes no score; it stops a pilot shredding their own ribbon |
| L5 Ramp Ward | **The sharpest one.** On a Rhino a danger prism does nothing BUT drain (1.2), so a trailing Rhino with Time 5 holding the ramp has no trap consequence at all for as long as it holds a straight-ish line. The arena is 720-2160 u across and the ramp needs 3.6 s of straight to top out, so the window is short on rungs 3-4 and long on the Panes and the Swell. It denies no scoring event; it deletes a punishment |

### 3.3 Headlong (`HEADLONG.md`; `GameModes.Headlong = 49`; Rhino only; comeback rate **0.35 per gate**; 24 gates = 3 laps x 8 rings)

The corner budget is cut against the ramp. **The compile-time copy:** `HeadlongCircuitSettings`
(`Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs:34-78`) restates
`RhinoThrottleScaler 50`, `RhinoMinimumSpeed 0`, `RhinoMaxBoostMultiplier 16.8`, `RhinoGraceBand 1`,
`RhinoRotationThrottleScaler 0.2`, `RhinoTurnScaler 90`, `BoostStickBudget 0.28`,
`BoostPlateauDeviation 0.3`, and derives `RhinoTopSpeed 840` and `FlatOutRadius 666.2`.

**The test that holds it in step is `RhinoRampGradingTests.The_settings_curve_matches_the_shipped_asset`**
(`Assets/_Scripts/Tests/Editor/RhinoRampGradingTests.cs`): it sweeps deviation 0..1 and asserts
`HeadlongCircuitSettings.SpeedAtStick(d)` equals `RhinoThrottleScaler x so.MultiplierFor(d) + RhinoMinimumSpeed`.
Note what "the shipped asset" means there: the test builds its `RampBoostActionSO` with
`Ramp(max: 16.8f, band: 1f)` set by reflection; it does NOT load
`RhinoRampBoostAction.asset`. So the test pins the generator to the test's own literal, and the
asset is a third copy that only `HeadlongCircuitTests` (`:48` asserts `RhinoTopSpeed == 840`,
`:63` asserts `FlatOutRadius == 666.2`) and a human reading all three keep honest. A fourth copy:
`Tools/Build/regatta_course_measurements.json` lists `HeadlongCircuit.cs` in its `sources`, and
`author_regatta_assets.py --check` goes `COURSE_STALE` on any byte change to that file.

**None of the options in §2 touches `maxBoostMultiplier`, `straightnessGraceBand`,
`accelerationPerSecond`'s ceiling, or `RotationThrottleScaler`.** `HeadlongCircuitSettings` is
untouched by every row of this packet. What WOULD move every corner on the course, for the record:
any Space proposal that re-cut turn authority (the "Space -> turn-authority re-cut" the ramp doc's
follow-ups call "the obvious shape"), any Time proposal that reached the ceiling, and any change
to the grace band. This packet deliberately proposes none of them; the designer is free to, and
then §4.ab of the `/vessel` skill is the checklist (the settings copy, both test files, the Regatta
hash, and the course ladder's `CornerRadiusFactor`).

| Option | Consequence |
|---|---|
| Charge: Hot Edge | The sword is "the interference" on the circuit (rival ribbons across the line). A longer blade clears more of a rival's ribbon per pass. No corner moves |
| Charge: Keen Strike | A Rhino that laps a rival faster steals more petals in passing (the Strike requires being faster, which an overtake is). Petals in Headlong feed Ramp Spool, so a leader stripped of Time spools slower: a self-balancing theft |
| Space: Reach (declare) | Nothing changes |
| L5 Bright Edge | Human-only; nothing on the course |
| L5 Clean Edge | A pilot's blade no longer cuts their own lap-1 ribbon on lap 2; the hull still clears it on contact (`VesselDamagePrismEffect` in the hull container). Nothing on the course |
| L5 Ramp Ward | The circuit sits in the barren Skim Race cell (`HEADLONG.md` §7) and neither the doc nor `HeadlongController.cs` names a danger prism, so inert here (not verified by opening the scene) |

What Ramp Spool already does to Headlong, since the course was cut before Time had an endpoint:
`HEADLONG.md` §9 records it. At Time 10 the ramp tops out in 1.4 s instead of 3.6, so short legs
convert more. Headlong's card authors no Time row, so nothing moves at rest; the comeback reaches
it at a 15-gate deficit (§4). Still wants a playtest (`BACKLOG.md` 5.2). Unchanged by this packet.

---

## 4. The comeback check

`ElementalComebackSystem` raises ALL FOUR elements by `deficit x ComebackRatePerScoreDeficit`
(`ElementalComebackSystem.cs:267-296`), capped so the layer never lifts an element past 10, and
the qualitative upgrade unlocks at effective level >= `UnlockLevel` 5 through the replicated
`NetElementUnlocks` bits (`R_VesselElementalAbilityHandler.cs:98-101, :169`). So each L5 is, in
practice, a rule handed to the trailing domain at:

| Mode | Rate (card) | Deficit that unlocks every L5 | Plausible? |
|---|---|---|---|
| Astro League | 1 / goal (`ArcadeGameAstroLeague.asset`) | **5 goals** behind | in a blowout, yes |
| Cleave | 0.0125 / prism (`ArcadeGameCleave.asset`) | **400 prisms** behind (a third of the 1200 target) | yes, routinely |
| Headlong | 0.35 / gate (`ArcadeGameHeadlong.asset`) | **15 gates** behind of 24 | almost never (nearly two laps) |

Against the two recorded traps (Wildlife Liberation: a core promise gated behind a level the
comeback hands the loser; The Bends: an upgrade that became a hard counter to the only way you
could be scored on):

| L5 | Is it a hard counter to the mode's scoring event? | Does it gate a core promise? | Verdict |
|---|---|---|---|
| Bright Edge | No. Astro League scores goals, Cleave scores prisms, Headlong scores gates; a wider flourish counters none of them. The energize pop (the one gated act) stays gated on the real ritual | No; the base combo set is ungated | passes |
| Ramp Ward | No scoring event in any of the three modes is a danger-prism debuff. In Cleave it removes the arena's ONLY punishment for a Rhino (1.2) while boosting, for whoever is 400 prisms behind. That is a comeback lever, not a counter; but it is the one row where "trailing pilot becomes unpunishable" is literally true in a window. Scoped to `DangerPrism` so a rival's Strike or blast still lands (the Bends lesson, applied up front) | No | passes, **with the Cleave window named for the designer** |
| Clean Edge | No. It restricts the holder, it does not deny a rival | It gates ordinary cutting by domain, which collides with the v3 ruling (§2b) | passes the comeback test; fails the ruling test until design rules |
| Keen Strike (if Charge took the steal) | No. More petals per Strike is the Squirrel's shipped Charge; a Strike still needs to be faster | No | passes |
| Hot Edge (scaling, not L5, but the comeback reaches it too) | A trailing Cleave domain at Charge 5+ banks ~2x energy, so its blades are long sooner. Bounded at `maxScale 120` and by the per-impact `debrisSpeedLimit`; it accelerates catch-up on the thing the mode scores, which is what the comeback is for | No | passes; playtest the endpoint |

Nothing proposed makes a trailing Rhino unbeatable in Astro League: no option touches the ball's
physics except through the lever arm the mode already prices, and none wards a rival's contact.

---

## 5. The recommendation (one)

**Ship, in this order, if accepted:**

1. **Space = Reach**, declared. Zero risk, zero code, zero mode change: it writes down what every
   Rhino has been doing. It also closes a reporting defect (two surfaces, the lockup card and the
   launch panel's controls block, currently tell the pilot Space does nothing on this hull).
2. **Charge = Hot Edge**, x1 -> x2.5 floored x0.5 on the energy bank, with **Bright Edge** as its
   level-5. It is the Rhino's own loop (kills -> energy -> length -> tip speed -> kills), it reaches
   humans and AI alike, it has a consequence in all three modes and the largest in the mode whose
   score IS the loop, and it costs one field and three reads. Alternative if the designer wants
   zero C# on this branch: **Keen Strike** (the Squirrel's Charge row on the Rhino's existing steal
   asset), accepting that it is near-inert in Cleave and Headlong.
3. **Time 5 = Ramp Ward**, zero code, with the Cleave window stated above. If the designer reads
   "trap-immune while boosting" as too much for the trailing pilot, leave Time's L5 open; Broadside
   filled the row deliberately without one and nothing is worse for waiting.
4. **Mass 5 stays open** until BACKLOG 27's fork (does growth close the gap or open it?) is
   settled; Armored Slabs is the right shape once it is, and is new code either way.
5. **Space 5 stays open.** Clean Edge is the only existing shape and it needs a ruling on the
   ungated-cutting law before it is a proposal at all.

Why not four invented L5s: the prompt's own rule. Two of the four rows have a real level-5 built
from a shipped primitive; the other two do not, and the honest table says so.

---

## 6. How the designer answers

Tick one per row, edit in place, and hand it back. Everything below the ticks is the authoring task
a session then runs; nothing in it is started until the ticks exist.

```
[ ] Space row: declare "Reach" (30 -> 50, Input 1 RT)        accept / edit label / reject
[ ] Charge row: Hot Edge x1 -> x2.5 floor x0.5                 accept / edit endpoints / take Keen Strike instead / reject
[ ] Charge L5: Bright Edge                                    accept / reject (row then ships with no L5, like Time)
[ ] Time L5: Ramp Ward (WhileBoosting, DangerPrism only)      accept / reject (leave open)
[ ] Mass L5: leave open pending BACKLOG 27                    agree / rule the GapWeight fork now: growth CLOSES the gap / OPENS it
[ ] Space L5: leave open                                      agree / rule that a domain skip on cutting is allowed, then Clean Edge
[ ] Trail Slabs stays on the straight-line gesture (§8)      agree / bind to a button: __
```

### 6.1 The authoring task that follows (what a session does with the ticks)

The HOME rule, restated because it is the one thing the prompt's own table got wrong by age:
**a number lives on the asset or component that OWNS it, as an `ElementalFloat` read through
`EvaluateLive(status)` (or `EvaluateReplicated` where the outcome must agree across peers); the
map asset carries the qualitative half only** (label, description, input, unlock/relock, the L5's
name and prose). Never author a scaling field into `Rhino.asset`
(`ELEMENT_SCALING_UNIFICATION.md`).

| Step | Row | Where the edit lands | What |
|---|---|---|---|
| 1 | Space | `Assets/Resources/ElementalAbilityMaps/Rhino.asset`, entry `Element: 3` | `AbilityLabel: Reach`; `AbilityDescription` naming `Skimmer.Scale` on `ForceFieldSkimmer Variant.prefab` 30 -> 50 and `ShieldSkimmerScaleDriver.BaseScale` as the reader; `Input: 1`. **No number on the map.** The scaling is already home |
| 2 | Charge | `RhinoSkimmerDamagePrismEffectSO.cs` + `RhinoSkimmerDamagePrismEffect.asset` | add `[SerializeField] ElementalFloat chargeEnergyMultiplier = ElementalFloat.Multiplier(1f, 2.5f, Element.Charge, 0.5f)`; multiply at `:147` and `:171` via `EvaluateLive(status)`; author the block on the asset; map entry `Element: 1` gets `AbilityLabel: Hot Edge`, `Input: 0`, prose naming the field. (Keen Strike instead: author `stealScale` on `RhinoSwordStealBySkimmerEffect.asset` only) |
| 3 | Charge L5 | `ShieldSwipeActionExecutor.cs:223-224` | library pick on `energized || handler.IsUpgradeActive(Element.Charge)`; keep the FX/pop semantics on the real `IsEnergized`; map `UpgradeLabel: Bright Edge` + `UpgradeDescription` |
| 4 | Time L5 | `Rhino.prefab` root | add `VesselElementalImmunity` (`condition 1 WhileBoosting`, `upgradeGate 4 Time`, `wardedSources 1 DangerPrism`); map `UpgradeLabel: Ramp Ward` on entry `Element: 4` |
| 5 | HUD | `FrogletTools > Vessels > Wire Vessel Ability Row` (`VesselAbilityRowWirer`), then **Audit Vessel Ability Rows** | the Rhino already has four icon objects (`LaserTargeting`, `Crystal`, `ForceField` in the vessel prefab, `BoostContainer` in the HUD variant; `BACKLOG.md` "Wiring, once the maps land" item 2). Gauges: Charge <- `ShieldSkimmerScaleDriver.OnScaleChanged` (the energy meter the HUD already reads), Time <- `RampBoostActionExecutor.Straightness01` (published, read by nothing), Space <- the blade length, Mass <- `GrowTrailActionExecutor.CurrentScale` |
| 6 | Gates | terminal | `python3 Tools/Build/element_ability_table.py Rhino` (Charge and Space rows must name their abilities; each accepted L5 must print a `gated at` line; the Space row must show `Scale 30 -> 50`), `python3 Tools/Build/check_elemental_floats.py` (the new SO-hosted float must be evaluated), `python3 Tools/Build/peer_press_harness/run.py --self-test`, `bash Tools/Build/unity_refcompile/run.sh`, then `/verify-unity` before any commit (CLAUDE.md, LOCKED) |
| 7 | Paper trail | `FLEET_MAPS.md` §1 coverage + §2 Rhino -> APPROVED + SHIPPED; `FLEET_GAPS.md` §1; `ARCHITECTURE.md` §7.2; `BACKLOG.md` 5.10; CLAUDE.md's fleet table; `STEAM_RELEASE_TASKS.md` R10; `RHINO_ENERGY_SWORD.md` tuning knobs (the new field); `HEADLONG.md` §9 if Ramp Ward lands | the Dolphin branch updated FLEET_MAPS and not CLAUDE.md; do not repeat that |
| 8 | Playtest | `Docs/UNITY_VERIFICATION_CHECKLIST.md` | Hot Edge's endpoint in Cleave (destruction rate at Charge 10 vs rest); Ramp Ward's window on the Panes; `BACKLOG.md` 5.2 (Ramp Spool's lost ceiling term) is still open and is the same session |

---

## 7. What this does not decide

- **Whether the Rhino's Trail Slabs ever grow.** BACKLOG 27 is a design fork (`GapWeight` sign),
  and either answer re-prices Cleave's per-rung volume ladder and `PhaseThresholds` against a grown
  slab (`CLEAVE.md` "Stated cost", CLAUDE.md "a cell whose prisms are not nominal must author its
  volume ladder"). The Mass row's number is correct and reaches nothing until then.
- **Hot Edge's endpoints.** x2.5 is the fleet's Charge convention, traceable to the Squirrel and the
  Urchin, not a Cleave measurement. The playtest decides the number; the packet decides the dial.
- **The Clean Edge ruling.** Is a same-domain skip on damage a "gate on ordinary cutting" in the
  sense the v3 energy-sword ruling forbids? The energy-sword doc's own guidance (skip the bank,
  never the damage) says yes. Only Garrett can say otherwise.
- **Sword replication.** Energy, energize phase and the blade's look are local-authoritative
  (`RHINO_ENERGY_SWORD.md` follow-ups). Bright Edge improves on that (the Charge bit is replicated,
  the local `IsEnergized` is not) but does not fix it.
- **The Rhino's omni-crystal vessel blast.** `RhinoVesselExplosionByCrystalEffect.asset` spawns
  `AOESlowExplosion.prefab`, whose container is empty on both lists (`BACKLOG.md` "Elemental economy
  follow-ups"). The Charge and Space channels the tool finds on it are therefore inert twice over.
  Whether that blast should do anything again is a Broadside pricing decision, not a map decision.
- **The Rhino's danger-prism slow.** The hull container carries no `VesselChangeSpeedByPrismEffectSO`
  (`BACKLOG.md` "Rhino follow-ups"). Ramp Ward's Cleave consequence is as large as it is BECAUSE of
  that gap; closing the gap would shrink the ward's effect without touching the ward.
- **Headlong's AI distances, the mode's "boost held" readout, and the 2026-09-25 retune's
  playtest.** All recorded in `HEADLONG.md` §9 and `RHINO_RAMP_BOOST.md`; none touched here.
- **`STEAM_RELEASE_TASKS.md` R10.** The prompt's definition of done moves it to "awaiting design".
  This packet is the artifact that move points at; the row itself is not edited here (it also still
  says "1 of 4", which 0.3 corrects). Move it when the ticks come back.
- **`FLEET_MAPS.md` §2.** The prompt asked for the proposal to live there. The Rhino section there
  is kept as the dated record it is; one pointer line under its table names this file. Folding this
  packet into that table is the authoring task's step 7, after the ticks.
- **The `/element-ability-table` text renderer** printing nothing under an OPEN row while the JSON
  carries the channels (0.2). A tool note; not fixed here (no C# or Python was touched).

---

## 8. Trail Slabs: passive scalar or bound action? (the prompt's sixth item)

Today the slab is bound to `FullSpeedStraightAction` (event 0) beside the ramp, i.e. to the
straight-line GESTURE, not to a button, and it is dead (0.4). The question is where it should live
once it works.

**Recommendation: keep it on the gesture.** The slab is the ramp's wake: the Rhino's whole identity
is holding the line, the ramp pays speed for it, and the slab paying MASS for the same act is one
rule with two rewards rather than two abilities. It also gives the Mass card a readout for free
(`GrowTrailActionExecutor.CurrentScale`) that tracks the same `Straightness01` the Time card would
show. And it is the only binding an AI Rhino can use: the AI holds a straight line constantly and
never presses a button it is not given.

If design wants it on a button instead, these events are raised by every strategy and bound by
nothing on `Rhino.prefab`: `Button1Action 6` (X), `Button2Action 7` (A), `Button3Action 8` (B),
`FlipAction 3`, `IdleAction 4`, and `MinimumSpeedStraightAction 5` (the mirror of the ramp's
gesture, raised by all six strategies and bound by nothing in the fleet; `RHINO_RAMP_BOOST.md`
follow-ups call it "the natural home for a Rhino brake/anchor"). The triggers (events 1 and 2) are
the sword's and are not free. Either way the AI entry needs a `Duration` > 0 or the grow ends the
frame it starts (0.4).
