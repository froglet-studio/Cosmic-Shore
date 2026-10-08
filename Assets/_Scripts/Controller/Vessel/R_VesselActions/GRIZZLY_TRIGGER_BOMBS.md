# Grizzly — Trigger Bombs (LT + RT)

Each trigger owns **one bomb**. Pull and release to fire it; pull again to **freeze** it where it
is; let go to **detonate** it. How hard you squeeze is the bomb: the peak pressure of the firing
pull sets the **ammo spent**, the bomb's **visible size** and the **blast's size** together. A
Grizzly inside its own blast is **launched** along its nose.

Replaced the bomb pump (2026-10-02 → 2026-10-08) at the design owner's request, after a playtest:
*"I was expecting to have two different explosives each controlled by the two triggers. The amount
I press the analog trigger should control the amount of ammo used and the size of the explosion.
The projectile should be visible and travel at decent speed — faster than the vessel but not too
fast to track. Pulling the trigger again should freeze it in place. Letting go of the trigger
should detonate the explosion. This explosion should launch the Grizzly."*

## The gesture (per trigger)

| State | Pull | Release |
|---|---|---|
| Idle | **arm** — this pull's peak pressure is the bomb | — |
| Arming | — | **fire**: spend ammo, launch a visible bomb |
| InFlight | **freeze** the bomb in place | — |
| Frozen | — | **detonate** it where it hangs |

A bomb nobody freezes still goes off: on the first prism it hits, or where it comes to rest when
its 3 s fuse runs out. LT and RT are fully independent — two bombs can be in flight, frozen, or
going off at once. It is the charged cannon's lifecycle (`GRIZZLY_CHARGED_CANNON.md`) with
**pressure** where the cannon has **hold time**.

## Why the pump never launched you

The pump's blasts were spawned with `AffectSelfOverride = false` so they would spare the pilot's
own trail — and `AffectSelfOverride` is also what makes the shooter a valid impact pair for
`VesselImpulseByExplosionEffectSO`, the effect that launches a Grizzly caught in its own blast.
So no pump bomb could ever launch its pilot; the pump's only push was a separate 10–40 u/s kick
along the nose. The trigger bombs detonate with `AffectSelfOverride = true`, exactly as the cannon
does, and accept the cannon's trade: **a blast that can launch you also breaks your own trail
inside it.**

## The launch

`VesselImpulseByExplosionEffectSO` (in `GrizzlyExplosionImpactorDataContainer`, on
`AOEGrizzlyExplosion.prefab`):

- **Direction:** along the Grizzly's **nose** — the one direction the pilot controls — not away
  from the blast. Point, then blow.
- **Strength:** `blast scale / ExplosionDuration (1.2 s) × selfLaunchMultiplier (1.5)`, eased
  1.5 → 0.5 over `impulseDuration` (1 s) and clamped by the vessel's **100 u/s** velocity ceiling.
  A full squeeze (scale 120) hands over 150 u/s and sits on the ceiling for ~0.7 s; a tap (scale
  30) hands over 37.5 u/s. A full launch carries the hull **~95 u** (`GrizzlyTimeCourse
  .CarryAfterLaunch`).
