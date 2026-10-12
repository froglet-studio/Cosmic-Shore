# Grizzly — Trigger Bombs (LT + RT)

Each trigger owns **one bomb**. Pull and release to fire it; pull again to **freeze** it where it
is; let go to **detonate** it. How hard you squeeze is the bomb: the peak pressure of the firing
pull sets the **ammo spent** and the **size** of both the bomb and its blast. A Grizzly inside its
own blast is **thrown away from the bomb**. **Only the trigger detonates a bomb** — never a clock,
never a contact. A bomb **cruises** — constant velocity, no drag, no range limit — until the
trigger freezes it **or it touches another vessel**, which freezes it too.

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
- **2026-10-08 — the fourth pass:** *"the bombs are stopping due to a friction of some sort. they
  should continue with constant velocity and no limitations on distance until frozen by use of the
  trigger or hitting another vessel. make the bomb look better too."* (Asked about "the rhino's
  bombs" — the trigger bombs are the Grizzly's; no other hull carries them.) The "friction" was the
  projectile's `cos(πt / 2T)` ease-out, which every round in the fleet flies; the bomb now opts out
  of it (`Projectile.Cruises`).

## The gesture (per trigger)

| State | Pull | Release |
|---|---|---|
| Idle | **arm** — this pull's peak pressure is the bomb | — |
| Arming | — | **fire**: spend ammo, launch a visible bomb |
| InFlight (cruising) | **freeze** the bomb in place | — |
| InFlight → **touches another vessel** | (frozen for you, where it touched) | — |
| Frozen | — (idempotent) | **detonate** it where it hangs |

**There is no fuse and no contact detonation.** A bomb flies **through** prisms — lighting them as
it passes (below) — and breaks nothing: its impact container,
`GrizzlyBombProjectileImpactContainer`, has all four effect lists empty, and it is fired with
`stopOnFirstPrismImpact: false`. It **cruises** (`Projectile.Cruises`): the launch velocity, held —
no ease-out, no lifetime, no range limit. Two things stop it:

- **the trigger's next pull** — however far out it has got; and
- **touching another vessel's hull** — any hull but the one that fired it (teammates included,
  since the ask was "another vessel"). `Projectile.VesselStruck` is raised for every hull the bomb's
  swept vessel query finds (`sweptVesselDetection` is on for the bomb, so a hull it crosses between
  fixed steps still counts), the executor ignores its own hull, and `FreezeShot` stops it **at the
  contact point** — `Projectile.Freeze` latches, so the rest of that frame's sweep halts there
  rather than stepping on to the end of the segment. A hull-stopped bomb is Frozen exactly as a
  pulled one is (armed look, AI timer), so **the next pull-and-release on that trigger blows it**.

A bomb is never a contact detonation: hitting a vessel stops it, it does not set it off. So a bomb
you forgot about is still yours: the next pull on that trigger reaches for it rather than firing a
new one. LT and RT are fully independent — two bombs can be cruising, frozen, or going off at once.
It is the charged cannon's lifecycle (`GRIZZLY_CHARGED_CANNON.md`) with **pressure** where the
cannon has **hold time**.

A bomb is retired without a blast only on **turn end**, the executor being **disabled**, or
**re-`Initialize`** — a bomb that can fly forever is exactly the one that could leak, so those
three are the whole of its retirement. An unfrozen bomb flies on until one of them, however far.

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
muzzle (the cannon is untouched). It reads as a **sea mine**:

- **Danger colour, two shades.** Both bombs wear the palette's **DANGER** colour
  (`SO_ColorSet.GetDangerSignalColor` — the danger rim, normalised), each turned **0.045** of the
  hue wheel (`sideHueShift`, ~16°) off it in opposite directions: **LT toward crimson-magenta, RT
  toward red-orange** (`GrizzlyTriggerBombConfigSO.BombColor`). Both read as danger; the two read
  as two. `fallbackDangerColor` stands in only when the palette authors no danger colour.
