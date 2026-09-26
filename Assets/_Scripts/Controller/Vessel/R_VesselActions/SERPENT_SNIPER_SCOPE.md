# Serpent — Scope (Space) + Sniper Shot (Charge)

Hold the **left trigger**: a magnified view down the vessel's own nose opens in an eyepiece beside
the flight view, zoomed by the trigger's own depth, and a **reticle at the shot's true angular
size** appears in both pictures — growing in the eyepiece as the zoom narrows it, staying small over
the flight view. The pilot's own camera never moves. While the scope is up, the **right trigger**
stops being the cloak and becomes a **sniper shot** — one hitscan round down the scope's line that
destroys what it hits, **including super-shielded mass**, on a long cooldown.

Two of the Serpent's three open element slots are now filled. **Mass stays open.**

| Element | Ability | Input | Quantitative parameter | Level-5 upgrade |
|---|---|---|---|---|
| Charge | **Sniper Shot** | RT (`RightStickAction`) | cooldown: 12 s at rest → 5.4 s at Charge 10 | **Pierce** — the round may break SUPER-SHIELDED mass. Below it armour is not a target at all and the round flies past it |
| Mass | *(open design slot)* | — | — | — |
| Space | **Scope** | LT (`LeftStickAction`) | magnification: 22° FOV at full zoom at rest → 11° at Space 10, floored at 8° | **Deep Focus** — ×1.6 more zoom depth, and the floor drops with it (13.8° at rest, 6.9° at Space 10) |
| Time | Solid Fuel Pellets | A / Space (`Button1Action`) | burn duration ×1.6 at L10 | — *(restored 2026-09-25, `SERPENT_FUEL_PELLETS.md`)* |

## Why these elements

