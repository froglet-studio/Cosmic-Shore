# Serpent — Solid Fuel Pellets (Time)

The Serpent's Time ability, restored to its original design on 2026-09-25.

**Press the button and one solid fuel pellet burns** — a fixed amount of fuel (a quarter of the
tank). Every burn is its own event with its own duration. **Press again while one is still
burning and a second pellet lights beside it**: the burns overlap, and each adds the same increment
of speed, so four pellets burning at once is four times the effect of one. The tank holds exactly
four pellets and **does not refill on a clock**: a pellet comes back only while the Serpent's own
wall eats the Serpent's own trail (below). That makes it two things at once: a manoeuvre (a burst
of speed you can stack on demand) and a resource to manage (keep some in hand, or dump all four
for one big overlap and fly on empty until you build).

## The wall refills the tank (Garrett, 2026-10-31)

> "lets remove the automatic regeneration on the serpents fuel pellets. the way for it to regen a
> pellet is when its trail gets eaten by its wall. when it flies out at normal speed in plane with
> a wall its trail will get pulled into the wall. it shoul get 1 pellet for 2 seconds of building.
> pellets should last twice as long at base and scale from there, so max time is also twice as long."

- **No clock refill.** The `Boost` resource (index 1) on `Serpent.prefab` authors
  `resourceGainRate 0`. The tank starts full (four pellets) and only the wall puts fuel back.
