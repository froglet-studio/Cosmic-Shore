# Serpent — Scope (Space) + Sniper Shot (Charge)

Hold the **left trigger**: the view drops into the cockpit and magnifies with the trigger's own
depth. While it is up, the **right trigger** stops being the cloak and becomes a **sniper shot** —
one hitscan round down the scope's line that destroys what it hits, **including super-shielded
mass**, on a long cooldown.

Two of the Serpent's three open element slots are now filled. **Mass stays open.**

| Element | Ability | Input | Quantitative parameter | Level-5 upgrade |
|---|---|---|---|---|
| Charge | **Sniper Shot** | RT (`RightStickAction`) | cooldown: 12 s at rest → 5.4 s at Charge 10 | **Pierce** — the round carries through up to 3 prisms instead of stopping at the first |
| Mass | *(open design slot)* | — | — | — |
| Space | **Scope** | LT (`LeftStickAction`) | magnification: 22° FOV at full zoom at rest → 11° at Space 10, floored at 8° | **Deep Focus** — ×1.6 more zoom depth, and the floor drops with it (13.8° at rest, 6.9° at Space 10) |
| Time | Boost Duration | A / Space (`Button1Action`) | boost duration ×1.6 at L10 | — *(unchanged)* |

## Why these elements

**Space is reach/presence fleet-wide, and a scope's whole claim is reach** — so Space owns the
magnification, and the element and the mechanic are the same statement. **Charge is
threat/energy**, and on a weapon whose per-shot effect is already absolute (one round, one prism,
whatever its armour) the only honest axis left is *how often you get to use it* — so Charge owns
the recovery. Neither ability scales through the map's generic `MultiplierAtFullLevel`; both are
pinned to `1` there because the action SOs carry dedicated authored fields, per the
no-double-dipping rule.

## The right trigger is CONTEXTUAL, and neither ability knows about the other

The Serpent already bound RT to `CloakSeedWallAction`. Both actions are now bound to RT and
`R_VesselActionHandler` runs every action bound to an input, so **each one asks the scope whether
the context is its own**:

- `SniperShotActionSO` fires only while `SniperScopeActionExecutor.IsScoped`.
- `CloakSeedWallActionSO` declines to start while it is.

Keeping the question in the two SOs rather than in the handler means re-binding either ability
changes nothing about the rule, and the cloak stays safe on any hull:
`ActionExecutorRegistry.Get` falls back to a child search and returns null on a vessel with no
scope, which reads as "not scoped".

**`IsScoped` is maintained on EVERY peer**, not only where the camera is. The handler round-trips
every press and release through the server, so both executors already run on every machine, and
the sniper resolves its own shot on every machine. Had the flag been local-pilot-only, a remote
replica would have run the cloak *and* the shot on the same press.

## The view

**The pilot's own camera never moves.** The scope is a **second, magnified picture** — a round
eyepiece in the top-left corner, under the goal stack — and the flight view behind it is untouched.

That is round 4's inversion, and it is the whole design now. Rounds 1–3 did the obvious thing:
take the gameplay camera into the cockpit and narrow its field of view. It worked exactly as
specified and read as **nauseating**, because a magnified view is a lever on every motion that
reaches it — a 22° scope multiplies the pilot's own turn, the vessel's roll, the camera's settle
and the speed tunnel's narrowing by the same ~4× it multiplies the target, and the pilot has no
way to tell which half of what they are seeing they caused. Putting the magnification in a window
leaves the flight view at 1×, where motion is read, and confines the lever to the picture being
aimed with.

`ScopePipView` (`_Scripts/Utility/`) owns it:

- **It is not a second gameplay camera.** Created at runtime, **never tagged MainCamera**, renders
  only into a `RenderTexture`, and left **disabled** and stepped by hand — outside all four systems
  `Docs/REAR_VIEW.md` §3 names (the speed tunnel resolves `CameraManager`'s *active* controller;
  `ApplyCameraGraphicsSettings` pushes the player's FOV and AA onto three managed cameras and no
  others; background colour is per camera; `Camera.main` returns the first **enabled** MainCamera).
  §3.1.1 is the carve-out this shape was written for, and `ConnectingArenaPreview` is its sibling.
- **Being outside the speed tunnel is what makes the zoom pure.** Nothing narrows this camera but
  the trigger.
- **It is a rigid attachment, and both halves matter.** The eye is written outright every frame —
  any lag at all at zero distance puts the camera inside the hull it is trying to see past — and it
  aims along the vessel's **own forward** rather than at anything, because a look-at vector is
  ~zero in the cockpit and `SafeLookRotation` would decline it, freezing the view.
- **The eye is MEASURED**, at `1.05 ×` the vessel's own circumscribing hull radius
  (`PrismOcclusionCorridor.MeasureCircumscribedRadius`, the rotation-invariant hull-only
  measurement the occlusion corridor already sizes itself from), cached per hull. A constant could
  not serve both a Serpent and a Squirrel.
