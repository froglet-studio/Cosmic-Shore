# Camera feel: a gameplay camera that is a little bit alive

Asked for by Yash on 2026-10-25: "I want the camera to be a little bit alive rather than just ... the vessel staying
fixed in the centre of the screen no matter what." Skims should wiggle it, hits should land with a good impact shake,
drifts and shots should be felt, different objects should carry different inertia, and all of it subtle: "not too
much, not too fancy". After flying the Squirrel (same day): hits were right, but drifts barely moved the hull; it
should slide further off the centre, build "in an accelerated way, not a linear move", and the pilot should feel the
G-force in their neck, more or less by vessel: a bigger, heavier hull's pilot feels less, an agile one's more.

Every hull in the fleet flies `CameraMode.FixedCamera` (measured 2026-10-25: all 17 `CameraSettingsSO` assets), so the
camera was hard-attached: the hull sat on one pixel whatever it did. Before this, five systems called
`CustomCameraController.Shake` (the Serpent's sniper, the Rhino's sword, the Thresher, the Astro League ball and
silence), and nothing else moved the camera.

## What it is

One layer, added by `CustomCameraController.ApplyFeel` on top of the settled pose (after the spectate blend, the
portal hand-over and the legacy shake) and taken off again by `UndoFeel` as the first line of the next
`UpdateCamera`. So no follow, zoom, rear view, warp-field or portal line ever sees it: the eased and dynamic rigs
smooth from the clean pose, not from a shaken one. `CameraFeelTests.TheFeelComesOffBeforeAnyPoseIsWorked` holds that.

| Part | What drives it | What you see |
|---|---|---|
| **G-force** | the framed point's ACCELERATION and a drift's SLIDE (never its speed), on one spring | a boost drops the camera back so the hull pulls ahead; a turn swings it outside so the hull leads into the turn; a drift slides the hull out across the frame; the same G tilts the view like the pilot's neck (thrown back by a boost, leaned outward in a turn or slide); a steady cruise frames exactly as before |
| **Kicks** | one damped spring every one-off event nudges | a skim pushes away from the prism, a hit shoves and tilts, a shot recoils, a crystal lifts |
| **Shake** | trauma 0..1, felt squared, drains linearly | hits and blasts |
| **Rumble** | a level 0..1, felt linearly | a fine wobble while skimming (it builds with each prism and fades) and while drifting |
| **Breathing** | always on | a 0.1 degree drift at 0.15 Hz, so the view never reads as frozen |

The code: `Controller/Camera/CameraFeel.cs` (the state and math, plain C#), `CameraFeelConfigSO.cs` (every number,
`Resources/CameraFeelConfig.asset`), `VesselCameraFeel.cs` (where gameplay reports events).

### Units

Every distance is a **fraction of the follow distance**, so the same number reads the same on screen on an Urchin at
6.7 u, a Squirrel at 17 u and a Serpent at 250 u. At a 60 degree field of view the screen's half-height at the hull is
about 0.58 of the follow distance, so a fraction f moves the hull about f / 0.58 of the half-height. Angles are
degrees. A kick's number is its **true peak**: the impulse that reaches it through the spring's damping is worked out
in `CameraFeel.Kick` (`PeakFactor`), so retuning the spring does not retune every event.

### G lags acceleration and slide, never speed

The camera is modelled as a mass on a spring riding the hull: the framed point's velocity is estimated from its motion
(25 ms smoothing) and its change drives the G spring as a pseudo-force. A constant acceleration settles at a constant
lag (`maxLag` x drive), a constant velocity at none. The acceleration is normalised by the follow distance
(`fullLagAcceleration` is in follow distances per second squared), so a boost reads alike on every hull.

**Why drifts needed their own input.** A drift locks the hull's course while its nose swings, so its real
acceleration is near zero: the lag alone never saw a drift, which is why the first version's drift (a 2% lean on a
first-order ease) read as barely there. The slide (the sine of the slip angle, as a share of `driftFullSlipDegrees`
35) is now a second rest point on the same spring: `driftSlide` 0.08 of the follow distance at a full drift, four
times the first version's offset. The camera is left behind toward the side the hull slides FROM, so the hull slides
out across the frame. It is scaled by speed (`driftFullSpeed`, 4 follow distances per second, through a smoothstep),
so a drift at a crawl stays gentle.

**Not a linear move.** The spring (`inertiaFrequency` 1.3 Hz, `inertiaDamping` 0.7) starts from still, gathers speed,
swings about 4% past and settles, the way a head is thrown and caught; the first version eased in along a decaying
exponential, which starts at full speed. Lag and slide together round off toward `maxGOffset` 0.14 (tanh), never
stopping at a wall.

