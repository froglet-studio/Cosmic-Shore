# Thresher — the wrecking ball

**Status: PROTOTYPE, flyable from Toy Box > Vessel Changer, not yet opened in Unity by anyone.**
`VesselClassType.Thresher = 14` (named for the thresher shark, which stuns prey with a whip of its
tail; a thresher is also what a flail is). A small, nimble two-stick ship towing a heavy wrecking
ball on a chain. Turn hard and the ball whips round; wind the chain out and let it reel in, and the
ball cracks red-hot through everything. The **chain slices**, the **ball is always destructive**,
and prisms are **binary** — there is no partial damage anywhere.

The mechanic was prototyped in a 2D browser sandbox (cruise 380 u/s). Every chain dial is authored
in the sandbox's units and scaled to this game by one factor (§4), so the two read side by side.

## 1. Controls (two-thumb flyer)

| Input | Gamepad event | Touch event | What it does |
|---|---|---|---|
| Sticks | — | — | Normal flight, exactly a Squirrel's (vector flight model, 120°/s pitch/yaw). Turn one way and snap back to whip; pitch and yaw snaps both work. |
| **RT hold** (Winch) | `RightStickAction` (1) | `OnlyRightStickAction` (11) | Let chain out (`payOut`) up to `maxLen` × the Space scaling: longer reach, slower whip. |
| **RT release** | | | Chain reels in fast (`reelIn`) conserving spin — **the crack**. The chain turns **lime** (the palette's CTA colour, a quick ease, never a flicker) while releasing now would reach smash speed (READY). |
| **LT hold** (Plant) | `LeftStickAction` (2) | `OnlyLeftStickAction` (12) | The ball skids to a stop (`skidSeconds`, still destructive) and becomes a pivot; the ship swings round it, faster every lap. RT up winds you in and spins you faster; RT held lets you out. The sticks keep a **low-sensitivity** say: they steer your path round the ball at `pivotSteer` 45 °/s (under half of free flight's 120), never the spin. |
| **LT release** | | | Fly off along the orbit's tangent; the ball is yanked after you (`yank`). |

The bindings use the Squirrel's InputEvents slots, so no input plumbing changed. Keyboard:
Left/Right Shift; one-thumb mouse: LMB/RMB. The triggers are read as press/release EDGES only.

## 2. What the ball and the chain do to prisms

| Ball state | Colour | Rival prism | Own-domain prism | Explosion |
|---|---|---|---|---|
| Below smash speed | your **domain** colour, brightening toward smash | **destroyed** (`Prism.Damage`); the ball keeps `crushKeep` of its speed | **bounces** off (restitution `bounce`) | none |
| At / above smash speed | **red** (palette danger) | destroyed; the ball keeps `plough` → `ploughHot` | **destroyed** | **yes, destroys every domain** |
| …with Charge L5 **Lit** | **lit** domain colour (shielded rim) | destroyed | **bounces** | **yes, spares your domain** |

- **Every kill goes through `Prism.Damage`** with the ball's own velocity as the impact vector (the
  Rhino sword's contact-velocity idea at its core: a point mass's contact velocity IS its velocity),
  so debris flies along the ball's path and shields behave exactly as for every other hull — a
  shielded prism pops on the first hit and dies on the next contact; a super-shield holds.
- **The explosion** is the Grizzly's blast (`AOEGrizzlyExplosion.prefab`) spawned at the contact
  through `ExplosionHelper`, **45 u** diameter × the Charge scaling (×2 at full Charge), `AffectSelfOverride` **on**
  (all domains) unless Lit (then off — the fleet rule that domain-sparing lives in the EXPLOSION
  layer, never in `Prism.Damage`). At most one per `explosionMinInterval` (0.08 s), so a hot ball
  through a row makes a string of blasts rather than one per prism. Its container is knock-back only
  (no scoring); an unlit blast near your own hull can knock you too.
- **Sweep, not colliders**: the ball's path each frame is swept as a sphere through
  `PrismSpatialIndex.QuerySegment`, nearest-first, so a fast ball never tunnels. Each prism is hit
  once per contact; a bounce ends that frame's path.
- **The chain slices**: the 12 verlet links are GAMEPLAY (`ThresherChainLinks`, stepped in the chain
  step on every peer). While the chain is **taut** (always, while planted) every **rival** prism a
  link touches is `Prism.Slice`d along the plane the chain is sweeping, debris at that link's own
  velocity. It **never cuts its own domain**, and skips shielded prisms — **Space L5 Reaper Chain**
  makes it cut while slack too, and cut shields (the first cut pops one).
- **Own fresh trail**: the fleet's one rule (`SelfTrailContactConfigSO.SuppressesSkimContact`) — the
  ball and chain ignore prisms this pilot laid within the grace window.

## 3. Elements (approved 2026-10-08) — one parameter per element

| Element | Ability | Scales (`ThresherConfigSO`, `EvaluateReplicated`) | Level 5 |
|---|---|---|---|
| Charge | **Wrecking Ball** (passive) | `explosionSizeMultiplier` ×1 → ×2 (floor ×0.5) | **Lit** — lit colour, explosions spare own domain, bounces own domain even hot |
| Mass | **Heavy Iron** (passive) | `ballMassMultiplier` ×1 → ×1.75 on mass; radius × its cube root | **Wrecker** — a smash costs the ball no speed |
| Space | **Winch** (RT) | `chainReachMultiplier` ×1 → ×1.5 on `maxLen` | **Reaper Chain** — cuts slack and shielded |
| Time | **Plant** (LT) | `spinMultiplier` ×1 → ×1.75 on spin-up rate and cap | **Slingshot** — unlock yank ≥ smash speed |

Upgrades are read through the replicated unlock bits at the moment of use (`LitActive`,
`WreckerActive`, `ReaperActive`, `SlingshotActive`). The scalings use `EvaluateReplicated`, not
`EvaluateLive`: every peer simulates the ball and element levels do not replicate.

**One L5 per element (rule 26).** The hull is a Squirrel clone, and the Squirrel's containers gate
their own upgrades on Charge, Space and Time. The Thresher runs forked containers without them —
`ThresherImpactorDataContainer` (no crystal blast, no lifeform wither), `ThresherSkimmerImpactor
DataContainer` (no steal, no overtake) and `ThresherSkimmerBoostPrismEffect` (skim boost with no
element scaling and no danger-bonus gate) — and its skimmer's Space-scaled size and self-skim are
off. `element_ability_table.py Thresher` reads 4/4 / 4/4 / 4/4 with only the Thresher's own gates.

## 4. Chain physics (`ThresherChainSolver`, pure C#)

- **Ball**: a point mass with drag (`ballDrag`). **Rope**: a position-based max-distance constraint,
  mass-weighted (the ship takes `tug × m/(1+m)` of the overshoot, ship mass 1). A taut chain cancels
  the ball's outward speed and keeps its tangential speed: turning whips the ball round.
- **Floor**: the rope never drags the ship below `minShip ×` its throttle target.
- **Reel in** conserves `v_tangential × r` (capped at `reelSpinCap` per change); **pay out** spends
  spin by `sqrt(oldL/newL)`. **Crack**: a slack chain snapping taut throws `crack` of the cancelled
  radial speed sideways.
- **Lock**: `Plant()` skids the ball (`skid`) for `skidSeconds`, then it is a pivot; the ship orbits
  at `max(|v| × lockKeep, minShip × cruise)` (no stall even planting straight at the ball), spinning
  up by `lockSpin` to `lockMax × cruise`. `Release()` yanks the ball at `yank ×` the ship's velocity.
- **Steering while planted** (`SteerPivot`): the stick, mapped through the hull as the way the nose
  would move, turns the ship's heading about the CHAIN — the only turn a taut chain allows — by at
  most `pivotSteer` (45 °/s) × dt; its in-plane part is ignored. Held, it bends the orbit off its
  plane the way turning bends a path on a globe (the ship steers over the sphere round the ball).
  Speed, radius and spin are untouched, and it is ignored while towing, where the sticks fly the
  hull directly.
- **Bounce** reflects only the inward velocity component and puts the ball at the contact point.

Scale `k = gameCruise / sandboxCruise = 70 / 380 = 0.1842` on every speed and length; rates and
shares are unscaled, so every timing matches the sandbox. `gameCruise` 70 is the prefab's neutral
throttle target (`DefaultMinimumSpeed 15 + 0.5 × DefaultThrottleScaler 110`).

| Dial | Sandbox | Game | Meaning |
|---|---|---|---|
| `restLen` / `maxLen` | 120 / 760 | 22.1 / 140.0 u | chain reeled in / let out (× Space, so 210 u at full Space) |
| `payOut` / `reelIn` | 1000 / 1800 /s | 184.2 / 331.6 u/s | winch rates (full let-out 0.64 s, full reel 0.36 s) |
| `reelSpinCap` | 1.6 | 1.6 | spin multiplier cap per change |
| `ballDrag` / `ballMass` / `tug` | 0.35 / 0.4 / 0.2 | same | drag (1/s), mass (× Mass), ship's share of the yank |
| `minShip` / `crack` | 0.6 / 0.6 | same | tug floor; crack share |
| `smashSpeed` / `whiteHotSpeed` | 700 / 1300 | 128.9 / 239.5 u/s | red-hot threshold; hottest (longest hit-stop, best plough) |
| `plough` / `ploughHot` / `crushKeep` | 0.9 / 0.97 / 0.85 | same | speed kept per kill: hot, white-hot, below smash |
| `bounce` | 0.6 | 0.6 | restitution off own-domain prisms |
| `ballR` / `maxBall` | 17 / 2600 | 3.13 u / 478.9 u/s | swept radius (× ∛Mass); speed cap |
| `skid` / `skidSeconds` / `lockKeep` | 7 / 0.3 s / 1 | same | plant brake, duration, speed kept |
| `lockSpin` / `lockMax` / `yank` | 220 /s² / 2.2 / 0.5 | 40.5 u/s² / 154 u/s / 0.5 | spin-up (× Time), cap (× Time), unlock yank |
| `pivotSteer` | 45 °/s | 45 °/s | full-stick steering while planted (a rate, unscaled; 0 = sticks ignored) |

Measured feel (`Tools/Build/thresher_chain_harness`, 60 Hz, rest levels; peak ball speed × smash):
steady turns reeled in 15/30/60/90/120 °/s → 0.62/0.74/0.94/1.09/1.21, let out → 0.54/0.52/0.38/0.38/0.38
(the 140 u chain is longer than the turn circle — 70/ω, 67 u at 60 °/s — so a let-out ball falls
slack in a steady turn and only the reel cracks it).
Wind up, turn at 120 °/s for T, snap back 0.1 s, release: T 0.3/0.5/0.8/1.2 s → 0.67/0.94/1.34/1.74.
**The reel is the crack, not the snap** — the snap alone never reached smash in the grid swept.
Orbit from cruise (× cruise, RT up): 1.02 at 0.5 s, 1.31 at 1 s, 1.88 at 2 s, 2.19 at 3 s.

## 5. Feedback and look

- **Hit-stop**: one per whip (the combo's first smash), 30 → 90 ms by heat, local pilot only. Solo
  sessions drop `Time.timeScale` to 0.05 (AstroLeague's pattern, restoring to constants); multiplayer
  freezes only this hull and ball (`ThresherVesselTransformer.Update` skips the frame).
- **Shake** (`CustomCameraController.Shake`) scaled by heat; a crush shakes lightly. **Haptic**:
  `HapticController.PlaySkim(lerp(0.45, 1, heat))`. **Combo**: one world counter `x2, x3…` per whip.
- **Audio** (all **shipped EMPTY**, LOCKED FMOD convention): `smashEvent`, `crushEvent`,
  `bounceEvent`, `sliceEvent`, `plantEvent`, `yankEvent` on `ThresherExecutor`.
- **Look**: a studded ball that rolls (runtime-built: a sphere and six cube studs) on
  **`ThresherBallMaterial` / `ThresherBallFresnelShader`** — the game's own fresnel convention, a
  dull body under a bright rim, `lerp(_DarkColor, _BrightColor, (1 − N·V)^1.5)`. Power 1.5, not the
  crystals' 4, because the ball is a few units across and often 100+ u away: area-weighted the rim
  is 23% of the disc (a crystal's is 6.7%, a hairline that would be sub-pixel here). The flat studs
  flare edge-on and go dull face-on, which is what makes the roll readable. Colours (`ThresherConfigSO`
  § Ball shading) are the §2 hue normalised by its max channel: a slow ball's body sits under the
  bloom threshold (0.16) and brightens toward 0.4 as it nears smash; a hot ball's body sits ON the
  0.5 bloom clamp (the whole ball blooms) and its rim whitens with heat; nothing exceeds 1.0
  (tonemapping is None, `Docs/PALETTE.md` §2.2). Compiled with DXC (both passes, ± instancing). The
  drawn ball's ROLL is capped at `ballMaxVisualSpin` 540 °/s: a white-hot ball truly rolls ~7000
  °/s, which at 60 fps strobed the studs backwards (wagon-wheel) and read as jitter. **No dial on
  the ball** — the heat gauge lives only on the HUD's Charge card. A skid trail while planting.
- **The chain** IS the gameplay chain (the 12 links), drawn as a **fresnel tube** —
  **`ThresherChainMaterial` / `ThresherChainFresnelShader`**. It is a LineRenderer (a VIEW-aligned
  ribbon), whose normals all face the lens, so a mesh fresnel would be flat; but a view-aligned
  ribbon is exactly a cylinder's silhouette, and across a unit cylinder seen side-on `N·V =
  sqrt(1 − x²)`, so the fragment computes the tube's true fresnel from `x = 2·uv.y − 1` (power 0.8:
  the chain is a few pixels wide at chase distance, so the rim has to cover most of it to read). It
  is drawn at the width that CUTS (`2 × chainCutRadius`). Iron body (`chainColor`, under the bloom
  threshold); the rim takes the ball's hue, `chainRimSlack` 0.35 while slack and `chainRimTaut`
  0.75 while taut (the state in which it slices), CTA lime (`chainRimReady` 0.95) at READY.
- **Every state change is a quick EASE to the new colour, never a cut or a flicker**
  (`colorBlendRate` 15/s: 95% in 0.2 s, on unscaled time so a smash's turn to red lands inside its
  hit-stop): ball domain → red / lit at smash speed, chain slack → taut → READY lime, and the HUD's
  chain-out gauge (its own matching `colorBlendRate`). A strobing READY flicker was tried and read
  as noise; the change itself is the signal. The skid trail keeps `lineMaterial` (vertex colour).
- **HUD** (`ThresherHUDController` / `ThresherHUDView`, `ThresherHUDVariant.prefab`, a variant of
  `VesselHUDPrefab`): the four-icon row Wrecking Ball · Heavy Iron · Winch · Plant
  (`Tools/Build/author_thresher_icon_placeholders.py`, `--check`), a **ball-heat gauge** on the
  Charge card in the ball's colour and a **chain-out gauge** on the Space card that turns lime at
  READY. Petal bars are authored (transplanted from the Squirrel's variant).
- **Camera**: `ThresherCameraSettingsSO` at `(0, 8, -55)` so the reeled-in ball is in front of the
  lens. Beyond that the executor drives the local pilot's camera (§5a).

## 5a. Camera — the ball is always in frame, and a fast spin is watched, not ridden

Local pilot only (`IsLocalPilot`: never a remote replica, never the autopilot); pose only — FOV is
the speed tunnel's (vessel rule 21) and is only READ. Dials in `ThresherConfigSO` § Camera.

- **Zoom to keep the ball in frame.** Every frame `ThresherCameraFraming.RequiredDistance` solves
  (by bisection) the smallest chase distance at which the rendered ball sits inside the view
  shrunk by `cameraFramingMargin` 1.15 and at least `cameraMinAhead` 6 u in front of the lens, for
  the camera as `CustomCameraController` actually poses it (behind and above, LOOKING AT the hull —
  a level-camera closed form zoomed out on every pitch turn). The distance is clamped to [the
  prefab's own 55 u, `cameraMaxDistance` 450 u] and eased **out at `cameraZoomOutRate` 12/s** (90%
  in ~0.2 s) and **back in at `cameraZoomInRate` 0.7/s** (half-way in ~1 s), then written through
  `SetCameraDistance`. Needed distances (harness): reeled in 38/28/12/54 u behind/abeam/ahead/
  overhead — i.e. none; let out 161/161/15/290; at full Space 232/240/15/430.
- **The zoom follows the chain's REACH, not the ball's swing.** The target is
  `max(RequiredDistance(ball), ReachDistance(chain length))`, where `ReachDistance` is what a ball
  anywhere on the chain's horizontal circle would need (sampled every 15°). Tracking the ball alone
  pumped the camera in and out at the swing's rhythm (abeam 161 u ↔ ahead 15 u at full let-out);
  now the distance moves with the winch and the ball's own position only adds (a ball looped
  overhead).
- **The follow frame turns at most `cameraMaxFollowTurnRate` 240 °/s** (`CustomCameraController.
  MaxFollowTurnRate`, a default-0 seam). The fleet's cameras are hard-attached to the hull
  (`CameraMode.FixedCamera`), so a hull that turns faster than any stick can carries the camera in
  the same frame — and hooking onto a planted orbit swings the hull up to **91° in one frame**
  (reeled in; 67° let out — measured offline by mimicking the transformer, while the rope's own
  tug turns it at most 85–131 °/s). That was the camera "getting whipped around". Below the cap the
  frame tracks the hull exactly, so ordinary flying is unchanged; a snap is spread over ~0.4 s.
- **Spectate a fast spin.** Once a planted orbit circles at `spectateSpinRate` **1.8 rad/s
  (~100 °/s)** or faster, the camera detaches: `CustomCameraController.Spectate` (a new, default-null
  seam) blends over `spectateBlendSeconds` 0.6 s to a still vantage on the orbit's axis, tilted
  `spectateTiltDegrees` 25° toward the side the ship was on, far enough that the whole orbit fits
  the narrower FOV with margin, looking at the pivot; screen-up is the way the camera was facing,
  laid into the orbit plane, so the ship's heading stays "up" across the cut. It watches the ship
  spin. It re-attaches (blending back out) on releasing LT or when the spin falls under
  `spectateReleaseFraction` 0.75 of the threshold. It detaches only after the spin has held over the
  threshold for `spectateEngageSeconds` 0.2 s, so a tap of LT does not bob the camera up and
  straight back down. At the shipped dials planting reeled in at cruise
  is already 3.2 rad/s (spectates at once); a fully let-out orbit at its cap is 1.1 rad/s (ridden),
  1.9 at full Time (spectates). While detached the vantage LEANS after the orbit's plane at
  `spectatePlaneFollowRate` 1.5/s, so steering a planted orbit turns it in front of a still camera
  instead of leaving it to drift out of an obsolete view.
- **The camera seam** (`CustomCameraController.Spectate` / `SpectateBlendSeconds` /
  `SpectateView`): applied as a BLEND over the settled follow pose, before shake and the portal
  carry; the follow pose is kept separately while blended so the chase smoothing never chases the
  vantage. Null (every other vessel, always) is a no-op: the blend stays 0 and no follow line runs
  differently. Cleared on a follow-target change; a teleport cuts the blend. `MaxFollowTurnRate`
  likewise: 0 for every other vessel (the frame IS the target's rotation), cleared on a
  follow-target change, reset on a teleport and by `SnapToTarget`.
- **Hand-back**: `OnDisable` / `OnDestroy` / losing local pilot / disabling `cameraFraming` clear
  the vantage and the follow-turn cap and restore the prefab's distance.

## 6. Flight model hooks

`ThresherVesselTransformer : VesselTransformer` overrides seams only, never `MoveShip`. Two seams
were added to `VesselTransformer`, both **default-off and bit-identical** for every other vessel:
`ComputeExternalAcceleration` (default `Vector3.zero`; here: step the chain, return the rope's
delta-v, and turn the hull with the tug so grip does not erase it) and `NoseConvergence` (default:
the drift expression it was extracted from; here: 0 while planted). Also `ComputeNoseAcceleration`
(0 while planted), `RotateShip` (face the orbit tangent; snap onto it on release) and `Update`
(skip the frame during a multiplayer hit-stop). These are the Gibbon prototype's seams
(`cece/hopeful-bardeen-rmqhmd`, 720507e), re-cut; whichever branch lands second adopts the other's.

## 7. Networking

None of its own. Press and release round-trip through `R_VesselActionHandler`, so every peer runs
the same chain from the same inputs and the hull's replicated motion; scalings and upgrades use the
replicated level and unlock bits. A hull whose transformer is off is stepped by
`ThresherExecutor.LateUpdate` from its observed motion. Peers can disagree on a marginal hit by a
frame of drift. Untested in MPPM.

## 8. Files

| File | What |
|---|---|
| `Controller/Vessel/ThresherChainSolver.cs` | Pure physics, `ThresherDials` (the defaults), `ThresherChainLinks` |
| `Controller/Vessel/ThresherVesselTransformer.cs` | Seam overrides |
| `Controller/Vessel/VesselTransformer.cs` | + `ComputeExternalAcceleration`, `NoseConvergence` (default-off) |
| `R_VesselActions/Executors/ThresherExecutor.cs` | Ball sweep, chain cut, explosions, upgrades, feedback, visuals |
| `R_VesselActions/Data Containers/ThresherActionSO.cs` / `ThresherConfigSO.cs` | Trigger routing; every dial |
| `UI/Controller/ThresherHUDController.cs`, `UI/View/ThresherHUDView.cs` | HUD pair |
| `_Prefabs/Spacevessels/Thresher.prefab`, `_Prefabs/UI Elements/VesselHUD/ThresherHUDVariant.prefab` | Hull (Squirrel clone), HUD |
| `_SO_Assets/VesselActions/Thresher/*.asset` | Config, Winch and Plant actions |
| `_SO_Assets/Effects/Effect Containers/{Vessel,Skimmer}Containers/Thresher*.asset`, `Skimmer Prism Effects/ThresherSkimmerBoostPrismEffect.asset` | Forked containers (§3) |
| `Resources/ElementalAbilityMaps/Thresher.asset`, `_SO_Assets/Classes/SO_Class_Thresher.asset`, `_SO_Assets/Camera/ThresherCameraSettingsSO.asset` | Map, class, camera |
| `_Graphics/Icons/AbilityIcons/Thresher/` | Placeholder icons |
| `Controller/Camera/CustomCameraController.cs` | + `Spectate` vantage seam (default null) and `MaxFollowTurnRate` follow-frame cap (default 0), §5a |
| `_Graphics/Materials/Shaders/ThresherBallFresnelShader.shader`, `_Graphics/Materials/ThresherBallMaterial.mat` | The ball's fresnel look (§5) |
| `_Graphics/Materials/Shaders/ThresherChainFresnelShader.shader`, `_Graphics/Materials/ThresherChainMaterial.mat` | The chain's fresnel tube (§5) |
| `Tests/Editor/ThresherChainSolverTests.cs`, `Tools/Build/thresher_chain_harness/` | 46 tests (solver, planted steering, camera framing and reach envelope); offline runner + feel table |

Registered in `Vessel Prefab Container`, `DefaultNetworkPrefabs` (own `GlobalObjectIdHash`
1887398589), `ToyVesselRoster.Default`, `CrystalHullFusionConfig` (reuses the Squirrel's bakes —
same hull mesh) and `SO_Classlist_All` / `_Classes`. Still the Squirrel's: the model, the skimmer
prefab, `DriftAudioController`, jets and tails.

## 9. Known weaknesses

- A turn throws the ball outward, so hits land on the side away from the turn.
- With the chain let out the ball falls slack in any steady turn (0.38–0.54× smash): only the reel
  (or the plant) heats it. Shorten `maxLen` if a let-out whip should work on its own.
- The chase camera can sit well back (up to ~240 u at full Space) whenever the chain is out and the
  ball trails behind; the ball then sits low in the frame, between the lens and the hull.
- Planted steering is hull-relative, so in the spectate view (looking down the orbit's axis) the
  stick still means "the way the nose would go", not "toward the top of the screen".
- READY underestimates during fast snaps (it projects the current spin through a full reel and
  ignores the snap crack).

## In-editor verification

1. **Toy Box > Vessel Changer → Thresher.** The hull swaps in with a ball 22 u behind on a chain, in
   front of the camera; the HUD shows the four Thresher icons and petal bars. Console clean.
2. **Slow ball**: tow gently into a RIVAL trail — prisms are destroyed, no explosion, the ball is in
   your domain colour. Into your OWN older trail — the ball bounces off, nothing breaks.
3. **Whip**: hold RT ~1 s, turn hard ~1 s, snap back briefly, release. The chain eases to lime first;
   on release the ball goes red, destroys everything including your own trail, each hit a blast
   (string, not per prism), first hit with a hit-stop, debris along its path, `x2, x3…` counter.
4. **Chain**: fly so the taut chain sweeps through rival prisms — they are sliced; your own are not;
   shielded rival prisms are skipped.
5. **Plant**: hold LT — skid (trail), orbit, spin-up; RT up winds in. Release: fly off on the tangent,
   ball yanked after you. Planting straight at the ball still orbits. While planted, the sticks
   steer the orbit (45 °/s, under half the normal turn rate) without changing its speed.
6. **Upgrades** (FrogletTools element tools or crystals to L5): Charge — hot ball lit, blasts spare
   your trail, bounces off it; Mass — a hot ball keeps its speed through a row; Space — chain cuts
   slack and pops shields; Time — releasing a slow orbit still throws a red ball.
7. **Other vessels**: Squirrel, Dolphin, Scarab fly unchanged.
8. **MPPM two clients**: the guest sees the host's ball and chain; a host hit-stop does not freeze
   the guest, and the host's camera zoom/spectate never moves the guest's camera.
9. **Camera**: let the chain out and swing it — the camera pulls back at once so the ball never
   leaves the frame; reel in and it drifts back to its usual distance over a couple of seconds.
   Plant reeled in — the camera lifts off to a still vantage over the orbit and you watch the ship
   spin; release LT and it eases back behind the hull. Planting never whips the camera round — the
   hull visibly swings onto the orbit inside a camera that turns smoothly after it. Swinging a
   let-out ball does not pump the camera in and out. Fly another vessel afterwards: its camera is
   at its own distance and hard-attached.
10. **Ball and chain look**: the ball is a dark body with a bright rim in your domain colour (no
    ring round it); its studs flash as it rolls, and stay legible (no backwards strobe) when white
    hot; it brightens toward smash speed, turns red and blooms when hot, lit-blue with Charge L5.
    The chain reads as a round iron tube with a domain-coloured rim, brighter while taut, easing to
    lime at READY — no colour ever flickers; each change is a quick blend.

Tuning order if it feels wrong: `maxLen` vs the hull's turn rate (whether the ball swings or falls
slack), then `smashSpeed`, `explosionDiameter`, then `crack` / `reelSpinCap`. Camera: `spectateSpinRate`
(when it detaches), then `cameraZoomInRate` (how lazily it comes home).

## Follow-ups

- **Art and sound**: real ball/chain art and icons; the six FMOD events.
- **Hull**: its own model, skimmer and jets (still the Squirrel's).
- **Virtual (swarm) prisms** are not swept by the ball or chain.
- **AI**: no AI abilities are authored (both triggers are holds).
- **Random spawns**: `VesselSpawner.SpawnShip(Random)` can now hand out the Thresher.
- **Gibbon id clash**: the Gibbon branch claims `VesselClassType = 13` (the Butterfly's); it needs 15.
- **Pre-existing debt walked past (not fixed here)**: `R_VesselElementStatsHandler.cs` declares
  `R_ShipElementStatsHandler` and `SkimmerBoostPrismEffect.cs` declares `SkimmerBoostPrismEffectSO`
  — `check_generated_assets.py` reports both as "script can not be loaded" on every vessel prefab
  and on the Squirrel's live boost asset. The Thresher prefab's remaining audit findings are its
  inherited Squirrel ones (stale nested-prefab overrides, `ShowTopMostFoldoutHeaderGroup` keys).