- **It aims down the same vector the shot is cast along** (`vessel.forward`), so the reticle is a
  promise rather than a decoration — by construction, not by tuning.
- **The picture is SQUARE** (1:1 render target, camera aspect 1) because the window is round, so
  `ScopeDiscGraphic` samples the unit circle straight onto `[0,1]²` with nothing squashed and
  nothing cropped away.
- **The clip planes ARE borrowed** from `Camera.main` here — unlike the connecting panel's preview,
  which derives its own. That preview frames a whole arena from outside and clips out of a borrowed
  far plane; this camera sits on the vessel looking down the same line the gameplay camera already
  renders, so its planes are correct for this shot by construction.

**What this retired:** `VesselFirstPersonView` is **deleted**, and `CustomCameraController` no
longer carries `FirstPerson` / `FirstPersonOffset` or the rigid-attach branch. An unreferenced
camera vantage bound at two `IsLocalPilot` sites is exactly the "unreferenced subsystem eventually
mistaken for a live feature" trap CLAUDE.md records, so it went with its caller rather than being
kept just in case. `VesselSpeedTunnel.SetHomeFieldOfViewOverride` **is** kept, with no caller, and
that asymmetry is deliberate: it is a **guard** rather than a feature — the one sanctioned way to
magnify the gameplay camera — and deleting it re-opens the direct `Camera.fieldOfView` write the
law exists to prevent. `Docs/SPEED_TUNNEL.md` §2.1 now carries the finding as the thing to read
before reaching for it.

## The zoom is a pure function of the trigger

Nothing else reaches it. Not the stick, not the speed, not the camera the pilot is flying with:

```
zoom01 ← MoveTowards(zoom01, deadzone(LeftTriggerAnalog), zoomResponse · dt)
fov    = lerp(unscopedFieldOfView, fieldOfViewAtFullZoom / spaceDepth, zoom01)
```

`zoomResponse` is the one remaining term and it is a function of the **trigger's own history**, not
of the world: it exists so a BINARY trigger (mouse, keyboard — 0 or 1) ramps instead of snapping. It
ships at **12/s**, up from 6: at 6 a full pull took ~167 ms to arrive, which on a pad reads as the
scope moving on its own — the same complaint from the other end.

Two terms were removed to get here, and both were defensible when written:

- **The Steady-Eye stick bleed.** Below Space 5 the magnification eased back out as the pilot
  hauled the stick over. It was a real design idea — commit to a line, settle, then take the long
  shot — and it made the zoom a function of *steering*, i.e. a second thing moving the view at
  exactly the moment the pilot is already moving it. Retired with its upgrade.
- **The speed-tunnel lerp base.** `ScopedFieldOfView` lerped from `VesselSpeedTunnel.HomeFieldOfView`
  (or the live camera), so the scoped view went on narrowing with speed. That was *correct* as a
  composition — "a scoped pilot who accelerates still reads their speed in the optics" — and it
  meant magnification changed when the pilot did nothing but accelerate. Gone with the main-camera
  cockpit.

> **The rule this leaves behind.** A magnified view is a lever on every input that reaches it, so a
> zoom that is a function of anything but the zoom control amplifies motion the pilot never asked
> for. Compose magnification with *nothing*.

**Space 5 — "Deep Focus"** replaces Steady Eye, and it is deliberately not movement-shaped: one
more magnification step. `upgradeZoomDepthMultiplier` (1.6) multiplies the zoom depth **and divides
the floor by the same number**, so the extra reach is actually reachable instead of running into a
ceiling the upgrade cannot pass — 22° at full zoom at rest becomes 13.8°, and Space 10 reaches
6.9°.

## The shot

- **Hitscan**, not a projectile:
  `PrismSpatialIndex.QueryCone(origin, forward, range, coneHalfAngle, minPathRadius, …)`.
- **The line is the CAMERA's line** — the vessel's forward axis, which is exactly what the cockpit
  camera is aimed down, so the shot lands where the view is pointing. Deriving it from `Course`
  would put the round somewhere the pilot is not looking whenever the vessel is sliding.
- **The path is a CONE, not a ray — and not a tube.** `QueryCone` tests a prism's *centre* and a
  prism is several units across, so a mathematical line threaded through a lattice of centres
  misses almost everything it visually passes through. The first cut fixed that with a
  fixed-radius capsule and introduced a worse problem: a radius is measured in world units while
  the pilot aims in **angle**, so 4 u was a blunderbuss at the muzzle and **0.076°** — about 7 px
  inside the 22° scope — at the 3,000 u reach. See §"Round 2" below.
- **`QueryCone`'s snapshot is unordered** (it walks buckets), so hits are sorted along the axis
  before anything is destroyed — a sniper round has to stop at the *first* thing it reaches.
- **Own-domain mass is skipped.** The Serpent's identity is the wall it weaves; a rifle that cut
  through it would make the two abilities fight. `Domains.Blue` is the neutral sentinel and stays
  hostile to everyone.

### Breaking a super-shield uses the ONE sanctioned sequence

