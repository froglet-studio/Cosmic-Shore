# Camera feel: a gameplay camera that is a little bit alive

Asked for by Yash on 2026-10-25: "I want the camera to be a little bit alive rather than just ... the vessel staying
fixed in the centre of the screen no matter what." Skims should wiggle it, hits should land with a good impact shake,
drifts and shots should be felt, different objects should carry different inertia, and all of it subtle: "not too
much, not too fancy".

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
| **Inertia** | the framed point's ACCELERATION (never its speed) | a boost drops the camera back so the hull pulls ahead; a turn swings it outside so the hull leads into the turn; a steady cruise frames exactly as before |
| **Kicks** | one damped spring every one-off event nudges | a skim pushes away from the prism, a hit shoves and tilts, a shot recoils, a crystal lifts |
| **Shake** | trauma 0..1, felt squared, drains linearly | hits and blasts |
| **Rumble** | a level 0..1, felt linearly | a fine wobble while skimming (it builds with each prism and fades) and while drifting |
| **Drift lean** | the slip between where the hull points and where it travels | the camera banks into the turn, looks a little along the slide and swings out |
| **Breathing** | always on | a 0.1 degree drift at 0.15 Hz, so the view never reads as frozen |

The code: `Controller/Camera/CameraFeel.cs` (the state and math, plain C#), `CameraFeelConfigSO.cs` (every number,
`Resources/CameraFeelConfig.asset`), `VesselCameraFeel.cs` (where gameplay reports events).

### Units

Every distance is a **fraction of the follow distance**, so the same number reads the same on screen on an Urchin at
6.7 u, a Squirrel at 17 u and a Serpent at 250 u. At a 60 degree field of view the screen's half-height at the hull is
about 0.58 of the follow distance, so a fraction f moves the hull about f / 0.58 of the half-height. Angles are
degrees. A kick's number is its **true peak**: the impulse that reaches it through the spring's damping is worked out
in `CameraFeel.Kick` (`PeakFactor`), so retuning the spring does not retune every event.

### Inertia lags acceleration, never speed

The camera is modelled as a mass on a spring riding the hull: the framed point's velocity is estimated from its motion
(25 ms smoothing) and its change drives the lag spring as a pseudo-force. A constant acceleration settles at a constant
lag (`maxLag` x drive, soft-clamped by tanh), a constant velocity at none. The acceleration is normalised by the follow
distance (`fullLagAcceleration` is in follow distances per second squared), so a boost reads alike on every hull.

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
| The vessel drifts | read each frame from `IVesselStatus.IsDrifting` / `Course` | drift lean and rumble |
| The vessel accelerates | read each frame from the framed point | inertia |

### Different objects, different weight

A prism lands with `PrismWeight`: its size (the cube root of its volume against `referencePrismVolume` 60, held in
0.6..2, so a prism twice as long lands a little harder, not twice as hard) times its shield tier (shielded 1.25,
super-shielded 1.6). A Squirrel trail prism (20 x 0.75 x 5.5 = 82.5) lands at 1.11, a Sparrow's (2 x 2 x 5 = 20) at
0.69, environment mass at up to 2.

### Each hull carries its own inertia and weight

The `vessels` table on the config. Inertia above 1 is a heavier camera: it lags further (`maxLag` x inertia) and
settles slower (`inertiaFrequency` / sqrt(inertia)). Weight scales how hard the hull's hits, shots and skims land.
Unlisted hulls are 1 and 1. First pass, for playtest:

| Hull | Inertia | Weight | Why |
|---|---|---|---|
| Urchin | 0.75 | 0.9 | small, close camera |
| Squirrel | 0.8 | 0.9 | light, agile skimmer |
| Dolphin | 0.85 | 0.9 | light |
| Sparrow, Manta | 0.9 | 1 | nimble |
| Butterfly | 1.1 | 0.9 | floaty, long camera |
| Serpent | 1.15 | 1.1 | long camera |
| Grizzly | 1.3 | 1.25 | heavy |
| Rhino | 1.35 | 1.25 | heavy |
| Orca | 1.4 | 1.3 | the 4-seat hull |
| Starling | 0.4 | 0.5 | a swarm seen from 1200 u: it should float, not shake |

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
hull's row every frame, so a change shows at once. To turn a part off, set its amplitude to 0 (`breathAngle`,
`rumbleAngle`, `maxLag`, ...); to turn all of it off, untick `feelEnabled`. If the whole thing reads too strong or too
weak, change `strength` before any single number.

## Known gaps and findings

- **Not editor-verified.** See `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **The legacy `Shake` still leaks into the eased chase rigs** (the Stoat, Angler and Starling: `chaseEaseRate` > 0).
  It adds to `transform.position` and is never taken off, so the next frame's ease starts from the shaken pose and
  keeps ~89% of it per frame at rate 7: a coherent 10 Hz shake builds to several times its authored size there. Left
  as it was on purpose: the shakes on those hulls were tuned by eye with the leak in. If they read too big, route
  `ApplyShake` through the same undo as the feel.
- **No field-of-view kick on boost.** The inertia already pulls the hull ahead on a boost; a FOV kick would be a
  seventh part and is not asked for yet.
- **Crew picture-in-picture views** (`CrewViewRender`) are their own cameras and do not carry the feel.
- **The Vessel Studio's cameras do not model it.** This is a game-camera feature, not a studio one.

## Log

| Date | Change |
|---|---|
| 2026-10-25 | First version (Yash's ask): six parts, the event table, per-hull inertia and weight, the accessibility setting wired, closed-form springs. `Ys-bleeding-edge`. |