- **Core** — a small dark body with a hot fresnel rim in the bomb's shade, casting no shadow.
- **Spike crown** (`Spikes` child) — twelve cones on the icosahedron's axes, wearing the core's own
  fresnel material and colour. **Half-retracted in flight** (`spikesFlightScale` 0.8 — short horns)
  and **tumbling** (240 °/s about an off-axis spin, so it never reads as a wheel). The mesh is
  generated once at `Awake` and shared by every bomb (`GrizzlyBombVisual.SpikeMesh`), so the prefab
  shows a bare core in the editor. Its normals are **radial**: the core's `SpreadFresnelShader`
  pushes every vertex one world unit along its normal (`_Spread`'s default), and radial normals make
  each spike ride that push with the sphere instead of bloating sideways off it
  (`GrizzlyBombSpikeMeshTests`).
- **Halo** — an additive glow billboard (`glow1_ADD`) that **breathes** at 4/s; each peak flashes
  toward a warm spark colour.
- **Ping ring** (`Ring` child, `ring_ADD` billboard) — a ring born at the bomb that races out to 7×
  its size and fades, 1.6 times a second: a sonar return that says "live, and moving".
- **Comet** — the streak is the Astro League ball's plasma comet (`CosmicShore/BallTrail`,
  `Resources/BallTrail.mat`, ASTROLEAGUE.md): a soft halo, a white-hot core filament, three braided
  filaments unravelling down the wake and energy packets pouring tail-ward — tinted to the bomb's
  shade (at full value; brightness belongs to the white core) through a property block, 0.4 s long,
  1.4× the bomb's diameter wide. The trail's gradient is white with alpha 1 → 0, which is that
  shader's along-trail coordinate, not an opacity.
- **Armed** — the instant it freezes (the trigger, or a hull): the comet ends, the **spikes snap out**
  to full length with an elastic overshoot (`spikesArmedScale` 1.05 over 0.22 s), the halo
  **flashes** (+120 %, settling over the same 0.22 s) and then flares to 5× the core and strobes at
  14/s, the tumble slows to 45 °/s, and the ring **reverses** — it collapses from 6× onto the bomb,
  brightening as it closes, 3.5 times a second: "this one is about to go".
- **Lit prisms (LIT, `Docs/LIT.md`).** A hot body sliding through solid mass with no reaction reads
  as a clipping bug, so the bomb publishes a LIT volume every frame: a **cylinder** over the stretch
  it covered in the last 0.3 s (`litWakeSeconds`), starting one radius ahead of it, or a **sphere**
  once it is frozen — radius **1.5× the bomb's diameter** (`litRadiusFactor`; a prism is lit when
  its ORIGIN is inside, so the volume must reach past the bomb's skin). Every prism inside is drawn
  lit in the **firing pilot's domain colour** — LIT's rule 3, a light says *whose* force is in that
  mass — not in the bomb's danger shade. Visual only: nothing reads it to decide an outcome.

All colour rides `MaterialPropertyBlock`s on shared materials — no material instances. Pooled
shells are reset on every `Arm`, and the light starts its fade when the shell is disabled.

**Note on size.** The core's fresnel shader inflates it by a world unit all round, so a bomb's
*visible* diameter is its scale **+ 2** (4.5 for a tap, 7 for a full squeeze), not the scale the
table below lists. This predates this pass and is left as shipped; the spikes are built to ride it.

## Ammo

**Ammo** is index 1 on `Grizzly.prefab`'s `ResourceSystem` (Energy stays index 0, the cannon's).
Full pool 1.0, starts full, regenerates **0.15 / s**; about three full squeezes in a burst, then
one every ~2.3 s. A pull the pool cannot pay for in full fires **the biggest bomb it can pay for**;
under 0.08 it fizzles. The HUD shows it as an orange **Ammo** bar above the Energy bar.

## The projectile's flight

Muzzle speed **90 u/s** on top of the hull's own velocity (~140 u/s from a cruising Grizzly — well
ahead of you, easy to track), each trigger yawed **3°** off the muzzle (LT left, RT right) — and it
**holds that velocity** (`Projectile.Cruises`): no `cos(πt / 2T)` ease-out (vessel contract rule 34
no longer applies to this round), no lifetime, no range limit. It pulls away from a cruising
Grizzly at ~90 u/s for as long as nobody stops it; a boosting or bomb-launched Grizzly can still
catch it. The natural moves: fire and pull to freeze it where you want it, or fire it at a rival —
it stops on their hull — and blow it when you choose.