Super-shielded mass is invulnerable to `Prism.Damage` outright — it early-returns through
`AbsorbSuperShieldHit`. The sanctioned teardown drops the shields **first** and then devastates,
which is precisely what the Rhino's energised blade does
(`RhinoSkimmerDamagePrismEffectSO.PopSuperShield`):

```csharp
prism.DeactivateShields(impact, debrisSpeedLimit);          // sheds the stellation as debris
prism.Damage(impact, domain, name, devastate: true, debrisSpeedLimit: …);
```

Both calls get the same impact vector and the same true-velocity ceiling, so the stellation's
shards and the prism's own pieces fly on identical terms — one effect, two meshes
(`Docs/PRISM_ANIMATION.md §4.8.1`). Nothing pops out of existence and **mass is conserved**: this
is an ACTIVE force with a long cooldown and a scope you have to hold, which is what keeps it clear
of the no-imposed-decay law.

**This is the fleet's second force that can break a super-shield**, after the Rhino's energised
sword. Anything relying on "only the Rhino can open super-shielded mass" — an arena whose rails or
lining are super-shielded for exactly that reason — should be re-checked against a Serpent.

## Tuning knobs

`Assets/_SO_Assets/VesselActions/Serpent/SniperScopeAction.asset`

| Field | Ships at | What it does |
|---|---|---|
| `unscopedFieldOfView` | 60° | the window's FOV with the scope raised but not zoomed — the wide end of the dial. A constant, never a read of the live gameplay camera, or magnification becomes a function of speed |
| `fieldOfViewAtFullZoom` | 22° | FOV at full zoom at the resting Space level |
| `zoomDepthAtFullSpace` | 2 | zoom depth multiplier at Space 10 (divides the angle → 11°) |
| `minFieldOfView` | 8° | floor, so no element level makes the scope a soda straw |
| `upgradeZoomDepthMultiplier` | 1.6 | **Deep Focus** (Space 5): multiplies the depth AND divides the floor, so the extra reach is reachable |
| `zoomResponse` | 12 /s | how fast the applied zoom chases the trigger — its ONLY job is ramping a BINARY trigger (mouse/keyboard). High enough to be a pass-through on a pad |
| `zoomDeadzone` | 0.08 | dead travel at the top of the trigger, so a resting pad cannot creep the view in |

`SniperScopeActionExecutor.steadyZoomFloor` (0.35) — fraction of the zoom surviving at full stick
deflection **before** Space 5.

`Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset`

| Field | Ships at | What it does |
|---|---|---|
| `cooldownSeconds` | 12 | wait between shots at resting Charge — the ability's whole cost |
| `cooldownMultiplierAtFullCharge` | 0.45 | → 5.4 s at Charge 10 |
| `rangeUnits` | 3000 | the whole flight; there is no projectile to outrun |
| `coneHalfAngleDegrees` | 0.5° | angular half-width of the hitscan — 26.2 u at 3,000 u, ~49 px across at the 22° scope |
| `minPathRadius` | 6 u | radius floor near the muzzle; the angular term takes over past 688 u |
| `pierceCount` | 3 | prisms a PIERCING round takes; 0 = unlimited |
| `debrisSpeed` / `debrisSpeedLimit` | 90 / 120 | true-velocity debris and its ceiling |
| `beamSeconds` | 0.35 s | how long the tracer takes to fade out |
| `beamStartWidth` | 1.5 u | tracer width at the muzzle; the far end is drawn at the cone's own radius |
| `impactFlareSeconds` / `impactFlareRadius` | 0.3 s / 18 u | the flare at the kill point |
| `shakeIntensity` / `shakeDuration` | 0.6 / 0.18 s | the report, local pilot only |

## Files

