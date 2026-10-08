# Grizzly — Trigger Bombs (LT + RT)

Each trigger owns **one bomb**. Pull and release to fire it; pull again to **freeze** it where it
is; let go to **detonate** it. How hard you squeeze is the bomb: the peak pressure of the firing
pull sets the **ammo spent** and the **size** of both the bomb and its blast. A Grizzly inside its
own blast is **thrown away from the bomb**. **Only the trigger detonates a bomb** — never a clock,
never a contact.

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
- **2026-10-08 — the third pass:** *"the bombs should launch the grizzly 3x more. the bombs should
  not detonate with time or on impact. they can cause prisms to become lit as they pass through so
  it doesn't look like a clip. but bombs should only detonate when the trigger tells them to. make
  both bombs the danger color, but shift the colors a bit so they look different."*

## The gesture (per trigger)

| State | Pull | Release |
|---|---|---|
| Idle | **arm** — this pull's peak pressure is the bomb | — |
| Arming | — | **fire**: spend ammo, launch a visible bomb |
| InFlight (flying, or hanging at rest) | **freeze** the bomb in place | — |
| Frozen | — | **detonate** it where it hangs |

**There is no fuse and no contact detonation.** A bomb flies **through** prisms — lighting them as
it passes (below) — and touches nothing: its impact container,
`GrizzlyBombProjectileImpactContainer`, has all four effect lists empty, and it is fired with
`stopOnFirstPrismImpact: false`. A bomb nobody freezes eases to rest at the end of its 3 s throw and
**hangs there, live** (`Projectile.HoldAtFlightEnd`), until that trigger's next pull freezes it and
the release blows it. So a bomb you forgot about is still yours: the next pull on that trigger
reaches for it rather than firing a new one. LT and RT are fully independent — two bombs can be in
flight, hanging, frozen, or going off at once. It is the charged cannon's lifecycle
(`GRIZZLY_CHARGED_CANNON.md`) with **pressure** where the cannon has **hold time**.

A bomb is retired without a blast only on **turn end**, the executor being **disabled**, or
**re-`Initialize`** — a bomb that can hang forever is exactly the one that could leak, so those
three are the whole of its retirement.

## The launch — away from the bomb, three times harder

`GrizzlyTriggerBombExecutor.LaunchSelf`, applied at detonation:

- **Direction: from the bomb to you.** Leave a frozen bomb **behind** you and blow it — you are
  thrown forward. Beside you — you are thrown sideways. Ahead of you — it stops you dead and throws
  you back. (Sitting exactly on it, the nose stands in for "away".) The charged cannon still
  launches you along the nose (`VesselImpulseByExplosionEffectSO`); only the trigger bombs push
  away from themselves.
- **Strength:** the blast's own impulse, `blast scale / ExplosionDuration (1.2 s)`, ×
  `selfLaunchMultiplier` (**4.5** — three times the 1.5 it first shipped at) — **full at the bomb,
  easing to half at the blast's edge** (`selfLaunchEdgeStrength` 0.5), nothing outside it — over
  `selfLaunchSeconds` (1 s, cosine ease-out).
- **Its own ceiling.** Every shove a vessel takes shares `VesselTransformer`'s **100 u/s**
  velocity ceiling, and a full launch used to sit on it — so tripling the multiplier alone would
  have changed nothing a pilot could feel. The launch now carries its own ceiling,
  `selfLaunchCeiling` **300 u/s** (`ShipVelocityModifier.ceiling`): a live modifier may RAISE the
  shared cap for its own lifetime, never lower it, so every knock-back, Rush and cannon kick the
  Grizzly takes keeps the 100. A full squeeze ridden close hands over 750 u/s at the bomb, sits
  **on the 300 ceiling for its whole second** — 350 u/s with cruise, seven times cruise — and
  carries the hull **~300 u** (`GrizzlyTimeCourse.CarryAfterLaunch`).
