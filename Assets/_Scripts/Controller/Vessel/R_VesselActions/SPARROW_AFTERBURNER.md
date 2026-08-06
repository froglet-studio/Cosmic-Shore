# Sparrow — Afterburner (TIME): indefinite boost, base strafing roll, elemental ward

**Status:** SHIPPED. Replaces the Sparrow's "Overheat Boost".

The Sparrow's TIME ability used to be a boost you had to ration: holding it built **heat** in a
resource, hitting the ceiling **overheated** you — force-releasing the boost, turning your own trail
into a danger trail for 7 s and slamming you if you flew back through it. The strafing roll was
locked behind TIME level 5.

That is gone. The boost is now **indefinite**, the **strafing roll is base kit**, and TIME level 5
buys something the meter used to occupy the icon for: **immunity to elemental debuffs while
boosting**.

| | before | after |
|---|---|---|
| Boost duration | heat-limited (~10 s to overheat, 7 s lockout) | unlimited, hold as long as you like |
| Strafing roll | TIME level-5 unlock | **base** — no unlock, level 0 onward |
| TIME quantitative | boost speed (×1.5 at level 10) | **unchanged** — boost speed (×1.5 at level 10) |
| TIME level-5 upgrade | Barrel Roll | **Elemental Ward** — elemental debuffs cannot land while boosting |
| Boost icon | radial heat gauge | binary **roll-charge** pip: full = roll available, empty = spent |
| Self-danger trail | overheating made your trail dangerous | none (`EnableDangerMode` survives, now uncalled — see Follow-ups) |

## 1. Elemental debuff immunity is a PLATFORM state, not a Sparrow feature

The ask was explicit: the immunity "should be a general state that anyone can leverage", and the
Serpent should hold it while stopped. So it lives in the Elementals fundamental, not in the Sparrow.

Every buff and debuff in the game routes through `ResourceSystem.ApplyElementalEffect`
(CLAUDE.md ▸ Elementals). **One gate there is the whole mechanic:**

```csharp
// ResourceSystem.ApplyElementalEffect
if (magnitude < 0f && IsElementallyImmune) return;
```

Three pieces, all vessel-agnostic:

| Piece | Where | What it is |
|---|---|---|
| The state | `ResourceSystem.IsElementallyImmune` + `SetElementalDebuffImmunity(source, immune)` + `OnElementalImmunityChanged` | Source-keyed grants (a `HashSet`), so two concurrent holders can't clear each other. Immune while any grant stands. |
| The read | `IVesselStatus.IsElementallyImmune` | Convenience accessor for HUD / VFX / gameplay, alongside `IsSlowed` / `IsBoosting`. |
| The driver | `VesselElementalImmunity` (vessel root) | Declarative: pick a `Condition` (`Always` / `WhileBoosting` / `WhileTranslationRestricted`) and an optional `upgradeGate` element. |

Wired today:

| Vessel | Condition | Upgrade gate |
|---|---|---|
| Sparrow | `WhileBoosting` | `Time` (level 5 — "Elemental Ward") |
| Serpent | `WhileTranslationRestricted` (stopped to weave) | `None` — ungated, stopping is the whole cost |

**Scope, deliberately narrow:**
- Blocks **negative** magnitudes only. Buffs still land while immune.
- **Prevents**, never **cleanses**. A debuff already ticking keeps decaying on its own — otherwise
  the state would be a spammable purge (tap boost to wipe a debuff) rather than a shield.
- **Not** gated: `AdjustLevel`. That is the persistent crystal/comeback progression writer, not the
  debuff channel — collecting a crystal is a player action, not something to be immune to.
- What it therefore covers today: `VesselElementalDebuffByDangerPrismEffectSO` (the all-element
  danger-prism debuff) and `VesselOvertakeBySkimmerEffectSO`'s debuff direction. It does **not**
  cover the non-elemental danger-prism punishments — the speed slam
  (`VesselChangeSpeedByPrismEffectSO`) and the Rhino's input mute
  (`SparrowDebuffByRhinoDangerPrismEffectSO`) still land. Danger prisms still hurt; they just can't
  drain your elements.

The upgrade gate resolves through `IsUpgradeActive(Element.Time)` — the **replicated**
`NetElementUnlocks` bits, never a raw local level read, so every peer agrees on who is warded.
AI reaches the identical component with its own `IVesselStatus`, so an AI Sparrow at Time 5 is
warded too, with nothing extra wired.

## 2. The strafing roll is base kit

`BarrelRollController` lost exactly one line — the `IsUpgradeActive(Element.Time)` gate. Everything
else about it is unchanged (perimeter detection on the left stick, CW right half / CCW left half,
visual 360° on the model child, small real root bank, `ModifyVelocity` orthogonal displacement,
travel-aligned bridging prisms via `BlockRotationOverride`).

It stays **one roll per boost press**: a fresh `IsBoosting` false→true edge arms a roll, triggering
consumes it, and holding the stick at the perimeter never repeats. With an indefinite boost that
once-per-press rule is what keeps the roll from becoming a continuous barrel spin — so it is now the
thing the icon has to display.