| File | Role |
|---|---|
| `_Scripts/Utility/VesselSpeedTunnel.cs` | **+** the sanctioned home-FOV override surface — kept in round 4 as a GUARD, with no caller |
| `_Scripts/Controller/Camera/CustomCameraController.cs` | round 1 added `FirstPerson` / `FirstPersonOffset` and the rigid-attach branch; **round 4 removed all of it**. `ApplyShake` stays extracted |
| `_Scripts/Utility/VesselFirstPersonView.cs` | round 1 added the cockpit platform driver; **round 4 deleted it** |
| `_Scripts/Controller/Vessel/VesselController.cs` | round 1 bound that view at the four `IsLocalPilot` sites; **round 4 removed the four lines** |
| `…/R_VesselActions/Data Containers/SniperScopeActionSO.cs` | **new** — Space ability config |
| `…/R_VesselActions/Data Containers/SniperShotActionSO.cs` | **new** — Charge ability config |
| `…/R_VesselActions/Executors/SniperScopeActionExecutor.cs` | **new** — scope state, zoom drive, `IsScoped` |
| `…/R_VesselActions/Executors/SniperShotActionExecutor.cs` | **new** — hitscan, cooldown, super-shield teardown, tracer |
| `…/R_VesselActions/Executors/SniperBeam.cs` | **new** — the pooled domain-coloured tracer + impact flare |
| `…/R_VesselActions/Executors/SniperScopeOverlay.cs` | **new** — the eyepiece, the reticle inside it and the recharge ring around it |
| `_Scripts/UI/View/ScopeRingGraphic.cs` | **new** — the generated ring/arc |
| `_Scripts/UI/View/ScopeDiscGraphic.cs` | **new** (round 4) — the generated circular picture |
| `_Scripts/UI/View/AbilityLockupView.cs` | **+** (round 4) a LOCKED card draws its cooldown |
| `_Scripts/Utility/ScopePipView.cs` | **new** — the runtime RenderTexture camera; round 4 re-pointed it at the SCOPE |
| `_Scripts/Controller/Managers/PrismSpatialIndex.cs` | **+** `QueryCone` / `ConeContains` |
| `_Scripts/UI/View/CloakSeedWallActionSO.cs` | **+** declines while scoped |
| `_Scripts/UI/Controller/SerpentVesselHUDController.cs` | **+** drives the Charge card's cooldown veil |
| `Assets/Resources/ElementalAbilityMaps/Serpent.asset` | Charge + Space entries authored; Time's `Input` corrected |
| `Assets/_SO_Assets/VesselActions/Serpent/SniperScopeAction.asset` | **new** |
| `Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset` | **new** |
| `Assets/_Prefabs/Spacevessels/Serpent.prefab` | two executor children, registry entries, LT + RT bindings |

## Round 2 — what the first playtest found

Four reports, all of them true, and three of them one defect each:

> *"I didn't notice the shot at all. The UI did not indicate if a shot was ready. Let's use a PIP
> so players can still see themselves fly while zoomed in. The hitscan will hit very little."*

### 1. The hitscan was a needle — and widening it is the wrong fix

The instinct was right even though the path was not literally zero: it was a capsule of radius 4
tested against prism **centres**, which at 3,000 u subtends **0.076°** — about **7 px** inside the
22° scope. Functionally a needle.

The tempting fix is a bigger radius, and it is wrong in both directions at once: **a radius is a
world distance and a pilot aims in ANGLE.** Anything wide enough to be aimable at 3,000 u is a
blunderbuss at 100 u, which is the opposite of a sniper rifle. A bundle of parallel rays — the
other suggestion — has the same problem plus gaps between the rays, and costs N queries.

So the path is an **angular cone**: `radius(t) = max(minRadius, t · tan(halfAngle))`. At a **0.5°**
half-angle that is 26.2 u at 3,000 u and, crucially, **the same on-screen size at every range** —
about 49 px across at the 22° scope, 135 px at full magnification. *That* is what lets the reticle
be drawn at the beam's true size rather than at a guess, which is the whole of §2 below.

The `minPathRadius` floor (6 u) is not a fudge: a pure cone has **zero** radius at the apex, so
mass the ship is about to fly into would be missed by the one weapon pointed straight at it. The
angular term overtakes it at 688 u.