**Space is reach/presence fleet-wide, and a scope's whole claim is reach** — so Space owns the
magnification, and the element and the mechanic are the same statement. **Charge is
threat/energy**, and on a weapon whose per-shot effect is already absolute (one round, one prism,
whatever its armour) the only honest axis left is *how often you get to use it* — so Charge owns
the recovery. Both abilities scale through a dedicated authored field on their own action SO
(`cooldownMultiplierAtFullCharge`, `zoomDepthAtFullSpace`), read at use time through
`ElementalScaling.Multiplier`. This doc originally added that both were "pinned to `1`" in the map
to avoid double-dipping the generic `MultiplierAtFullLevel`; **that channel was deleted on
2026-09-18** (`Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md`), so there is no longer
a generic multiplier to pin — the dedicated fields are simply where the numbers live.

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
- **The picture is SQUARE**, matching the petal-shaped eyepiece it is drawn into
  (`ScopePetalGeometry`'s bounding box is square to within 0.5%), and the petal's own coordinates
  ARE the UVs — so nothing is squashed and nothing is cropped away: the shape is the crop. A camera
  targeting a RenderTexture takes its aspect from that texture, so there is nothing else to keep in
  step, and `fieldOfView` is vertical either way, which is what the reticle's projection reads.
  (Round 4 made it square for a round window, round 7 put it back to 16:9 with the `RawImage`, and
  round 8 squared it again for the petal — see those rounds for why the surface itself never moved
  after round 7.)
- **It draws the way the GAME'S camera draws**, adopted through `OffscreenCameraSetup` — HDR, the
  volume layer mask and the post-processing stack. That is not a quality setting here: the world is
  authored HDR-emissive against the gameplay volume's tonemapper, so a camera on URP's defaults
  renders a flat, near-black picture. It is what round 5 was about, below.
- **The clip planes are DERIVED, not borrowed** — near hugs the eye (0.3), far reaches the arena.
  Borrowing them was the first cut's choice and was wrong for a camera sitting *on* the hull: a
  near plane sized for a chase camera 250 units back clips away everything this one is close to.
  Its three sibling windows each derive their own for their own reasons.

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

`SniperScopeActionExecutor` authors NO tuning of its own — every number above lives on the SO,
which is what "the zoom composes with nothing but its own trigger" costs: the executor has no
second input to weigh, so it has no field to weigh it with. (It carried one, `steadyZoomFloor`,
for the retired Steady Eye bleed; round 4 removed the bleed and the field with it.)

`Assets/_SO_Assets/VesselActions/Serpent/SniperShotAction.asset`

| Field | Ships at | What it does |
|---|---|---|
| `cooldownSeconds` | 12 | wait between shots at resting Charge — the ability's whole cost |
| `cooldownMultiplierAtFullCharge` | 0.45 | → 5.4 s at Charge 10 |
| `rangeUnits` | 3000 | the whole flight; there is no projectile to outrun |
| `coneHalfAngleDegrees` | 1.5° | angular half-width of the hitscan — **78.5 u** at 3,000 u (round 10; it was 0.5° = 26 u, then 10° = 529 u for one playtest) |
| `minPathRadius` | 6 u | radius floor near the muzzle; the angular term takes over past **229 u** |
| `pierceCount` | **0** | prisms the round takes, at EVERY tier; 0 = unlimited — everything the cone contains |
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
| `…/R_VesselActions/Executors/SniperScopeActionExecutor.cs` | **new** — scope state, zoom drive, `IsScoped`; **+** (round 9) passes the shot's range through to the overlay |
| `…/R_VesselActions/Executors/SniperShotActionExecutor.cs` | **new** — hitscan, cooldown, super-shield teardown, tracer; **+** (round 9) `RangeUnits`, the anchor the flight view's reticle is projected at |
| `…/R_VesselActions/Executors/SniperBeam.cs` | **new** — the pooled domain-coloured tracer + impact flare |
| `…/R_VesselActions/Executors/SniperScopeOverlay.cs` | **new** — the eyepiece, the reticle inside it and the recharge ring around it; **+** (round 9) a second reticle over the FLIGHT view, and `ReticleRadiusPixels` generalised into the shared `ReticlePixels` both draw through |
| `…/R_VesselActions/Executors/SniperScopeDiagnostics.cs` | **new** (round 6) — warn-once reporting for a system whose failure mode is a blank screen; **+** (round 9) `NoGameCamera`, which stands the flight reticle down and leaves the eyepiece alone |
| `_Scripts/UI/View/ScopeRingGraphic.cs` | **new** — the generated ring/arc |
| `_Scripts/UI/View/ScopePetalGeometry.cs` | **new** (round 8) — the CHARGE petal's outline, traced off the shipped sprite, plus the inradius/circumradius/area derived from it |
| `_Scripts/UI/View/ScopePetalImage.cs` | **new** (round 8) — the eyepiece. A `RawImage` SUBCLASS overriding only `OnPopulateMesh`, so every part of the surface that renders is inherited |
| `_Scripts/Utility/CSDebug.cs` | **+** (round 6) `CSLogChannel.SerpentScope` |
| `_Scripts/UI/View/ScopeDiscGraphic.cs` | **added round 4, DELETED round 7** — the generated circular picture. Replaced round 3's `RawImage` and was never seen on screen; retired so two windows cannot compete for the next report |
| `_Scripts/UI/View/AbilityLockupView.cs` | **+** (round 4) a LOCKED card draws its cooldown |
| `_Scripts/Utility/ScopePipView.cs` | **new** — the runtime RenderTexture camera; round 4 re-pointed it at the SCOPE, round 5 made it adopt the game's image, round 7 put its surface back to a `RawImage`, round 8 made its target SQUARE for the petal |
| `_Scripts/Utility/OffscreenCameraSetup.cs` | **new** (round 5) — the ONE place a runtime off-screen camera adopts the game camera's framing and image. Extracted after the same finding had been written down at three other windows |
| `_Scripts/Controller/Arcade/Preview/ModePreviewArena.cs` | **+** (round 5) `AdoptGameCameraSettings` routes through the helper |
| `_Scripts/UI/Elements/ConnectingArenaPreview.cs` | **+** (round 5) `AdoptUrpSettings` routes through the helper |
| `_Scripts/UI/Elements/ToyPreviewCamera.cs` | **+** (round 5) `EnsureRig`'s URP block routes through the helper |
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

So the path is an **angular cone**: `radius(t) = max(minRadius, t · tan(halfAngle))`. The property
that matters is that it is **the same on-screen size at every range**, whatever the angle — *that*
is what lets the reticle be drawn at the beam's true size rather than at a guess, which is the whole
of §2 below. The angle shipped at **0.5°** (26.2 u at 3,000 u, ~49 px across at the 22° scope) and
was raised to **10°** by authored instruction in round 9b, which is 529 u at 3,000 u — see §9b for
what that costs.

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

### 4. The PIP — **round 2's arrangement; rounds 4-8 inverted and re-cut it**

> Kept because two of its paragraphs are still live doctrine (the four reasons this is not a second
> gameplay camera, and why the retired `Pip` prefab was not revived). **Four claims below were
> superseded and are left as written rather than silently patched**, because which round changed
> each one is the useful part:
>
> | Claim here (round 2) | Superseded by | Ships as |
> |---|---|---|
> | the PIP shows the chase shot, the **cockpit** has the screen | round 4 (the magnification was nauseating on the flight view) | the flight view is the ordinary chase; the WINDOW carries the magnified picture |
> | the panel is **16:9** | round 8 (positions are the UVs, so the petal's square bbox sets the rect) | `PipHeightFraction` 0.5 on **both** axes — square |
> | **216p**, 20 Hz, **no post-processing** | round 5 (an un-adopted camera renders this HDR world near-black) | `RenderSize` 540, `RefreshHz` 30, post-processing **ON**, AA and shadows off |
> | `RenderHeight` is the dial | round 8 renamed it | **`RenderSize`** |
>
> The current window is described in **round 7** (the surface) and **round 8** (its shape).

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

### 3. The window is circular — **RETIRED in round 7**

> This subsection is kept as the record of a change that was reverted. The circular surface below
> replaced round 3's `RawImage` in the same pass that re-pointed the camera, and **it never rendered**
> — which is what rounds 4, 5 and 6 were all reporting. Round 7 restored round 3's surface verbatim
> and kept only the camera. The panel is a 16:9 `RawImage` again and `ScopeDiscGraphic` is deleted.
> The reticle-projection formula at the end of this subsection survives, with half the panel's
> HEIGHT standing in for `R`.

`ScopeDiscGraphic` was a new generated `MaskableGraphic`: a triangle-fan disc that samples a texture,
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

## Round 5 — "I no longer saw the pip"

**One cause, and this codebase had already written it down three times.**

A bare `AddComponent<Camera>()` comes up with **URP's defaults, not the project's** — no
post-processing, no volume layer mask, SDR. Cosmic Shore's world is authored almost entirely
HDR-emissive against the gameplay volume's tonemapper, so a camera that skips it renders a flat,
colourless, near-black version of a world the game shows lit. Round 4's window explicitly set
`renderPostProcessing = false` and copied no `volumeLayerMask`, on the reasonable-sounding ground
that a second render of the world should be paid for wherever possible.

**Why it surfaced at round 5 and not at round 2, when the window first appeared:** rounds 2–4
framed the pilot's own **lit hull**, dead centre, from a chase pose. A bright, high-contrast
object is unmistakable even rendered wrong — it reads as *low quality*, which nobody reports. The
inversion pointed the same camera down the nose at **open space**, where the whole picture *is* the
skybox and the volume, and a flat render of that is a dark circle on a dark screen behind a
55%-alpha hairline rim. Nothing about the code path changed; the subject did.

**The general rule, which is why this is worth a section:** *a picture that renders WRONG and a
picture that does not render at all are the same report.* The distinction is available to whoever
wrote the camera and to nobody looking at the screen, so the failure arrives as "the window is
gone" and sends you hunting for a layout, a binding, a null, a compile error — everything except
the image settings, which are the one thing that *looks* like a preference.

**Three prior sites had each rediscovered it, and each wrote it down locally:**

| site | its own words |
|---|---|
| `ModePreviewArena.AdoptGameCameraSettings` | *"comes up with URP's DEFAULTS… a flat, aliased, bloom-free version of a world the tap-in phase then showed correctly — which reads as the preview being low quality rather than as two different cameras"* |
| `ConnectingArenaPreview.AdoptUrpSettings` | *"would render a flat, bloom-free version of a world the game shows lit"* |
| `ToyPreviewCamera.EnsureRig` | *"POST-PROCESSING IS NOT OPTIONAL: every lifeform and prism material in the game is authored HDR-emissive against the gameplay volume's tonemapper, and drawn without it a creature comes out as a blown-out white silhouette with no colour in it"* |

Three copies of one finding is three chances to not read any of them, and the fourth window paid
the playtest. It is now **one helper** — `OffscreenCameraSetup` (`_Scripts/Utility/`) — and all
four sites route through it:

- `AdoptGameCameraFraming(target, excludeUiLayer)` — *what* it sees and clears to. Clip planes are
  deliberately **not** copied: all four windows frame completely different shots and each records
  why a borrowed far plane clips its subject away.
- `AdoptGameCameraImage(target, postProcessing = true, antiAliasing = false, shadows = false)` —
  *how* it draws. **The defaults are the rule**: shadows and anti-aliasing are the two a small
  window may honestly decline, and post-processing is not, so a caller that switches the tonemapper
  off has to say so at the call site. The **load-bearing half lives on
  `UniversalAdditionalCameraData`**, not on `Camera` — which is the second reason this keeps
  happening, since the `Camera` fields are the ones whose absence looks obviously wrong.

### Two things that make the window legible whatever it is pointed at

Adopting the tonemapper fixes the render. It does not fix the fact that a scope pointed at empty
space is *correctly* showing very little, and the round-4 eyepiece drew nothing of its own:

- **An opaque BACKING disc**, drawn under the picture and on from the moment the scope is raised.
  A window showing nothing and no window at all must not look the same. It also covers the frames
  before the first render lands.
- **The rim is now full-strength** (4 px, α 0.9, up from 3 px at α 0.55). A 55%-alpha hairline
  around a dark circle is not a readable object on a nebula background.

Both discs are **children** of the window rather than a graphic on the window itself, because UGUI
draws a parent before its children — which gives exactly one slot in the order, and the eyepiece
needs two before the rings.

### Two hardenings in the same pass

Neither is the cause; both are failures that would have presented identically, and one of them
would have been permanent:

- **A zero hull measurement is no longer latched.** `MeasureCircumscribedRadius` skips *disabled*
  renderers, so a hull asked before its art is switched on answers 0 — and a latched 0 parks the
  eye on the vessel's own origin, *inside* its geometry, for the life of that vessel. It re-asks
  while the answer is 0 (one walk per frame, in exactly the case where the alternative is a
  permanently black window), warns once by name, and floors the eye at 2 units so the fallback is
  still in *front* of the ship.
- **The render target is `Create()`d outright.** The surface binds it in the same frame, and a
  canvas sampling an uncreated target draws nothing.

## Round 6 — "the pip was not there anymore at all"

The same four words as round 5, after a round-5 fix that was specifically designed to make those
four words impossible: the eyepiece gained an **opaque backing disc** and a **full-strength rim**
precisely so that *showing nothing* and *not being there* could not look the same. It still read as
nothing. That is a different fault from round 5's, and round 5's diagnosis (URP camera defaults →
a flat near-black picture) is now known to be **incomplete rather than wrong** — it was a real
defect in a real code path, and it was not the whole story.

### The finding is about the process, not the code

**Three rounds were spent guessing at a blank screen, and a blank screen is one report for at
least four unrelated faults.** Since round 4 retired the main-camera cockpit, the eyepiece is the
ability's **only** visible output, so the pilot cannot distinguish:

1. the scope never engaged (no input, no executor, no `Player`);
2. it engaged but `DrawOverlay` returned at one of its four gates;
3. the overlay ticked but its own geometry or canvas made it invisible;
4. it drew correctly and something opaque is on top of it.

Nothing in the console separated those. So this round adds no feature and changes no look: it
makes the instrument **answer the question itself**, in the house shape
(`MouseFlightDiagnostics`, `PrismOcclusionDiagnostics`, `VesselVisionDiagnostics`) —
`SniperScopeDiagnostics`, warn-once per reason for the lifetime of the process.

The split is the house one and it is the point: **a refusal is an unconditional warning naming the
gate**, because a system whose failure mode is silence has to be loud when it fails; **the happy
path is one line on `CSLogChannel.SerpentScope`**, off by default, because bring-up telemetry for a
working system is console spam. Enabling that one channel (FrogletTools ▸ Toolbox ▸ Logging) is
therefore the whole procedure for separating case 1/2 from case 3/4.

There is deliberately **no rung for "it is being covered"**. Case 4 is not decidable from source —
only a rendered frame can name what is on top — and an unconditional warning saying "everything
checks out, look elsewhere" would be a permanent false positive the day the real defect is fixed.
The guidance rides the happy-path line instead and names the tool that answers it: **FrogletTools
▸ Diagnostics ▸ Report On-Screen UI, in play mode**, which names every enabled `Graphic` covering
≥2% of the display, biggest first, with its path and its effective alpha. CLAUDE.md already records
why: *a rendered frame is the one thing static analysis of scenes and prefabs cannot see*, and the
`Pip.prefab` navy-quad bug was found that way after three confident wrong answers were read out of
the YAML.

### What was ruled out, and how

Everything below was **measured off the shipped assets**, not assumed, and every one of them came
back clean — which is itself the finding, because each was a plausible total-blackout cause:

| Hypothesis | Check | Result |
|---|---|---|
| The scope executor is not on the hull | guid `dcc95f…ef88` in `Serpent.prefab` | present |
| It is not in the registry's serialized list, so `Initialize` never runs and `_registry` stays null (which would gate the **whole** overlay behind `_shot`) | `ActionExecutorRegistry._executors` contains both executor fileIDs | both present |
| `VesselStatus._shipInstance` is unreferenced, so `ShipTransform` **throws** (it is `Vessel.Transform` with no guard, and `VesselStatus.Vessel` logs an error and returns null) | `_shipInstance` on the Serpent's `VesselStatus` | wired |
| Round 4 broke the LT binding | `Serpent.asset` Space entry `Input` | `2`, unchanged by round 4 |
| The rings are painted black on a near-black backing | `SniperShotActionExecutor.ResolveTracerColour` | a domain signal colour, falling back to `Color.white` — never black |

The registry check is worth keeping because of how *nearly* it was the answer: `Get<T>()` falls
back to `GetComponentInChildren<T>(true)`, so an executor missing from `_executors` would still
receive `Engage` (and `_status` from its argument) and would still zoom — while `_registry`, which
is only ever assigned in `Initialize`, stayed null, taking `_shot` and therefore the entire overlay
with it. **An ability that half-works is not evidence that its wiring is complete.**

### Two real defects fixed on the way

Neither is provably the round-6 report; both are genuine faults found while reading for it, and
both are the kind that produce exactly this symptom.

- **Hiding the instrument by `Canvas.enabled` can permanently freeze every graphic under it.** A
  UGUI `Graphic` caches its canvas in `m_Canvas`, and `Graphic.IsActive()` is
  `base.IsActive() && m_Canvas != null` — so every `SetVerticesDirty` / `SetMaterialDirty` is a
  **silent no-op** while that cache is null. When a canvas is disabled beneath them,
  `OnCanvasHierarchyChanged` nulls the cache and *then* tests `IsActive()`, which is false **because
  it just nulled it**, so it returns without re-caching — and it does the same on the way back up.
  `Create()` built the graphics and immediately disabled the canvas, so from that moment the rings
  could never rebuild: they went on drawing whatever mesh they had, at whatever size, for the whole
  session. Measured consequence: the **rim and backing happen to survive** (the field assignment in
  the setter still lands, and the pending `SetAllDirty` from `OnEnable` rebuilds once at the end of
  that first frame), while the **reticle is frozen at the round-1 zoom and the recharge arc at the
  round-1 sweep** — i.e. the cone-sized reticle stops being a measurement, which is the one thing it
  exists to be. `SetVisible` now toggles the **window GameObject**, which runs `Graphic.OnEnable` →
  `CacheCanvas()` + `SetAllDirty()` and therefore recovers by construction. That makes `Tick`'s
  existing "`SetVisible(true)` **first**, then write the radii" ordering load-bearing rather than
  incidental, and `SelfCheck` now asserts it — via `IsActive()`, **not** the `canvas` property,
  whose getter re-caches on read and would heal the very thing being tested.

  General rule: **`Canvas.enabled` and `GameObject.SetActive` are not interchangeable ways to hide
  a generated UI — only one of them lets its graphics rebuild afterwards.**

- **`IVesselStatus.ShipTransform` is `Vessel.Transform` with no guard**, and `VesselStatus.Vessel`
  logs an error and returns `null` when `_shipInstance` is unreferenced — so that property
  **throws** rather than answering null, and a throw in `DrawOverlay` takes the whole instrument
  with it rather than just the window. It is now `SniperScopeActionExecutor.ResolveHull`, which
  prefers `Vessel.Transform` and falls back to this executor's own root, caches the answer (because
  the null path logs an *error*, which per-frame is the spam the logging convention exists to
  prevent), re-resolves only while the answer is still null, and is cleared on re-init. A scope
  that is slightly mis-seated is worth more than one that does not exist.

### What the next playtest should produce

Instead of four words, one of these:

- a **warning** naming the gate (`no Player`, `not the local pilot`, `no SniperShotActionExecutor`,
  `no hull`) — case 1 or 2;
- a **warning** naming the unusable state (canvas disabled, stale `m_Canvas`, zero radius,
  transparent backing, eyepiece off screen) — case 3;
- with `[SerpentScope]` enabled, **one line** giving the eyepiece's centre, radius and the screen
  size — which means it *is* being drawn, and the next step is the on-screen UI reporter — case 4;
- or nothing at all in either place, which now means `Update` itself is not running and the
  question moves to the input binding.

## Round 7 — "you were showing the pip fine ... there has been no pip since we tried to swap those"

> *"i still don't see the pip. you were showing the pip fine when you had the vessel view in the pip
> and the zoom covering the whole screen, but there has been no pip since we have tried to swap
> those. this state should be easier since it is just the same pip of the same size in the same
> place showing the zoomed in camera."*

That is the answer, and it came from the pilot rather than from any of the three rounds spent
looking for it. **The window was on screen for rounds 1–3 and has not been on screen since round 4**
— and round 4 did not re-point it, it *replaced* it.

### The finding

Round 4 was one change that did two things:

| | round 3 (seen) | round 4–6 (never seen) |
|---|---|---|
| surface | `RawImage` | generated `ScopeDiscGraphic` (a circular `MaskableGraphic`) |
| rect | `Pip`, a direct child, pivot `(0, 1)`, 16:9 | `ScopeWindow` → `Backing` + `Picture`, pivot `(0.5, 0.5)`, square |
| render target | 16:9 | square, `Create()`d |
| camera subject | the chase shot, self-resolved | the eye past the nose, trigger FOV |

Only the last row was the design. The other three were incidental, and one of them was the window.

**The general rule: when one change replaces a surface AND re-points it, the report cannot tell you
which half broke.** The pilot's report is the same either way — *"I don't see it"* — so the
inversion and the rebuild had to be separable to be testable, and they were not. Rounds 5 and 6 each
found a real defect (URP camera defaults; a `Canvas.enabled` visibility toggle that freezes UGUI
mesh rebuilds, plus two lifetime hardenings), each shipped it, and each changed nothing on screen,
because none of them was about the surface. **Three correct fixes that buy no part of the complaint
is the signal that the thing being fixed is not the thing that is broken** — the same rule
`SCARAB.md §3.7` records from the other direction, where successive correct fixes each bought a
*diminishing* amount of one complaint.

### What round 7 does

Restores round 3's surface **verbatim** and keeps round 4's camera. Nothing else moves:

- `ScopePipView` takes a `RawImage` again and renders 16:9 (`RenderHeight` — renamed `RenderSize`
  in round 8, when the target went square — 540, up from round 3's
  360 — the same panel now carries a *magnified* picture, and one that cannot be read is the same as
  no picture). A camera targeting a RenderTexture takes its aspect from that texture, so there is
  nothing else to keep in step.
- `SniperScopeOverlay`'s panel is round 3's rect exactly: a child named `Pip`, anchors and pivot
  `(0, 1)`, `sizeDelta = (h · 16/9, h)` with `h = max(90, Screen.height × 0.5)`, at
  `(16, −220)`.
- The eye, the trigger-driven field of view, the hull measurement, the URP adoption, the
  GameObject-based visibility toggle and the diagnostics all stay — they are rounds 4–6's, they are
  correct, and now they are attached to a surface that draws.

Two things kept from round 4 because they answer real problems rather than being part of the
substitution:

- **The reticle stays INSIDE the panel.** Round 3 drew it at screen centre because the middle of the
  screen *was* the scope. It is not any more, so a reticle there would measure a view nobody is
  looking through. It is projected against **half the panel's HEIGHT**, because a vertical field of
  view is what the panel's vertical extent subtends.
- **The backing stays.** A plain `Image` with no sprite draws a solid quad in its colour — the exact
  behaviour `PipUI.SilenceUntexturedGraphics` exists to *suppress* elsewhere, wanted here — so the
  panel is an object on screen before the first render lands and when it is pointed at empty space.
  It is inset by 3 px behind the picture, so it also reads as the panel's frame.

The recharge ring moved with the reticle: concentric with it, just inside the picture's top and
bottom edges. A ring clearing the panel's *full* extent would be a circle around a 16:9 rect with
most of itself out in the flight view.

### What was retired

`ScopeDiscGraphic` is **deleted**, with its `.meta`. It was referenced by nothing but this scope (no
prefab, scene or asset carries its guid — checked), and CLAUDE.md's rule is explicit: an
unreferenced subsystem is eventually mistaken for a live feature. **It is not deleted because it was
proved wrong** — reading it produced no defect, and the round-4 hierarchy above it has candidate
faults of its own — it is deleted because the surface it replaced is the one the pilot has seen, and
carrying two windows would leave the next round asking the same unanswerable question. `ScopeRingGraphic`
stays: it is the rings, and the rings drew in round 3.

### What is still not known

**Why the disc did not render.** The geometry, the winding (UGUI draws `Cull Off`), the UV
mapping, the white-texture fallback and the rect arithmetic all read as correct, and the round-6
self-check would have named a disabled canvas, a stale `m_Canvas`, a zero radius, a transparent
backing or an off-screen window. So either the fault is in something none of those cover, or the
self-check never ran (which is itself reported, now, on the `[SerpentScope]` channel). That is
recorded rather than resolved: the pilot asked for the window back, not for a diagnosis, and the
restored surface is the one that has been on screen.

## Round 8 — "the pip is working. it is great. Now make the shape the Charge pentagon"

The eyepiece is now **one petal of the CHARGE element flower** — the element that owns this
weapon — so the window says which element is firing without a label.

### The shape is MEASURED, not drawn

`ScopePetalGeometry` carries the outline traced off the alpha of the shipped
`Resources/ElementPetals/charge_petal.png` (256², opaque bounding box x 81..174, y 23.5..117).
Every row's span walks a straight line to within a pixel, so the sprite really is a pentagon and
these really are its corners:

| corner | pixel | normalised (x right, y UP) |
|---|---|---|
| apex (points at the flower's centre) | (127.5, 117.0) | (0.500000, 0.000000) |
| right shoulder (the widest row) | (174, 53) | (1.000000, 0.684356) |
| top-right | (157, 23.5) | (0.817204, 1.000000) |
| top-left | (98, 23.5) | (0.182796, 1.000000) |
| left shoulder | (81, 53) | (0.000000, 0.684356) |

**Two facts fall out of the trace and confirm it rather than being assumed.** The two lower edges
meet at **72.30°** — the same 72° the five-petal flower is built from, which is what this document
and CLAUDE.md already say about every element petal ("all sharing an inward-pointing 72° apex").
And the shape is symmetric about its own centre line to half a pixel. Neither was put in; both came
out.

Everything derived from the outline is **computed in a static constructor rather than transcribed**,
so re-tracing the sprite moves all of it at once:

| derived | value | why it is load-bearing |
|---|---|---|
| area fraction of its bbox | 0.600 | — |
| **inradius** | 0.2950 of the side = **0.59 × half-side** | the cap on anything drawn as a circle inside the shape |
| **circumradius** | 0.5921 = **1.18 × half-side** | why the recharge ring is drawn INSIDE (below) |
| convex, CCW, centre interior | ✓ | what makes a triangle fan from the centre a legal tessellation |

### The surface is SUBCLASSED, and that is round 7's rule applied to a re-shape

`ScopePetalImage : RawImage` overrides **only `OnPopulateMesh`**. Nothing else about the surface is
rebuilt: the texture property, the `mainTexture` white fallback, the material handling and the whole
rebuild path are `RawImage`'s, unchanged.

That is deliberate and it is the whole risk story. Round 4 replaced this window's `RawImage` with a
generated `MaskableGraphic` **and** re-pointed its camera in one change; the replacement never
rendered, the fault was never diagnosed, and three playtest rounds came back as the same four words.
Re-shaping the window is a second chance to make exactly that mistake. **When a component renders
and its shape is wrong, subclass it and override the geometry — do not write a new one.** A broken
emit then reads as a wrong SHAPE, which a pilot can report, rather than as an absence, which they
cannot.

The **backing is the same component with no texture assigned** — `RawImage` falls back to a white
texture, so a textureless instance is a flat shape in its own colour. That is what makes the frame
the petal's own outline rather than a rectangle behind it, and it means there is no second outline to
keep in step. The frame is produced by insetting the *rect* by 3 px, which shrinks the petal about
its own centre and so follows every edge including the apex.

### Positions ARE the UVs, which is why the render target went square

`ScopePetalGeometry`'s coordinates are normalised over the petal's own bounding box, so the same
numbers are the vertex positions in a square rect and the UVs into a square picture. The bounding box
is square to within 0.5%, so:

- the rect went **16:9 → square** (the half-screen height and the 16/220 margins are round 3's,
  untouched — the height and the place the pilot already reads do not move, only the width);
- `ScopePipView.RenderSize` renders a **square** target.

Nothing is squashed and nothing is cropped away: **the shape IS the crop.** Unity's `fieldOfView` is
vertical either way, so the reticle's own projection — half the eyepiece's height against the
window's vertical FOV — is unchanged.

### A shaped window cannot bound its furniture by its bounding box

The reticle and the recharge ring are still `ScopeRingGraphic` circles at the optical centre (which
is the bbox centre, UV 0.5/0.5 — verified interior). Their radii are now capped by the petal's
**inradius**, not by a fraction of the half-height, and the binding constraint is the pair of long
edges running down to the apex rather than the top edge — so the cap is meaningfully tighter than a
half-width. At 1080p: side 540, inradius 159.3, recharge ring 148.3, reticle budget 139.3, and
ring + band (153.3) fits inside inradius − border (156.3).

**The recharge ring is inside the petal rather than around it, and that is a measurement not a
preference.** A circumscribing ring needs 1.18 × the half-side plus its gap and band, which at this
window's authored 16 px left margin puts the instrument ~49 px off the left edge of the screen. The
alternative — a band that sweeps the petal's own perimeter, which is the prettier answer and would
cost no footprint — is a **second** new generated graphic, and round 7's lesson is that this window
gets one change at a time. Recorded as a follow-up.

### Feather, and the one place a bisector would not do

Antialiasing is the family's zero-alpha feather ring, offset along each corner's **miter** rather
than its bisector. That matters here rather than being pedantry: the apex is a 72° corner, where the
miter is `1/sin 36° = 1.70` and a plain bisector offset would produce a feather 41% too thin, making
the point read as harder-edged than the rest of the outline. Verified numerically — a 1.5 unit offset
moves **both** adjacent edges out by exactly 1.5 at every corner, apex included.

## Round 9 — "there should be a reticle in both the zoomed section and the main view"

> *"both should match the radius of the blast, this means it should grow as the player zooms in on
> the zoom view and stay small on the main view. the reticles should only appear while using
> zoom."*

Rounds 1–8 left exactly one reticle, and it lives inside the eyepiece. Round 3 had drawn one over
the middle of the screen — correctly, because at the time the middle of the screen *was* the scope
— and round 8's inversion moved it into the window and left the flight view bare. That is a real
gap rather than a tidy-up: a scoped Serpent pilot is reading **two pictures at once**, the eyepiece
to identify the target and the flight view to keep flying, and only one of them was telling them
where the round would go.

### Both reticles are ONE function; the difference is entirely in the arguments

`ReticlePixels(coneHalfAngle, tan(fov/2), halfHeight, minPixels)` is the whole of it, and it
replaces the old single-view `ReticleRadiusPixels`. A vertical field of view is what a picture's
vertical extent subtends, so half that extent in pixels over `tan(fov/2)` is that picture's
pixels-per-unit-tangent, and the cone's own tangent scales straight through it:

| | half-height | field of view | radius at 1080p |
|---|---|---|---|
| **Eyepiece** | half the petal's side (270 px) | the scope's own, 60° → 8° as the trigger goes down | **4.1 px → 33.6 px** (capped at the petal's 139.3 px budget) |
| **Flight view** | half the gameplay camera's `pixelHeight` (540 px) | `Camera.main.fieldOfView`, live | **4.7 px** at 90°, 8.2 px at 60° |

That table IS the request: the eyepiece's grows **8×** across the zoom dial because its field of
view narrows while the cone does not, and the flight view's stays a few pixels because the gameplay
camera's field of view is not the scope's dial. Two circles the pilot can compare, both of them
true, and neither of them authored.

The floor is the **caller's** rather than baked into the helper, because the two views want
opposite ones. The eyepiece keeps round 3's 6 px so its ring stays a ring at the wide end of the
dial. The flight view takes **3 px**: it is *supposed* to be small, so a floor generous enough for
the eyepiece would here be an inflation of the very number the ring exists to state, which is the
one thing a measurement may not do. At the widest field of view the fleet runs, 3 px does not bind.

### The flight reticle is PROJECTED, not centred — and that is not belt-and-braces

The obvious implementation is a circle at screen centre. It would be right most of the time on this
hull and wrong in a way nobody could diagnose the rest of it. The shot leaves along
`hull.forward` from `hull.position` (`SniperShotActionExecutor.ResolveShot`), and the gameplay
camera is only on that axis when it is exactly behind the hull:

- **In the steady state it is.** `SerpentCameraSettingsSO.followOffset` is a pure `(0, 0, -250)`
  and `CustomCameraController` places the camera at `target.position + target.rotation * offset`
  and looks at the target — so the axis projects to a single point at screen centre, and the range
  you project at does not matter.
- **Through a turn it is not.** `followSmoothTime 0.1` / `rotationSmoothTime 7` mean the camera
  lags, which is exactly when a pilot is swinging the nose onto a target.
- **In the rear view it is behind the pilot entirely** (`CustomCameraController.EffectiveOffset`
  mirrors z), where a centred reticle would be marking a shot going the other way.

So the centre is `Camera.main.WorldToScreenPoint(hull.position + hull.forward × RangeUnits)`,
re-measured every frame, and the reticle is **stood down outright** when that projection's `z` is
non-positive. That guard is not decoration: `WorldToScreenPoint` on a point behind the camera
returns a **mirrored** position, which is a plausible-looking lie rather than an obvious failure —
the reticle would appear on screen, in the wrong place, with nothing to say so.

`SniperShotActionExecutor.RangeUnits` is new, and exists only for this: the eyepiece's camera sits
*on* the shot's axis, so there every range projects to the same place and the number is never
needed.

### Only while scoped, and only ever one authority on that

Both reticles are built with the rest of the instrument, shown by `SniperScopeOverlay.Tick` and
taken down by `Hide`, and `Tick` is only ever reached from `SniperScopeActionExecutor.Update`
behind `if (!_engaged) return`. There is no second path and no new lifetime to get wrong — a pilot
who is not holding the trigger has no mark on their screen at all, which is what makes the mark
mean *I am aiming* rather than *I am a Serpent*.

### Three details that are decisions rather than defaults

- **The flight reticle's root is its own**, a sibling of the eyepiece rather than a child, so it can
  be placed in screen coordinates and stood down on its own — a rear-view flip hides it while the
  eyepiece keeps working. It is anchored to the canvas's bottom-left with a centred pivot and zero
  size, which makes `anchoredPosition` a screen pixel coordinate outright: the canvas is
  ScreenSpaceOverlay with deliberately no `CanvasScaler`, so a canvas unit IS a screen pixel and
  `WorldToScreenPoint`'s answer is written straight in with no mapping to keep in step.
- **`SetVisible` only ever switches it OFF.** Its visibility has two levels — the scope is up,
  *and* the aim point can be projected this frame — and `DrawFlightReticle` owns the second, so
  leaving the ON case to that method is not an oversight. Re-activating it every tick would mean a
  frame it had just stood down (the rear view) gets re-activated on the next one and stood down
  again, and each toggle runs `Graphic.OnEnable → SetAllDirty`: a rebuild of both its meshes, every
  frame, for as long as the pilot is looking backwards. `SetActive` with the value an object
  already has is a no-op, so the split costs nothing.
- **It is built FIRST**, so it is the earliest sibling. UGUI draws siblings in order, and on the
  rare frame the aim point projects into the top-left the opaque eyepiece should cover it rather
  than have a stray ring floating over the picture.
- **Its band and pip are derived from its own radius**, not authored. The eyepiece's fixed 2 px band
  and 2.5 px dot are proportionally tiny inside a 33 px ring and would together fill a 5 px one
  solid; `thickness = min(2, r/2)` and `dot = clamp(0.3r, 1, 2.5)` keep it reading as a ring with a
  pip in it at any size the cone works out to.

### What it does NOT change

The cone itself is untouched — still the authored `coneHalfAngleDegrees 0.5`, still deliberately not
a function of zoom (a locally-smoothed value cannot decide which conserved mass dies on every peer).
The eyepiece's reticle, its recharge ring, the petal, the window camera and every number in rounds
3–8 are byte-for-byte unchanged; `ReticlePixels` called with the eyepiece's old arguments and a
6 px floor is the old `ReticleRadiusPixels` exactly. The flight reticle also deliberately ignores
`minPathRadius` (the 6-unit tube the cone floors at inside ~687 u), for the same reason the
eyepiece's always has: both rings state the CONE, which is the part of the shot that is a constant
angular size, and a ring that grew as a target came closer would stop being comparable between the
two views.

### One new diagnostic rung

`SniperScopeDiagnostics.Reason.NoGameCamera` — no perspective `Camera.main` to project through.
It stands the flight reticle down and leaves the eyepiece alone, which is precisely why it is its
own reason rather than a refusal of the whole instrument: the eyepiece carries its own camera and
is unaffected, so reporting it as "the scope window is not drawn" would send the next reader to the
wrong half.

## Round 9a — the playtest, and the two things it reported at once

> *"I got a lot of this error, but i could not see either reticle — `Property
> (_PrismSightPeerApex) exceeds previous array size (8 vs 4). Cap to previous size. Restart Unity
> to recreate the arrays.`"*
>
> *"this is filling the consul with this error. And still no retical in site. please check that
> you are putting it in the center of the charge petal shaped zoom window."*

Two reports in one sentence, and they are **independent**. Separating them was the whole of this
round; neither defect is where the report points, and one of them is not in this feature at all.

### The array error was a SUPERSESSION artefact, and the fix is a RENAME — not a restart

`_PrismSightPeerApex` is one of the five peer-bank globals the LIT system publishes. Nothing in
the scope touches it. It is written from exactly one place — `PrismLit.Flush`, at a fixed length of
`PrismLit.Slots` = **8** — and the shader declares 8, with `PrismLitTests` holding the two in step.

The 4 came from the system `PrismLit` **replaced**. Commit `8618ea98` ("promote LIT to a
fundamental") deleted `Assets/_Scripts/Utility/PrismDestructionSight.cs`, whose `PeerSlots` was
**4**, and wrote `PrismLit` in its place with 8 — **inheriting the retired system's property
names**. Unity binds a shader GLOBAL array at the length of its **first** write, keyed on the
property NAME, and keeps that length **for the whole editor session**; so an editor that had ever
run the old code had those names pinned at 4, every frame after the script reload logged the
error, and peers 5–8 of the LIT system were silently dropped.

Round 9a's first answer was *restart Unity*, which is what the message itself says. **It came back
with the error still on screen, and that is the finding**: a restart only helps the machine that
performs it, only until the next supersession, and only if whoever hits the wall knows to. The fix
that does not depend on any of that is the **rename** — the bank is now
`_PrismLitPeerApex/Axis/Gape/Tint/Shape/Count`, sized by `PRISM_LIT_PEER_SLOTS` and tuned by
`PRISM_LIT_PEER_DESATURATION`/`_GAIN`. **A name Unity has never been asked to bind cannot carry a
pinned length**, so the first write in any session, fresh or reloaded, is the one that sets it. It
also stops the bank being named for a system that no longer owns it.

Proven by `Tools/Shaders/verify_prism_sight_composition.py`, which compiles and RUNS the shipped
HLSL: all five composition properties hold under the new names, byte for byte.

**General rule (now in `Docs/LIT.md` and `CLAUDE.md`): superseding or resizing anything that
publishes a shader global ARRAY is a one-time, session-scoped, loud-and-lossy event that no
offline gate can see — and the answer is to rename the globals in the same commit, not to tell
everyone to restart.**

Its practical cost here was the second report: the spam buried the `[SerpentScope]` lines that are
the one documented way to tell why a reticle is missing.

### "still no reticle in sight" — the reticle is CORRECTLY PLACED and a few pixels across

The question asked — *is it in the centre of the petal window?* — has a clean answer, and it is
yes, by construction:

- `ScopePetalGeometry.Centre` is **(0.5, 0.5)**, the petal outline's own bounding-box centre, which
  is the picture's optical centre because the outline is normalised over that box and its
  coordinates double as the render target's UVs. That is where the camera's axis lands.
- `MakeRing` / `MakeCross` anchor every reticle piece at **(0.5, 0.5) of the parent rect** with a
  centred pivot, zero `anchoredPosition` and zero `sizeDelta`. A UGUI anchor is a fraction of the
  parent's RECT and is independent of the parent's pivot, so the eyepiece's own top-left pivot does
  not move them.
- `MakePetal` insets the RECT symmetrically on all four sides, so the picture shrinks about that
  same centre.

So the three things that have to agree — the petal's optical centre, the rect's centre, and the
reticle's anchor — are one point. **The placement was never the defect.**

The defect is the SIZE, and it is arithmetic rather than a guess. `SniperShotAction.asset` authors
`coneHalfAngleDegrees: 0.5`. A reticle drawn at the shot's true angular size is therefore
`H · tan(0.5°) / tan(fov/2)`:

| view | half-height | field of view | ring radius |
|---|---|---|---|
| eyepiece, wide end | 270 px (side 540 at 1080p) | 60° | 4.1 px → floored to **6** |
| eyepiece, zoomed | 270 px | 22° | **12.1 px** |
| eyepiece, Deep Focus | 270 px | 11° | **24.5 px** |
| flight view | 540 px | 60° | **8.2 px** |

That is a **2 px hairline ring 6–24 px across, inside a 540 px window**, drawn over a magnified
render of a lit arena — about 1% of the window. It is the correct measurement and it is under the
threshold of being noticed, which is exactly what came back twice.

**The fix is not to draw it bigger than it is.** `ScopeCrosshairGraphic` adds four fixed-size
**posts** around each ring: **the posts LOCATE and the ring MEASURES.** Their arms are a fixed
16 px so the mark is always the same findable size whatever the weapon's angle or the zoom; their
inner ends sit 6 px outside the ring, so they point at it and the whole reticle visibly opens up as
the pilot zooms in and the ring grows. The mark is therefore **~44–92 px across** where the ring
alone was 12–48, and **the ring itself is untouched** — every number the instrument states is the
number it stated before.

Three details that are not decoration:

- The eyepiece's ring is now capped so the POST's outer end fits the petal's inradius budget, not
  the ring's edge. In practice it never binds (24 px against ~139), which is the point: it is a
  guarantee that the mark stays inside the shaped window at any weapon angle, not a tuning.
- The recharge arc's radius clears the posts as well as the ring, so a wide-open reticle cannot
  collide with the readout around it.
- The posts' ENDS are hard while their long sides carry the usual zero-alpha feather. A faded inner
  end would blur the gap the pilot looks at the target through, which is the one part of a reticle
  that has to be crisp.

**General rule: a mark drawn at a true physical size is a measurement, and a measurement can be
correct and unreadable at the same time. Add a locator at a fixed size; never inflate the number.**

The diagnostic line was widened to say both, so the next report cannot conflate them again — it now
prints the ring radius AND the mark's span for each view:

| What the line says | What it means |
|---|---|
| no `[SerpentScope]` line at all, no warning | the scope never engaged; the executor is not ticking |
| an unconditional `[SerpentScope]` warning | a named gate refused — the warning says which |
| `ring 6 px` and it does not grow with the trigger | the zoom is not reaching `ReticlePixels` |
| `Flight reticle STOOD DOWN` | no gameplay camera, or the aim point projected behind it |
| both rings and marks printed and plausible | they are drawing and something is **over** them — `FrogletTools > Diagnostics > Report On-Screen UI`, in play mode |

That last row is deliberately not a warning, for the reason this file already records: an
unconditional "everything checks out" becomes a permanent false positive the day the real defect is
fixed. **The whole procedure is one switch** — `CSLogChannel.SerpentScope` in FrogletTools >
Toolbox > Logging.

## Round 9b — "I still see no reticle at all. increase the radius of the sniper shot to 10"

Round 9a answered a size complaint with a **locator** (four fixed 16 px posts, the ring untouched)
on the reasoning that the fix for a mark too small to see is never to draw it bigger than it is.
The posts shipped and the report came back **identical**: *"I still see no reticle at all."*

**That is the fourth consecutive round in which a correct change bought no part of the
complaint**, and this file already records what that means (round 7): *when successive correct
fixes keep buying diminishing amounts of the same complaint, the defect is one layer below the one
being fixed.* So this round does two separate things, and they are separate on purpose.

### 1. The weapon's angle, by instruction

`SniperShotAction.asset` — `coneHalfAngleDegrees: 0.5` → **`10`**. This is an authored gameplay
change, not a UI one, and it is stated plainly rather than folded into the reticle work:

- **The hitscan cone is twenty times wider.** `PrismSpatialIndex.QueryCone` now sweeps a cone of
  radius **529 u at 3,000 u** where it swept 26.2 u, and the `minPathRadius` floor stops mattering
  past **34 u** rather than past 688 u. Everything about pierce, super-shield teardown and debris
  is unchanged; what changed is how much mass one trigger pull can reach.
- **The eyepiece ring is 82 px at the wide end** (`270 · tan10 / tan30`), against 6 px before —
  13.7× — and the flight view's is **95 px at the game's default 90° FOV**, against 4.7.

### 2. What the cap now does, which is the honest cost

`ReticleBudgetPixels` caps anything drawn at the eyepiece's optical centre to the petal's measured
**inradius** (a shaped window cannot bound its own furniture by its bounding box), and round 9a
took the posts' outer ends out of that budget as well. At 0.5° that cap **never bound**. At 10° it
binds over most of the dial:

| scope FOV | ring would be | ring is drawn at |
|---|---|---|
| 60° (wide end) | 82.5 px | **82.5** |
| 50° | 102.1 px | **102.1** |
| ~44° | 117.2 px | **117.2** — the ceiling |
| 22° (full zoom) | 244.9 px | **117.3**, pinned |
| 8° (Deep Focus) | 680.8 px | **117.3**, pinned |

So the original request's *"it should grow as the player zooms in"* now holds across roughly the
**first third** of the trigger's travel and not past it. That pin is not a bug — a 10° cone
genuinely subtends more than a 540 px window can show once magnified, and a ring that left the
petal would be a worse lie than one that stops growing. **If it reads badly, the dial to move is
the weapon's angle; never the cap, which is what keeps the mark inside the shape.**

### 3. The one gap in `SelfCheck`, closed

Every check `SelfCheck` ran was a **construction** fact: canvas enabled, `Graphic.IsActive()`,
eyepiece size, backing alpha, panel on screen. **Every one of them is satisfied by a window that
draws perfectly with nothing in it** — which is precisely the report. The eyepiece is a `RawImage`
subclass and the rings are bare `MaskableGraphic`s, so there is a whole class of failure that takes
the marks out and leaves the picture untouched, and none of it throws:

- the graphic is **disabled**,
- its `CanvasRenderer` is **culled** (a mask whose rect does not intersect it),
- its colour is **transparent**.

`CheckMarkVisible` now tests all three on the reticle ring, its posts and the recharge arc, and
reports the first fault **unconditionally by name**. A fourth case — a colour that is black against
a black backing — is deliberately *not* guessed at here: `ResolveTracerColour` falls back to
**white**, not to a palette field that can author black (`Docs/PALETTE.md` §2.5), which is what
keeps it off the list.

**What this round does NOT claim.** It does not explain why the marks were invisible. It raises the
ring from 6 px to 82 and closes the one blind spot in the check that was supposed to catch this, so
the next report can only be one of two things — *still nothing*, which now means the rings are not
drawing at all and size was never the cause, or *there it is*. Either answer is progress; a fifth
round of reading source would not be. `CSLogChannel.SerpentScope` in FrogletTools > Toolbox >
Logging, and `FrogletTools > Diagnostics > Report On-Screen UI` in play mode, are still the
separator — and note round 9a's console was being **flooded** by an unrelated per-frame
`_PrismSightPeer*` array error, which is exactly the shape of thing that buries a once-per-session
diagnostic line. That spam is fixed (see `Docs/LIT.md`), so the scope's own report is readable now
in a way it was not when rounds 9 and 9a were played.

## Round 10 — "still no reticle in the zoom window ... just get something to appear in the pip"

The playtest after round 9b, and it reported three things at once:

> still no reticle in the zoom window. the effect cone widened like a cone (too big not desired),
> but the destruction was small (too small not desired). it should pierce through everything. do a
> full audit of what is going on. the zoom is working great, but i havn't seen you draw anything in
> the center of the pip. start there. just get something to appear in the pip

### What round 9b actually proved

It was a size experiment and it came back NEGATIVE, which is worth as much as a fix. The eyepiece
ring went from **6–24 px** to **82–117 px** — a factor of roughly ten, across the whole dial, with
four fixed-size posts around it — and the report is identical to the three before it. **Size was
never the cause.** Nothing that makes the mark bigger can make it appear, and the two remaining
explanations are that the generated geometry never reaches the screen or that nothing parented
under the eyepiece does.

Neither is decidable from source, and rounds 5 and 6 of this instrument were both spent trying:

- `ScopeRingGraphic.OnPopulateMesh` was read in full again and is correct. Its radius is measured
  from `GetPixelAdjustedRect().center`, which is `(0, 0)` for a centred pivot at any rect size;
  `Graphic.DoMeshGeneration` only guards `width >= 0 && height >= 0`, which zero passes; and every
  `Radius`/`Thickness`/`Sweep01` setter calls `SetVerticesDirty`.
- `ScopePipView` was grepped for re-parenting or sibling reordering that could put the picture over
  the marks. It writes `_surface.texture`, `_surface.enabled` and `_camera.enabled` and touches no
  transform in the canvas.
- The colour was ruled out: `SO_ColorSet.GetDomainSignalColor` normalises by the brightest channel
  and returns **white** when the palette authors black, `ResolveTracerColour` falls back to white
  when there is no ColorSet at all, and `WithAlpha` then forces the alpha explicitly. It cannot be
  transparent and it cannot be black.
- `Tick` is running: `_pip.Tick` is the last thing in it, the zoom works, and every ring write is
  above that line — a null ring would throw before the picture ever updated.

### What shipped instead: a PROBE

`SniperScopeOverlay` now draws a fifth mark inside the eyepiece, out of five plain
`UnityEngine.UI.Image` quads — a centre pip and four posts — laid out around the **same measured
radius** the real reticle was given that frame, at full alpha, drawn last.

A sprite-less `Image` falls through to `Graphic.OnPopulateMesh`, which emits one quad filling its
rect. It shares **no geometry code** with `ScopeRingGraphic` or `ScopeCrosshairGraphic`, and shares
only the canvas, the parent and the draw order with them. So one playtest now splits the two
remaining explanations:

| What the next playtest shows | What it means |
|---|---|
| The probe appears, the ring still does not | The fault is inside the generated `MaskableGraphic` path |
| Neither appears while the picture does | The fault is in what is parented under the eyepiece |
| Both appear | It was the zero-sized rect (below), and the instrument is fixed |

This is the same discipline round 7 used on the window itself: **when a component renders and
another does not, reach for the one that provably renders rather than reasoning about the one that
does not.**

### And the one measurable difference, removed

Every generated graphic in this overlay was built with `rect.sizeDelta = Vector2.zero`. The
eyepiece's two petals — the components that provably render — are built with a **full-size** rect.
That was the only structural difference between them, so it is gone: `MakeRing` and `MakeCross` now
give their rects `GraphicRectPixels` (512) on both axes.

**Nothing about the geometry moves.** A centred pivot on centred anchors keeps `rect.center` at
`(0, 0)` whatever the size, every radius these components draw is measured from that centre in
absolute units, and there is no mask anywhere in this canvas, so a rect is not a clip. It is
removed because it was the last difference worth removing, not because it was diagnosed.

`SelfCheck` now checks the probe alongside the three marks it already checked.

### The weapon half — "the destruction was small ... it should pierce through everything"

Two separate defects, both real, both found by reading `SniperShotActionExecutor.ResolveShot`.

**The round destroyed ONE prism.** The budget was

```csharp
bool pierces = IsPierceUnlocked;                            // Charge 5
int budget = pierces ? (so.PierceCount <= 0 ? int.MaxValue  // authored 3
                                            : so.PierceCount)
                     : 1;                                   // <- every un-upgraded shot
```

so an un-upgraded rifle on a **twelve-second** cooldown was worth exactly one prism, and an
upgraded one three. That is not a sniper round, and it is what the pilot measured. **The round now
pierces by default**: the budget is the authored number at every tier, and the asset authors **0**,
which is unlimited — everything the cone contains.

**Charge 5 "Pierce" was then an upgrade whose whole content had become the base behaviour**, which
is a dead upgrade rather than a generous one. It is re-cut onto the thing the ability already does
and had never gated: **ARMOUR**. Below Charge 5 a super-shielded prism is not a target at all, so
the round flies *through* it and kills whatever is behind; at Charge 5 the sanctioned teardown in
`DestroyPrism` runs and the Serpent is the fleet's second force that can take a super-shield off.

The gate lives in `IsValidTarget` rather than in the loop, and that placement is the whole of why
it works: `Prism.Damage` **hard-ignores** super-shielded mass, so an un-upgraded round that treated
armour as a target would stop on it, set the tracer's end point there, and destroy nothing —
visibly dying against a wall it was supposed to pass. Excluding it from the target set instead
means the round never notices it. **"Pierce" now names a CAPABILITY rather than a count: how many
prisms the round goes through is the weapon's, and what it can go through is the element's.**

**The cone came back down.** `coneHalfAngleDegrees` **10° → 1.5°**. Ten degrees was round 9b's size
experiment and it swept a **529 u** radius at 3,000 u — a shotgun, which is what the pilot saw and
did not want. Half a degree (26 u) is a needle. One and a half is a rifle: **78.5 u** at full
range, and the tracer is drawn at that same radius because the beam IS the volume tested. It also
keeps the eyepiece ring inside the petal for most of the dial — 24.5 px at fov 60, 72.7 px at fov
22, pinning at the 117 px cap only near the very bottom of the zoom.

The general rule round 9b left behind is unchanged and was followed here: **the dial to move is the
weapon's angle, never the cap.**

### The audit's clean bill

Everything else in the shot was re-read and is correct as it stands: the `QueryCone` snapshot is
re-sorted along the axis before anything dies and each prism is re-tested after the sort (a kill
can destroy others through its own side effects); the stop point is read *before* the kill, because
a destroyed prism's transform is on its way back to the pool; `IsValidTarget` still never eats the
pilot's own wall; `devastate: true` on the ordinary path stops an armoured prism costing two
twelve-second cooldowns; and the debris carries a true-velocity vector with its matching ceiling
rather than saturating the explosion prefab's legacy 33.33 u/s clamp.

One latent trap found and left alone, because it is not live: `coneHalfAngleDegrees` carries
`[Range(0.05f, 5f)]`, and a `RangeAttribute` is a **property drawer** — it clamps in the inspector
and not at deserialization. The authored 10 was therefore live the whole time, and would have
snapped to 5 the first time anyone touched that asset in the inspector. At 1.5 it is inside the
range again, so nothing is at risk now; but *a serialized value outside its own `Range` is a value
that changes the next time a human looks at it.*

## Round 11 — the probe answered, and the answer is ambiguous on purpose

> *"this is good i saw the recital and it changes size nicely."*

**The mark is on screen and it tracks the zoom.** That is the promise the whole instrument was
built to make and it is the first round in which a pilot has reported it kept. Nothing in the code
changed for this round; it is recorded because the round-10 table asked a question and the answer
is worth writing down along with what it does **not** settle.

### What it settled

Both weapon-half defects are closed by the same report. The cone reads as a rifle rather than a
shotgun at **1.5°**, and the round is not being described as destroying one prism. And the mark
inside the eyepiece is legible at a size the pilot could watch change — so the *posts LOCATE, ring
MEASURES* split (round 9a) plus the 1.5° cone together clear the readability floor that four
rounds of correct measurement did not.

### What it did not settle, and the open item

**Round 10 shipped TWO changes that could each have made the mark appear**, and one report cannot
separate them: the PROBE (five plain `Image` quads) and the **512 px rect** the generated rings
and posts were given in the same commit. The pilot's words name neither component.

So the probe is still in the instrument, and it is still scaffolding:

| If | then |
|---|---|
| The rings draw now (the rect was the fault) | the probe is a duplicated mark drawn at full alpha over a correct one, and should come out |
| Only the probe draws | `ScopeRingGraphic` / `ScopeCrosshairGraphic` never reach the screen here, and the probe is the reticle — which means the two generated classes should be retired instead |

**It was not removed on this pass and that is the deliberate call**, because removing it is exactly
the move that risks regressing the one thing the pilot has just approved: if it was the probe they
saw, the reticle vanishes again and round 12 is round 6. The experiment that settles it costs one
observation and no code — **switch the probe off and look**:

```csharp
// SniperScopeOverlay.Tick, in place of the DrawProbeReticle(radius, colour) call
if (_probe != null) foreach (var p in _probe) p.enabled = false;
```

Reticle still there → delete `BuildProbeReticle` / `DrawProbeReticle` / `_probe` / `_probeRect` /
`ProbePipPixels` and the `SelfCheck` line that reads them. Reticle gone → keep the probe, and the
next pass is about why a bare `MaskableGraphic` submits nothing under this canvas while a
`RawImage` subclass parented beside it submits fine.

Its cost while it stays, stated rather than hidden: the eyepiece draws its reticle **twice** — once
as a ring with locator posts, once as a pip with square posts at the same radius — and the probe's
copy is at **full alpha at every moment**, so it does not dim while the weapon recharges. The
instrument therefore reads as slightly busier than it is designed to, and the recharge dim is half
as loud as it should be.

The general rule this round leaves behind: **when two fixes ship in one commit and either could be
the one that worked, the report cannot tell them apart — so budget a second observation, and keep
the cheaper-to-undo one switchable.**

## Drive-by corrections

- **The doc's own opening paragraph still described the round-1 COCKPIT** — *"the view drops into
  the cockpit and magnifies"* — which round 4 deleted outright (`VesselFirstPersonView` is gone and
  `CustomCameraController` no longer carries a first-person flag). Eight rounds of record sat under
  a summary contradicting all of them, which is the shape of drift that sends a reader looking for
  a system that does not exist. Corrected in round 9 to describe the shipped eyepiece.
- **`Serpent.asset`'s Time entry had `Input: 0`** (`FullSpeedStraightAction`) while
  `ConsumeBoostAction` actually rides `Button1Action`. That field is not documentation: the ability
  lockup DRAWS each card's control chip from it (`map entry → InputHintBindingMap.BindingFor`), so
  the Serpent's Time card would have drawn the wrong glyph. Corrected to `6`; the ability table now
  reports `A / Space`.
- **`SniperShotActionExecutor` resolves its registry with `GetComponentInParent`, not
  `GetComponent`.** On every shipped vessel the registry lives on the `ShipActions` container and
  each executor sits on a **child** of it, so a same-object lookup returns null and every fallback
  below it is silently dead. `ToggleTranslationModeActionExecutor` still uses the same-object form
  and is deliberately left alone — **measured, it is a latent trap rather than a live defect**, and
  the measurement is the reason it is left rather than an excuse: that registry feeds exactly one
  field (`seedAssemblerExecutor`, resolved only when the prefab leaves it empty), the **Serpent**
  authors it outright (`fileID: 8230992318499782635`) so the fallback never runs, and the
  **Sparrow** — the only other vessel carrying the executor — has no `SeedAssemblerActionExecutor`
  and no `stationarySeedConfig` at all, so the null field matches reality. It bites the day a third
  vessel takes the ability, or the day somebody clears the Serpent's reference expecting the
  fallback to cover them. Worth noting alongside it: neither `ToggleStationaryModeAction.asset`
  serializes `stationaryMode`, so **both** fall back to the C# initializer `Mode.Serpent` — the
  Sparrow's stationary mode runs the Serpent branch and is carried only by that branch's own null
  guards (rule 4-i, met in the wild).

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
  target, no shadows, no AA, 30 Hz and a lifetime of exactly as long as the trigger is held;
  `ScopePipView.RenderSize` / `RefreshHz` are the dials if a phone disagrees. **Post-processing is
  NOT one of the dials** — round 5 established that the world is unreadable without the tonemapper
  it is authored against, so switching it off does not make the window cheaper, it makes it absent.
  **Unprofiled**, and it is now more expensive than round 3 on all three axes (2.0× the pixels of
  that 360p 16:9 target, 1.5× the refresh, and a post stack) because it is the picture the pilot
  aims with rather than a glance.
- **The scope overlay rebuilds eight small UI meshes per frame while held** (≈96 segments each,
  guarded by `Mathf.Approximately` so an unchanged value costs nothing). Cheap, and unmeasured.
- **The shot has no FMOD event.** `fireEvent` ships **empty**, which is silence — never a borrowed
  event (CLAUDE.md's audio convention). It is an inspector-visible TODO on the
  `SniperShotActionExecutor` component.
- **No AI uses either ability.** `AIPilot` presses nothing here, and `OnButtonPressed` early-returns
  under autopilot, so an AI Serpent flies exactly as it does today.
- **Nothing has been run in the editor.** See below.

## In-editor verification

Everything below is unverified — the branch was authored headlessly. The C# was type-checked
against a Roslyn stub harness whose stub signatures were each grepped out of the real sources (and
negative-controlled), and the six out-of-editor gates pass, but neither can see a camera. Round 9
re-ran that harness over the whole scope chain — `SniperScopeOverlay`, `SniperScopeDiagnostics`,
`SniperScopeActionExecutor`, `ScopeRingGraphic`, `ScopePetalImage`, `ScopePetalGeometry` and
`ScopePipView` — clean, with two negative controls (a wrong `Tick` arity and an undeclared
constant) both firing.

1. **Scene:** any Serpent-capable scene (Menu_Main freestyle is enough; swap to the Serpent with
   the vessel-changer toy). Confirm no console errors on spawn.
2. **Scope:** hold **LT** (or **Left Shift** on keyboard, **LMB** on the one-thumb mouse scheme).
   Expect: a **petal-shaped** window in the top-left (step 30 is where its shape is checked in
   detail) showing a view down your own nose, and **the flight
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
8. **Pierce:** fire down a line of prisms at ANY Charge level — expect every prism in the cone
   destroyed, not one. Then point at super-shielded mass below Charge 5: the round must fly
   PAST it and kill whatever is behind. At Charge 5 the same shot takes the armour off and kills
   it (step 7).
9. **The contextual trigger:** unscoped, press RT — the **cloak** should engage exactly as it does
   today. Scoped, press RT — the cloak must **not** engage.
10. **Teardown:** while scoped, open the overview / pause (or let a turn end). The window must
    close — this exercises `R_VesselActionHandler.ReleaseHeldInputs` and the executor's
    `OnDisable`. The flight camera and the player's FOV were never touched, so there is nothing
    else to check.
11. **Vessel swap while scoped:** scope, then swap hulls with the vessel-changer toy. The window
    must close and leave no `[SerpentScopeCamera]` or `SerpentScopeRT` behind.
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
16. **The window:** confirm it sits in the **top left** with a thin dark frame, and that the
    picture inside it is not squashed or stretched — round mass must read round. (This step has
    outlived three shapes: round 4 made it a circle, round 7 restored the 16:9 panel, round 8
    re-cut it as the SQUARE Charge petal. The shape itself is step 30's job; what this step
    checks is the aspect of the PICTURE, which must hold whatever the frame is.) Release and it
    must disappear. Swap hulls while scoped and confirm no stray camera or render texture is
    left behind (check the hierarchy for `[SerpentScopeCamera]`).
17. **The ability card (round 4).** Fire, then look at the bottom-right ability row: the **Charge**
    card is LOCKED (a dashed mark, no icon) and its **cooldown veil must now sweep clockwise over
    it** for ~12 s. Before round 4 it drew nothing. Check it works with the scope DOWN as well —
    the row is the readout a pilot who is not scoped has.

18. **The picture is LIT (round 5).** This is the round-5 assertion. Scope while pointing at
    ordinary arena mass and compare the window's picture with the same mass in the flight view
    beside it: colours, bloom and brightness must **match**. A flat, grey, bloom-free picture means
    the camera is not inside the gameplay volume — check `OffscreenCameraSetup.AdoptGameCameraImage`
    ran and that `Camera.main` existed when the window was built (it warns once if it did not).
19. **The panel is visible with NOTHING in it (round 5).** Point at empty space and scope. You must
    still plainly see a dark panel with its frame — that is the backing, and it is what makes
    "pointed at nothing" distinguishable from "no window". If the panel is only findable when
    something is in front of it, the backing is not drawing.
20. **A hull with its art off (round 5, optional).** If a hull can be caught with its renderers
    disabled, confirm the console warns *once* by name about a zero hull radius and the window still
    shows the world rather than the inside of the ship.
21. **The three sibling windows still draw correctly (round 5 regression).** `OffscreenCameraSetup`
    is now shared, so check each: the **connecting panel's** arena preview during a load, the
    **arcade card's** looking-phase preview, and a **Toy Box** card's toy picture. Each must look
    exactly as it did before — same brightness, same bloom, same anti-aliasing. These are the sites
    the helper was extracted FROM, so a regression here is a regression in the extraction.
22. **START HERE (round 6): read the console before looking at the screen.** Fly a Serpent, hold
    LT for a second, and read the console. A **warning** starting `[SerpentScope] The scope window
    is NOT being drawn:` names the gate or the unusable state outright — fix that and stop. No
    warning means every gate and every self-check passed.
23. **Then enable the channel (round 6).** FrogletTools ▸ Toolbox ▸ Logging, tick **`[SerpentScope]`
    scope eyepiece placement**, hold LT again. Exactly one line should appear giving the eyepiece's
    centre, radius and the screen size — e.g. `centre (301.0, 575.0) radius 270 px on a 1920x1080
    screen`. That line means the instrument *is* being submitted at those coordinates.
24. **If step 23 printed and you still see nothing, it is occlusion (round 6).** Run **FrogletTools ▸
    Diagnostics ▸ Report On-Screen UI IN PLAY MODE** while holding LT. It names every enabled
    `Graphic` covering ≥2% of the display, biggest first, with its path, its effective alpha and
    which `CanvasGroup` set it — plus any camera or VideoPlayer drawing over the game without
    appearing in a UI hierarchy. Whatever it names at the top-left is the answer. (The Serpent
    carries a `Pip`, whose `border` RawImage draws a 780×400 navy quad when its texture is missing;
    `PipUI.SilenceUntexturedGraphics` is supposed to have silenced it, and this is how you confirm.)
25. **If NEITHER step 22 nor step 23 printed anything (round 6), `Update` is not running.** The
    scope never engaged: check that LT is reaching `SniperScopeActionSO.StartAction` at all, and
    that the executor's own GameObject is active. Every path inside `DrawOverlay` now speaks, so
    silence in both places can only mean it was never called.
26. **The reticle tracks the zoom again (round 6 regression).** Hold LT and roll the trigger from
    just-past-the-deadzone to fully down. The inner ring must **shrink continuously**, and the
    recharge arc must fill smoothly after a shot. Both were frozen at their first-frame values by
    the `Canvas.enabled` hazard, which looks like a working instrument until you watch it move.
27. **START HERE (rounds 7–8): the eyepiece is on screen at all.** Fly a Serpent, hold LT. A dark
    shape roughly half the screen tall must appear in the top left, under the goal stack, whether or
    not anything is in front of the ship. If it is absent, the fault is NOT the surface (a `RawImage`
    shipped and was seen for three rounds, and round 8 only changed its vertex list) and steps 22–25
    are the order to work in. If a dark RECTANGLE appears, the petal emit is degenerate and the shape
    fell back to its bounding box — check `ScopePetalGeometry.Outline`.
28. **The picture is the MAGNIFIED forward view (round 7).** Roll the trigger down: the panel must
    zoom, and it must show what is **ahead of the nose**, not the ship from behind. Seeing your own
    hull from behind means `ScopePipView.PoseCamera` regressed to round 3's chase pose. Your flight
    view must not zoom at all.
29. **The reticle and recharge ring are INSIDE the eyepiece (round 7).** Nothing at screen centre;
    the reticle ring, its dot, and the recharge bed and arc all concentric inside the picture. Roll
    the trigger and the reticle must shrink; fire and the arc must fill. If any of them is at screen
    centre, `MakeRing`'s parent regressed.
30. **The shape is the CHARGE petal (round 8).** It must be a pentagon: a wide blunt top, two
    shoulders at its widest point, and a **point at the BOTTOM** — the same petal the Charge flower
    in the element bars is built from, upright and unrotated. Compare it against the Charge flower
    on any vessel's HUD row: one petal of that flower, at this size. Its edges must be straight and
    its sides symmetric.
31. **Nothing is squashed, and the furniture stays inside the shape (round 8).** Point at round
    arena mass and confirm it reads round, not oval — the render target and the rect are both square
    now, and a stretched picture means one of them regressed. Then roll the trigger to its widest and
    fire: neither the reticle nor the recharge ring may cross the petal's sloped lower edges at any
    point in the sweep. If either does, `ReticleBudgetPixels` is no longer reading
    `ScopePetalGeometry.Inradius01`.

32. **START HERE (round 9): there are TWO reticles and one of them is small.** Hold LT. Expect the
    eyepiece's reticle as before, **and** a second small ring with a centre pip out over the flight
    view, in your domain colour. At 1080p / 90° FOV it is about **9 px across** — deliberately
    small, so look for it rather than expecting it to announce itself. Release: **both** must
    vanish. If the flight one never appears, read the console first (step 22's rule) — a
    `[SerpentScope] … NoGameCamera` warning names the cause in one line.
33. **The flight reticle is where the shot GOES (round 9).** Fly straight and level: it should sit
    on or very near screen centre, because the Serpent's camera is authored exactly behind the hull
    on the shot's own axis. Now haul the stick over and hold the turn — as the camera's smoothing
    lags, the reticle must **slide off centre and track the nose**, not stay pinned to the middle of
    the screen. That drift is the feature: a centred circle would be marking the camera rather than
    the round. Fire mid-turn and confirm what dies is what the ring was on, not what screen centre
    was on.
34. **Only the eyepiece's grows (round 9).** Hold LT and roll the trigger from released to full.
    The eyepiece's ring must grow visibly (≈8×); the flight view's must **not move at all**. If
    both grow, the flight one is being handed the scope's field of view instead of the camera's.
    Then let go of the trigger and accelerate to top speed: the flight reticle **should** grow a
    little as the speed tunnel narrows the gameplay camera — that is correct and is the same
    measurement following its own optics — while the eyepiece's must not.
35. **Both are stood down when the scope is (round 9).** Fly unscoped around the arena: no ring
    anywhere on screen. Then open the overview / pause while scoped, and swap hulls while scoped —
    the flight reticle must go with the eyepiece in both cases, not linger.
36. **The rear view (round 9).** While scoped, flip to the look-back camera (**C**, or **LB+RB**).
    The flight reticle must **disappear** rather than appear mirrored somewhere plausible — the aim
    point is behind the camera there. The eyepiece must keep working throughout.
37. **MPPM (round 9).** Scope on client A; client B must get **neither** reticle. This is step 12's
    check widened to the new one.

## Follow-ups

- **Settle the PROBE and take it out (round 11).** One observation with the five `Image`
  quads disabled says whether the generated rings draw. Until then the eyepiece draws its
  reticle twice and the probe's copy never dims. Steps + both outcomes: round 11 above.
- Wire the four ability icons (art + `Wire Vessel Ability Row`). The Charge veil no longer needs
  one, but the row still cannot say WHICH ability is recharging.
- Profile the scope window on a phone. It is a second render of the world at 512² / 30 Hz and it is
  the one cost in this branch nobody has measured.
- Consider whether the window should be placeable (top-left is a guess that clears the goal stack),
  and whether a left-handed or small-screen layout wants it elsewhere.
- Author the FMOD event for the shot — this is the one half of "I didn't notice the shot" this
  branch does not close.
- ~~Consider whether the reticle should be shown UNSCOPED too~~ — **settled in round 9, and the
  answer stands**: the reticle is drawn over the flight view now, but still only while the scope is
  up. The reasoning is unchanged — the shot cannot fire unscoped, so a permanent reticle would be a
  claim about a weapon that is not available — and it is what makes the mark mean *I am aiming*.
- The flight reticle states the CONE, so like the eyepiece's it ignores `minPathRadius` — the
  6-unit tube the shot floors at inside ~687 u, which subtends a LARGER angle than the cone at
  close range. Both rings therefore under-state the shot's reach against a target you are nearly on
  top of. Deliberate (a ring that grew as a target closed would stop being comparable between the
  two views, and a sniper's shot is not a close-range one), and worth revisiting only if a playtest
  reports missing point-blank.
- Fill the **Mass** slot — the last open Serpent design slot. `Docs/ElementalAbilitySystem/FLEET_MAPS.md`
  §2 still proposes *wall prism scale* / **Fortified Wall**, which does not collide with either
  ability added here.
- Consider whether any arena that super-shields structure for the Rhino's sword alone needs
  re-checking now that a second hull can open it.