New surface for the HUD (and nothing else):

```csharp
public event Action<bool> OnRollChargeChanged;   // true = armed, false = spent
public bool IsRollArmed { get; }                 // for the HUD's initial seed
```

`OnDisable` clears the charge, so a pooled / swapped vessel can't inherit a stale armed state.

## 3. The boost icon: gauge out, charge pip in

The TIME ability icon (`OverheatButton` in `SparrowHUDVariant.prefab`) carried a radial fill driven
by heat. There is no heat left to show, so the same ring (`Holder/OverheatCounter`) is repurposed as
a **binary charge pip** for the roll:

| Roll state | Ring |
|---|---|
| Armed (a roll is available on this press) | fill 1, `rollArmedColor` |
| Spent (rolled; next boost press re-arms) | wipes to fill 0, `rollSpentColor`, one scale punch |

It is not a gauge: it only ever holds 0 or 1 and the transition between them is a wipe, not a
readout. The ring is a *sibling* of the ability icon, never the icon itself — so the four-icon
contract is untouched (`tintIconOnUpgrade` stays on, the "Elemental Ward" upgrade still tints the
Time icon and blooms its Time petal badge), and rule 9 of the vessel contract does not apply.

`SparrowHUDController` swapped `overheatingExecutor` for `barrelRollController` and lost its
`Update` poll entirely — the roll charge is evented, so the HUD does no per-frame work for it.

## Files

| File | Change |
|---|---|
| `_Scripts/Controller/Vessel/ResourceSystem.cs` | **+** the general immunity state (grants, `IsElementallyImmune`, `OnElementalImmunityChanged`) and the one gate in `ApplyElementalEffect` |
| `_Scripts/Controller/Vessel/IVesselStatus.cs` | **+** `IsElementallyImmune`; **−** the now-dead `IsOverheating` |
| `_Scripts/Controller/Vessel/VesselElementalImmunity.cs` | **NEW** — the shared declarative driver |
| `_Scripts/Controller/Vessel/VesselStatus.cs` | **−** `IsOverheating` (declaration + `ResetForPlay`) |
| `_Scripts/Controller/Vessel/BarrelRollController.cs` | **−** the Time-upgrade gate; **+** `OnRollChargeChanged` / `IsRollArmed`; charge cleared on disable |
| `_Scripts/UI/View/SparrowHUDView.cs` | `SetBoostState(heat, overheated)` → `SetRollCharge(armed)`; `boostFill` → `rollChargeIndicator` |
| `_Scripts/UI/Controller/SparrowHUDController.cs` | overheat executor → barrel-roll controller; `Update` poll removed; symmetric detach-first Subscribe/Unsubscribe |
| `_Scripts/…/Data Containers/SquirrelVesselHUDController.cs` | **−** its `OverheatingActionExecutor` lookup (see Follow-ups — it was always null) |
| `_Scripts/…/Data Containers/OverheatingActionSO.cs` | **DELETED** |
| `_Scripts/…/Executors/OverheatingActionExecutor.cs` | **DELETED** |
| `_Scripts/Controller/Vessel/VesselActions/OverheatingAction.cs` | **DELETED** (legacy, unreferenced) |
| `_SO_Assets/VesselActions/Sparrow/OverheatingAction.asset` | **DELETED** |
| `_Prefabs/Spacevessels/Sparrow.prefab` | Input 7 + AI ability → `BoostAction.asset`; `OverheatingBoostActionExecutor` GameObject removed (with its dead `TrailScaleModulator`); dead `Heat` resource removed; HUD controller rebound; **+** `VesselElementalImmunity` |
| `_Prefabs/Spacevessels/Serpent.prefab` | **+** `VesselElementalImmunity` (`WhileTranslationRestricted`, ungated) |
| `_Prefabs/UI Elements/VesselHUD/SparrowHUDVariant.prefab` | view fields → roll-charge names/colours |
| `Resources/ElementalAbilityMaps/Sparrow.asset` | TIME entry re-authored: "Afterburner" / "Elemental Ward" |

## Tuning knobs

| Knob | Where | Default | Notes |
|---|---|---|---|
| Boost speed at Time 10 | `Sparrow.asset` → Time `MultiplierAtFullLevel` | `1.5` | Consumed by `VesselTransformer.CurrentBoostAmount()`. Unchanged from before — the boost is longer now, so this is the first number to revisit if the Sparrow outruns the fleet. |
| Boost speed at Time −5 | `Sparrow.asset` → Time `MinMultiplier` | `0.5` | |
| Ward unlock level | `Sparrow.asset` → Time `UnlockLevel` / `RelockBelowLevel` | `5` / `4` | |
| Immunity window | `Sparrow.prefab` → `VesselElementalImmunity.condition` | `WhileBoosting` | `Always` makes the ward passive at Time 5 — one field, no code. |
| Immunity gate | `Serpent.prefab` → `VesselElementalImmunity.upgradeGate` | `None` | Set an element to make the Serpent's stopped ward an earned upgrade. |
| Roll trigger threshold | `Sparrow.prefab` → `BarrelRollController.perimeterThreshold` | `1` | |
| Roll displacement | `…nudgeSpeed` / `rollDurationSeconds` / `rootRollDegrees` | `60` / `0.6` / `15` | |
| Roll pip colours + wipe | `SparrowHUDVariant.prefab` → `rollArmedColor` / `rollSpentColor` / `rollChargeTweenDuration` / `rollSpendPunchScale` | cyan / dim grey / `0.15` / `0.3` | |

