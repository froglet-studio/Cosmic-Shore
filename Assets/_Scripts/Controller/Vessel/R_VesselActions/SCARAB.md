# Scarab — the Rocket League vessel (design proposal)

> **STATUS UPDATE (2026-08-14): the FOUNDATION is implemented on this branch** — enum 12,
> `Scarab.prefab` (Sparrow clone, weapons excised), `ScarabVesselTransformer` (integrator
> throttle + Snap Dash), `ScarabJukeController`, `PlaceSwitchActionSO`/executor (ring body v1),
> crystal→energy/charge effects, ability map (Space row + all L5 labels left open per the gate),
> containers, camera SO, class card, and all registrations (arcade card, prefab container,
> network prefabs, vessel-changer toy). In-editor verification steps + first-pass tuning:
> `Docs/UNITY_VERIFICATION_CHECKLIST.md` § Scarab. Everything below the next paragraph is the
> DESIGN record; §4–§5's ball/switch-trigger mode work and the HUD pass are NOT yet built.

> **Original design gate note — nothing beyond the foundation is implemented.** Written for Garrett to
> mark up before any code or asset lands (the `/vessel` design-approval gate). The element map is
> mirrored as a proposal row in `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2. Every file/line
> citation was verified against branch HEAD (2026-08-12); values marked *(proposal)* are
> first-pass tuning, everything else is shipped ground truth the design builds on.
>
> **Revision 2** — rewritten from the design notes of 2026-08-12, which changed the vessel's name,
> its ball model (player-generated, multi-ball, permanently coloured), replaced the braking wall
> with the **switch**, and re-cut the element map. The superseded first pass (name "Mantis", a
> single mode ball launched by a cavitation cone, a braking wall on the A button) is kept only in
> git history — **the notes are the record, do not re-litigate from the first pass.**

The **Scarab** is a dung beetle: the animal whose entire behavioural repertoire is *rolling a ball
it made itself*. It is Cosmic Shore's **Rocket League vessel** — and the distinction that governs
every decision below is that it is a *vessel*, not a Rocket League minigame clone. Its kit is
designed at the intersection of four uses (§1), and it earns its place by being a full citizen of
the HyperSea everywhere, not a special case that only functions inside one arena.

Its verbs: **fly fast and loose** (analog throttle + analog drift), **shove** (the juke — a side
bump that hits vessels and balls alike), **make balls out of crystal energy**, and **place
switches** — curved directional rings that both deflect balls and pay energy when a ball threads
them. The economy closes on itself: crystals make balls, balls through switches make energy,
energy makes balls.

- Class: `VesselClassType.Scarab = 12` (next free ID; highest today is `Sparrow = 11`,
  `Assets/_Scripts/Data/Enums/VesselClassType.cs`).
- Prefab: `Assets/_Prefabs/Spacevessels/Scarab.prefab` (name must equal the enum member —
  auditors and `ElementalAbilityMapSO.LoadFor` key off it).

---

## 1. The quadrality — four uses, one design

The Scarab is designed against **four simultaneous constraints**, and the design notes are
explicit that this is the method, not a nice-to-have: a mechanic earns its place by paying off in
all four lanes, and *each use case highlights why the others are more fun*. Constraint-based
design of this shape produces a cohesive, mutually reinforcing kit rather than a vessel that is
compromised everywhere.

| Lane | What the Scarab is there |
|---|---|
| **Minigame** (Astro League) | The striker: generate a ball, drive it, thread it through the enemy net; place switches to farm energy and to bat loose balls off your own goal-line |
| **Esport play** | The legibility constraint. Every verb must be readable by a spectator and masterable: analog throttle and drift give a continuous skill ceiling; the juke is a read; switch placement is the strategy layer |
| **Free space toy** (Menu_Main freestyle) | A self-contained loop with no score and no end condition: make balls, place rings, punt balls through them. The ball/switch pair IS a toy — see §9.2 |
| **HyperSea citizen** | An ordinary vessel anywhere: it lays trail, skims, collects crystals, fights, and its balls interact with prisms and the food web by the platform's own rules |

**Consequence — the Scarab is NOT mode-locked.** The first pass proposed an Astro-League-only
vessel; the notes overrule that. It ships into the hangar, the vessel-changer toy, and the arcade
cards like any other hull. Astro League is where it is *tuned*, not where it is *fenced*.

The chaos this produces is a deliberate lane choice: multiple simultaneous balls make the mode a
**party game**, not a competitive esport. The esport lens is a constraint on legibility, not a
mandate for determinism.

---

## 2. Kit summary

| Control | Verb | One line |
|---|---|---|
| **Left stick** | Pitch / yaw | Single-stick flight (Sparrow/Serpent family) |
| **RT (analog)** | **Throttle** | Continuous analog *acceleration* — not a target-speed dial. The fleet's first |
| **LT (analog)** | **Drift** | Analog heading/course decoupling (the Squirrel's exact `singleTriggerDrift` scheme) |
| **Right stick (to perimeter)** | **Juke** | Sparrow-style side bump: a lateral displacement that shoves enemy vessels and strikes balls |
| **A button** | **Switch** | Places a curved directional ring: deflects balls, pays energy when one threads it, then breaks |
| **Crystal contact at full energy** | **Ball** | The crystal materialises into a ball carrying your velocity — no button, an aimed act |

Four element-mapped abilities — **Ball · Switch · Juke · Throttle** — plus drift as base kit
(the Sparrow's strafing roll is the precedent for an unmapped base verb). Map in §7.

---

## 3. Control model

### 3.1 Input plumbing (ground truth the scheme rides on)

The gamepad naming trap first: on gamepad, `InputEvents.LeftStickAction (2)` / `RightStickAction
(1)` are raised by the **triggers** (LT/RT edge events at `TriggerDeadzone = 0.05f`), not the
sticks — the names come from the touch scheme (`Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`).
The analog values publish continuously as `InputStatus.LeftTriggerAnalog` / `RightTriggerAnalog`
(owner-write NetworkVariables — readable on remote peers). The A button (`buttonSouth`) raises
`InputEvents.Button1Action (6)` (GamepadInputStrategy.cs:57-61; `InputHintBindingMap` agrees:
`PadButtonSouth → Button1Action`; the enum-file comments saying Button1 = X are stale — code
wins. On desktop the raise site is the live `KeyboardInputStrategy` — the stray file at `Assets/`
root, **Space** key; `Controller/IO/KeyboardMouseInputStrategy.cs` is dead code no strategy
selector instantiates, a known audit trap).

**No dedicated InputEvent exists for right-stick deflection** — stick direction is polled state
only, which is exactly how the Sparrow's `BarrelRollController` consumes it. (The derived
straight-line gestures `FullSpeedStraightAction (0)` / `MinimumSpeedStraightAction (5)` do fold
right-stick components into their `XDiff`/`XSum` math — the Scarab leaves both unbound, so a
stick pinned at the perimeter for a juke perturbs nothing.)

| Physical control | Plumbing | Scarab binding |
|---|---|---|
| Left stick | `EasedLeftJoystickPosition` | Pitch/yaw via the transformer (§3.5) |
| RT | `InputStatus.RightTriggerAnalog` + `RightStickAction (1)` edges | Throttle integrated per-frame by `ScarabVesselTransformer` (§3.2); the **edges are the double-tap detector** for the Time-5 dash (§3.6) |
| LT | `InputStatus.LeftTriggerAnalog` + `LeftStickAction (2)` edges | `LeftStickAction (2)` → `[ScarabSharpDriftAction, ScarabDriftAction, DriftTrailAction]` — the Squirrel stack |
| Right stick | `RightNormalizedJoystickPosition` (polled) | `ScarabJukeController` (§3.4) |
| A (`buttonSouth`) / Space | `Button1Action (6)` | `PlaceSwitchAction` (§5) |
| RB | raises `FlipAction` + feeds binary `InputStatus.Throttle` | unbound (RB's `Throttle` feed is ignored — §3.2) |
| B / X / Y, LB, D-pad | `Button2Action` / `Button3Action` / — | unbound |

Combo events (`OnlyLeftStickAction (12)` / `OnlyRightStickAction (11)` / `BothSticksAction (13)`)
fire constantly under a two-analog-trigger scheme — all three stay unbound on gamepad. Touch: the
drift stack mirrors the Squirrel's touch overrides (`OnlyLeftStickAction` → drift). ⚠ **Touch has
no raise site for `Button1Action`** (the "Onscreen UI buttons" the enum comments promise don't
exist in code) — switch placement is gamepad/desktop-only until an on-screen button raising the
shared `OnButtonPressed` SOAP event is added (open item, §15).

### 3.2 Throttle — continuous analog acceleration (a different model from the fleet)

Every shipped vessel computes a **target speed** and eases toward it
(`ComputeThrottleTarget()` → `AdvanceSpeed()`, exponential `Mathf.Lerp(speed, target, 1.5f·dt)`).
The notes ask for something else: *continuous analog acceleration with no hard speed cap
initially*. That is an **integrator**, not a dial — hold the trigger and you keep gaining speed;
the trigger's depth is how hard you push, not how fast you end up.

**`ScarabVesselTransformer : SingleStickVesselTransformer`** — inherits left-stick-only
pitch/yaw/roll (`EasedLeftJoystickPosition`; the Sparrow/Serpent/Grizzly base) and replaces the
target model:

```
speed += RightTriggerAnalog × accelerationPerSecond × dt      // push
speed -= coastDragPerSecond × dt                              // release → long coast, never a stop
speed  = Clamp(speed, MinimumSpeed, TopSpeed)
TopSpeed = baseTopSpeed × ThrottleScalerMultiplier.EvaluateLive(status)   // ← the Time element
```

Notes and consequences, all of which are design decisions worth marking up:

- **Momentum is the feel.** A long coast (low `coastDragPerSecond`) is what makes the vessel read
  as a *thing with mass* rolling around an arena — the Rocket League register — and it is what
  makes drift meaningful, since a decoupled course only matters when you carry speed through it.
- **"No hard speed cap initially" is a genuine ambiguity** (§15): does the ceiling not exist at
  element level 0 (uncapped ramp, Time raising a *soft* limit), or does it exist and Time raise
  it? Recommended reading: the ceiling always exists and Time raises it, because a truly uncapped
  vessel breaks three things that are already shipped and measured — the **speed tunnel
  saturates** at `maxEffectSpeed 280` (so past ~280 the optics stop conveying speed at all), the
  **ball caps at 300** (`AstroLeagueSettings.maxSpeed`, so above that you can never impart your
  own velocity to a ball), and the **arena is only 400–540u long** (so at 300 u/s you cross the
  entire pitch in under two seconds and every touch becomes a coin flip). A ceiling in the
  200–260 band keeps the vessel the fastest thing on the pitch while leaving all three intact.
- The analog read is per-frame in the transformer (the `ShieldSwipeActionExecutor` /
  `MantaAnalogTurnBoostExecutor` precedent: continuous behaviours poll `InputStatus`, they do not
  ride events). `InputStatus.Throttle` — the binary right-**bumper** feed ("this is just the boost
  button", GamepadInputStrategy.cs:47) — is ignored entirely; repointing it would be a global
  input change affecting every vessel.
- `ThrottleScalerMultiplier` is the **existing** `ElementalFloat` on `VesselTransformer` (the
  Squirrel ships it disabled) — the Scarab enables it as its Time scaling, and the map's generic
  Time multiplier is pinned to 1 so `CurrentBoostAmount()` can never double-dip.
- Speed-tunnel law: nothing to author (absolute fleet-wide mapping) — the tunnel becomes the
  throttle's readout for free, crossing `minEffectSpeed 70` partway up the ramp.

### 3.3 Drift — analog, on LT

The Squirrel's scheme, zero new transformer code: prefab `singleTriggerDrift = 1` →
`GetTriggerSum()` returns `LeftTriggerAnalog × 2`, so LT's travel spans no-drift → full single →
full sharp analogically; `LeftStickAction (2)` binds `[ScarabSharpDriftAction, ScarabDriftAction,
DriftTrailAction]`. Drift decouples heading from course (`Course` slerps between
`transform.forward` and the drifted course by trigger sum) and **never touches speed** — 100%
retention. On a pitch this is the whole handling story: nose at the ball, momentum carrying you
across its path.

First-pass values *(proposal)*: single `Mult 1.4 / damping 0.5 / sfx on`, sharp `Mult 1.8 /
damping 0.25 / sfx off` (the Squirrel's shipped pair — start from proven feel).

### 3.4 The juke — `ScarabJukeController`

Modelled on the Sparrow's `BarrelRollController`
(`Assets/_Scripts/Controller/Vessel/BarrelRollController.cs`) — a plain per-frame poll, not a
`ShipActionSO` — with three differences:

1. **Right stick, not left.** Fire gate: `RightNormalizedJoystickPosition` radial magnitude ≥
   `perimeterThreshold (1) − ε` — the radially-clamped **raw** stick, deliberately not the eased
   vector (per-axis easing makes diagonal magnitudes direction-dependent; the Sparrow learned
   this). On the Scarab the right stick is otherwise unused (single-stick steering), so the juke
   collides with nothing.
2. **Cooldown-armed, not boost-armed.** The Sparrow arms one roll per boost press; the Scarab has
   no boost button, so the juke re-arms on a plain cooldown (`jukeCooldownSeconds 1.2`
   *(proposal)*), shown as a binary pip (§12). A cooldown on a vessel ability is input pacing, not
   world decay — nothing is removed from the world by a clock.
3. **It is an attack.** Displacement is the Sparrow's construction verbatim:
   `transformer.ModifyVelocity(dir.normalized × jukeSpeed, jukeDurationSeconds,
   ignoresTranslationRestriction: true)` — the cosine-eased impulse channel clamped at
   `velocityModifierMax 100`; direction = `ship.right × stick.x + ship.up × stick.y` projected
   onto the plane ⊥ `VesselStatus.Course`, with the same `transform.forward` and
   `ship.right × rollSign` fallbacks; a 360° smoothstep spin on the **visual child only** (the
   camera reads the root); real root bank `rootRollDegrees 15`; `BlockRotationOverride` set each
   rolling frame so bridging trail prisms lay travel-aligned, cleared when done. What is new is
   what the juke **hits**:
   - **Enemy vessels** — juking into an opponent shoves them (the Rocket League bump). ⚠ **There
     is no hull-vs-hull contact event in the platform at all** — `VesselImpactor.AcceptImpactee`
     handles prisms, crystals and skimmers, and has no vessel case. *All* vessel-on-vessel
     interaction is **skimmer-mediated**: one vessel's hull sweeping the other's skimmer volume
     runs that skimmer's `VesselSkimmerEffects` (the joust, the same-domain overtake buff, spin,
     shrink…), gated on **relative speed** and **opposing domain**, owner-authoritative. So the
     shove is a new `VesselSkimmerEffectsSO` in the Scarab's *skimmer* container, not its vessel
     container — and it must be a **new effect, not the joust**, for a reason that is easy to
     miss: `ModifyVelocity` displaces the vessel without touching `VesselStatus.Speed`, so a
     juking Scarab reads as *no faster than usual* and would fail the joust's
     faster-vessel-wins gate every time. The shove's own gate must read the juke state, not speed.
   - **Balls** — a ball caught by the juking hull is struck with the juke's true velocity, which
     is larger and more lateral than the vessel's flight velocity. Same caveat, different
     consumer: the ball samples striker velocity from **per-tick transform deltas** (not
     `Course * Speed`), so it sees the juke's real motion correctly with nothing to fix. This is
     how you hit a ball sideways without turning.

**Open decision (§15):** the original brief asked for the juke to fire a *short-range lateral cone
of destruction* — a real `AOEConicExplosion` that also shreds prisms. The revised notes describe
the juke as a bump. The two readings differ in exactly one respect: whether the juke destroys
**mass** (trail, switches) as well as shoving vessels and balls. The design below assumes the
bump; the cone is a one-prefab addition if wanted (`AOEConicExplosion`, `proportionalDebris 1`,
`devastating 1`, spawned with `Quaternion.LookRotation(jukeDir)` — and note super-shielded mass
both survives it and destroys the blast, so the Astro League edge lining protects itself for
free).

**Replication.** The Sparrow's roll needs none (displacement rides the owner-authoritative
NetworkTransform). The juke's *hits* are outcome-affecting, so `ScarabJukeController` is a
`NetworkBehaviour`: owner poll → execute locally (zero latency) → `Juke_ServerRpc(dir)` →
`Juke_ClientRpc` → non-owner peers play the visual (sender-filtered). The ball strike and the
vessel shove resolve **server-side**, where the ball already lives.

### 3.5 Pitch/yaw on the left stick

Free with `SingleStickVesselTransformer`. First-pass scalers *(proposal)*: Pitch/Yaw/Roll
`100/100/30`, `RotationThrottleScaler 0.1`.

### 3.6 Time 5 — the double-tap dash

The Time level-5 upgrade (§7) is a **double-tap of the throttle** producing a burst/dash gap
closer. The *signal* is already in the pipeline: RT crossing `TriggerDeadzone 0.05` raises
`RightStickAction (1)` **press and release** edges, so two press edges inside
`doubleTapWindowSeconds 0.3` *(proposal)* is the gesture — using the one event the Scarab was
otherwise going to leave unbound. The *detector* is net-new: nothing in the codebase does
double-tap, multi-tap or press-timing today (verified — zero hits fleet-wide). It is a timestamp
comparison, so it is small; the only decision is where it lives. Keep it local to the Scarab's
transformer unless a second vessel wants one, in which case it belongs on the shared input
layer so every strategy inherits it. The dash itself reuses
`ModifyVelocity` along `Course` (the same impulse channel as the juke, so the 100-unit clamp and
the eased envelope are shared and already tested), gated on `IsUpgradeActive(Element.Time)` at
the moment of the second tap.

---

## 4. The ball

This is the largest departure from the shipped mode, and it is worth stating plainly: **today
Astro League has exactly one ball**, owned by the match, re-centred on every goal, recoloured to
whoever touched it last, and contained by boundary reflection. The Scarab's design replaces all
four of those properties.

### 4.1 Generation — crystals become balls

Balls are made from **crystal energy**, not spawned by the mode:

1. Collecting crystals fills the Scarab's energy meter (a `ResourceSystem` resource — normalized
   0..1, no passive regen; the Sparrow missile-meter pattern).
2. When the meter reaches its **final threshold**, the *next crystal contact* does not collect —
   it **materialises that crystal into a ball**, carrying **inherited velocity** from the impact.
3. The meter drains by the ball's cost, and the crystal respawns as normal.

Two things make this the right shape rather than a spawn button. First, it is an **aimed act**:
you must fly *through* a crystal at the speed and heading you want the ball to have, so making a
ball is a piece of driving, not a menu choice — and the vessel's own throttle/drift skill sets
the ball's opening velocity. Second, it works entirely **through the crystal fundamental**: no
new spawner, no mode-local pickup, and every crystal source the platform already has (the mode's
respawning anchor crystal, fauna-dropped elemental hearts, freestyle crystals) feeds it.

**Wiring — crystal-side, not vessel-side.** The Astro League anchor crystal carries an
`OmniCrystalImpactor`, and its `AcceptImpactee` is already server-gated, already latched against
multi-collider double-fire, and already holds the striking `VesselImpactor` — so it can see the
Scarab's energy meter, domain, position and velocity in one place. The recommended shape is a
subclass, `BallForgeCrystalImpactor : OmniCrystalImpactor`, swapped onto a Scarab-facing crystal
prefab (`IsDomainMatching` is already a `protected virtual` seam and `TeamCrystalImpactor` is the
existing precedent for subclassing it): below threshold fall through to today's collect; at
threshold, materialise the ball and skip the collect so no fuel or score is granted, then
`Crystal.Respawn()` exactly as today.

The tempting alternative — a `VesselCrystalEffectSO` in the Scarab's own container — is worse for
a specific reason worth recording: the vessel-side and crystal-side chains run **independently**,
so the crystal would still run its own collect/explode/respawn regardless of what the vessel-side
effect decided, and the vessel-side path is not guaranteed to be on the server. Choose the
crystal side.

**The no-generation zone.** Balls may not be generated in the **last quarter of the arena nearest
the opponent's goal** — otherwise you would simply carry energy to their doorstep and materialise
point-blank shots, which is trivial and unfun. The gate is a spatial test at generation time
against the arena's own geometry (goal plane distance vs `Boundary.MaxExtent`), refused with a
distinct failure cue. Outside a mode with goals (freestyle), the gate is inert.

### 4.2 Permanent team colour

**A ball is its maker's colour forever.** No striker recolouring: an opponent can bat your ball
around, but it never becomes theirs. Today the ball recolours on *every* vessel contact
(`n_LastHitDomain` — deliberate or not), and that same value drives what the ball eats — so
permanence is not just cosmetic, it makes the ball's interaction with the world **stable and
readable**: your ball always eats the enemy's trail and always shields yours, from birth to
death. In a multi-ball arena, that readability is what keeps the chaos legible (the esport
constraint from §1 doing real work).

Cheap to build, as it turns out: the domain is written in exactly two places (on contact, and
back to neutral on kickoff re-centre) and read in three (the prism diet, the material tint, and a
public accessor with no callers today). Dropping the contact write and seeding the value at
creation is nearly the whole change — it must stay a replicated variable, because the prism-diet
scan runs on **every peer**.

⚠ **Colour does not currently drive scoring.** Goal credit comes from the striker list, not from
the ball — so a permanently-Ruby ball put through a net by a Jade last-toucher credits *Jade*
today. That is either exactly right (Rocket League's own-goal rule) or exactly wrong (your ball,
your point), and it is a decision, not an accident: §15.12.

### 4.3 Death at the boundary

**A ball that leaves the nucleus boundary is destroyed** — no carom. The arena boundary today
reflects the ball inward with `wallRestitution 0.72`; the Scarab's balls instead expire on
contact with it. This is what makes a ball a *resource you can waste*: a wild shot is gone, and
the balance target in §6 exists precisely to keep that from feeling punishing.

Continuity of existence applies — a ball must **dissipate visibly** (fade/collapse/evaporate),
never blink out. It is not prism mass, so mass conservation is not implicated; the continuity law
is platform-wide regardless. The mode's existing detonate path (which already hides the ball and
plays a client-side burst on a goal) is the animation to reuse.

Two implementation notes from the shipped boundary: `Contain` reports only *"it bounced"* and
**clamps the ball's position unconditionally**, discarding the penetration depth — a destroy
variant needs that depth, so the branch belongs at the call site with a pre-clamp read. And a
ball whose boundary reference is never set is **completely unbounded** (the containment call
early-outs), so runtime-created balls must be handed the boundary at creation or they coast out
of the world forever.

### 4.4 Multiple balls

Several balls coexist. This is the mode's chaos dial and the reason the notes place it in the
**party-game** lane. Every player generating their own permanently-coloured balls means the
arena state is a live population, not a single tracked object — closer to a playground than to a
match clock.

### 4.5 What this costs on the mode side (honest accounting)

The mode is **architecturally one-ball**, more so than it looks. Verified against the shipped
code, in rough order of cost:

1. **There is no ball prefab.** The ball is a *scene-placed* NetworkObject spawned by Netcode's
   scene sync — no prefab asset exists, and nothing registers it in the network prefab list. There
   is also **no spawn/despawn API**: the entire match-control surface is freeze / hide /
   re-centre. Step one of a multi-ball model is extracting the prefab and registering it. (Its
   `Awake` is self-sufficient — it builds its own rigidbody, physics material, layer exclusions
   and visuals — so a runtime instance comes up correctly once it is handed its settings, its
   boundary, its size scale and its spawn position, and once every goal is re-configured to see
   it.)
2. **Four hard-wired single references**: the controller's, the arena's (portal anticipation
   glow + boundary handoff), the goal's, and the goal-replay recorder's.
3. **The goal detector holds per-ball crossing state as plain fields** (last position + a
   has-sampled flag) — meaningless with N balls; it must become per-ball state or move onto the
   ball.
4. **Strike attribution is global and cannot express a ball.** The controller keeps one
   two-element striker list, and the strike handler's signature takes *(vessel, intensity)* with
   **no ball parameter at all** — so with N balls, whoever last hit *any* ball is credited for
   *every* goal. This is the single most misleading break, because it produces plausible-looking
   wrong scores rather than an error.
5. **A goal stops the whole match** — celebrate, pause the clock, global slow-mo, park every
   vessel, sweep every field prism, re-centre, kickoff-freeze. With other balls still in flight
   that is wrong by construction, and it is a *design* question (§15), not just plumbing.
6. **The HUD objective marker** finds a ball once with `FindAnyObjectByType` and caches it
   forever — it would latch onto an arbitrary ball for the match.
7. **The AI striker** reads the one ball directly for role assignment and intercept; nearest-ball
   selection does not exist.
8. **Cost scales**: each ball samples every vessel's velocity every physics tick, so N balls
   means N× redundant sampling — a real reason to cap the population (§13).

None of this is exotic, but it is substantial mode work and should be scoped up front rather than
discovered mid-implementation. Whether it belongs in the vessel branch at all is §15.11.

---

## 5. The switch

The Scarab's placeable structure is **not** a wall. It is a **curved, directional switch** — a
ring with a mouth and a curved deflecting panel, placed ahead of the vessel on the A button, with
its size and shape governed by the Mass element (§7).

A switch does **two jobs at once**, and every interesting decision comes from their interaction:

1. **It deflects.** The curved panel bats balls off their line. Aimed one way it funnels shots
   toward a goal; aimed the other it is a defensive backboard that turns an incoming shot away
   from your own net. Direction and curvature are the whole skill of placement.
2. **It pays.** A ball that threads the mouth triggers the switch: the placer receives **energy**
   (which is to say, progress toward the next ball) and the switch is **destroyed**.

**Any ball triggers it — friendly or enemy.** This is the design's best idea and it should not be
softened: because an enemy ball threading your switch still pays *you*, switches are worth
placing where the enemy's balls will go, i.e. defensively, in front of your own goal. The
defensive play and the economic play are the same play. A player who ignores defence starves.

**The technical crux — a prism can never bounce a ball.** The ball's collider excludes the
`TrailBlocks` layer outright, and its prism interaction is a spatial-index sweep that *eats*
mass with a drag multiplier: it has no reflection path against prisms at all, and the only
surfaces it bounces off today are the analytic arena boundary and vessels. So a switch made of
plain prisms would be a *speed bump*, not a deflector, and the entire "curved panels deflect
balls" mechanic would silently not exist. The switch must therefore be a **structure with an
analytic deflecting surface** — the same class of thing as the arena boundary, whose curved
inward-facing planes already reflect the ball correctly — while its *visual body* is prisms
(domain-coloured, blooming in, grazeable by fauna, a citizen of the mass economy). Recommended
shape: a mode-side `Switch` object owning (a) a curved reflector the ball resolves against, and
(b) a mouth-crossing detector — which is precisely what `AstroLeagueGoal` already is, so the
switch is a **player-placed goal that pays energy instead of points**. That reuse is the answer
to "work through the fundamentals" here; the alternative (teaching the ball to bounce off a new
prism class) is a much larger platform change for one consumer.

The goal detector is genuinely close to reusable — it disables its own collider and detects by
polling, its mouth is just its transform position, and `Configure` **already** accepts an
explicit inward normal, a per-instance mouth radius, and a `passThrough` mode that scores on
centre-crossing rather than against a back wall (all three were added for the central-goal
layout). Four couplings have to be broken for a player-placed instance: the controller
**overwrites goal positions and re-configures them** on every match-config change (so a placed
ring would be stomped); registration is a fixed serialized list indexed by domain with no
register/unregister API; the controller back-reference is serialized; and the ring's *visual* is
drawn separately by the arena, so a placed switch needs its own body (which is the prism pane
anyway). Its report hook detonates the ball and triggers the whole celebration — a switch wants a
different outcome.

Placement: on the **course**, not the nose (mid-drift you throw the switch where you are
*going*), at a base distance ahead *(proposal: 150u — the mode's own kickoff-line distance)*.
Bricks claim occupancy via `PrismSpatialIndex.TryReserve` before spawning (claim-before-spawn —
physics queries are blind to fresh prisms for 0.6s), spawn through the pooled
`PrismEventChannelWithReturnSO` channel the skyburst block-creator uses, and **bloom in** on the
prism clock (`TargetScale` + `SetGrowthRate` + `Initialize` — the one growth engine; never tween
scales). Bricks are **plain**: never super-shielded (a forbidden grant that also exits the food
web), and not shielded, since a shielded prism is a *free pass* for a ball rather than armour.

Cost: one **switch charge** *(proposal: 3 charges, refilled by crystals alongside energy — or a
single shared meter, §15)*.

---

## 6. The energy economy and balance

```
crystals ──► energy ──► BALL (at threshold, inherited velocity, aimed)
                ▲                              │
                │                              ▼
           SWITCH pays  ◄──── any ball threads a switch ────► switch destroyed