**The cone is deliberately NOT a function of zoom**, and this is the trap worth recording. Widening
it as the pilot zooms out is the obvious next feature and it would **desync the prismscape**: the
zoom is a locally smoothed value (`SniperScopeActionExecutor.Zoom01` eases toward the trigger on
the owner's machine only) while this shot resolves on **every peer**, so destruction would depend
on a number each machine holds a different version of. Conserved mass cannot ride a local ease.

New platform surface: `PrismSpatialIndex.QueryCone` — the tapering sibling of `QuerySegment`, same
bucket walk, same unordered-snapshot contract, with the radius a function of axial distance.

### 2. The readiness push was correct and invisible

`SerpentVesselHUDController` pushes `CooldownRemaining01` into the fleet's clockwise cooldown veil
every frame, and it has been doing so since round 1. The veil is drawn **on an ability ICON**, and
**the Serpent binds 0 of its 4 ability icons** — so its lockup renders four LOCKED cards and the
veil has nothing to sit on. A perfectly wired readout with nowhere to appear.

*General shape: a push into a display is only as visible as the display, and "the vessel has no
icons yet" is a fact about a different file.*

The long-term fix is to author that vessel's four icons (`FLEET_MAPS.md` §2). What ships now is a
**scope overlay** that says something the ability row could not anyway — **where the shot goes**:

- a **reticle** whose radius is the cone's own half-angle projected through the camera's **live**
  vertical field of view (`r = (h/2)·tan(halfAngle)/tan(fov/2)`), read every frame off the camera
  that is actually rendering, so it tracks the zoom **and** tracks the speed tunnel narrowing the
  view at speed. Anything inside the ring is inside the shot;
- a **recharge arc** around it, filling clockwise from the top — the same direction as the fleet's
  cooldown veil, because an arc that filled the other way would read as the opposite of every other
  recharge in the game — **drawn on a dim full-ring BED**, and at READY drawn as a complete bright
  ring rather than as nothing (see §"Round 3");
- a **ready flash** when the weapon comes back, so a pilot watching the target rather than the arc
  still sees it arrive.

Both readouts are driven, so the day the Serpent's icons are authored the row lights up and this
stays correct. It is **generated** — one runtime canvas, three `ScopeRingGraphic`s and a
`RawImage`, no sprites and no prefab — and built lazily for the **local pilot only** on the first
frame a scope is actually held, so every AI and every remote replica costs nothing.

### 3. The shot had no visible output at all

A hitscan is over in the frame it fires: there is no projectile to watch, and at 3,000 u the prism
that died is a few pixels. The only evidence of a shot was a camera kick — which is exactly "I
didn't notice the shot".

`SniperBeam` draws a **domain-coloured tracer** down the round's own path, at the cone's radius at
each end (so the beam *is* the volume that was tested — a tracer thinner than the cone teaches the
pilot to aim at something the weapon does not use), plus a **flare** at the stop point when
something died. It **fades** rather than vanishing (continuity of existence), and it is drawn on
**every peer** for free, because `R_VesselActionHandler` round-trips the press and the hitscan
therefore already resolves everywhere. A rifle only the shooter can see is a rifle nobody can learn
to dodge.

It shares **one** colour resolver with the reticle (`SniperShotActionExecutor.TracerColour`), read
live off the shared `ColorSet`, so the mark the pilot aims with and the mark the shot leaves can
never disagree about whose shot it was — and the freestyle domain-changer toy re-colours both.

**Still silent, deliberately.** `fireEvent` ships EMPTY, which is the audio convention: an unwired
slot is a visible TODO and a borrowed event is an invisible one. The shot will stay quiet until
somebody authors an FMOD event for it — that is the one piece of "I didn't notice the shot" this
branch does not close.

### 4. The PIP

`ScopePipView` shows the ordinary chase shot of your own vessel in the **top-left**, under the goal
stack, while the cockpit view has the middle of the screen. It is **half the screen's height**
(16:9), sized as a FRACTION rather than in pixels because this canvas deliberately carries no
`CanvasScaler` — every other number in it is a real screen measurement derived from the camera — so
a fixed rect would be a different fraction of the display on every device and would not survive a
resize. The scope takes away every cue a pilot flies by —
where the hull is, how it is banked, what is beside it — so without it a scoped Serpent could line
up a shot or fly, not both.

**It is not a second gameplay camera**, which the platform forbids for four concrete reasons
(`Docs/REAR_VIEW.md`): the speed tunnel resolves `CameraManager`'s *active* controller,
`ApplyCameraGraphicsSettings` pushes the player's FOV and AA onto three managed cameras and no
others, background colour is applied per camera, and `Camera.main` returns the first **enabled**
camera tagged MainCamera. This one is created at runtime, **never tagged MainCamera**, renders only
into a `RenderTexture` and is left **disabled** and stepped by hand — outside all four systems by
construction. It is the `ConnectingArenaPreview` shape.

**The retired `Pip` prefab was deliberately not revived.** Its `border` `RawImage` names a texture
guid no asset carries, and a `RawImage` with a missing texture draws a **solid quad in its own
tint** — a navy rectangle over ~55% of the display (CLAUDE.md records it). It also gates on
`AutoPilotEnabled`, which is false at `Start` on every vessel and is a documented anti-pattern. The
new window has no frame at all for the first reason and no ownership test of its own for the
second: it is created by the scope, which is already local-pilot-gated.

**The cost is real and is stated rather than hidden.** Unlike the connecting panel's preview — which
stands the gameplay camera *down* because the panel covers the screen — this is a genuine **second
render of the world**, because the first one is what the player is looking through. It is paid for
the only way left: 216p, no post-processing, no shadows, no anti-aliasing, a 20 Hz refresh, and a
lifetime of exactly as long as the trigger is held. If it proves too expensive on a phone,
`RenderHeight` and `RefreshHz` are the dials, and switching it off costs the ability nothing.

## Round 3 — "I still saw no cooldown"

Two reports. The layout one was a layout one; the first was a real defect I shipped in round 2.

### The recharge readout drew NOTHING for the two states that matter

The arc was a bare fill. `ScopeRingGraphic.OnPopulateMesh` returns early at `sweep01 <= 0`, so:

| state | `cooldown01` | `Sweep01` | what was on screen |
|---|---|---|---|
| the frame you fire | 1.0 | **0.0** | nothing |
| a second later | 0.92 | 0.08 | a 29° tick |
| ready | 0.0 | (flash only) | nothing, once the 0.35 s flash ended |

So the readout was invisible at the instant the weapon went away, invisible once it came back, and
a thin slice in between — which is indistinguishable from not having one. The only other cue was
the reticle dimming, which reads as a reticle, not as a timer.

The fix is the rule the goal stack already records (`Docs/GAME_MODE_TOPBAR.md`): **a progress bar
needs a BED.** A dim full ring is always drawn at the arc's radius and the fill runs over it, so
"recharging" is a ring FILLING rather than one appearing out of nowhere; and **READY is a complete
bright ring**, not the absence of one, with the flash now an expansion on top of it rather than the
only thing that ever draws. Three states, three distinct pictures: dim ring alone (just fired),
part-filled (recharging), solid bright (armed).

*General shape: an indicator whose only rendering is its VALUE has no rendering at its extremes,
and its extremes are usually the two readings the player actually needs.*

**What this does NOT fix, stated plainly:** the readout lives on the scope overlay, so it is only
on screen while the left trigger is held. The fleet's own ability row would carry it everywhere,
and the Serpent binds no icons for it to sit on — that is still the icon-authoring follow-up below,
and the `SerpentVesselHUDController` push into it is still live and still waiting for one.

### The PIP moved and grew

Top-left under the goal stack, at **half the screen's height** (was a sixth, bottom-right). Sized
as a fraction rather than in pixels, for the reason the class note gives — this canvas has no
`CanvasScaler`. Its render height went 216p → **360p** with it: a 216p texture upscaled into 540
screen pixels is visibly soft, and a picture of your own hull you cannot read is the same as no
picture. Still no post, no shadows, no AA, still 20 Hz, still only while the trigger is held; the
extra cost is 2.8× the pixels of a window that was already the cheapest render in the frame.

Its top margin (220) clears the goal stack's three authored rows
(`Tools/Build/author_goal_stack.py`: anchored `(16, -52)`, `48` per row), not the one row that is
populated today — a mode that authors secondary goals must not land one behind this window.

## Round 4 — "the zoom is nauseating"

> *"i see the icon light up when i use or try to use it but there is only a dashed light. no icon
> and no cooldown indication. the zoom is nauseating. it should be only a function of the analog
> trigger pull and nothing else like movement. the pip should show the zoom (keep flying the same)
> and it should be a circular window."*

Four asks, and three of them are one idea: **the magnification was on the wrong picture.**

### 1. The inversion

The PIP and the main view swapped jobs. The window now carries the **magnified** cockpit view and
the main camera keeps the ordinary chase shot, untouched. Everything the scope says — the picture,
the reticle, the recharge ring — is now said in **one place**, because a reticle over the middle of
the screen would be a measurement of a view nobody is aiming with.

That retired the main-camera cockpit outright (`VesselFirstPersonView` deleted,
`CustomCameraController.FirstPerson` / `FirstPersonOffset` and the rigid-attach branch removed, the
four `VesselController` bind lines removed, `CustomCameraController.FollowOffset` / `FollowTarget`
— added in round 2 for the old PIP — removed with their only reader). `SetHomeFieldOfViewOverride`
is kept with no caller because it is a guard, not a feature; see **The view** above.

### 2. The zoom composes with nothing

See **The zoom is a pure function of the trigger** above. Both removed terms were defensible, which
is the interesting part: one was a design idea (Steady Eye) and one was a *correct composition*
(the speed tunnel narrowing the scoped base). Neither is wrong on its own; both were wrong on a
magnified picture.

**Space 5 was re-scoped** from Steady Eye to **Deep Focus** as a direct consequence — the upgrade
existed to switch off a bleed that no longer exists, and re-pointing it at more magnification keeps
Space owning exactly one thing on this hull.

### 3. The window is circular

`ScopeDiscGraphic` is a new generated `MaskableGraphic`: a triangle-fan disc that samples a texture,
with UVs taken from the unit circle straight onto `[0,1]²` and a zero-alpha feather ring for
antialiasing.

**Deliberately not a `RawImage` behind a `Mask`.** A mask is a stencil pass — an extra graphic, an
extra draw call and two stencil state changes — to produce a shape the component can emit as
geometry, and it is the same call the rest of this HUD family already makes: a sprited circle is
crisp only at the size it was exported at, and this window is a fraction of whatever screen it is
drawn on.

The render target went **360p 16:9 → 512² square**, because a round window wants a square picture:
sampling a 16:9 source into a disc either squashes it or throws its sides away. Refresh went
20 Hz → **30 Hz** with it — the magnified picture is what the pilot is now aiming with, and 20 Hz
reads as judder at 4× magnification in a way a chase shot at 1× did not.

The instrument is laid out as one object: the disc, a rim ring at the same radius, the recharge bed
and arc **outside** the rim, and the cone-sized reticle **inside** it. The reticle's projection is
unchanged in form — the window's own radius stands in for half the screen height, and the window's
own field of view for the camera's:

```
r = R · tan(coneHalfAngle) / tan(windowFov / 2)
```

### 4. The ability card

**The lockup's cooldown veil now draws on a LOCKED card.** `AbilityLockupView.SetAbilityCooldown`
refused a locked slot, so the Serpent's recharge — pushed correctly from
`SerpentVesselHUDController` since the ability shipped — landed nowhere for three rounds of
playtest. The veil never needed an icon anyway: it sizes itself on the ability **plate**, which a
locked card has.

> **An indicator that refuses to draw because its decoration is missing is indistinguishable from
> an indicator nobody is driving.** Locked means "this vessel has not authored an icon for this
> slot", which is a fact about the ART; a slot that is being DRIVEN is an ability that exists.

`SetUpgraded` is deliberately **not** changed by the same argument read the other way: an upgrade is
a statement about an ability the player can see, and on a locked card there is nothing to light.

**The icon itself is still missing** and is still art — the dashed bar the report describes is
`BuildLockedMark`, the lockup's designed placeholder, doing its job. What changed is that the card
now also says whether the weapon is loaded.

## Drive-by corrections

- **`Serpent.asset`'s Time entry had `Input: 0`** (`FullSpeedStraightAction`) while
  `ConsumeBoostAction` actually rides `Button1Action`. That field is not documentation: the ability
  lockup DRAWS each card's control chip from it (`map entry → InputHintBindingMap.BindingFor`), so
  the Serpent's Time card would have drawn the wrong glyph. Corrected to `6`; the ability table now
  reports `A / Space`.
- **`SniperShotActionExecutor` resolves its registry with `GetComponentInParent`, not
  `GetComponent`.** On every shipped vessel the registry lives on the `ShipActions` container and
  each executor sits on a **child** of it, so a same-object lookup returns null and every fallback
  below it is silently dead. `ToggleTranslationModeActionExecutor` carries that bug today and is
  deliberately left alone — it is another ability's play-tested behaviour.

## Checked and clear

- **Rule 26 (re-scoping an L5).** Space 5 on this hull could have granted two upgrades:
  `SerpentVesselExplosionByCrystalEffect.asset` reads `IsUpgradeActive(Element.Space)` for the
  blast's "spares allies" upgrade. It does **not** fire — the asset serializes no
  `_spaceUpgradeSparesAllies`, whose field initializer is `false`, and its
  `_heightMultiplierAtFullSpace` / `_coreMultiplierAtFullCharge` are both `1`, which the code
  skips outright. Nothing to switch off. (Confirmed by
  `Tools/Build/element_ability_table.py Serpent`, which labels both reads *inert — authored x1*.)

## Known limitations

- **The cooldown's element scaling is owner-local.** Element levels never replicate, so a remote
  replica cannot derive the owner's Charge-scaled cooldown. The *press* does replicate and the
  owner is the only machine that can produce one, so the owner's cooldown is the only one that can
  be authoritative: a non-owner therefore enforces only a **floor** (the fastest the ability can
  ever recover), which can never refuse a shot the owner admitted while still bounding a duplicated
  or replayed press. The exact fix, if it is ever needed, is to replicate the resolved cooldown the
  way `R_VesselActionHandler.NetEchoSightShape` replicates the Dolphin's cone — deliberately not
  paid for here.
- **No ability ICONS.** The Serpent still binds 0/4 icons, so the ability lockup renders four
  LOCKED cards (its designed state for an un-iconed vessel) with a dashed placeholder mark where
  the art goes. Since round 4 the Charge card's cooldown veil **does** draw on it, so the row
  reports the recharge; what is missing is the icon, which is art. Wiring real icons needs that art
  plus an in-editor pass with **FrogletTools ▸ Vessels ▸ Wire Vessel Ability Row**.
- **The scope window is a second render of the world**, and unlike the connecting panel's preview
  there is no gameplay camera to stand down — the player is flying with it. Paid for with a 512²
  target, no post, no shadows, no AA, 30 Hz and a lifetime of exactly as long as the trigger is
  held; `ScopePipView.RenderSize` / `RefreshHz` are the dials if a phone disagrees. **Unprofiled**,
  and round 4 made it more expensive on both axes (2.0× the pixels of round 3's 360p 16:9 target,
  1.5× the refresh) because it is now the picture the pilot aims with rather than a glance.
- **The scope overlay rebuilds six small UI meshes per frame while held** (≈96 segments each,
  guarded by `Mathf.Approximately` so an unchanged value costs nothing). Cheap, and unmeasured.
- **The shot has no FMOD event.** `fireEvent` ships **empty**, which is silence — never a borrowed
  event (CLAUDE.md's audio convention). It is an inspector-visible TODO on the
  `SniperShotActionExecutor` component.
- **No AI uses either ability.** `AIPilot` presses nothing here, and `OnButtonPressed` early-returns
  under autopilot, so an AI Serpent flies exactly as it does today.
- **Nothing has been run in the editor.** See below.

## In-editor verification

Everything below is unverified — the branch was authored headlessly. The C# was type-checked
against a Roslyn stub harness whose 34 stub signatures were each grepped out of the real sources
(and negative-controlled), and the six out-of-editor gates pass, but neither can see a camera.

1. **Scene:** any Serpent-capable scene (Menu_Main freestyle is enough; swap to the Serpent with
   the vessel-changer toy). Confirm no console errors on spawn.
2. **Scope:** hold **LT** (or **Left Shift** on keyboard, **LMB** on the one-thumb mouse scheme).
   Expect: a **round** window in the top-left showing a view down your own nose, and **the flight
   view completely unchanged** — the camera must not move by a pixel. Ease the trigger: the picture
   inside the window magnifies and the reticle grows with it; on mouse/keyboard it should ramp in
   over ~1/12 s rather than snapping.
3. **The zoom composes with nothing.** This is the round-4 assertion and it has three parts, all
   read off the WINDOW: (a) hold a steady trigger and haul the stick over — the magnification must
   not move; (b) hold a steady trigger and accelerate to top speed — the magnification must not
   move (the flight view's own speed tunnel still narrows, which is correct, and must not reach the
   window); (c) set Settings ▸ FOV to something non-default — the window is unaffected by it, and
   the flight view still honours it.
4. **Release:** the window disappears. The flight camera and the player's FOV are untouched
   throughout, so there is nothing to restore — if either moves at any point in steps 2–4,
   something is writing the gameplay camera that should not be.
5. **Deep Focus:** at Space 5 a full trigger pull should magnify visibly further than at Space 4
   (13.8° vs 22° at rest), and the reticle should grow with it. Raise Space to 10 and it reaches
   6.9°.
6. **Shot:** scoped, press **RT**. Expect a camera kick, the prism under the reticle destroyed with
   ordinary animated debris, and the **Charge** card's veil sweeping clockwise over ~12 s. Press
   again during the veil — nothing should happen.
7. **Super-shield:** point at Skim Race track mass (`SegmentSpawner` super-shields it) or an Astro
   League rail and fire. Expect the stella octangula to shed as debris *and* the prism to die —
   both, in one shot. Anything less (a deflect wobble, or the shield popping with the prism
   surviving) means the two-step sequence regressed.
8. **Pierce:** raise Charge to 5 and fire down a line of 3+ prisms — expect 3 destroyed, not 1.
9. **The contextual trigger:** unscoped, press RT — the **cloak** should engage exactly as it does
   today. Scoped, press RT — the cloak must **not** engage.
10. **Teardown:** while scoped, open the overview / pause (or let a turn end). The camera must
    return to third person and the FOV to the player's setting — this exercises
    `R_VesselActionHandler.ReleaseHeldInputs` and the executor's `OnDisable`.
11. **Vessel swap while scoped:** scope, then swap hulls with the vessel-changer toy. The new hull
    must arrive in third person at its own FOV, with no stranded zoom.
12. **MPPM, two clients:** scope and fire on client A. On client B the same prisms must die, and
    **B must see A's tracer** in A's domain colour. Then check B's own camera never moved, that B
    got no reticle and no PIP — `IsScoped` is shared, the screen is not.
13. **The reticle is a measurement.** Scope and read the ring **inside the window** against what
    dies: a prism just inside it must die and one just outside must not. Then ease the zoom — the
    ring must grow while covering the same mass, which is the whole claim. It must NOT move when
    you accelerate.
14. **The recharge arc:** fire, then watch the arc fill clockwise from the top over ~12 s and flash
    once as it completes. Raise Charge to 10 and confirm it fills in ~5.4 s instead.
15. **The tracer:** fire into empty space — expect a beam to the full 3,000 u and **no** flare.
    Fire at mass — expect the beam to stop at the kill and a flare there. Fire twice in quick
    succession (Charge 10) and confirm the second beam does not start from where the first ended,
    which is the pooled-instance reset.
16. **The window:** confirm it is a **circle**, not a rounded rectangle and not a squashed one —
    a straight edge anywhere means the disc's UVs or the render target's aspect drifted. Confirm
    **no UI is drawn inside it** and no navy rectangle anywhere. Release and it must disappear.
    Swap hulls while scoped and confirm no stray camera or render texture is left behind (check the
    hierarchy for `[SerpentScopeCamera]`).
17. **The ability card (round 4).** Fire, then look at the bottom-right ability row: the **Charge**
    card is LOCKED (a dashed mark, no icon) and its **cooldown veil must now sweep clockwise over
    it** for ~12 s. Before round 4 it drew nothing. Check it works with the scope DOWN as well —
    the row is the readout a pilot who is not scoped has.

## Follow-ups

- Wire the four ability icons (art + `Wire Vessel Ability Row`). The Charge veil no longer needs
  one, but the row still cannot say WHICH ability is recharging.
- Profile the scope window on a phone. It is a second render of the world at 512² / 30 Hz and it is
  the one cost in this branch nobody has measured.
- Consider whether the window should be placeable (top-left is a guess that clears the goal stack),
  and whether a left-handed or small-screen layout wants it elsewhere.
- Author the FMOD event for the shot — this is the one half of "I didn't notice the shot" this
  branch does not close.
- Consider whether the reticle should be shown UNSCOPED too (it currently is not: the cone is the
  same cone, but the shot cannot fire unscoped, and a permanent reticle on a hull with no crosshair
  would be a claim about a weapon that is not available).
- Fill the **Mass** slot — the last open Serpent design slot. `Docs/ElementalAbilitySystem/FLEET_MAPS.md`
  §2 still proposes *wall prism scale* / **Fortified Wall**, which does not collide with either
  ability added here.
- Consider whether any arena that super-shields structure for the Rhino's sword alone needs
  re-checking now that a second hull can open it.