- **Reach:** blast radius is half its scale (the AOE sphere's collider radius is 0.5): **100 u**
  for a full squeeze, **25 u** for a tap.
- **Your own trail is SPARED.** The blast is spawned with `AffectSelfOverride = false`: your own
  domain's mass shields instead of breaking and teammates are untouched, while enemy mass and
  pilots inside it are hit and knocked back radially (the impulse effect's bystander path). The
  launch does not need the blast to hit you, because the executor applies it directly.
- A dug-in Grizzly is blasted out of turret stance (through the controller, so the replicated flag
  stays in sync).
- Applied on the **simulating machine** only (your client, or the server for an AI): the hull's
  transform is what replicates.

## Small before, big after

| Squeeze | Bomb size | Ammo | Bomb (diameter) | Blast (radius) | Launch at the bomb |
|---|---|---|---|---|---|
| tap (≤ 0.1 pressure) | 0 | 0.08 | 2.5 | 50 (25 u) | 187.5 u/s (× the 1.5 → 0.5 ease, capped at 300) |
| half | ~0.5 | ~0.22 | ~3.75 | ~125 (~62 u) | ~469 → ceiling |
| full (≥ 0.95) | 1 | 0.35 | 5 | 200 (100 u) | 750 → ceiling 300 u/s |

Every blast is at least **ten times** its bomb (`TheBombIsSmallAndItsBlastIsHuge`), and a full
squeeze out-blasts the charged cannon's full charge (120).

## The look — `GrizzlyBomb.prefab` + `GrizzlyBombVisual`

The bombs have their own projectile, built from the cannon's round (`GrizzlyShell`) by
`Tools/Build/author_grizzly_bomb_assets.py`, and their own pool, factory and `Gun` on the cannon's
muzzle (the cannon is untouched):

- **Danger colour, two shades.** Both bombs wear the palette's **DANGER** colour
  (`SO_ColorSet.GetDangerSignalColor` — the danger rim, normalised), each turned **0.045** of the
  hue wheel (`sideHueShift`, ~16°) off it in opposite directions: **LT toward crimson-magenta, RT
  toward red-orange** (`GrizzlyTriggerBombConfigSO.BombColor`). Both read as danger; the two read
  as two. `fallbackDangerColor` stands in only when the palette authors no danger colour.
- **Core** — a small dark body with a hot fresnel rim in the bomb's shade, casting no shadow.
- **Halo** — an additive glow billboard (`glow1_ADD`) around the core that **breathes**: 2.5
  breaths/s at the muzzle, quickening (quadratically) to 11/s as the throw runs out, and holding
  11/s for as long as the bomb hangs at rest. Each peak flashes toward a warm spark colour.
- **Armed** — freezing a bomb stops its streak, **flares the halo** to 5× the core and strobes it
  at 14/s: "this one is about to go".
- **Streak** — a 0.3 s trail in the bomb's shade behind the flying bomb (Unity's Default-Line
  material, coloured per shot through the trail's own gradient), sized to the bomb.
- **Lit prisms (LIT, `Docs/LIT.md`).** A hot body sliding through solid mass with no reaction reads
  as a clipping bug, so the bomb publishes a LIT volume every frame: a **cylinder** over the stretch
  it covered in the last 0.3 s (`litWakeSeconds`), starting one radius ahead of it, or a **sphere**
  once it is at rest — radius **1.5× the bomb's diameter** (`litRadiusFactor`; a prism is lit when
  its ORIGIN is inside, so the volume must reach past the bomb's skin). Every prism inside is drawn
  lit in the **firing pilot's domain colour** — LIT's rule 3, a light says *whose* force is in that
  mass — not in the bomb's danger shade. Visual only: nothing reads it to decide an outcome.

All colour rides `MaterialPropertyBlock`s on shared materials — no material instances. Pooled
shells are reset on every `Arm`, and the light starts its fade when the shell is disabled.

## Ammo

**Ammo** is index 1 on `Grizzly.prefab`'s `ResourceSystem` (Energy stays index 0, the cannon's).
Full pool 1.0, starts full, regenerates **0.15 / s**; about three full squeezes in a burst, then
one every ~2.3 s. A pull the pool cannot pay for in full fires **the biggest bomb it can pay for**;
under 0.08 it fizzles. The HUD shows it as an orange **Ammo** bar above the Energy bar.

## The projectile's flight

Muzzle speed **90 u/s** on top of the hull's own velocity (~140 u/s from a cruising Grizzly — well
ahead of you, easy to track), each trigger yawed **3°** off the muzzle (LT left, RT right). The
flight eases to rest over the 3 s throw (`Projectile.MoveProjectileAsync`: `cos(πt / 2T)`), so a
cruising Grizzly catches its own bomb after ~2.3 s; range ≈ `speed × 2T/π` ≈ 270 u (vessel
contract rule 34) — and there it waits. The natural move: fire, pull to freeze as you close on
it, fly past, release.

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
self-launch is applied on the simulating machine only. Since a bomb no longer ends on its own,
every peer's copy of it lives exactly as long as the replicated trigger says. Known limit: a tap
shorter than one network tick can be sampled smaller on a remote peer than on the owner.