```

Authored balance targets from the notes, to be tuned against in playtest:

- **~80% of generated balls should end in a goal.** Below that, balls read as wasted resources
  and the generation loop feels like a tax. This is the number that governs boundary-death
  frequency, arena scale, and how much velocity a fresh ball inherits — and it is measurable in
  a playtest (§14).
- **No generation in the final quarter near the opponent's goal** (§4.1) — the anti-trivialisation
  rule.
- **Enemy balls pay your switches** (§5) — the defensive incentive that keeps players from all
  crowding the enemy net.
- **Multiple balls are chaos on purpose** — party-game lane, not esport determinism (§1).

The whole loop is built from existing fundamentals: crystals (unchanged), prisms/mass (the switch
body), domains (ball ownership), elementals (the four scaling knobs). Nothing here adds a
fundamental or a parallel system, and nothing is removed from the world by a timer.

---

## 7. The four abilities × four elements

Convention: **Space = reach/presence · Time = rate/mobility · Charge = threat/energy · Mass =
size/volume.** One scaled parameter per element; every map multiplier pinned to **1** with the
real scaling on an authored field/`ElementalFloat` (the Dolphin no-double-dip pattern).

Map asset: `Assets/Resources/ElementalAbilityMaps/Scarab.asset` (exact folder + name, 4 entries,
`UnlockLevel 5 / RelockBelowLevel 4 / LatchPolicy Relock`).

| Element | Ability | Quantitative | L5 upgrade |
|---|---|---|---|
| **Charge (1)** | **Ball generation** | Energy required per ball (`ballEnergyCostAtFullCharge` ×0.5 at L10 — the authored-cooldown-style field the Squirrel's ring and the Dolphin's crystal seed both use) — *from the notes* | **Split Shot** *(proposal)* — a threshold hit yields **two** balls on slightly diverging headings (the Twin Seed / Twin Rings shape) |
| **Mass (2)** | **Switch** | Switch structure size — ring aperture + panel span (`switchScaleElemental` 1 → 2.5) — *from the notes* | **Second Pass** *(proposal)* — the switch survives its first trigger, paying twice before it breaks |
| **Space (3)** | **Juke** *(recommended)* | **(open design slot)** → propose: juke displacement + hit reach | **(open design slot)** |
| **Time (4)** | **Throttle** | Top speed of the throttle ramp (`ThrottleScalerMultiplier` ElementalFloat 1 → 1.5, the existing dormant `VesselTransformer` field, enabled) — *from the notes* | **Snap Dash** — double-tap the throttle for a burst/dash gap closer (§3.6) — *from the notes* |

Three rows come straight from the design notes. **Space is genuinely open** and is not filled here
— per the design-approval gate, an unapproved mapping is never invented to complete a map. The
recommendation is Space → the juke (reach/presence is exactly what a bump's distance and hit
radius are), which leaves drift as **unmapped base kit** — legitimate, and precedented by the
Sparrow's strafing roll. The notes name five verbs for four slots, so *something* must ride
unmapped; drift is the natural one because it has no reach/size/rate parameter that is not
already the throttle's.

Upgrade-name collision check (shipped + reserved + retired-on-record): **Split Shot**, **Second
Pass**, **Snap Dash** are all free.

**Contract-shape note (deliberate deviation, flagged).** Only the switch is a plain
`InputEvents → ShipActionSO` binding. Ball generation is impact-driven (a crystal effect — the
contract's sanctioned unbound case), the juke is a polled `NetworkBehaviour` (the
`BarrelRollController` precedent; the InputEvents pipe cannot carry a direction), and the throttle
is transformer-internal with its map `Input` declared for hint routing only. Named here so the
deviation is a decision on the record rather than something the auditors trip over later. The
juke's HUD hint has no address today — the fix is a hint-only `InputEvents` member plus a
right-stick glyph (§15).

---

## 8. Astro League integration

**Adding the vessel to the mode is one asset edit.** The whole restriction mechanism is the
arcade card's `Vessels` list (`ArcadeGameAstroLeague.asset`, today exactly `[SO_Class_Rhino]`),
read by three enforcement layers that follow automatically: `GameDataSO.SyncFromArcadeGame` →
`AllowedVesselClasses` + launcher clamp, `ServerPlayerVesselInitializer.ResolveSpawnVesselType` →
server-side spawn clamp, and the AI clamp in `ServerPlayerVesselInitializerWithAI`. There is no
mode-local vessel check and none may be added (the mode's own rule). ⚠ **List order is
load-bearing**: `ClampVesselToGame` falls back to `AllowedVesselClasses[0]` for any illegal hull,
and the scene's AI data authors Squirrel — clamped to Rhino today *because Rhino is index 0*.
Append the Scarab; do not reorder.

**Unlike the first pass, the Scarab is not fenced to the mode** (§1) — it also belongs in the
hangar list, the vessel-changer toy's collection, and any other card design wants.

**The court protects itself.** The play boundary is collider-less analytic math; the only
mode-authored prisms are 480 **super-shielded** edge-lining prisms, which no-op damage and
consumption and **destroy any explosion that touches them**. A juke-cone (if adopted) into the
lining is eaten by the court; balls ignore super-shielded mass entirely. Nothing to author.

**Crystals on this pitch** feed ball generation with no new spawners: the mode's single neutral
anchor crystal respawns *inside* the court (a contested midfield prize — now the ball source,
which makes it far more interesting than it is today), and fauna drop elemental crystal hearts at
their death positions, on-court once the cleanup crew is released at Restless+. Charge income
therefore rises exactly when the pitch is crowded, which is emergent from the food web rather
than authored.

**The volume ladder needs a retune.** The mode's phase window is authored for Rhino trail
(~0.75 volume/prism): Restless at LiveVolume 30,600 over a 30,000 super-shielded lining floor —
a +600 gameplay band. Switch bodies are placed mass and will move that number, so the mode's
`PhaseThresholds` must be re-measured and re-authored when the Scarab ships, with the stated side
effect that Rhino-only matches silt differently. This is the ecosystem masterplan's
author-explicit-volumes clause arriving on the switch dial; it is a playtest decision, not a
free parameter.

---

## 9. The other three lanes

### 9.1 Esport
The legibility constraint (§1) is why the ball keeps its colour (§4.2), why the switch's two jobs
are visually distinct (a curved panel you can read the angle of, a mouth you can read the aim
of), and why the throttle is analog rather than a boost button — a continuous input is a
continuous skill.

### 9.2 Free space toy
In Menu_Main freestyle there is no arena, no goals and no score — and the Scarab still has a
complete loop: fly through a crystal to make a ball, place a ring, punt the ball through the
ring, get energy, make another. That is a **toy** in the platform's exact sense (no score, no end
condition, something to play with indefinitely), and it composes with the existing toys rather
than duplicating them. Two rules degrade gracefully with nothing authored: the no-generation
zone is inert without goals, and boundary death applies at the cell's nucleus boundary if one
exists (open question §15: what bounds a ball in an environment-free freestyle cell — a lifetime
would be a forbidden timer, so the answer is probably a distance-from-owner leash or simply
letting balls fly free).

### 9.3 HyperSea citizen
Everywhere else the Scarab is an ordinary vessel: it lays trail (drift-shaped), skims, collects
crystals, takes danger-prism punishment, and its balls interact with prisms by the platform's
existing rules — eating opposing mass, shielding friendly mass. Its juke is a real anti-vessel
tool in any mode with opponents. Nothing in the kit requires an arena to function.

---

## 10. Ecology & platform-law compliance

- **Mass is conserved.** The switch *creates* prisms through the standard pooled factory channel
  with the standard bloom stamps; the juke-cone (if adopted) *removes* them as an active force.
  No timers, no TTLs, no decay: switches persist until threaded, destroyed, or eaten by fauna.
  Ability cooldowns pace input; they remove nothing from the world. **Balls are not prism mass** —
  their boundary death is not a mass sink.
- **Continuity of existence.** Switches bloom in; switch destruction on trigger animates out;
  **balls dissipate at the boundary rather than popping** (§4.3); HUD pips bloom/wither.
- **Clock-material law.** Every prism the Scarab creates animates by pool-pull + one initial
  stamp; no CPU animation of prism visuals anywhere in the kit.
- **No SuperShield grants** (§5). Shield semantics respected: shielded prisms are a free pass for
  a ball, which is exactly why switch bodies are plain.
- **Maintained-mechanism law.** Nothing holds an element above 10 — energy and switch charges are
  `ResourceSystem` meters, not element levels.
- **Elementals own all buff/debuff.** The juke's anti-vessel effect routes through the impact
  effect system; any debuff it applies goes through `ResourceSystem.ApplyElementalEffect`.
- **The Cell owns the environment.** No mode-local spawners, no parallel crystal system, no
  bespoke arena edge — the switch reuses the goal detector and the boundary's reflection math.
- **Collider budget.** Per switch: its brick bodies (pooled, phase-LOD-managed) plus one analytic
  reflector (no collider) and one mouth detector (no collider — plane-crossing math, as the
  shipped goal already is). Balls carry one SphereCollider each — but the real per-ball cost is
  **CPU, not colliders**: every ball runs a prism spatial-index sweep and samples every vessel's
  velocity each physics tick, both of which scale linearly with the population. State a cap in
  the mode config and measure it *(first pass: 3 live balls per player)*. Juke: zero. If the cone
  is adopted: one capsule trigger for a fraction of a second, prism damage via Burst sweep.
- **Nothing to author** for the speed tunnel or the occlusion corridor (both platform laws bound
  automatically on `IsLocalPilot`); the corridor's hull measurement should be checked for the
  skinned-mesh armature-scale trap that once oversized the Sparrow's by ~5×.

---

## 11. New code & assets inventory

| File | Contents |
|---|---|
| `_Scripts/Controller/Vessel/ScarabVesselTransformer.cs` | `SingleStickVesselTransformer` subclass: the throttle **integrator** + Time-scaled ceiling + double-tap dash (§3.2, §3.6) |
| `_Scripts/Controller/Vessel/ScarabJukeController.cs` | NetworkBehaviour: right-stick poll, cooldown, displacement + visual roll, vessel shove + ball strike, fire RPCs (§3.4) |
| `_Scripts/.../Data Containers/PlaceSwitchActionSO.cs` + `Executors/PlaceSwitchActionExecutor.cs` | Charge gate, placement, occupancy claim, pooled spawn, spend (§5) |
| `_Scripts/.../Impactors/BallForgeCrystalImpactor.cs` | `OmniCrystalImpactor` subclass: the threshold branch — collect as usual, or materialise a ball with inherited velocity and skip the collect (§4.1) |
| Mode-side: **a ball prefab + network registration** (neither exists), ball registry with spawn/despawn, per-ball goal state, per-ball attribution, `Switch` object (reflector + mouth detector), boundary-death path, no-generation zone, and a goal-outcome decision (§15.13) | §4.5, §5 — scoped as mode work, not vessel work |

New assets: `Scarab.prefab` (clone Sparrow for the single-stick + juke skeleton — **never** the
five placeholder vessels, all of which serialize `vesselType: 0`; root carries the Netcode trio
and the full `[RequireComponent]` set, with `_shipInstance` / `vesselHUDController` /
`_nearFieldSkimmer` / `gameData` wired) · `Resources/ElementalAbilityMaps/Scarab.asset` ·
`_SO_Assets/VesselActions/Scarab/` · `ScarabImpactorDataContainer.asset` (the baseline prism trio
+ the crystal-collection effects that fill the energy meter) ·
`ScarabSkimmerImpactorDataContainer.asset` (**the anti-vessel shove lives here**, not in the
vessel container — all vessel-on-vessel interaction is skimmer-mediated, §3.4; then **run Audit
Vessel Skimmers**, whose container-null and pointer-at-disabled-twin failures are silent by
design) · a crystal prefab variant carrying `BallForgeCrystalImpactor` (§4.1) ·
`ScarabCameraSettingsSO.asset` · `SO_Class_Scarab.asset` (correct name + location) ·
`ScarabHUDVariant.prefab` · a switch prefab/definition · **a ball prefab** (§4.5 — the shipped
ball is a scene object, so this does not exist yet).

Edits: `VesselClassType.cs` (+`Scarab = 12`) · `EnumIntegrityTests.cs` (count 13→14 +
`[TestCase]`, **same commit** or the suite fails) · `Vessel Prefab Container.asset` (+prefab —
mandatory the moment the enum member exists, since `VesselSpawner` rolls Random over all members
and an unregistered class is a LogError storm plus a destroyed player) · `DefaultNetworkPrefabs`
· `ArcadeGameAstroLeague.asset` (append after Rhino) · hangar `SO_Classlist_*` +
`VesselChangerToy.DefaultCollection` (the Scarab is **not** mode-fenced) · Astro League
`PhaseThresholds` retune (§8). Prism pool: reuse an existing `PrismType` for v1. Telemetry:
`DefaultVesselTelemetry` **on the prefab** with stat SOs wired. Animation: a concrete
`VesselAnimation` subclass (a `[RequireComponent]`). `VesselCustomization._shipGeometries`
populated, ≥2 material slots per hull MeshRenderer.

---

## 12. HUD

- **Four-icon row** (LOCKED order charge → mass → space → time): **Ball · Switch · Juke ·
  Throttle**, authored at the shared row geometry. Standard three-layer upgrade signal; any icon
  doubling as a live gauge sets `tintIconOnUpgrade = false` and overrides `SetAbilityUpgraded`
  re-anchoring rest scales (the Squirrel reference).
- **Element flowers**: fleet-required — author them (FrogletTools > Vessels > **Wire Elemental
  Petal Bars**, then assign `ElementalBarsController.elementBars`), don't rely on the loud
  runtime fallback.
- **Energy meter**: the ball-generation gauge, and the most important readout on the HUD — it
  must make the **threshold** unmistakable, because crossing it changes what touching a crystal
  *does*. Recommend a fill that visibly latches/charges at full rather than a bar that quietly
  tops out. Driven event-only off `ResourceSystem.OnResourceChanged` (subscribe the controller
  directly — the Serpent's pattern; the Sparrow reaches the same event transitively through its
  gun executor).
- **Switch charges**: discrete pips as **sibling** images of the Switch icon (never the ability
  icon itself — that belongs to the upgrade tint/badge system), sprite-state driven off the same
  resource event.
- **Juke pip**: one binary ring — armed ↔ recharging, fill wipe + spend punch (the Sparrow's
  `rollChargeIndicator` exactly; binary stays visibly binary).
- **Control hints**: LT → drift and A → switch derive automatically; RT → the Time entry's `Input`
  places the RT glyph on Throttle even with no `ShipActionSO` bound to the event (the map is the
  hint system's first lookup, verified). The **juke has no hint address** (§15).

---

## 13. Tuning knobs (first pass — all *(proposal)*)

| Knob | Where | Value |
|---|---|---|
| `accelerationPerSecond` / `coastDragPerSecond` | transformer | 70 / 12 |
| `baseTopSpeed` (Time-scaled ×1→1.5) | transformer | 180 → 270 |
| `DefaultMinimumSpeed` | prefab | 10 |
| Pitch/Yaw/Roll · `RotationThrottleScaler` | prefab | 100/100/30 · 0.1 |
| Drift single / sharp (`Mult`, damping) | drift SOs | 1.4, 0.5 / 1.8, 0.25 |
| `jukeSpeed` / `jukeDurationSeconds` / `jukeCooldownSeconds` | juke controller | 80 / 0.5 / 1.2 |
| `doubleTapWindowSeconds` / dash impulse | transformer | 0.3 / 120 for 0.4s |
| Ball energy cost (Charge-scaled ×0.5 at L10) | crystal effect SO | 1.0 meter → 0.5 |
| Ball inherited velocity fraction | crystal effect SO | 1.0 (full vessel velocity) |
| Live balls per player cap | mode config | 3 |
| No-generation zone | mode config | nearest 25% of arena length to the opponent goal |
| Switch charges / cost / crystal grant | prefab `ResourceSystem` | 3 / 1 / +1 |
| Switch placement distance | switch SO | 150 |
| Switch size (Mass-scaled) | switch SO | 1 → 2.5 |
| Target ball→goal conversion | playtest metric | ~80% |
| Astro League `PhaseThresholds` | cell config | re-measure with switches in play |

---

## 14. In-editor verification (when implemented — a human at the editor)

Auditors first (all asset-only): **Audit Vessel Ability Rows**, **Audit Vessel Skimmers**, **Audit
Vessel Elemental Morphs**, **Audit Corridor Vessel Radii**, **Validate Speed Tunnel Law**, plus
`EnumIntegrityTests` green. Then in `MinigameAstroLeague` (MPPM two-client where noted):

1. **Throttle**: hold RT → speed climbs continuously to the ceiling and holds; release → long
   coast, never a dead stop; Time L10 seeded → higher ceiling. Confirm the speed tunnel tracks it
   and does not saturate at cruise.
2. **Double-tap dash** (Time 5 seeded): two RT taps inside the window → burst along course; below
   Time 5 → nothing. Confirm a single tap and a slow double-tap never trigger it.
3. **Drift**: half LT ≈ single tier, full LT = sharp; course visibly decouples from nose; speed
   unchanged.
4. **Juke**: right stick to perimeter → lateral shunt + visual roll; camera does not roll; pip
   spends and re-arms; holding the stick pinned does not re-fire. Juke into an enemy → they are
   shoved; juke into a teammate → nothing. MPPM: remote peer sees it.
5. **Ball generation**: collect crystals → energy climbs, threshold latch is unmistakable on the
   HUD; fly through a crystal at threshold → a ball materialises carrying your velocity and your
   colour, meter spends, crystal respawns. Below threshold → normal collection, no ball.
6. **No-generation zone**: attempt generation in the opponent's quarter → refused with a clear
   cue; one metre outside it → succeeds.
7. **Colour permanence**: have an opponent strike your ball repeatedly → it stays your colour, and
   keeps eating their trail and shielding yours.
8. **Boundary death**: shoot a ball at the wall → it dissipates visibly (no pop, no carom).
9. **Multi-ball**: three balls live simultaneously → goals detect correctly for each; the HUD
   objective marker picks a sensible target; whatever §15.13 decides a goal does, it does.
   ⚠ **Test attribution deliberately**: have player A strike ball 1 and player B then score
   ball 2 in the same window. Global attribution produces a *plausible-looking wrong score*, not
   an error, so it will not surface by itself.
10. **Switch**: A with a charge → curved ring blooms ahead on the course; a ball threading the
    mouth pays energy and the switch breaks; **an enemy ball threading it pays you too**; a ball
    striking the panel off-mouth **deflects** (this is the §5 crux — if it merely slows, the
    analytic reflector is not wired and the mechanic does not exist).
11. **Mass 5 / Charge 5** (seeded): switch survives its first trigger; threshold hit yields two
    balls.
12. **Conversion rate**: over ~20 generated balls, count goals — target ~80%. This is the headline
    balance number and the one most likely to demand retuning arena scale or inherited velocity.
13. **Freestyle**: in Menu_Main, the full make-ball → place-ring → thread-ring loop runs with no
    arena and no errors.

Anything not verifiable this way gets a 🔴 entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md` at
implementation time.