## In-editor verification

Not editor-verified — I cannot run Unity. Every step below is unrun. Mirrored in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`.

1. **Compile + console.** Open the project. Expect zero errors and no `[VesselHUDView]` warnings
   about the Sparrow's ability row. (The Sparrow's `ElementalBarsController.view` reference is
   dangling **on `bleeding-edge` already** — `fileID 7416581124810081342` resolves to nothing — so a
   missing-bars fallback warning there is pre-existing, not from this branch.)
2. **Indefinite boost.** `MinigameFreestyleMultiplayer_Gameplay` (or Menu_Main freestyle), Sparrow.
   Hold boost for 60 s: speed stays elevated, no force-release, trail never turns into danger
   prisms, and flying back through your own trail does not slam you.
3. **Roll is base kit.** With Time at level 0 (do not collect Time crystals), boost + hold the left
   stick at full deflection → the vessel rolls once and strafes. Release boost, press again → one
   more roll. Holding the stick at the perimeter through a single press → exactly one roll.
4. **The icon.** Watch the boost (4th, rightmost) ability icon's ring: full/cyan when you press
   boost, wipes empty with a punch the instant you roll, stays empty until the next press. No
   partial fills at any point — if you see the ring at a fraction that is not 0 or 1 outside a
   0.15 s transition, the wire is wrong.
5. **Ward, locked.** Time below 5, boost, fly into a danger prism (a Rhino's, or Ribcage traps):
   all four element flowers dip and recover over ~4 s.
6. **Ward, unlocked.** Raise Time to 5 (`ResourceSystem.TimeTestHarness = 0.5` on the vessel, or
   collect Time crystals) — the Time icon tints and grows its white Time petal badge. Now:
   - **while boosting**, hit the same danger prism → flowers do **not** dip. You are still slowed
     and still take the input mute; only the elemental drain is denied.
   - **not boosting**, hit it → flowers dip normally.
7. **Serpent, ungated.** Serpent in the same scene, Time at any level. Stopped (turret/weave
   stance) + danger prism → no flower dip. Moving → normal dip.
8. **No stuck immunity.** Boost into a vessel swap / turn end while immune, then take a danger hit
   while not boosting → flowers dip. (`VesselElementalImmunity.OnDisable` revokes; the
   `RemoveWhere(!o)` prune in `ResourceSystem` is the backstop.)
9. **MPPM, two clients.** Both on Sparrows, one at Time 5, one below. Confirm on **both** machines
   that only the Time-5 pilot resists the elemental drain while boosting — this is what the
   replicated `NetElementUnlocks` path buys, and a local-level read would pass step 6 and fail here.
10. **Audit.** `FrogletTools > Vessels > Audit Vessel Ability Rows` — the Sparrow row must still
    report 4/4 icons in charge → mass → space → time order.

## Follow-ups

- **`VesselPrismController.EnableDangerMode` / `DisableDangerMode` now have no callers.** The
  overheat executor was the only one. **Keep them** — `FLEET_MAPS.md` §2 proposes the Serpent's
  "Venom Wake" L5 as exactly this machinery reused ("boost trail becomes a danger trail"). This is
  a capability waiting for a caller, not dead code to delete.
- **`SquirrelVesselHUDView.SetOverheatHeat` / `JuiceOverheatEngaged` / `JuiceOverheatRecovered` are
  now uncalled.** They already never fired: `SquirrelVesselHUDController` looked up
  `OverheatingActionExecutor` via `GetComponentInChildren`, and that component only ever existed on
  `Sparrow.prefab`, so the reference was null on every Squirrel and the gauge never moved. Left in
  place (with the serialized fields on `SquirrelHUDVariant.prefab`) rather than ripped out on a
  Sparrow branch; retire them, or give them a real Squirrel meter, in a Squirrel pass.
- **`TrailScaleModulator`** lost its only scene instance with the executor GameObject. The class
  already had **zero** callers of `Apply()`/`Revert()` before this branch, so it was inert either
  way. The script file is left on disk; delete it in a general dead-code sweep.
- **AI never rolls.** `BarrelRollController` polls stick input, and autopilot vessels produce none —
  so the roll is inert for AI. Unchanged by this branch; trigger synthesis is
  `Docs/ElementalAbilitySystem/BACKLOG.md` Phase 2.5.
- **Balance to watch.** Boost is now unbounded in duration, which makes the TIME quantitative
  (`MultiplierAtFullLevel 1.5`) load-bearing in a way it was not when heat capped the hold. If the
  Sparrow reads as too fast for too long, that number and `VesselTransformer.MaxBoostMultiplier` are
  the levers — not a reintroduced meter.
