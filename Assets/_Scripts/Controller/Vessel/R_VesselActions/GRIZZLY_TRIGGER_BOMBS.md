# Grizzly — Trigger Bombs (LT + RT)

Each trigger owns **one bomb**. Pull and release to fire it; pull again to **freeze** it where it
is; let go to **detonate** it. How hard you squeeze is the bomb: the peak pressure of the firing
pull sets the **ammo spent** and the **size** of both the bomb and its blast. A Grizzly inside its
own blast is **thrown away from the bomb**.

History, all at the design owner's request after playtests:

- **2026-10-02 → 10-08 — the bomb pump.** Blasts kicked you along the nose; they never launched
  you, because they were spawned with `AffectSelfOverride = false` and the cannon's self-launch
  effect only reaches a shooter its blast is allowed to hit.
- **2026-10-08 — trigger bombs:** *"two different explosives each controlled by the two triggers…
  the amount I press… should control the amount of ammo used and the size of the explosion… visible
  and travel at decent speed — faster than the vessel but not too fast to track. Pulling the
  trigger again should freeze it in place. Letting go… should detonate. This explosion should
  launch the Grizzly."*
- **2026-10-08 — the second pass:** *"the bombs should launch you away from the bomb and make the
  bomb look good… smaller before it explodes but bigger explosion."*

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

## The launch — away from the bomb

`GrizzlyTriggerBombExecutor.LaunchSelf`, applied at detonation:

- **Direction: from the bomb to you.** Leave a frozen bomb **behind** you and blow it — you are
  thrown forward. Beside you — you are thrown sideways. Ahead of you — it stops you dead. (Sitting
  exactly on it, the nose stands in for "away".) The charged cannon still launches you along the
  nose (`VesselImpulseByExplosionEffectSO`); only the trigger bombs push away from themselves.
- **Strength:** the blast's own impulse, `blast scale / ExplosionDuration (1.2 s)`, ×
  `selfLaunchMultiplier` (1.5) — **full at the bomb, easing to half at the blast's edge**
  (`selfLaunchEdgeStrength` 0.5), nothing outside it — over `selfLaunchSeconds` (1 s, cosine
  ease-out), clamped by the vessel's **100 u/s** velocity ceiling. A full squeeze (scale 200)
  hands over 250 u/s at the bomb, so a full launch ridden close sits **on the ceiling for its
  whole second** and carries the hull **~100 u** (`GrizzlyTimeCourse.CarryAfterLaunch`).
