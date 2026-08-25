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
if (magnitude < 0f && IsImmuneTo(source)) return;
```

`source` is the debuff's **class** (`ElementalDebuffSources`: `DangerPrism` / `Explosion` /
`VesselContact` / `Other`) and a grant holds a **mask** of the classes it wards. That second
dimension arrived with the Dolphin's Drift Ward (§1.1) — the state was a bare bool until a ward
needed to promise less than "nothing can debuff me".

Three pieces, all vessel-agnostic:

| Piece | Where | What it is |
|---|---|---|
| The classes | `ElementalDebuffSources` (`[Flags]`, `CosmicShore.Data`) | What applied the debuff: `DangerPrism` / `Explosion` / `VesselContact` / `Other` (the bucket for a call that names nothing) / `All` = `~0`. A debuff names ONE; a grant holds a MASK; blocked iff they overlap. |
| The state | `ResourceSystem.ImmuneDebuffSources` + `IsImmuneTo(source)` + `SetElementalDebuffImmunity(source, immune, wardedSources)` + `OnElementalImmunityChanged` | Grantor-keyed grants (`Dictionary<Object, ElementalDebuffSources>`), so two concurrent holders can't clear each other. Warded against the union of every standing grant's mask. |
| The read | `IVesselStatus.IsImmuneToElementalDebuff(source)` (+ `ImmuneDebuffSources` for HUD / VFX) | There is deliberately **no bare `IsElementallyImmune` bool** any more — a caller that reads "immune" and assumes *total* immunity is wrong for the Dolphin and wrong silently, so every reader must name the class it cares about. |
| The driver | `VesselElementalImmunity` (vessel root) | Declarative: pick a `Condition` (`Always` / `WhileBoosting` / `WhileTranslationRestricted` / `WhileDrifting`), an optional `upgradeGate` element, and the `wardedSources` mask. |

Wired today:

| Vessel | Condition | Upgrade gate | Wards (`wardedSources`) |
|---|---|---|---|
| Sparrow | `WhileBoosting` | `Time` (level 5 — "Elemental Ward") | `All` (`-1`) |
| Serpent | `WhileTranslationRestricted` (stopped to weave) | `None` — ungated, stopping is the whole cost | `All` (`-1`) |
| Dolphin | `WhileDrifting` | `Time` (level 5 — "Drift Ward") | **`DangerPrism` only** (`1`) — see §1.1 |

**Scope, deliberately narrow:**
- Blocks **negative** magnitudes only. Buffs still land while immune.
- **Prevents**, never **cleanses**. A debuff already ticking keeps decaying on its own — otherwise
  the state would be a spammable purge (tap boost to wipe a debuff) rather than a shield.
- **Not** gated: `AdjustLevel`. That is the persistent crystal/comeback progression writer, not the
  debuff channel — collecting a crystal is a player action, not something to be immune to.
- **Scoped by class.** The three classed debuffs today are
  `VesselElementalDebuffByDangerPrismEffectSO` → `DangerPrism`,
  `VesselElementalDebuffByExplosionEffectSO` → `Explosion`, and
  `VesselOvertakeBySkimmerEffectSO`'s debuff direction → `VesselContact`. An `All` ward covers all
  three plus anything added later; the Dolphin's covers the first alone.
- It does **not** cover the non-elemental danger-prism punishments — the speed slam
  (`VesselChangeSpeedByPrismEffectSO`) and the Rhino's input mute
  (`SparrowDebuffByRhinoDangerPrismEffectSO`) still land. Danger prisms still hurt; they just can't
  drain your elements.

The upgrade gate resolves through `IsUpgradeActive(Element.Time)` — the **replicated**
`NetElementUnlocks` bits, never a raw local level read, so every peer agrees on who is warded.
AI reaches the identical component with its own `IVesselStatus`, so an AI Sparrow at Time 5 is
warded too, with nothing extra wired.

### 1.1 A ward has a SCOPE, because "immune" is not one promise

The state shipped as a bare bool, which was right while its only two holders wanted total
immunity. The Dolphin's Time-5 **Drift Ward** broke that: it was authored to answer *danger
prisms* — the drift is a manoeuvre through your own hazardous arena — and an unscoped grant made
it answer everything, including `VesselElementalDebuffByExplosionEffectSO`. That effect is the
Dolphin crystal blast's debuff, which is **the entire scoring event of The Bends**, a mode in
which every pilot is a Dolphin. The interaction was worse than a coincidence: `Bends`'
comeback buff hands the *trailing* pilot Time 5, so falling behind bought a hard counter to the
only way you could be scored on, and `VesselCombatHitByExplosionEffectSO.requireDebuffableVictim`
(correctly) then scored the attacker nothing either. `BENDS.md` shipped it as that branch's top
open risk with three ugly levers, one of which was "gate the ward out of this mode — no
mechanism exists for that today".

The fix is not a mode gate. It is that the **debuff channel now carries its source class**, so a
ward states what it wards. Danger prisms are terrain; a blast is a weapon another pilot aimed;
an overtake is a duel. An ability earned against one of those has no business cancelling the
others, and now cannot be authored to by accident: narrowing is one inspector mask, and the
default (`All`) is what every existing grant already meant.

Two properties worth keeping:

- **`All` is `~0`, not the OR of today's members.** It is serialized on prefabs, so an
  "everything" ward authored today must keep covering a class added tomorrow.
- **An unclassified debuff lands in `Other`**, which only an `All` ward blocks. So adding a
  source class can never silently *widen* a narrow ward, and forgetting to classify a new debuff
  fails in the safe direction (it still lands on the Dolphin).

## 2. The strafing roll is base kit

`BarrelRollController` lost exactly one line — the `IsUpgradeActive(Element.Time)` gate. Everything
else about it is unchanged (perimeter detection on the left stick, CW right half / CCW left half,
visual 360° on the model child, small real root bank, `ModifyVelocity` orthogonal displacement,
travel-aligned bridging prisms via `BlockRotationOverride`).

It stays **one roll per boost press**: a fresh `IsBoosting` false→true edge arms a roll, triggering
consumes it, and holding the stick at the perimeter never repeats. With an indefinite boost that
once-per-press rule is what keeps the roll from becoming a continuous barrel spin — so it is now the
thing the icon has to display.

### 2.0 The arm is a WINDOW, not the whole boost hold

**A press arms the roll for `rollArmWindowSeconds` (0.3 s), not for the press's whole lifetime.**
Reach the perimeter inside that window and you roll; let it lapse and the charge is spent unfired —
the boost keeps running, but the next roll needs another press.

Once-per-press was the right rule and the wrong *duration*, and making the boost indefinite is what
exposed it. `IsBoosting` stays true for as long as the trigger is held, so an arm that lived for the
press lived for the whole flight: minutes later, the first moment the stick happened to touch full
deflection — an ordinary hard turn — cashed the charge and spun the vessel. Nothing about that read
as an ability; it read as the ship losing composure at random. The roll now only answers a
**deliberate press-then-slam**, which is what a pilot doing it on purpose does anyway.

What is deliberately unchanged: a stick already pinned at max when boost starts still rolls
immediately (the window opens the same frame it is tested), holding the stick at the perimeter still
never repeats, and the AI is still inert (it produces no stick input). `0` disables the window and
restores the armed-for-the-whole-press behaviour, which is the escape hatch, not a mode.

The window is why the charge now has **three** states rather than two
(`CosmicShore.Data.RollChargeState`: `Spent` / `Armed` / `Lapsed`). Every boost press now ends in a
charge change, so "no roll available" had to split by cause: the pip empties for both, and only a
real `Spent` earns the consume punch — a punch on a lapse would be the HUD announcing a roll the
pilot never got.

### 2.0.1 The root roll: the ability owns the roll axis for its duration

The roll has always applied a small **real** roll to the vessel root alongside the visual 360°
(`rootRollDegrees`, 15°) — the up vector rotated about the vessel's own forward, which is exactly
what the two-stick `VesselTransformer.Roll()` does with a little `YDiff`. Since the camera reads the
**root's** rotation, that is the horizon tilt the pilot actually feels while the model spins.

**It was landing and reading backwards, and the arithmetic says why.** The bank-into-turn and the
root roll are the same rotation about the same axis, so they simply add — and the roll's trigger is
a **full stick deflection**, i.e. precisely when the bank is at its maximum:

| | rate | over the 0.6 s roll |
|---|---|---|
| `SingleStickVesselTransformer.Roll()` at full stick, cruise (speed 35) | `35 × 0.1 + 30` = **33.5 °/s** | **20.1°** |
| …boosting (speed 110 = `25 × 4 + 10`) | `110 × 0.1 + 30` = **41.0 °/s** | **24.6°** |
| `rootRollDegrees`, signed to match the animation | 25 °/s | **15°** |

The bank is signed `-stick.x` and the roll `+rollSign` (`= +1` for `stick.x ≥ 0`), so they are
**opposite**: the net was ~5–10° of bank *into the turn*, and the pilot saw the horizon tilt the
wrong way by a wide margin. The authored 15° described nothing that happened on screen — the
`§4a` "the authored number is not the effective one" trap, reached through vector addition.

**The fix is a handover, not a bigger number.** `VesselTransformer.BankIntoTurnSuppressed` (default
false, cleared by `ResetTransformer`) suspends the bank for the roll's duration, so the 15° is the
whole roll the vessel gets and the authored number is honest at any speed and whether or not the
pilot keeps holding the stick. **Pitch and yaw are untouched** — the vessel still turns exactly as
hard while it rolls; only the cosmetic bank stands down. It is the same "an ability owns one
transformer property for its duration" shape the roll already uses for `BlockRotationOverride`, and
it is cleared in the routine's tail *and* in `OnDisable`.

**It is honoured in all three `Roll()` bodies** — the base, `SingleStickVesselTransformer` and
`ScarabVesselTransformer` — because the overrides do not call base. `GunVesselTransformer` inherits
the base body and is covered for free. A base-only gate would have reached **neither** the Sparrow
nor the Serpent, which is the same trap `TurnScalar` already documents in that file.

The roll is also now advanced by the **delta of the animation's own smoothstep** rather than a flat
`dt / duration`, so the tilt accelerates and settles *with* the spin instead of drifting across it —
and the authored degrees land exactly (summing `dt / duration` overshoots on the frame that ends the
loop).

### 2.1 It works identically in the stationary (turret) stance

Stopped — `IsTranslationRestricted`, from `ToggleStationaryModeAction` — the boost gives no speed:
`VesselTransformer.Update` returns before throttle and course travel, so pressing boost changes
nothing about how fast you are going. **The roll is unaffected by that.** It arms on the same
false→true `IsBoosting` edge, triggers on the same full stick deflection, spends the same charge
pip, and **strafes the same distance**. Stopped, that displacement is the Sparrow's only dodge.

Making the displacement survive the restriction is a deliberate, narrow carve-out rather than a
blanket one:

| Piece | Where | What it does |
|---|---|---|
| The opt-in | `ShipVelocityModifier.ignoresTranslationRestriction` | Per-modifier flag, default **false**. Only the roll sets it — every other `ModifyVelocity` caller uses the unchanged 2-arg overload and stays fully held while restricted (knockback, bounce, deviation, spin, AstroLeague, NudgeShard, `ModifyVelocityActionSO`). |
| The application | `VesselTransformer.MoveRestricted` | Restricted position update: `position += velocityShift * dt`. No throttle, no course term. Deliberately does **not** write `VesselStatus.Speed` or `Course`, so nothing downstream (gun velocity inheritance, telemetry, the speed tunnel) reads differently than it did before. |
| The projection plane | `BarrelRollController` | Restricted, `Course` is **stale** — `MoveShip` is what refreshes it and it does not run — so it holds the heading from the moment the stance engaged while the turret has gone on rotating. The nudge projects on current **facing** there instead. |

Two things fall out of that branch, both fixes:

- **Velocity modifiers now age while restricted** (they just don't displace unless exempt).
  Previously `ApplyVelocityModifiers` was skipped entirely, so a knockback taken in turret stance
  froze mid-flight and lurched out the instant you released the stance.
- **The body flare write is edge-triggered.** `VesselAnimation.StopFlareBody` writes through
  `renderer.materials[0]` (clone + array allocation); it now also runs for stopped vessels, so it
  fires on the flare→rest transition instead of every frame. Seeded on so the first pass still
  normalizes the material exactly as before.

Rolling does **not** change the stance — you are still stopped when the roll ends, still in
stationary fire mode (`SparrowModeSwitchingFireSO` is untouched), and the trail spawner stays off
(so the roll skips its `BlockRotationOverride` bridging-prism work while restricted — there is no
trail to bridge).

### 2.2 Stopped, pitch and yaw run at 3×

Not part of the roll — part of the same stance. A stopped Sparrow is an aiming platform rather
than a flying one, so it swings onto targets three times as fast:
`VesselTransformer.restrictedTurnMultiplier` (default **3**, serialized so it is per-vessel
authorable) scales the whole pitch/yaw rate — the throttle-derived term as well as the
`PitchScaler`/`YawScaler` — whenever `IsTranslationRestricted` is set, via the shared
`TurnScalar` property read at use time.

**It is applied in `SingleStickVesselTransformer` as well as the base class, and that is
load-bearing:** both the Sparrow and the Serpent — the only two vessels with a stationary stance —
run `SingleStickVesselTransformer`, which *overrides* `Pitch`/`Yaw`. A base-only change would have
compiled, read correctly, and reached neither vessel.

**Roll is deliberately not scaled.** In the single-stick transformer `Roll` is the bank *into* the
turn and shares the yaw axis of the stick; tripling it would tip the vessel three times as far off
level for the same push. The stopped turn will therefore read flatter than a flying one — that is
the trade, and `RollScaler` is the knob if it wants a nudge.

**This also reaches the Serpent** (same transformer, same default), so its stopped weave stance
turns 3× too. If that is not wanted, set `restrictedTurnMultiplier` to `1` on `Serpent.prefab` —
one inspector field, no code.

New surface for the HUD (and nothing else):

```csharp
public event Action<RollChargeState> OnRollChargeChanged;   // Armed | Spent | Lapsed
public bool IsRollArmed { get; }                            // for the HUD's initial seed
```

`RollChargeState` lives in `CosmicShore.Data` (`_Scripts/Data/Enums/`) rather than beside the
controller: it is read by the UI layer as well as written by the gameplay one, so it belongs in the
extracted leaf both can see — `SparrowHUDView` already imports that namespace, and
`Assembly-CSharp` auto-references the asmdef, so nothing had to be wired.

`OnDisable` clears the charge, so a pooled / swapped vessel can't inherit a stale armed state.

## 3. The boost icon: gauge out, charge pip in

The TIME ability icon (`OverheatButton` in `SparrowHUDVariant.prefab`) carried a radial fill driven
by heat. There is no heat left to show, so the same ring (`Holder/OverheatCounter`) is repurposed as
a **binary charge pip** for the roll:

| Roll state | Ring |
|---|---|
| `Armed` (the press's 0.3 s window is live — a roll is available *now*) | fill 1, `rollArmedColor` |
| `Spent` (rolled; next boost press re-arms) | wipes to fill 0, `rollSpentColor`, one scale punch |
| `Lapsed` (window ran out unfired; next boost press re-arms) | wipes to fill 0, `rollSpentColor`, **no punch** |

It is not a gauge: it only ever holds 0 or 1 and the transition between them is a wipe, not a
readout. The ring is a *sibling* of the ability icon, never the icon itself — so the four-icon
contract is untouched (`tintIconOnUpgrade` stays on, the "Elemental Ward" upgrade still tints the
Time icon and blooms its Time petal badge), and rule 9 of the vessel contract does not apply.

`SparrowHUDController` swapped `overheatingExecutor` for `barrelRollController` and lost its
`Update` poll entirely — the roll charge is evented, so the HUD does no per-frame work for it.

## Files

| File | Change |
|---|---|
| `_Scripts/Controller/Vessel/ResourceSystem.cs` | **+** the general immunity state (grants, `ImmuneDebuffSources` / `IsImmuneTo`, `OnElementalImmunityChanged`) and the one gate in `ApplyElementalEffect`. *Later (§1.1): grants became masks and `ApplyElementalEffect` took a source class; the bare `IsElementallyImmune` bool is gone.* |
| `_Scripts/Data/Enums/ElementalDebuffSources.cs` | **NEW** (§1.1) — the source classes a debuff names and a ward is held against |
| `_Scripts/Controller/Vessel/IVesselStatus.cs` | **+** `IsImmuneToElementalDebuff(source)` / `ImmuneDebuffSources`; **−** the now-dead `IsOverheating` |
| `_Scripts/Controller/Vessel/VesselElementalImmunity.cs` | **NEW** — the shared declarative driver |
| `_Scripts/Controller/Vessel/VesselStatus.cs` | **−** `IsOverheating` (declaration + `ResetForPlay`) |
| `_Scripts/Controller/Vessel/BarrelRollController.cs` | **−** the Time-upgrade gate; **+** `OnRollChargeChanged` / `IsRollArmed`; charge cleared on disable. **−** the `IsTranslationRestricted` gate (§2.1); facing-plane projection + no bridging-prism override while stopped |
| `_Scripts/Data/Enums/VesselVelocityModifier.cs` | **+** `ignoresTranslationRestriction` + a 4-arg ctor; the 3-arg ctor delegates with `false` so every existing call site is unchanged |
| `_Scripts/Controller/Vessel/VesselTransformer.cs` | **+** the restricted branch (`ApplyVelocityModifiers(translationRestricted: true)` + `MoveRestricted`), the 3-arg `ModifyVelocity` overload, and the edge-triggered body-flare write. **+** `restrictedTurnMultiplier` / `TurnScalar` on `Pitch`+`Yaw` (§2.2) |
| `_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs` | **+** `TurnScalar` on its `Pitch`/`Yaw` overrides — the transformer the Sparrow and Serpent actually run (§2.2) |
| `_Scripts/Tests/EditMode/ShipModifierTests.cs` | **+** two tests pinning the exemption flag's default-false and its 4-arg assignment |
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
| Stopped turn rate | `Sparrow.prefab` → `VesselTransformer.restrictedTurnMultiplier` | `3` | §2.2. Pitch + yaw only. Serialized, so per-vessel — `Serpent.prefab` currently inherits the same `3`. `1` = stopped turns at flying rate. |
| Roll displacement | `…nudgeSpeed` / `rollDurationSeconds` / `rootRollDegrees` | `60` / `0.6` / `15` | One number for both stances — a stopped dodge covers exactly the same distance as a flying strafe (§2.1). If the stopped dodge wants its own reach, that is a new serialized field, not a scaling of this one. |
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
4b. **The roll while stopped (§2.1).** Same scene. Toggle the stationary/turret stance, then:
   - Boost + full left-stick deflection → the Sparrow **rolls and strafes sideways**, exactly as
     it does flying. Speed does not change (it is stopped; the boost contributes nothing).
   - It is still **once per press**: hold boost and keep the stick pinned → exactly one roll.
     Release, press again → one more. The charge ring arms/wipes on the same beats as step 4.
   - You are **still stopped** afterwards: stance unchanged, still in stationary fire mode, still
     laying no trail (no bridging prisms appear during the stopped roll).
   - Aim somewhere well away from the heading you had when you stopped, then dodge — the strafe
     must go where the stick points relative to your **current** facing, not skew off toward the
     old heading. (This is the stale-`Course` fix; a skew here means the projection plane is wrong.)
4c. **No banked lurch.** Stopped, take a knockback (fly a Rhino into you, or clip a danger prism)
   — you must not move. Then release the stance: you must **not** lurch. (Before this branch the
   modifier froze and fired late.)
4d. **Stopped turn rate (§2.2).** Flying, note how long a full 180° yaw takes. Toggle the stance
   and repeat: it must take roughly **a third** as long. Pitch likewise. Release the stance —
   the turn rate must drop straight back to the flying rate (the scalar is read per frame, so a
   rate that stays fast after releasing means `TurnScalar` is being cached somewhere).
   The bank into the turn is unchanged by design, so the stopped turn reads flatter.
4e. **Serpent inherits it.** Serpent, stopped weave stance: its pitch/yaw are also 3×. Intended
   or not, it is `restrictedTurnMultiplier` on `Serpent.prefab` — set it to `1` to opt out.
5. **Ward, locked.** Time below 5, boost, fly into a danger prism (a Rhino's, or Ribcage traps):
   all four element flowers dip and recover over ~4 s.
6. **Ward, unlocked.** Raise Time to 5 (`ResourceSystem.TimeTestHarness = 0.5` on the vessel, or
   collect Time crystals) — the Time icon tints and grows its white Time petal badge. Now:
   - **while boosting**, hit the same danger prism → flowers do **not** dip. You are still slowed
     and still take the input mute; only the elemental drain is denied.
   - **not boosting**, hit it → flowers dip normally.

   > **"You are still slowed" was aspirational until 2026-08-15.** The immunity gate is, and always
   > was, a single check on the negative branch of `ResourceSystem.ApplyElementalEffect` — it never
   > touched `ModifyThrottle`, so the *statement* was structurally right. But the Sparrow had **no
   > `VesselChangeSpeedByPrismEffectSO` in its impact chain at all**, so nothing was slowing it in
   > the first place and there was no slow for the ward to leave standing. Fixed by wiring
   > `SparrowVesselChangeSpeedByPrism` (the Squirrel's numbers); this step is now actually
   > falsifiable. `SparrowDebuffByRhinoDangerPrismEffectSO` was never the slow despite its
   > `vesselSlowedByRhinoDangerPrismEvent` field and its "Slow Viewer Integration" header — it
   > mutes an input and raises events, nothing more.
7. **Serpent, ungated.** Serpent in the same scene, Time at any level. Stopped (turret/weave
   stance) + danger prism → no flower dip. Moving → normal dip.
7b. **Dolphin, SCOPED (§1.1).** Dolphin at Time 5, drifting. (a) danger prism → **no** flower dip;
   (b) caught in another Dolphin's crystal blast while still drifting → flowers **do** dip, and in
   The Bends the attacker **scores the bend**. (b) failing is the regression this scope exists to
   prevent, and it is invisible in single-vessel play.
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