---

## 15. Open questions & follow-ups (for markup)

1. **Space row** (§7): the notes assign Charge/Mass/Time and leave Space unstated. Recommended:
   Space → juke displacement + hit reach, with drift as unmapped base kit. Needs sign-off, and
   the Space **L5** is open regardless.
2. **Juke: bump or cone?** (§3.4) The original brief asked for a short-range lateral cone of
   destruction; the notes describe a side bump. Difference: whether the juke destroys **mass**
   (trail, switches) as well as shoving vessels and balls.
3. **"No hard speed cap initially"** (§3.2): ceiling absent at level 0 and Time raises a soft
   limit, or ceiling always present and Time raises it? Recommendation and the three measured
   reasons are in §3.2.
4. **One meter or two?** (§4.1, §5) Ball energy and switch charges could be a single resource
   (spend it on either — a real strategic tension) or two meters (clearer, less interesting).
5. **Ball population cap** and what happens at the cap — refuse generation, or expire the oldest
   (an expiry would be an imposed clock and should be avoided).
6. **Freestyle ball bounds** (§9.2): with no arena, what ends a ball's life? A lifetime is a
   forbidden timer; a distance-from-owner leash or "balls simply fly away forever" are the
   candidates.
7. **Do switches score match points**, or only energy? The notes say "switches act as goals and
   scoring mechanisms" while also describing panels that deflect balls *into goals* — read here
   as energy-scoring rings alongside the mode's fixed goals. Confirm.
