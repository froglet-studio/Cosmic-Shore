# Grizzly — Bomb Pump (LT + RT)

Both triggers blow a bomb behind the hull, and each blast kicks the Grizzly forward along its
nose. Alternate LT → RT → LT to pump speed. Added 2026-10-02 at the design owner's request:
*"LT and RT are bombs; any other functions should be shifted to other buttons."*

## The rules

| Rule | Why |
|---|---|
| **Bomb size = PEAK trigger pressure of the pull.** Feather tap = small puff, full squeeze = big kick. | The size has to be independent of the cooldown. Hold time would cost you rate; pressure costs you nothing. |
| **Fixed cooldown per trigger** (`cooldownPerTrigger`, 0.45 s), the same for every size. | Two triggers with two clocks pump twice as often as one, so alternating IS the rhythm. A release during a trigger's cooldown fizzles. |
| **The bomb goes off on RELEASE.** | Pressure has to be read over the whole pull before the size is known. |
| **The kick is along the NOSE**, not away from the blast. | Same reasoning as the cannon's self-launch (`VesselImpulseByExplosionEffectSO`): facing is the one direction the pilot controls. |
| **The blast spares your own domain.** | Enemy mass and pilots inside it are hit like any Grizzly blast; your own trail shields instead of breaking, and teammates are untouched. |
| **Blowing a bomb while dug in un-plants you** (as Rush does). | Otherwise the turret stance's translation hold swallows the kick. |

No energy cost, and no element scales it yet (see Follow-ups).

## Controls (what moved)

| Control | Before | Now |
|---|---|---|
| LT / right mouse / L-Shift | cycle weapon | **bomb (left)** |
| RT / left mouse / R-Shift | fire (cannon / sniper / claw) | **bomb (right)** |
| X / Q / middle mouse | — | fire (cannon / sniper / claw) — gesture unchanged |
| RB / E | — | cycle weapon |
| A / Space, B / R | Dig In, Rush | unchanged |

The ability map (`Resources/ElementalAbilityMaps/Grizzly.asset`) was re-pointed (Charged Cannon
`Input 1 → 8`, Weapon Systems `Input 2 → 3`), so the ability lockup's control chips follow on
their own. `ControlGlyphSet` gained the two rows those inputs needed (X → `x button` / `Q`,
RB → `R1` / `E`), and `InputHintBindingMap` gained `KeyE → FlipAction`.

## How it works

- **`GrizzlyBombActionSO`** — two assets, `GrizzlyBombLeftAction` (LT) and
  `GrizzlyBombRightAction` (RT). They carry only which trigger they are.
- **`GrizzlyBombPumpConfigSO`** (`GrizzlyBombPumpConfig.asset`) — all tuning, wired directly on
  the executor.
- **`GrizzlyBombPumpExecutor`** (Grizzly root) — the per-vessel state.
- **`GrizzlyBombNetworkRelay`** (Grizzly root, `NetworkBehaviour`) — relays each bomb to peers.

**Peak pressure is tracked from the raw input, not from the action callbacks.** Presses and
releases reach an executor only after the owner → server → everyone round trip, so a tap shorter
than one round trip would release before its press arrived. `Update` watches
`InputStatus.Left/RightTriggerAnalog` every frame on the simulating machine and records each
pull's peak; the release callback consumes it.

**A device with no analog reading is a full press.** Mouse buttons and the shift keys report 1.0
(so every desktop bomb is full size), and a strategy that never writes the analog channel (touch)
reads 0, which is treated as 1 rather than as "no press".

**Only the simulating machine blows bombs** (the human owner, or the server for an AI). It
applies the kick (the transform replicates) and calls the relay, which re-spawns the same-sized
blast on every other peer (the `MantaBombNetworkRelay` model). The size has to travel on that RPC
because the replicated input events carry no analog value.

**Why the kick is applied directly instead of riding the blast:** the cannon's self-launch needs
`AffectSelfOverride = true`, and in `ExplosionImpactor` that same flag also makes a blast destroy
your own-domain prisms. A bomb going off behind the hull twice a second would shred the Grizzly's
own trail. So the bomb spares your domain and the executor pushes the hull itself.

## Files

| File | Role |
|---|---|
| `Data Containers/GrizzlyBombActionSO.cs` | Trigger action (side only) |
| `Data Containers/GrizzlyBombPumpConfigSO.cs` | Tuning + the pure pressure → size / kick / blast maps |
| `Executors/GrizzlyBombPumpExecutor.cs` | Pressure tracking, cooldowns, kick, blast spawn |
| `../GrizzlyBombNetworkRelay.cs` | Owner → peers bomb replay |
| `_SO_Assets/VesselActions/Grizzly/GrizzlyBomb{Left,Right}Action.asset`, `GrizzlyBombPumpConfig.asset` | Assets |
| `_Prefabs/Spacevessels/Grizzly.prefab` | Bindings + both components on the root |
| `Tests/Editor/GrizzlyBombPumpTests.cs`, `ControlChipBindingTests.cs` | Edit-mode tests |