The gun still asks for a lifetime (`Gun.FireGun`'s `projectileTime`); the executor hands it a
nominal 1 s (`NominalFlightSeconds`) that a cruising round never reads past its first step. The
config's old `projectileTime` (the 3 s "throw") is retired.

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
self-launch is applied on the simulating machine only. Since a bomb never ends on its own,
every peer's copy of it lives exactly as long as the replicated trigger says. Known limits: a tap
shorter than one network tick can be sampled smaller on a remote peer than on the owner; and the
**hull stop is per peer** — each peer sweeps its own copy of the bomb against its own (replicated,
so slightly lagged) copies of the hulls, so a bomb that only GRAZES a rival can stop on one peer
and fly on on another. A clean hit stops on all of them. The blast then goes off where each peer's
copy hangs; the launch that matters (the owner's) uses the owner's copy.

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
| `Controller/Projectiles/GrizzlyBombVisual.cs` | The bomb's look: spike crown (+ its generated mesh), pulse, ping ring, comet, armed beat, danger shade, lit wake |
| `Controller/Projectiles/Projectile.cs` | `Cruises` — constant velocity, no lifetime; `VesselStruck` — every hull the flight touches; `Freeze` latches so a mid-sweep freeze stops at the contact |
| `Controller/ImpactEffects/Impactors/ProjectileImpactor.cs` | Raises `VesselStruck` for every hull, before the domain rule (that rule is the effects', not the listener's) |
| `Data/Enums/VesselVelocityModifier.cs`, `Controller/Vessel/VesselTransformer.cs` | `ShipVelocityModifier.ceiling` + the `ModifyVelocity(…, ceiling)` overload |
| `_Prefabs/Projectile/GrizzlyBomb.prefab` | The bomb (generated) |
| `_SO_Assets/Effects/Effect Containers/Projectile Containers/GrizzlyBombProjectileImpactContainer.asset` | The bomb's impact container — all four lists empty (generated) |
| `_SO_Assets/VesselActions/Grizzly/GrizzlyBomb{Left,Right}Action.asset`, `GrizzlyTriggerBombConfig.asset` | Assets |
| `_Prefabs/Spacevessels/Grizzly.prefab` | Bindings, executor, the bombs' pool / factory / `Gun`, **Ammo** resource, **AmmoBar** HUD |
| `UI/View/GrizzlyHUDView.cs`, `UI/Controller/GrizzlyHUDController.cs` | `ammoFill` / `SetAmmo`, bound to the resource named `Ammo` |
| `Tests/Editor/GrizzlyTriggerBombTests.cs` | Size / ammo / affordability / small-vs-big / launch 3× / danger shades / autopilot; `GrizzlyBombSpikeMeshTests` — the crown is seated (bases inside, radial normals, even spread) |
| `Tools/Build/author_grizzly_bomb_assets.py` | Authors `GrizzlyBomb.prefab` and its container from `GrizzlyShell.prefab` and wires the bombs' pool onto the Grizzly (`--check`) |

## Tuning knobs

`GrizzlyTriggerBombConfig.asset`:

| Field | Shipped | Effect |
|---|---|---|
| `pressureForMinBomb` / `pressureForMaxBomb` / `pressureExponent` | 0.1 / 0.95 / 1 | Pressure window → size 0..1 |
| `ammoIndex` / `minAmmoCost` / `maxAmmoCost` | 1 / 0.08 / 0.35 | Ammo per tap / full squeeze |
| `projectileSpeed` | 90 | Muzzle speed over the hull's; the bomb then cruises at that velocity, unlimited range |
| `minProjectileScale` / `maxProjectileScale` | 2.5 / 5 | Bomb diameter — small on purpose |
| `sideYawDegrees` | 3 | LT left / RT right |
| `minBlastScale` / `maxBlastScale` | 50 / 200 | Blast diameter → reach (½) and launch strength |
| `selfLaunchMultiplier` / `selfLaunchCeiling` / `selfLaunchSeconds` / `selfLaunchEdgeStrength` | 4.5 / 300 / 1 / 0.5 | The launch away from the bomb |
| `fallbackDangerColor` / `sideHueShift` | (1, 0.072, 0.105) / 0.045 | Danger shade per trigger |
| `aiFireStickBand` / `aiFreezeDistance` / `aiDetonateBehindDistance` / `aiFireIntervalSeconds` | 0.35 / 18 / 10 / 0.6 | Autopilot |
| `fireEvent` / `detonateEvent` | empty | FMOD — ship silent until audio authors them (LOCKED convention) |

`GrizzlyBomb.prefab` → `GrizzlyBombVisual` (authored by the generator — edit there, re-run):

| Group | Fields (shipped) |
|---|---|
| Comet | `trailWidthFactor` 1.4, `trailBraidSpread` 0.7, `trailIntensity` 1.2; length `TRAIL_SECONDS` 0.4 (generator) |
| Pulse | `pulseHz` 4, `haloScale` 3, `pulseAmplitude` 0.22 |
| Spikes | `spikesFlightScale` 0.8, `spikesArmedScale` 1.05, `spikesDeploySeconds` 0.22, `spinDegreesPerSecond` 240, `armedSpinDegreesPerSecond` 45; geometry `SpikeBaseRadius` 0.42 / `SpikeTipRadius` 1 / `SpikeBaseHalfWidth` 0.13 (code constants) |
| Ring | `ringPingHz` 1.6, `ringMinScale` 1.4, `ringMaxScale` 7, `armedRingHz` 3.5, `armedRingMaxScale` 6, `ringIntensity` 0.9 |
| Armed | `armedHaloScale` 5, `armedPulseHz` 14, `armFlash` 1.2 |
| Colour | `rimIntensityPeak` / `Trough` 6 / 1.5, `sparkColor`, `sparkMix` 0.45, `coreDarkness` 0.12 |
| LIT | `litRadiusFactor` 1.5, `litWakeSeconds` 0.3 | Ammo regen is `resourceGainRate` on the
Ammo entry of `Grizzly.prefab` (0.15).

The launch more than doubled the Grizzly's top speed (150 → 350 u/s), so **Grizzly Time's circuit
was re-cut** against it — see `Controller/Arcade/GRIZZLYTIME.md` §4.

## In-editor verification

Not run in the editor — no Unity in the authoring sessions. Steps:

1. **The bomb.** Fly a Grizzly (Menu freestyle via the Vessel Changer). Squeeze RT: a **small spiked
   mine** in **red-orange** leaves slightly right of the nose, faster than you, **tumbling**, a ring
   **pinging** out from it, a braided **plasma comet** behind it. Squeeze LT: a **crimson-magenta**
   one leaves left. Both clearly danger-red; clearly different. Ammo bar (orange) drops ~⅓ each.
2. **No friction, no range.** Leave a bomb alone: it holds its speed — it does **not** slow, does
   not stop, does not go off. 10 s later it is still flying (~2 km out) and still yours.
3. **No contact with mass.** Fire a bomb into a wall of prisms: it **passes through** at full speed,
   and the prisms it passes through **light up** in your domain colour as it goes, then fade behind
   it. Nothing breaks, nothing explodes.
4. **Freeze.** Pull RT on a flying bomb, however far out: it stops dead, the comet ends, the
   **spikes snap out** with a little overshoot, the glow **flashes, then flares wide and strobes**,
   the tumble slows, and the ring **collapses inward** onto it, over and over.
5. **Stops on a vessel.** Fire a bomb at an AI or a second player's hull: it **stops on contact**
   (where it touched, not past it), in the same armed look — and does **not** go off. Pull and
   release RT: it detonates there. Your own hull never stops it (fire it from a standstill, then
   fly through it).
6. **Launch away, 3×.** Fly past a frozen bomb and release when it is just behind you: a **huge**
   blast, and **you are thrown forward, hard** — ~7× cruise for a second, ~300 u. Beside you —
   thrown sideways; ahead of you — thrown back.
7. **Other shoves unchanged.** Get knocked back by an enemy blast, or Rush: no harder than before.
8. **Your trail survives** your own blast; an enemy trail inside it breaks; an enemy pilot inside
   it is knocked away.
9. **Pressure.** Feather LT: a tiny bomb, small dip, a 25 u blast. Full squeeze: a 100 u blast.
10. **Turn end** with a bomb cruising or frozen: it disappears with no blast.
11. **The cannon is unchanged**: X fires the cannon's own shell, which still eases to rest, still
    blows on impact and still launches along the nose.
12. **AI** — an AI Grizzly fires, freezes, flies past and blows its bombs, lunging forward each
    time, and leaves none flying.
13. **MPPM two clients** — each peer sees the other's bombs in their two shades, cruise, freeze
    (on the trigger and on a hull) and blow in the same places.
14. **Console clean** — no `[GrizzlyTriggerBomb]` errors; no missing-script warnings on
    `Grizzly.prefab` or `GrizzlyBomb.prefab`; no pink (missing-shader) comet or ring.

## Follow-ups

- **A bomb nobody freezes flies forever.** That is the ask (no distance limit), and it is retired on
  turn end — but in an endless freestyle session a forgotten bomb keeps flying (one per trigger, a
  few hundred metres a second) until the pilot pulls that trigger again, so float precision far
  from the origin is the only thing it will eventually meet. If that ever shows, a soft cap (retire
  silently past N km) is one line in the executor's `Update`.
- **The core's `_Spread` inflation** (see "Note on size") makes the visible bomb 2 units wider than
  its authored scale. Zeroing `_Spread` in the core's property block would make the table's sizes
  true, at the cost of shrinking every bomb — a look decision, not made here.

- **No element scales the trigger bombs** — the four elements are spoken for (Space = cannon, Mass
  = weapon systems, Charge = Dig In, Time = Rush). Candidates for design: Space on blast scale,
  Time on ammo regen.
- **No per-trigger HUD pips** beyond the Ammo bar. `OnBombStateChanged` / `OnBombFired` /
  `OnBombDetonated` are the hooks — a hanging bomb is a good candidate for one (it is easy to
  forget which trigger has a bomb out).
- **The cannon still launches along the nose, under the shared 100 u/s cap.** If it should push
  away from its blast too, or throw harder, that is one branch in
  `VesselImpulseByExplosionEffectSO` (its self path) — a design call, not made here.