## AI

`GrizzlyTriggerBombExecutor`'s **autopilot drive**, on the simulating machine: while the stick is
straight (inside `aiFireStickBand` 0.35) and the pool can buy a full bomb, fire one; freeze it
**18 u** ahead (`aiFreezeDistance`); **fly past it** and detonate once it is **10 u behind**
(`aiDetonateBehindDistance`, well inside the 100 u blast), or after 1.5 s frozen — so the AI is
thrown forward, away from its bomb, and never leaves one hanging. Triggers alternate at most every
0.6 s; every step goes through the replicated press/release, so every peer runs the same bomb.
Gated on the PILOT, so the menu's lava-lamp Grizzly and released companions bomb-jump too.
`aiFireStickBand = 0` turns it off.

## Files

| File | Role |
|---|---|
| `Data Containers/GrizzlyBombActionSO.cs` | Trigger action (side only); press → `OnPress`, release → `OnRelease` |
| `Data Containers/GrizzlyTriggerBombConfigSO.cs` | All tuning + the pure pressure → size / ammo / scale maps and `BombColor` |
| `Executors/GrizzlyTriggerBombExecutor.cs` | Per-trigger state machines, pressure tracking, fire / freeze / detonate, `LaunchSelf`, autopilot |
| `Controller/Projectiles/GrizzlyBombVisual.cs` | The bomb's pulse, armed flare, danger shade, streak, lit wake |
| `Controller/Projectiles/Projectile.cs` | `HoldAtFlightEnd` — park at the end of the throw instead of ending the flight |
| `Data/Enums/VesselVelocityModifier.cs`, `Controller/Vessel/VesselTransformer.cs` | `ShipVelocityModifier.ceiling` + the `ModifyVelocity(…, ceiling)` overload |
| `_Prefabs/Projectile/GrizzlyBomb.prefab` | The bomb (generated) |
| `_SO_Assets/Effects/Effect Containers/Projectile Containers/GrizzlyBombProjectileImpactContainer.asset` | The bomb's impact container — all four lists empty (generated) |
| `_SO_Assets/VesselActions/Grizzly/GrizzlyBomb{Left,Right}Action.asset`, `GrizzlyTriggerBombConfig.asset` | Assets |
| `_Prefabs/Spacevessels/Grizzly.prefab` | Bindings, executor, the bombs' pool / factory / `Gun`, **Ammo** resource, **AmmoBar** HUD |
| `UI/View/GrizzlyHUDView.cs`, `UI/Controller/GrizzlyHUDController.cs` | `ammoFill` / `SetAmmo`, bound to the resource named `Ammo` |
| `Tests/Editor/GrizzlyTriggerBombTests.cs` | Size / ammo / affordability / small-vs-big / launch 3× / danger shades / autopilot |
| `Tools/Build/author_grizzly_bomb_assets.py` | Authors `GrizzlyBomb.prefab` and its container from `GrizzlyShell.prefab` and wires the bombs' pool onto the Grizzly (`--check`) |

## Tuning knobs

`GrizzlyTriggerBombConfig.asset`:

| Field | Shipped | Effect |
|---|---|---|
| `pressureForMinBomb` / `pressureForMaxBomb` / `pressureExponent` | 0.1 / 0.95 / 1 | Pressure window → size 0..1 |
| `ammoIndex` / `minAmmoCost` / `maxAmmoCost` | 1 / 0.08 / 0.35 | Ammo per tap / full squeeze |
| `projectileSpeed` / `projectileTime` | 90 / 3 | Muzzle speed over the hull's; the throw (not a fuse) |
| `minProjectileScale` / `maxProjectileScale` | 2.5 / 5 | Bomb diameter — small on purpose |
| `sideYawDegrees` | 3 | LT left / RT right |
| `minBlastScale` / `maxBlastScale` | 50 / 200 | Blast diameter → reach (½) and launch strength |
| `selfLaunchMultiplier` / `selfLaunchCeiling` / `selfLaunchSeconds` / `selfLaunchEdgeStrength` | 4.5 / 300 / 1 / 0.5 | The launch away from the bomb |
| `fallbackDangerColor` / `sideHueShift` | (1, 0.072, 0.105) / 0.045 | Danger shade per trigger |
| `aiFireStickBand` / `aiFreezeDistance` / `aiDetonateBehindDistance` / `aiFireIntervalSeconds` | 0.35 / 18 / 10 / 0.6 | Autopilot |
| `fireEvent` / `detonateEvent` | empty | FMOD — ship silent until audio authors them (LOCKED convention) |