- **Reach:** you are launched only if you are **inside** the blast when it goes off. Blast radius
  is half its scale (the AOE sphere's collider radius is 0.5): **60 u** for a full squeeze, **15 u**
  for a tap. Small bombs are for hitting things; big ones are for riding.
- A dug-in Grizzly is blasted out of turret stance (`selfLaunchUnplants`).

## Ammo

A new resource, **Ammo** (index 1 on `Grizzly.prefab`'s `ResourceSystem`; Energy stays index 0 and
belongs to the charged cannon). Full pool 1.0, starts full, regenerates **0.15 / s**.

| Squeeze | Bomb size | Ammo | Bomb scale | Blast scale (radius) | Launch |
|---|---|---|---|---|---|
| tap (≤ 0.1 pressure) | 0 | 0.08 | 8 | 30 (15 u) | 37.5 u/s |
| half | ~0.5 | ~0.22 | ~14 | ~75 (~37 u) | ~94 u/s |
| full (≥ 0.95) | 1 | 0.35 | 20 | 120 (60 u) | 150 → ceiling 100 u/s |

A pull the pool cannot pay for in full fires **the biggest bomb it can pay for**; a pool under
0.08 fizzles. A full pool is about **three full squeezes**, then one every ~2.3 s. The HUD shows
the pool as an orange **Ammo** bar just above the Energy bar.

## The projectile

Fired from the charged cannon's `Gun` (shared — `Gun` keeps no per-shot state), each trigger
yawed **3°** off the muzzle (LT left, RT right) so two bombs read as two. Muzzle speed **90 u/s**
on top of the hull's own velocity, so a bomb from a cruising Grizzly leaves at ~140 u/s — well
ahead of you, easy to track. Projectile flight eases to rest over the fuse
(`Projectile.MoveProjectileAsync`: `cos(πt / 2T)`), so a cruising Grizzly **catches its own bomb**
about 2.3 s after firing — the moment to freeze-and-blow is built into the flight. Range is
`speed × 2T/π` ≈ 270 u at cruise (vessel contract rule 34). The bomb is sized 8–20 by the
squeeze, so you can see how big the blast will be before it goes off.

## Controls

| Control | Does |
|---|---|
| LT / right mouse / L-Shift | left bomb |
| RT / left mouse / R-Shift | right bomb |
| X / Q / middle mouse | charged cannon / sniper / claw (`GRIZZLY_CHARGED_CANNON.md`) |
| RB / E | cycle weapon |
| A / Space, B / R | Dig In, Rush |

Mouse buttons and the shift keys report no analog value, so every desktop bomb is full size.

## Multiplayer

Every peer simulates, exactly as the cannon does: presses and releases are replayed to every peer
(owner → server → all, `R_VesselActionHandler`) and each peer fires its own local bomb. The size
comes from `InputStatus.Left/RightTriggerAnalog`, an **owner-written NetworkVariable** every peer
reads, so peers size the bomb from the same pull. The owner-only relay the pump needed
(`GrizzlyBombNetworkRelay`) is deleted. Known limit: a tap shorter than one network tick can be
sampled smaller on a remote peer than on the owner — the owner's blast is the one that launches
the owner's hull, and the hull's transform is what replicates.

## AI

An autopilot presses no triggers (`AIPilot` writes stick and throttle only), so
`GrizzlyTriggerBombExecutor` carries an **autopilot drive** on the simulating machine: while the
stick is straight (inside `aiFireStickBand` 0.35) and the pool can buy a full bomb, fire one; freeze
it **18 u** ahead (`aiFreezeDistance`, inside the 60 u blast); detonate it once the hull is within
**10 u** or has passed it (or after 1.5 s) — a launch. Triggers alternate at most every 0.6 s. Every
step goes through `PerformShipControllerActionsReplicated` / `StopShipControllerActionsReplicated`,
so every peer runs the same bomb. The drive is gated on the PILOT, so the menu's lava-lamp Grizzly
and released companions bomb-jump too. `aiFireStickBand = 0` turns it off.

## Files

| File | Role |
|---|---|
| `Data Containers/GrizzlyBombActionSO.cs` | Trigger action (side only); press → `OnPress`, release → `OnRelease` |
| `Data Containers/GrizzlyTriggerBombConfigSO.cs` | All tuning + the pure pressure → size / ammo / scale maps |
| `Executors/GrizzlyTriggerBombExecutor.cs` | Two per-trigger state machines, pressure tracking, fire / freeze / detonate, autopilot drive |
| `_SO_Assets/VesselActions/Grizzly/GrizzlyBomb{Left,Right}Action.asset`, `GrizzlyTriggerBombConfig.asset` | Assets |
| `_Prefabs/Spacevessels/Grizzly.prefab` | Bindings, executor (wired to the cannon's `Gun`), **Ammo** resource, **AmmoBar** HUD |
| `UI/View/GrizzlyHUDView.cs`, `UI/Controller/GrizzlyHUDController.cs` | `ammoFill` / `SetAmmo`, bound to the resource named `Ammo` |
| `Tests/Editor/GrizzlyTriggerBombTests.cs` | Edit-mode tests of the maps and the affordability rule |

The executor and config keep the pump's script GUIDs (renamed in place), so every prefab and asset
reference survived the rename.

## Tuning knobs (`GrizzlyTriggerBombConfig.asset`)

| Field | Shipped | Effect |
|---|---|---|
| `pressureForMinBomb` / `pressureForMaxBomb` / `pressureExponent` | 0.1 / 0.95 / 1 | Pressure window → size 0..1 |
| `ammoIndex` | 1 | The Ammo resource |
| `minAmmoCost` / `maxAmmoCost` | 0.08 / 0.35 | Ammo per tap / full squeeze |
| `projectileSpeed` | 90 | Added to the hull's velocity |
| `projectileTime` | 3 | Fuse; the flight eases to rest over it |
| `minProjectileScale` / `maxProjectileScale` | 8 / 20 | Visible bomb size |
| `sideYawDegrees` | 3 | LT left / RT right |
| `minBlastScale` / `maxBlastScale` | 30 / 120 | Blast size → reach (½ scale) and launch strength |
| `aoePrefabs` | `AOEGrizzlyExplosion` | Same blast as the cannon |
| `aiFireStickBand` / `aiFreezeDistance` / `aiDetonateDistance` / `aiFireIntervalSeconds` | 0.35 / 18 / 10 / 0.6 | Autopilot drive |
| `fireEvent` / `detonateEvent` | empty | FMOD — ship silent until audio authors them (LOCKED convention) |

Launch strength itself lives on `VesselImpulseByExplosionEffect.asset` (`selfLaunchMultiplier` 1.5,
`impulseDuration` 1) and is shared with the cannon. Ammo regen is `resourceGainRate` on the Ammo
entry of `Grizzly.prefab`'s `ResourceSystem` (0.15).

## In-editor verification

Not run in the editor — no Unity in the authoring session. Steps:

1. **Fire and watch.** Fly a Grizzly (Menu freestyle via the Vessel Changer). Squeeze RT fully and
   release: a visible bomb leaves the nose slightly right, clearly faster than you, and the orange
   Ammo bar drops by about a third.
2. **Freeze and launch.** Fire RT, pull RT again as you close on it: the bomb stops dead. Fly up
   to it and release: it blows, and **you are thrown forward along your nose** hard (~3× cruise
   for about a second). Turn first, then blow — you go where you point.
3. **Pressure.** Feather LT (light pull) and release: a small bomb, a small Ammo dip, a small blast
   (15 u). It should not launch you unless you are right on it. Full squeeze: big bomb, big dip.
4. **Two bombs.** Fire LT and RT back to back: two bombs fly side by side; freeze and blow each
   independently.
5. **Fuse and impact.** Fire and do nothing: the bomb slows, stops ~270 u out, and goes off. Fire
   into a prism wall: it goes off on contact.
6. **Empty pool.** Fire full bombs until the bar is low: the next squeeze fires a smaller bomb; at
   the bottom a pull does nothing. The bar refills at ~0.15/s.
7. **AI.** Release an AI Grizzly from the Spawn Matrix hangar: it fires, freezes and blows bombs
   just ahead of itself and visibly lunges forward on each.
8. **MPPM two clients.** Client A fires and freezes; client B sees A's bombs in the same places at
   the same sizes, and A lunging on detonation.
9. **Regression.** X still fires the charged cannon exactly as before; Energy bar unaffected by
   trigger bombs. Console clean — in particular no `[GrizzlyTriggerBomb]` errors.

## Follow-ups

- **No element scales the trigger bombs** — the four elements are spoken for (Space = cannon, Mass
  = weapon systems, Charge = Dig In, Time = Rush). Candidates for design: Space on blast scale
  (the cannon's own channel), Time on ammo regen.
- **No HUD cue per trigger** beyond the Ammo bar — a bomb in flight / frozen per side would be a
  pair of pips. `OnBombStateChanged` / `OnBombFired` / `OnBombDetonated` are the hooks.
- **Own-trail damage** is the cannon's accepted trade; if trigger bombs need to spare the trail,
  that is an `ExplosionImpactor` change (split "self is a valid pair" from "destroy own prisms"),
  not a config value.
