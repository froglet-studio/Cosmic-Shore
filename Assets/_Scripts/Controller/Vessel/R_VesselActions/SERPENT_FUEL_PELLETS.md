# Serpent — Solid Fuel Pellets (Time)

The Serpent's Time ability, restored to its original design on 2026-09-25.

**Press the button and one solid fuel pellet burns** — a fixed amount of fuel (a quarter of the
tank). Every burn is its own event with its own duration. **Press again while one is still
burning and a second pellet lights beside it**: the burns overlap, and each adds the same increment
of speed, so four pellets burning at once is four times the effect of one. Fuel refills at a fixed
rate and the tank holds exactly four pellets. That makes it two things at once: a manoeuvre (a
burst of speed you can stack on demand) and a resource to manage (keep some in hand, or dump all
four for one big overlap and fly on empty while it refills).

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
| `Executors/ConsumeBoostActionExecutor.cs` | The pellet tank: gate on fuel, spend, overlapping burns, multiplier, teardown |
| `Data Containers/ConsumeBoostActionSO.cs` | Shared stateless config: per-pellet multiplier, burn duration, Time scaling, fuel index + pellet cost |
| `_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset` | The authored numbers (dead magazine keys removed) |
| `UI/Controller/SerpentVesselHUDController.cs` | Pushes the fuel level to the view off `ResourceSystem.OnResourceChanged` |
| `UI/View/SerpentVesselHUDView.cs` | Paints the four pips (`SetPelletFuel`) |
| `_Prefabs/UI Elements/VesselHUD/SerpentHUDVariant.prefab` | `TimeIcon` + re-parented pips |
| `_Prefabs/Spacevessels/Serpent.prefab` | Executor `config` wired; `abilityIcons` Time binding; `shieldIcon` nulled |
| `Tools/Build/author_serpent_pellet_icon.py` | Derives `PelletSlots.png` from `Base.png` |
| `Resources/ElementalAbilityMaps/Serpent.asset` | Time entry relabelled **Solid Fuel Pellets** |

## Tuning knobs

| Knob | Where | Shipped | Effect |
|---|---|---|---|
| `boostMultiplier` | `ConsumeBoostAction.asset` | 3 | Boost ONE pellet gives; n pellets give `1 + (3 − 1) × n` = 3/5/7/9x |
| `boostDuration` | `ConsumeBoostAction.asset` | 3 s | One pellet's burn at rest |
| `timeDurationMultiplier` | `ConsumeBoostAction.asset` | ×1 → ×1.6 at Time 10, floor ×0.25 | Time → burn duration (3 s → 4.8 s) |
| `resourceCost` | `ConsumeBoostAction.asset` | 0.25 | Pellet size; capacity = `maxAmount / resourceCost` = 4 |
| `resourceGainRate` | `Serpent.prefab` → `ResourceSystem` → `Boost` (index 1) | 0.07 / s | Refill: one pellet per ~3.6 s, a full tank in ~14 s |
| `pipFillRate` | `SerpentVesselHUDView` | 1.5 pellets/s | How fast the pips pour up to the real level |
| `pelletIgniteEvent` | `ConsumeBoostActionExecutor` on `Serpent.prefab` | *empty* | Dedicated FMOD ignite sound; empty falls back to the shared `BoostActivate` category |

Level 5 (Time) is still an **open design slot** — nothing is invented here. The old FLEET_MAPS
proposal *Endless Coil* ("chains without the reload pause") no longer means anything, because the
reload is gone.

## Verification status

**Not run in the editor.** The four changed C# files were type-checked with Roslyn against a stub
harness; the prefab edits were checked for dangling local fileIDs; every offline gate passes.

## In-editor verification

1. Menu_Main → freestyle, swap to the **Serpent**. The lower-right row shows a **Time** card whose
   icon is four pellets; Charge/Mass/Space are LOCKED cards. No old Serpent HUD art (decagon, wall
   seeds, glyph roots) anywhere on screen. Clean console.
2. Tank starts full: all four pellets lit.
3. Tap **A / Space** once → one pellet goes dark at once, the vessel boosts (~3x cruise) for 3 s.
   Hold the button — the burn still ends after 3 s; release early — the burn still lasts 3 s.
4. Tap four times quickly → all four go dark; speed steps up with each press (3x → 5x → 7x → 9x)
   and steps back down as each burn ends, in the order they were lit.
5. With the tank empty, press → nothing happens. Watch a pellet pour back in over ~3.6 s at half
   brightness, go fully lit, and become burnable.
6. Raise Time (debug harness `TimeTestHarness` on `ResourceSystem`) → each burn lasts longer
   (4.8 s at Time 10). Speed per pellet does not change.
7. Put an **AI** Serpent in a match (any arena card with AI backfill that allows the Serpent, or
   Spawn Matrix → Vessels → Serpent): it now boosts visibly every ~8 s.
8. MPPM two clients: both see the other Serpent's boost (speed replicates through the transform).

## Follow-ups

- Time L5 is an open design slot — needs Garrett's markup.
- The Seed Wall view/controller code (`shieldIcon`, `shieldIconsByCount`, `SetShieldCount`) now has
  no target on any shipped HUD. Delete it when the Mass slot is designed, or re-home it if that
  design brings the wall back.
- Author a dedicated FMOD ignite event into `pelletIgniteEvent`.