- **Building refills it.** `SerpentWallAssembler` reports each of the OWNER's own trail prisms
  (same domain and same `PlayerName`) that LANDS in one of its walls to the owner's
  `ConsumeBoostActionExecutor.CreditWallBuilding`. The time since the previous landing is building
  time, paid at one pellet per `secondsOfWallBuildingPerPellet` (2 s). A gap longer than
  `wallBuildingGapSeconds` (0.4 s, just above the wall's 0.3 s steal sweep) starts a new stretch and
  pays nothing for the pause. Landings in the same frame pay once, so two walls eating at once
  refill no faster than one, and an opponent's or a teammate's prisms pay nothing.
- **A pellet is a pellet**: the credit is in pellets, so at Time 5 (0.2 pellets) 2 s of building
  still returns one whole (smaller) pellet.
- **Burns twice as long.** `boostDuration` 3 s -> **6 s** at rest; Time still scales it x1 -> x1.6,
  so Time 10 is 9.6 s (was 4.8 s).
- The wall is reached through `SerpentWallAssembler.Snapshot.Fuel`, which
  `SeedAssemblerActionExecutor.StartSeed` fills from its sibling executor, so a wall laid by the
  cloak or the stopped stance both refuel. Every peer rebuilds walls from replicated inputs, so
  every peer credits the same landings, the same way every peer already ran the pellet burns.

## What was wrong, and what changed

The ability had drifted into a **magazine**: four charges that came back only after the LAST burn
ended **plus a 7-second reload**, gated on the fuel resource AND the charge counter at once. Three
defects rode along:

| Defect | Effect | Fix |
|---|---|---|
| Magazine + reload on top of the fuel tank | Read as a cooldown ability, not a fuel tank; the refilling fuel was invisible because the charge counter gated first | The fuel resource IS the magazine. Pellets held = `floor(fuel / pelletCost)`; no counter, no reload |
| `StopAction` (button **release**) cancelled every burn | A human could never overlap burns (you must release to press again), so stacking was impossible | A release does nothing; a lit pellet burns out on its own clock |
| Same `StopAction` on the AI path | `AIPilot` authors `Duration: 0`, so every AI burn was cancelled the frame it started — AI Serpents never boosted | Same fix |
| Stacking was `3 × n` (3x, 6x, 9x, 12x) | One burn added 2 cruise units, four added 11 — not "4x the effect" | `1 + (m − 1) × n` (3x, 5x, 7x, 9x): the speed a burn ADDS is exactly n times what one adds |

Burns are tracked as **end times** retired in `Update`, not as one cancellable task per burn —
a cancelled task that skips its own tail is how a boost multiplier gets stranded on (vessel skill
rule 13). Every teardown path (`OnDisable`, turn end, re-`Initialize` on a vessel swap) goes
through `ClearBurns`, which always writes `IsBoosting`/`BoostMultiplier` back.

## The icon

The Serpent's original fuel HUD art (`_Graphics/Design Assests/HUD UI/Serpent Fuel/`) is the Time
card's icon again: four pellet **slots** with the four lit **pellets** (`Line_1..4`) drawn over
them. Pip *n* is fully lit once the tank holds *n* pellets; the pellet currently refilling shows its
partial fill at half brightness, and spending snaps the pips down at once while refilling pours in
(`pipFillRate`). The pips read the fuel resource itself, never a count of presses, so the readout
and the gate that decides whether a press burns cannot disagree.

- **Mirrored to fly right** (Garrett, 2026-10-30), nose first like every hull on the row: the
  generator mirrors `PelletSlots.png`, `Line_1..4.png` are flipped, their anchored x negated and
  their fill origin moved to the right (the pellet's round nose) on `SerpentHUDVariant.prefab`.
- `PelletSlots.png` is **derived**, never hand-edited: `Tools/Build/author_serpent_pellet_icon.py`
  keys the translucent decagon plate and its rim stroke out of `Base.png` (the ability lockup
  retired the fleet's decagon backdrops — every card draws its own trapezoid plate). `--check`
  fails if it drifts.
- `SerpentHUDVariant.prefab` gains `TimeIcon` under the old `Boost Button`, with `Line_1..4`
  re-parented beneath it, so the lockup re-homes the button into the Time slot and the pips travel
  with the icon (its chrome sweep retires the host's direct children other than the icon).
- `Serpent.prefab` binds it: `abilityIcons` gains `element: Time → TimeIcon`. The Serpent is now
  **1/4** on the ability row (Charge, Mass and Space still render LOCKED).
- **Binding one icon changes how the lockup treats the rest of the HUD root.** With zero icons it
  clears the whole root; with one it keeps any root branch a HUD-root component still references.
  The view's `shieldIcon` pointed at the retired Seed Wall readout (`Wall Button`), which would
  have come back as old UI in the lockup's corner, so that reference is nulled on the prefab. The
  Seed Wall is not bound to any input any more (RT is scope/cloak), so nothing is lost; the view
  and controller code for it is untouched and simply has no target.

## Files

| File | Role |
|---|---|
| `Executors/ConsumeBoostActionExecutor.cs` | The pellet tank: gate on fuel, spend, overlapping burns, multiplier, teardown, the wall-building refill (`CreditWallBuilding`) |
| `../../Assemblers/SerpentWallAssembler.cs` | Reports each of the owner's own trail prisms landing in its wall (`Snapshot.Fuel`) |
| `Executors/SeedAssemblerActionExecutor.cs` | Hands the wall its owner's pellet executor when it seeds |
| `Data Containers/ConsumeBoostActionSO.cs` | Shared stateless config: per-pellet multiplier, burn duration, Time scaling, fuel index + pellet cost |
| `_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset` | The authored numbers (dead magazine keys removed) |
| `UI/Controller/SerpentHUDController.cs` | Pushes the fuel level to the view off `ResourceSystem.OnResourceChanged` |
| `UI/View/SerpentVesselHUDView.cs` | Paints the four pips (`SetPelletFuel`) |
| `_Prefabs/UI Elements/VesselHUD/SerpentHUDVariant.prefab` | `TimeIcon` + re-parented pips |
| `_Prefabs/Spacevessels/Serpent.prefab` | Executor `config` wired; `abilityIcons` Time binding; `shieldIcon` nulled |
| `Tools/Build/author_serpent_pellet_icon.py` | Derives `PelletSlots.png` from `Base.png` |
| `Resources/ElementalAbilityMaps/Serpent.asset` | Time entry relabelled **Solid Fuel Pellets** |

## Tuning knobs

| Knob | Where | Shipped | Effect |
|---|---|---|---|
| `boostMultiplier` | `ConsumeBoostAction.asset` | 3 | Boost ONE pellet gives; n pellets give `1 + (3 − 1) × n` = 3/5/7/9x |
| `boostDuration` | `ConsumeBoostAction.asset` | 6 s | One pellet's burn at rest (3 s until 2026-10-31) |
| `timeDurationMultiplier` | `ConsumeBoostAction.asset` | ×1 → ×1.6 at Time 10, floor ×0.25 | Time → burn duration (6 s → 9.6 s) |
| `resourceCost` | `ConsumeBoostAction.asset` | 0.25 | Pellet size; capacity = `maxAmount / resourceCost` = 4 |
| `resourceGainRate` | `Serpent.prefab` → `ResourceSystem` → `Boost` (index 1) | **0** | No clock refill (0.07 / s until 2026-10-31) |
| `secondsOfWallBuildingPerPellet` | `ConsumeBoostAction.asset` | 2 s | Seconds of the wall eating your own trail per pellet back |
| `wallBuildingGapSeconds` | `ConsumeBoostAction.asset` | 0.4 s | Longest gap between own-trail landings still counted as building |
| `pipFillRate` | `SerpentVesselHUDView` | 1.5 pellets/s | How fast the pips pour up to the real level |
| `pelletIgniteEvent` | `ConsumeBoostActionExecutor` on `Serpent.prefab` | *empty* | Dedicated FMOD ignite sound; empty falls back to the shared `BoostActivate` category |

**Level 5 (Time): Fifth Pellet** (Garrett, 2026-10-30: "Time L5 lets give players a fifth
pellet"). `upgradeResourceCost` 0.2 replaces the 0.25 pellet while `IsUpgradeActive(Time)`, so the
same 1.0 tank holds five; a pellet still burns and boosts exactly as before (five at once is 11x),
and 2 s of wall building still returns one (smaller) pellet. The TIME card
re-lays itself as five rows (`SerpentVesselHUDView.SetFivePellets`: the slots swap to
`PelletSlots5.png` from `author_serpent_pellet_icon.py`, the pips shrink to 0.8 and close up to
24.9 apart, and a fifth pip is cloned from the first).

## Verification status

**2026-10-31 (wall refill, 6 s burns): not run in the editor**; C# type-checked offline only.
Steps 3, 5, 5a, 6 and 7 below are the hand test.

**Earlier: not run in the editor.** The four changed C# files were type-checked with Roslyn against a stub
harness; the prefab edits were checked for dangling local fileIDs; every offline gate passes.

## In-editor verification

1. Menu_Main → freestyle, swap to the **Serpent**. The lower-right row shows a **Time** card whose
   icon is four pellets; Charge/Mass/Space are LOCKED cards. No old Serpent HUD art (decagon, wall
   seeds, glyph roots) anywhere on screen. Clean console.
2. Tank starts full: all four pellets lit.
3. Tap **A / Space** once → one pellet goes dark at once, the vessel boosts (~3x cruise) for 6 s.
   Hold the button — the burn still ends after 6 s; release early — the burn still lasts 6 s.
4. Tap four times quickly → all four go dark; speed steps up with each press (3x → 5x → 7x → 9x)
   and steps back down as each burn ends, in the order they were lit.
5. With the tank empty, press → nothing happens. Fly on: **no pellet comes back**, however long
   you wait.
5a. Lay a wall (cloak, or stop for the stance), then fly out at cruise in the wall's plane so the
   trail is pulled into it → a pellet pours in at half brightness over ~2 s of the wall eating
   the trail, goes fully lit, and is burnable. Fly away from the plane (the wall stops eating)
   → the refill stops. Two walls eating at once refill no faster than one.
6. Raise Time (debug harness `TimeTestHarness` on `ResourceSystem`) → each burn lasts longer
   (9.6 s at Time 10). Speed per pellet does not change.
7. Put an **AI** Serpent in a match (any arena card with AI backfill that allows the Serpent, or
   Spawn Matrix → Vessels → Serpent): it burns its opening four pellets and then refills only
   when its walls eat its trail.
8. MPPM two clients: both see the other Serpent's boost (speed replicates through the transform).

## Follow-ups

- Time L5 is an open design slot — needs Garrett's markup.
- The Seed Wall view/controller code (`shieldIcon`, `shieldIconsByCount`, `SetShieldCount`) now has
  no target on any shipped HUD. Delete it when the Mass slot is designed, or re-home it if that
  design brings the wall back.
- Author a dedicated FMOD ignite event into `pelletIgniteEvent`.