- **Reach:** blast radius is half its scale (the AOE sphere's collider radius is 0.5): **100 u**
  for a full squeeze, **25 u** for a tap.
- **Your own trail is SPARED.** The blast is spawned with `AffectSelfOverride = false`: your own
  domain's mass shields instead of breaking and teammates are untouched, while enemy mass and
  pilots inside it are hit and knocked back radially (the impulse effect's bystander path). The
  launch no longer needs the blast to hit you, because the executor applies it directly — so the
  cannon's "launch costs your trail" trade is gone for the bombs.
- A dug-in Grizzly is blasted out of turret stance (through the controller, so the replicated flag
  stays in sync).
- Applied on the **simulating machine** only (your client, or the server for an AI): the hull's
  transform is what replicates.

## Small before, big after

| Squeeze | Bomb size | Ammo | Bomb (diameter) | Blast (radius) | Launch at the bomb |
|---|---|---|---|---|---|
| tap (≤ 0.1 pressure) | 0 | 0.08 | 2.5 | 50 (25 u) | 62.5 u/s |
| half | ~0.5 | ~0.22 | ~3.75 | ~125 (~62 u) | ~156 → ceiling |
| full (≥ 0.95) | 1 | 0.35 | 5 | 200 (100 u) | 250 → ceiling 100 u/s |

Every blast is at least **ten times** its bomb (`TheBombIsSmallAndItsBlastIsHuge`), and a full
squeeze out-blasts the charged cannon's full charge (120).

## The look — `GrizzlyBomb.prefab` + `GrizzlyBombVisual`

The bombs used to fire the cannon's round (`GrizzlyShell`), which is why they looked like cannon
shells. They now have their own projectile, built from that shell by
`Tools/Build/author_grizzly_bomb_assets.py`, and their own pool, factory and `Gun` on the
cannon's muzzle (the cannon is untouched):

- **Core** — a small dark body with a hot fresnel rim in the **firing pilot's domain colour**
  (`SO_ColorSet.GetDomainSignalColor`), casting no shadow.
- **Halo** — an additive glow billboard (`glow1_ADD`) around the core that **breathes with the
  fuse**: 2.5 breaths/s at the muzzle climbing to 11/s at the end of the fuse (quadratically — the
  last second is the frantic one), each peak flashing toward a warm spark colour. You can read how
  long a bomb has left without a HUD.
- **Armed** — freezing a bomb stops its streak, **flares the halo** to 5× the core and strobes it
  at 14/s: "this one is about to go".
- **Streak** — a 0.3 s domain-coloured trail behind the flying bomb (Unity's Default-Line
  material, coloured per shot through the trail's own gradient), sized to the bomb.

All colour rides `MaterialPropertyBlock`s on shared materials — no material instances. Pooled
shells are reset on every `Arm`.

## Ammo

**Ammo** is index 1 on `Grizzly.prefab`'s `ResourceSystem` (Energy stays index 0, the cannon's).
Full pool 1.0, starts full, regenerates **0.15 / s**; about three full squeezes in a burst, then
one every ~2.3 s. A pull the pool cannot pay for in full fires **the biggest bomb it can pay for**;
under 0.08 it fizzles. The HUD shows it as an orange **Ammo** bar above the Energy bar.

## The projectile's flight

Muzzle speed **90 u/s** on top of the hull's own velocity (~140 u/s from a cruising Grizzly — well
ahead of you, easy to track), each trigger yawed **3°** off the muzzle (LT left, RT right). The
flight eases to rest over the 3 s fuse (`Projectile.MoveProjectileAsync`: `cos(πt / 2T)`), so a
cruising Grizzly catches its own bomb after ~2.3 s; range ≈ `speed × 2T/π` ≈ 270 u (vessel
contract rule 34). The natural move: fire, pull to freeze as you close on it, fly past, release.

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

Every peer simulates, as the cannon does: presses and releases are replayed to every peer
(owner → server → all) and each peer fires its own local bomb, sized from
`InputStatus.Left/RightTriggerAnalog` (an owner-written NetworkVariable every peer reads). The
self-launch is applied on the simulating machine only. Known limit: a tap shorter than one
network tick can be sampled smaller on a remote peer than on the owner.

## AI

`GrizzlyTriggerBombExecutor`'s **autopilot drive**, on the simulating machine: while the stick is
straight (inside `aiFireStickBand` 0.35) and the pool can buy a full bomb, fire one; freeze it
**18 u** ahead (`aiFreezeDistance`); **fly past it** and detonate once it is **10 u behind**
(`aiDetonateBehindDistance`, well inside the 100 u blast), or after 1.5 s — so the AI is thrown
forward, away from its bomb. Triggers alternate at most every 0.6 s; every step goes through the
replicated press/release, so every peer runs the same bomb. Gated on the PILOT, so the menu's
lava-lamp Grizzly and released companions bomb-jump too. `aiFireStickBand = 0` turns it off.

## Files

| File | Role |
|---|---|
| `Data Containers/GrizzlyBombActionSO.cs` | Trigger action (side only); press → `OnPress`, release → `OnRelease` |
| `Data Containers/GrizzlyTriggerBombConfigSO.cs` | All tuning + the pure pressure → size / ammo / scale maps |
| `Executors/GrizzlyTriggerBombExecutor.cs` | Per-trigger state machines, pressure tracking, fire / freeze / detonate, `LaunchSelf`, autopilot |
| `Controller/Projectiles/GrizzlyBombVisual.cs` | The bomb's fuse pulse, armed flare, domain tint, streak |
| `_Prefabs/Projectile/GrizzlyBomb.prefab` | The bomb (generated) |
| `_SO_Assets/VesselActions/Grizzly/GrizzlyBomb{Left,Right}Action.asset`, `GrizzlyTriggerBombConfig.asset` | Assets |
| `_Prefabs/Spacevessels/Grizzly.prefab` | Bindings, executor, the bombs' pool / factory / `Gun`, **Ammo** resource, **AmmoBar** HUD |
| `UI/View/GrizzlyHUDView.cs`, `UI/Controller/GrizzlyHUDController.cs` | `ammoFill` / `SetAmmo`, bound to the resource named `Ammo` |
| `Tests/Editor/GrizzlyTriggerBombTests.cs` | Size / ammo / affordability / small-vs-big / launch / autopilot |
| `Tools/Build/author_grizzly_bomb_assets.py` | Authors `GrizzlyBomb.prefab` from `GrizzlyShell.prefab` and wires the bombs' pool onto the Grizzly (`--check`) |

## Tuning knobs

`GrizzlyTriggerBombConfig.asset`:

| Field | Shipped | Effect |
|---|---|---|
| `pressureForMinBomb` / `pressureForMaxBomb` / `pressureExponent` | 0.1 / 0.95 / 1 | Pressure window → size 0..1 |
| `ammoIndex` / `minAmmoCost` / `maxAmmoCost` | 1 / 0.08 / 0.35 | Ammo per tap / full squeeze |
| `projectileSpeed` / `projectileTime` | 90 / 3 | Muzzle speed over the hull's; fuse |
| `minProjectileScale` / `maxProjectileScale` | 2.5 / 5 | Bomb diameter — small on purpose |
| `sideYawDegrees` | 3 | LT left / RT right |
| `minBlastScale` / `maxBlastScale` | 50 / 200 | Blast diameter → reach (½) and launch strength |
| `selfLaunchMultiplier` / `selfLaunchSeconds` / `selfLaunchEdgeStrength` | 1.5 / 1 / 0.5 | The launch away from the bomb |
| `aiFireStickBand` / `aiFreezeDistance` / `aiDetonateBehindDistance` / `aiFireIntervalSeconds` | 0.35 / 18 / 10 / 0.6 | Autopilot |
| `fireEvent` / `detonateEvent` | empty | FMOD — ship silent until audio authors them (LOCKED convention) |

`GrizzlyBomb.prefab` → `GrizzlyBombVisual` (authored by the generator — edit there, re-run):
`pulseHzAtLaunch` 2.5, `pulseHzAtFuseEnd` 11, `haloScale` 3, `pulseAmplitude` 0.22,
`armedHaloScale` 5, `armedPulseHz` 14, `rimIntensityPeak` / `Trough` 6 / 1.5, `sparkColor`,
`sparkMix` 0.45, `coreDarkness` 0.12, `trailWidthFactor` 0.7; streak length `TRAIL_SECONDS` 0.3.
Ammo regen is `resourceGainRate` on the Ammo entry of `Grizzly.prefab` (0.15).

## In-editor verification

Not run in the editor — no Unity in the authoring session. Steps:

1. **The bomb.** Fly a Grizzly (Menu freestyle via the Vessel Changer). Squeeze RT: a **small**
   glowing bomb in your domain colour leaves slightly right of the nose, faster than you, with a
   short streak; its glow breathes, faster and faster as its fuse runs. Ammo bar (orange) drops ~⅓.
2. **Freeze.** Pull RT again: it stops, the streak ends, the glow **flares wide and strobes**.
3. **Launch away.** Fly past the frozen bomb and release when it is just behind you: a **huge**
   blast, and **you are thrown forward, away from it** (~3× cruise for a second). Try it beside
   you — thrown sideways; ahead of you — stopped. Blow it at the far edge of its blast — a weaker
   throw.
4. **Your trail survives** your own blast; an enemy trail inside it breaks; an enemy pilot inside
   it is knocked away.
5. **Pressure.** Feather LT: a tiny bomb, small dip, a 25 u blast. Full squeeze: a 100 u blast.
6. **Two bombs, fuse, impact, empty pool** — as before: independent; an unfrozen bomb goes off at
   rest (~3 s) or on the first prism; a low pool fires smaller; an empty one fizzles.
7. **The cannon is unchanged**: X fires the cannon's own shell and still launches along the nose.
8. **AI** — an AI Grizzly fires, freezes, flies past and blows its bombs, lunging forward each time.
9. **MPPM two clients** — each peer sees the other's bombs pulse, freeze and blow in the same places.
10. **Console clean** — no `[GrizzlyTriggerBomb]` errors; no missing-script warnings on
    `Grizzly.prefab` or `GrizzlyBomb.prefab`.

## Follow-ups

- **No element scales the trigger bombs** — the four elements are spoken for (Space = cannon, Mass
  = weapon systems, Charge = Dig In, Time = Rush). Candidates for design: Space on blast scale,
  Time on ammo regen.
- **No per-trigger HUD pips** beyond the Ammo bar. `OnBombStateChanged` / `OnBombFired` /
  `OnBombDetonated` are the hooks.
- **The cannon still launches along the nose.** If it should push away from its blast too, that is
  one branch in `VesselImpulseByExplosionEffectSO` (its self path) — a design call, not made here.