**The pilot's neck.** The same G tilts the view: `gPitch` 1.5 degrees nose-up at the full forward G (a boost throws
the head back; a stop tips it forward), `gRoll` 3.5 degrees leaned outward at the full sideways G (a turn or a slide).
Read against the fleet's full G, so a hull that is pushed less also leans less.

The springs step in closed form (`CameraFeel.Spring`), exact at any frame rate: a kick peaks at the same height at
30 fps and 144 fps. A substepped Euler step at 240 Hz fell 8% short at 4.5 Hz (found by running the suite on the
engine's edit-mode runner, 2026-10-25).

## Where events come from

Every call reaches only the cameras following THAT vessel (`CustomCameraController.FeelsFor`, the follow target's own
`VesselStatus`), so a call site never asks whether the vessel is the local pilot's, an AI's or a remote player's. The
player's camera feels the player's ship and a spectator's camera feels the ship it watches.

| Event | Call site | Feel |
|---|---|---|
| A prism enters a skimmer | `SkimmerImpactor.AcceptImpactee`, after the skimmer-prism effects | `Skim`: nudge away from the prism, rumble; scaled by how close to the centre it passed |
| The hull hits a prism | `VesselImpactor.AcceptImpactee`, beside the impact SFX | `Impact`, weighed by the prism (below) |
| The hull passes through another vessel's skimmer | `VesselImpactor`, skimmer branch | `Bump` (weight `vesselBumpWeight`, 1) |
| The hull rams a creature's heart | `VesselImpactor`, embedded-crystal branch | `Bump` |
| The hull collects a crystal | `VesselImpactor`, omni and elemental branches, after the latch | `Pickup` |
| A blast catches the vessel | `ExplosionImpactor.AcceptImpactee`, under the one-blast-one-victim ledger | `Blast` |
| A round strikes the vessel | `ProjectileImpactor.AcceptImpactee`, after the own-domain rule | `Struck`, at most one per `struckMinInterval` (0.15 s) so a stream of rounds rattles without pinning the shake |
| The vessel fires | `Gun.FireGun` and `Gun.FireRingBlast` (one per trigger, not per spike) | `Shot`: recoil back and nose up |
| The vessel drifts | read each frame from `IVesselStatus.IsDrifting` / `Course` | the slide (G spring) and rumble |
| The vessel accelerates | read each frame from the framed point | the lag (G spring) |

### Different objects, different weight

A prism lands with `PrismWeight`: its size (the cube root of its volume against `referencePrismVolume` 60, held in
0.6..2, so a prism twice as long lands a little harder, not twice as hard) times its shield tier (shielded 1.25,
super-shielded 1.6). A Squirrel trail prism (20 x 0.75 x 5.5 = 82.5) lands at 1.11, a Sparrow's (2 x 2 x 5 = 20) at
0.69, environment mass at up to 2.

### Every hull feels it differently: size, agility, character

Three things set how much G a pilot feels, so no two hulls feel the same:

1. **How hard it maneuvers** is not a number anyone types: the G is measured from the hull's own motion, so a hull
   that turns, boosts and drifts harder feels more, and a gentle one less.
2. **Its size** (standing in for mass: there is no mass in the flight model). When the camera starts following a hull
   it measures it (`CustomCameraController.MeasureHullSize`): the mean of the length, width and height of the box
   around its model's meshes, in the hull's own orientation and at its world size, skimmer visuals, trails and
   particles left out. A bigger hull's pilot feels less G, and feels it build more slowly:
   G x (`referenceHullSize` 5 / size) ^ `sizeGExponent` 0.35, held in 0.55..1.4, and the spring's frequency x
   (5 / size) ^ `sizeResponseExponent` 0.25. The measured size is the private field `_feelHullSize` on the
   `CustomCameraController` (the Inspector's Debug view shows it, with `_feelGForce` and `_feelResponse`).
3. **Its character**, the `vessels` table: `gForce` multiplies what size gives (agile drifters above 1, lumbering hulls
   below), `weight` scales how hard its hits, shots and skims land. Unlisted hulls are 1 and 1.

| Hull | Table G | Weight | Why | Size, from its model dossier (estimate) |
|---|---|---|---|---|
| Squirrel | 1.15 | 0.9 | agile drifter, the skim hull | not in a dossier: read `_feelHullSize` |
| Dolphin | 1.1 | 0.9 | agile drifter | 5.3 x 1.2 x 5.3: mean 3.9, size G 1.09, so ~1.2 |
| Manta | 1.1 | 1 | agile | 4.6 x 1.2 x 2.3 drawn: mean 2.7, size G 1.24, so ~1.36 |
| Urchin, Sparrow | 1 | 0.9, 1 | | Sparrow ~12.5 x 3 x 12.2: mean ~9.2, size G 0.81 |
| Serpent, Butterfly | 0.9 | 1.1, 0.9 | long cameras, floaty | |
| Orca | 0.9 | 1.3 | the 4-seat hull | 49 x ~10 x 37: mean ~32, size G 0.55 (the floor), so ~0.5, and ~0.6x as quick |
| Rhino, Grizzly | 0.85 | 1.25 | lumbering, heavy hitters | |
| Starling | 0.4 | 0.5 | a swarm seen from 1200 u: it should float, not shake | |

## Rules it keeps

- **The player's setting scales all of it.** `AccessibilitySettings.CameraShakeIntensity` (0..1, "camera shake /
  motion intensity") multiplies the whole feel and the legacy `Shake`, read once and kept current by its change event
  (`VesselCameraFeel.AccessibilityScale`). It was a stored setting with no consumer until now. Default 1 leaves the
  legacy shakes exactly as tuned.
- **Off during a placement** (the Butterfly's fold frames a point across the cell; the pilot is aiming), faded out
  under a spectate vantage (the Thresher's), reset by a teleport, a snap and a new follow target, carried through a
  portal without reading the jump as motion (`CameraFeel.Shift`).
- **Nothing per-frame allocates.** Events loop a list of at most a few cameras; the model is value types.
- **The nose has no lag.** The rule in `Docs/claude/ARCHITECTURE_CORE.md` (a human pilot's nose is the commanded
  rotation each frame) is about the hull, and it holds: the feel moves the camera only, never the hull, and its
  rotation is a tilt of at most a few degrees around the settled view.
- **Haptics are separate.** `HapticController`'s two-feel policy is about the motors; this does not call it and it
  does not call this.

## Tuning

Select `Assets/Resources/CameraFeelConfig.asset` and change it in play mode: the controller reads the config and the
hull's row every frame, so a change shows at once (the hull's size is measured once per follow). More drift: raise
`driftSlide` (and `maxGOffset` with it, or the slide rounds off); a snappier or lazier G: `inertiaFrequency`; more or
less overshoot: `inertiaDamping` (1 = none); more neck: `gRoll` / `gPitch`; less difference between big and small
hulls: lower `sizeGExponent`. To turn a part off, set its amplitude to 0 (`breathAngle`,
`rumbleAngle`, `maxLag`, ...); to turn all of it off, untick `feelEnabled`. If the whole thing reads too strong or too
weak, change `strength` before any single number.

## Known gaps and findings

- **Not editor-verified.** See `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **The legacy `Shake` still leaks into the eased chase rigs** (the Stoat, Angler and Starling: `chaseEaseRate` > 0).
  It adds to `transform.position` and is never taken off, so the next frame's ease starts from the shaken pose and
  keeps ~89% of it per frame at rate 7: a coherent 10 Hz shake builds to several times its authored size there. Left
  as it was on purpose: the shakes on those hulls were tuned by eye with the leak in. If they read too big, route
  `ApplyShake` through the same undo as the feel.
- **No field-of-view kick on boost.** The G already pulls the hull ahead on a boost; a FOV kick would be another
  part and is not asked for yet.
- **Size is a model measurement, not mass.** A hull whose model carries a large mesh that is not hull (a shield bubble,
  a decoy) measures big. Read `_feelHullSize` once per hull in the Debug Inspector; if one is wrong, correct it with the
  table's `gForce`, or leave such meshes out of `MeasureHullSize`.
- **Crew picture-in-picture views** (`CrewViewRender`) are their own cameras and do not carry the feel.
- **The Vessel Studio's cameras do not model it.** This is a game-camera feature, not a studio one.

## Log

| Date | Change |
|---|---|
| 2026-10-25 | First version (Yash's ask): six parts, the event table, per-hull inertia and weight, the accessibility setting wired, closed-form springs. `Ys-bleeding-edge`. |
| 2026-10-25 | After Yash flew the Squirrel ("hits good, drift too little, not linear, feel the G in the neck, by size and agility"): inertia and drift lean became one **G-force** spring fed by acceleration AND slide (`driftSlide` 0.08, four times the old offset, at drift speed), damping 0.7 for an accelerating start and a small overshoot; neck tilt from the same G (`gPitch` 1.5, `gRoll` 3.5); hull size measured from the model (bigger: less G, slower); the per-hull table became character (`gForce`) on top of size. `driftRoll`, `driftYaw`, `driftOffset`, `driftEase` retired. |
| 2026-10-27 | Garrett: the slide's return was jerky and broke the Squirrel's flow. The slide's rest point is now eased before the G spring (`driftSlideIn` 3/s, `driftSlideOut` 1.2/s), so a drift ending glides the hull home (peak return speed about a quarter of before) and never swings past centre; the build keeps its accelerating start. Drift screen rumble `driftRumble` 0.5 -> 0.15. Test `ADriftEnding_GlidesHome_WithoutSnappingOrSwingingPast`. |