8. **Right-stick hint address** (§12): approve a hint-only `InputEvents` member + a
   `PadRightStick` glyph, or accept a hint-less juke icon (the audit will flag it).
9. **Touch** (§3.1): no `Button1Action` raise site exists on touch — on-screen switch button, or
   gamepad/desktop-only at v1?
10. **AI Scarab**: `AIPilot` has no throttle setter and no stick synthesis, so an AI Scarab would
    idle with a dead juke; it *does* have a prefab-authored ability loop that could fire switch
    placement blindly. v1 recommendation: AI keeps flying Rhinos in Astro League (free, via the
    `Vessels` list order) until throttle/juke synthesis lands.
11. **Mode scope** (§4.5): the multi-ball, player-generated, boundary-death ball model is a
    substantial change to Astro League's shipped single-ball mode — starting with the fact that
    **no ball prefab exists** (it is a scene object with no spawn API). Is this an evolution of
    Astro League, or a second mode that shares its arena? The answer changes how much of §4.5 is
    in scope for the vessel branch.
12. **Who gets the point?** (§4.2) Goal credit today comes from the last striker, not the ball's
    colour, and the two are independent systems. With permanently-owned balls: last-toucher
    (Rocket League own-goals, keeps the shipped code) or ball-owner (your ball, your point)?
13. **What does a goal DO when other balls are live?** (§4.5) Today a goal stops the world —
    celebration, global slow-mo, every vessel parked, every field prism swept, kickoff freeze.
    With a population in flight, "score and reset" is incoherent. Candidates: goals stop
    nothing (the ball detonates, play continues — the party-game answer), or a short local
    celebration with no freeze. This is the biggest unresolved *design* question in the document.