`GrizzlyBomb.prefab` → `GrizzlyBombVisual` (authored by the generator — edit there, re-run):
`pulseHzAtLaunch` 2.5, `pulseHzAtRest` 11, `haloScale` 3, `pulseAmplitude` 0.22,
`armedHaloScale` 5, `armedPulseHz` 14, `rimIntensityPeak` / `Trough` 6 / 1.5, `sparkColor`,
`sparkMix` 0.45, `coreDarkness` 0.12, `trailWidthFactor` 0.7, `litRadiusFactor` 1.5,
`litWakeSeconds` 0.3; streak length `TRAIL_SECONDS` 0.3. Ammo regen is `resourceGainRate` on the
Ammo entry of `Grizzly.prefab` (0.15).

The launch more than doubled the Grizzly's top speed (150 → 350 u/s), so **Grizzly Time's circuit
was re-cut** against it — see `Controller/Arcade/GRIZZLYTIME.md` §4.

## In-editor verification

Not run in the editor — no Unity in the authoring session. Steps:

1. **The bomb.** Fly a Grizzly (Menu freestyle via the Vessel Changer). Squeeze RT: a **small**
   glowing **red-orange** bomb leaves slightly right of the nose, faster than you, with a short
   streak. Squeeze LT: a **crimson-magenta** one leaves left. Both clearly danger-red; clearly
   different. Ammo bar (orange) drops ~⅓ each.
2. **No fuse.** Leave a bomb alone: it slows, stops ~270 u out and **hangs there, pulsing** —
   it does not go off. 10 s later it is still there.
3. **No contact.** Fire a bomb into a wall of prisms: it **passes through**, and the prisms it
   passes through **light up** in your domain colour as it goes, then fade behind it. Nothing
   breaks, nothing explodes.
4. **Freeze.** Pull RT on a flying bomb: it stops, the streak ends, the glow **flares wide and
   strobes**. Pull RT on a bomb hanging at rest: same.
5. **Launch away, 3×.** Fly past the frozen bomb and release when it is just behind you: a **huge**
   blast, and **you are thrown forward, hard** — ~7× cruise for a second, ~300 u. Try it beside
   you — thrown sideways; ahead of you — thrown back. Blow it at the far edge of its blast — a
   weaker throw.
6. **Other shoves unchanged.** Get knocked back by an enemy blast, or Rush: no harder than before
   (the 100 u/s cap still holds for everything but the bomb launch).
7. **Your trail survives** your own blast; an enemy trail inside it breaks; an enemy pilot inside
   it is knocked away.
8. **Pressure.** Feather LT: a tiny bomb, small dip, a 25 u blast. Full squeeze: a 100 u blast.
9. **Turn end** with a bomb hanging: it disappears with no blast.
10. **The cannon is unchanged**: X fires the cannon's own shell, still blows on impact and still
    launches along the nose.
11. **AI** — an AI Grizzly fires, freezes, flies past and blows its bombs, lunging forward each
    time, and leaves none hanging.
12. **MPPM two clients** — each peer sees the other's bombs in their two shades, hang, freeze and
    blow in the same places.
13. **Console clean** — no `[GrizzlyTriggerBomb]` errors; no missing-script warnings on
    `Grizzly.prefab` or `GrizzlyBomb.prefab`.

## Follow-ups

- **No element scales the trigger bombs** — the four elements are spoken for (Space = cannon, Mass
  = weapon systems, Charge = Dig In, Time = Rush). Candidates for design: Space on blast scale,
  Time on ammo regen.
- **No per-trigger HUD pips** beyond the Ammo bar. `OnBombStateChanged` / `OnBombFired` /
  `OnBombDetonated` are the hooks — a hanging bomb is a good candidate for one (it is easy to
  forget which trigger has a bomb out).
- **The cannon still launches along the nose, under the shared 100 u/s cap.** If it should push
  away from its blast too, or throw harder, that is one branch in
  `VesselImpulseByExplosionEffectSO` (its self path) — a design call, not made here.