## Tuning knobs (`GrizzlyBombPumpConfig.asset`)

| Field | Shipped | Effect |
|---|---|---|
| `pressureForMinBomb` / `pressureForMaxBomb` | 0.1 / 0.95 | Pressure window mapped onto size 0 → 1. Max is under 1 because many triggers never report a full 1.0 |
| `pressureExponent` | 1 | >1 makes the top of the pull matter more |
| `cooldownPerTrigger` | 0.45 s | Per trigger; alternating gives one bomb every ~0.23 s |
| `minKick` / `maxKick` | 10 / 40 u/s | Forward velocity added per bomb. Overlapping kicks share the vessel's 100 u/s velocity-modifier ceiling |
| `kickDuration` | 0.8 s | Cosine ease-out |
| `minBlastScale` / `maxBlastScale` | 15 / 60 | Visual + damage size (the cannon spans 20 → 120) |
| `spawnBehindDistance` / `spawnSideOffset` | 6 / 3 | Where the bomb goes off; LT left, RT right |
| `aoePrefabs` | `AOEGrizzlyExplosion` | Same blast as the cannon |
| `bombEvent` | empty | FMOD event — ships silent until audio authors it |
| `aiPumpStickBand` | 0.35 | Autopilot drive (below). Stick deflection under which an AI blows full-size bombs; 0 disables |

For reference: Rush adds 80 u/s for 1 s; the Grizzly cruises at `DefaultThrottleScaler` 50.

## Autopilot drive (added for Grizzly Time, 2026-10)

`AIPilot` writes the stick and the throttle only, so an autopilot never pressed a trigger and an
AI Grizzly never pumped. While `AIPilot.AutoPilotEnabled`, `GrizzlyBombPumpExecutor.Update` skips
pressure tracking and calls `AutopilotPump`: it blows from whichever trigger's clock is older,
on the same 0.45 s per-trigger cooldown (so the rhythm is a human's alternating LT/RT), with a size
from `GrizzlyBombPumpConfigSO.AutopilotPumpSize` — 1 while the stick is inside `aiPumpStickBand`,
easing linearly to the smallest bomb at a full deflection. It goes through the same `Blow`, so the
kick, the blast and the relay to peers are identical to a human's. Gated on the PILOT, so the menu's
lava-lamp Grizzly and released companions pump too (the Manta's autopilot Soar rule, REDLINE.md §5).

`Grizzly.prefab`'s `AIPilot` was also serialized **disabled** (every other hull ships it enabled):
`StartAIPilot` does not enable the component and steering runs in `Update`, so no AI Grizzly had
ever steered. It is enabled now, with AI throttle 0.7 → 1.0 across skill (was a flat 0.6).

## In-editor verification

Not run in the editor — no Unity in the authoring session. Steps:

1. Fly a Grizzly (Menu_Main freestyle via the vessel changer, or any Grizzly scene). Pull LT
   lightly and release: a small blast behind-left, a small forward nudge. Squeeze RT fully and
   release: a large blast behind-right, a clear surge.
2. Alternate LT/RT as fast as possible: speed climbs and holds above cruise while you pump; stop
   and it eases back. Hammer ONE trigger: it fires at most once per 0.45 s.
3. Light taps and full squeezes at the same tempo fire equally often (size never touches cooldown).
4. Lay a trail, then bomb across it: your own trail shields (brief shield bloom) and survives;
   an opposing trail behind you is destroyed.
5. Press X: the charged cannon works exactly as before (hold, release, press to freeze, release
   to detonate). Press RB: weapon cycles Explosives → Sniper → Flamethrower. Chips on the ability
   row read X / RB on pad and Q / E on keyboard.
6. Dig in (A), then pull a trigger: you un-plant and kick forward.
7. Desktop: left/right mouse buttons (or the shift keys) blow full-size bombs.
8. **MPPM two clients:** client A pumps; client B sees A's blasts at the same SIZES and
   positions (allowing for replication lag), exactly once each, and A's speed surge.
9. Console clean — in particular no `[GrizzlyBombPump] No GrizzlyBombPumpConfigSO wired` error.

## Follow-ups

- **No element scales the bomb pump yet.** The four elements are already spoken for (Space =
  cannon, Mass = weapon systems, Charge = Dig In, Time = Rush) and the bombs are not one of the
  four row abilities. Candidates for design: Time on `cooldownPerTrigger`, or Space on blast size
  for parity with the cannon.
- **No HUD cue** for the pump (no row card, no trigger chip) — the triggers are not on the
  four-icon row. `GrizzlyBombPumpExecutor.OnBombBlown` is the hook for one.
- **AI Grizzlies never bomb** — `AIPilot.abilities` is empty on `Grizzly.prefab`.
- **`ControlGlyphSet` draws RT and RB with the same `R1` art** (RT was already authored on R1,
  for want of an Xbox R2 rest sprite). The Grizzly row only shows RB, so it is unambiguous there.
- Own-domain prisms caught in a bomb take a 2 s shield, which may read as noise behind a pumping
  Grizzly. If it does, tune `spawnBehindDistance` up or the blast scale down.
