# Thresher — the wrecking ball

**Status: PROTOTYPE, flyable from Toy Box > Vessel Changer, not yet opened in Unity by anyone.**
`VesselClassType.Thresher = 14` (named for the thresher shark, which stuns prey with a whip of its
tail; a thresher is also what a flail is). A small, nimble two-stick ship towing a heavy wrecking
ball on a chain. Turn hard and the ball whips round; wind the chain out and let it reel in, and the
ball cracks red-hot through everything. The **chain slices**, the **ball is always destructive**,
and prisms are **binary** — there is no partial damage anywhere. The chain's kills **charge** the
ball's next explosion; **A** switches to **Mass Mode**, where the ball builds instead of breaking;
an **omni crystal** windmills the ball round the ship (§2a–2c, added 2026-10-13).

The mechanic was prototyped in a 2D browser sandbox (cruise 380 u/s). Every chain dial is authored
in the sandbox's units and scaled to this game by one factor (§4), so the two read side by side.

## 1. Controls (two-thumb flyer)

| Input | Gamepad event | Touch event | What it does |
|---|---|---|---|
| Sticks | — | — | Normal flight, exactly a Squirrel's (vector flight model, 120°/s pitch/yaw). Turn one way and snap back to whip; pitch and yaw snaps both work. |
| **RT hold** (Winch) | `RightStickAction` (1) | `OnlyRightStickAction` (11) | Let chain out (`payOut`) up to `maxLen` × the Space scaling: longer reach, slower whip. |
| **RT release** | | | Chain reels in fast (`reelIn`) conserving spin — **the crack**. The chain turns **lime** (the palette's CTA colour, a quick ease, never a flicker) while releasing now would reach smash speed (READY). |
| **LT hold** (Plant) | `LeftStickAction` (2) | `OnlyLeftStickAction` (12) | The ball skids to a stop (`skidSeconds`, still destructive) and becomes a pivot; the ship swings round it, faster every lap. RT up winds you in and spins you faster; RT held lets you out. The sticks keep a **low-sensitivity** say: they steer your path round the ball at `pivotSteer` 45 °/s (under half of free flight's 120), never the spin. |
| **LT release** | | | Fly off along the orbit's tangent — or, while the camera spectates the spin, straight along the **aim** the sticks have set (§5a), the instant LT comes up; the ball is yanked after you (`yank`). |
| **A press** (Mass Mode) | `Button1Action` (6), shared map | `Button1Action` (on-screen button) | **Toggle** Mass Mode (§2b). The release is ignored (rule 37). |

The trigger bindings use the Squirrel's InputEvents slots, so no input plumbing changed. Keyboard:
Left/Right Shift; one-thumb mouse: LMB/RMB. The triggers are read as press/release EDGES only.
Mass Mode is `Button1Action`, which the gamepad strategy fires from `buttonSouth` (A on an Xbox
pad; the enum's own comment says "X" and is stale) and the keyboard from Space; it lives in the
SHARED map (`_inputEventShipActions`), so every device resolves it the same way.

## 2. What the ball and the chain do to prisms

| Ball state | Colour | Rival prism | Own-domain prism | Explosion |
|---|---|---|---|---|
| Below smash speed | your **domain** colour, brightening toward smash | **destroyed** (`Prism.Damage`); the ball keeps `crushKeep` of its speed | **bounces** off (restitution `bounce`) | none |
| At / above smash speed | **red** (palette danger) | destroyed; the ball keeps `plough` → `ploughHot` | **destroyed** | **yes, destroys every domain** |
| …with Space L5 **Breaker** | red | destroyed — **super-shielded included** (direct hits) | destroyed (super-shielded too) | yes |

(Charge L5 "Lit", which spared your own domain, was retired 2026-10-13 round 3 — rule 26; the hot
ball is always red now.)

- **Every kill goes through `Prism.Damage`** with the ball's own velocity as the impact vector (the
  Rhino sword's contact-velocity idea at its core: a point mass's contact velocity IS its velocity),
  so debris flies along the ball's path and shields behave exactly as for every other hull — a
  shielded prism pops on the first hit and dies on the next contact; a super-shield holds.
- **The explosion** is the Grizzly's blast (`AOEGrizzlyExplosion.prefab`) spawned at the contact
  through `ExplosionHelper`, **45 u** diameter × the chain's energy multiplier (§2a), `AffectSelfOverride`
  **on** (all domains). At most one per `explosionMinInterval` (0.08 s), so a hot ball
  through a row makes a string of blasts rather than one per prism. Its container is knock-back only
  (no scoring); an unlit blast near your own hull can knock you too.
- **Sweep, not colliders**: the ball's path each frame is swept as a sphere through
  `PrismSpatialIndex.QuerySegment`, nearest-first, so a fast ball never tunnels. Each prism is hit
  once per contact; a bounce ends that frame's path.
- **The chain slices rivals and shields its own — taut or slack** (2026-10-13; it used to cut only
  while taut): the 24 verlet links are GAMEPLAY (`ThresherChainLinks`, stepped in the chain step on
  every peer). Every **rival** prism a link touches is `Prism.Slice`d along the plane the chain is
  sweeping, debris at that link's own velocity, and each kill banks energy (§2a); every
  **own-domain** prism it touches is **shielded** (`ActivateShield`, untimed; already-armoured mass
  is left alone). A shielded rival loses its shield to the first cut and dies to the next; a
  super-shielded one refuses. (Space L5 Reaper Chain, which used to gate slack and shielded cuts,
  was retired 2026-10-13 — both are the base now; rule 26.)
- **Enemy vessels — petals only** (the LOCKED PvP rule, `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`).
  The ball's swept path and every chain link reach an enemy hull within `vesselHitRadius` 4 u (plus
  the ball's radius / the chain's cut width). A contact is a scored **Strike** that **steals**
  `vesselHitPetals` petals per element (**Charge**: 1 at rest → 3 at full) through
  `CombatHitDrain.TryAdmit` (a warded pilot is neither scored on nor robbed; `vesselHitCooldownSeconds`
  1 s per victim) and `ElementalTransfer.ApplyAllAuthoritative`, then raises `Event_CombatHitStats`.
  It is decided only on the machine that owns this hull (`IsDecidedHere` + `IsNetworkOwner`), because
  every peer simulates the ball. Nothing else touches the victim — no knock-back, no prism of theirs.
  **Charge L5 "Magnet"** pulls a free ball toward the nearest enemy hull within `magnetRange` 150 u at
  `magnetAcceleration` 220 u/s² (it moves only your ball).
- **Own fresh trail**: the fleet's one rule (`SelfTrailContactConfigSO.SuppressesSkimContact`) — the
  ball and chain ignore prisms this pilot laid within the grace window (Mass Mode's ball trail
  included — it is stamped with the pilot's `ownerID`).

## 2a. Chain energy — the chain's kills charge the ball

Every prism the chain **destroys** (a cut that only pops a shield banks nothing) banks
`energyPerSlice` 1 in the ball, up to `energyMax` 70. The ball's next hit on a **rival** prism
releases the whole bank as an explosion **even below smash speed** — the same Grizzly blast as a
smash — and a smash's own explosion spends it too. The bank multiplies the diameter:
`× min(energyMaxDiameterScale, 1 + energyDiameterPerUnit 0.1 × energy)`, where the cap is **Space**:
×4 at rest → ×8 at full Space (floor ×2). So 10 slices = ×2 (90 u); 30 = ×4 (180 u, the rest-Space
cap); 70 = ×8 (360 u) only at full Space. The **Space card** shows the bank as a fraction of that cap. A hit that
lands inside `explosionMinInterval` keeps the bank for the next one. The ball's rim leans toward
the CTA lime with the bank (`energyRimTint` 0.6 at full), so a charged ball reads before it hits.
Own-domain bounces never spend it.

## 2b. Mass Mode (A, toggle) — the ball builds instead of breaking

| Ball touches… | Mass Mode does | Speed cost |
|---|---|---|
| a **rival** prism | **steals** it (`Prism.Steal` — a shielded rival sheds its shield first and changes hands on the next touch; super-shielded refuses), banking `energyPerSteal` 0.5 | none |
| an **own-domain** prism | **shields** it (`ActivateShield`, untimed; an already-armoured prism is left alone) | none |
| anything, at any speed | **never explodes** and never destroys — the bank is held, not spent | — |

- **The ball lays a trail**: one prism of the pilot's domain per `massTrailSpacing` 6 u of the
  ball's own travel, `massTrailPrismScale` (8, 0.75, 5.5) × **Mass** (`massTrailScale` ×1 → ×2 on
  every axis, admitted past the pool's scale window), oriented along its path; **Mass L5 "Armoured
  Trail"** lays them **shielded**. Laid through the
  hull's OWN spawn channel and prism type (`VesselPrismController.PrismSpawnChannel` /
  `SpawnPrismType`), so it is ordinary conserved trail mass. Owed in distance, paid in whole
  prisms, at most `massTrailMaxPerFrame` 3 per frame with the excess dropped (rule 17). The prisms
  go into a `Trail` the executor owns (never the hull's wake), so they are a ribbon and
  identity-testable. A planted ball does not move and lays nothing.
- The chain still slices in Mass Mode, and its kills still bank.
- The ball wears its domain's **lit** colour in Mass Mode (it never shows red: it does not smash).
- Leave Mass Mode (A again) and the next rival hit spends everything banked.

## 2c. Windmill — an omni crystal

`ThresherWindmillByCrystalEffectSO`, the one entry added to `ThresherImpactorDataContainer.
vesselCrystalEffects` (the OMNI list — elemental crystals dispatch elsewhere and still only level
an element). For `windmillSeconds` **12** (doubled 2026-10-13) the ball circles the **ship** at `windmillSpeed` 1000 sandbox
(184 u/s — between smash 129 and white-hot 240, so it is red-hot all the way round) in the plane
ACROSS the ship's flight, like a windmill's sails, at the chain's length — the winch still works,
so RT sizes the windmill. The ship flies on untouched; the ball eases onto the circle at the reel
rate rather than teleporting. A planted pivot is abandoned; an LT release mid-windmill does not cut
it short; a second crystal tops the clock back up, never stacks it. When it runs out the ball is let
go at the speed it has and the rope takes over. It sweeps and explodes exactly as any hot ball (or
steals/shields in Mass Mode). Runs on every peer: `VesselImpactor.ExecuteOmniCrystalImpact` is
routed to all of them.

## 3. Elements (re-cut 2026-10-13, round 3, from Garrett's spec) — one parameter per element

| Element | Ability | Scales (`ThresherConfigSO`, `EvaluateReplicated`) | Level 5 |
|---|---|---|---|
| Charge | **Wrecking Ball** (passive) | `vesselHitPetals` 1 → 3 petals per element the ball or chain steals per enemy-vessel hit (floor 0.5) | **Magnet** — the free ball is drawn to enemy vessels |
| Mass | **Mass Mode** (A, toggle) | `massTrailScale` ×1 → ×2 on the size of the ball's Mass Mode trail prisms (floor ×0.5) | **Armoured Trail** — those prisms are laid shielded |
| Space | **Winch** (RT) | `energyMaxDiameterScale` ×4 → ×8, the largest blast the chain's bank can buy (floor ×2) | **Breaker** — a red ball's direct hit destroys super-shielded prisms |
| Time | **Plant** (LT) | `spinMultiplier` ×1 → ×1.75 on spin-up rate and cap — how fast the spin winds up to launch speed | **Perfect Release** — a launch timed inside the window leaves ×1.6 faster |

Retired by this re-cut (rule 26 — each old gate is deleted, not left beside the new one): Charge
"Lit", Mass "Wrecker" (and the ball's Mass-scaled weight/radius: the ball is now a fixed size),
Space "Reaper Chain" (and the Space-scaled chain reach: `maxLen` is fixed), Time "Slingshot", and
the Charge-scaled explosion size. Upgrades are read through the replicated unlock bits at the
moment of use (`MagnetActive`, `ArmouredTrailActive`, `BreakerActive`, `PerfectReleaseActive`). The scalings use `EvaluateReplicated`, not
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
| `restLen` / `maxLen` | 120 / 760 | 22.1 / 140.0 u | chain reeled in / let out (fixed since 2026-10-13; Space no longer scales it) |
| `payOut` / `reelIn` | 1000 / 1800 /s | 184.2 / 331.6 u/s | winch rates (full let-out 0.64 s, full reel 0.36 s) |
| `reelSpinCap` | 1.6 | 1.6 | spin multiplier cap per change |
| `ballDrag` / `ballMass` / `tug` | 0.35 / 0.4 / 0.2 | same | drag (1/s), mass, ship's share of the yank |
| `minShip` / `crack` | 0.6 / 0.6 | same | tug floor; crack share |
| `smashSpeed` / `whiteHotSpeed` | 700 / 1300 | 128.9 / 239.5 u/s | red-hot threshold; hottest (longest hit-stop, best plough) |
| `plough` / `ploughHot` / `crushKeep` | 0.9 / 0.97 / 0.85 | same | speed kept per kill: hot, white-hot, below smash |
| `bounce` | 0.6 | 0.6 | restitution off own-domain prisms |
| `ballR` / `maxBall` | 17 / 2600 | 3.13 u / 478.9 u/s | swept radius; speed cap |
| `skid` / `skidSeconds` / `lockKeep` | 7 / 0.3 s / 1 | same | plant brake, duration, speed kept |
| `lockSpin` / `lockMax` / `yank` | 220 /s² / 2.2 / 0.5 | 40.5 u/s² / 154 u/s / 0.5 | spin-up (× Time), cap (× Time), unlock yank |
| `pivotSteer` | 45 °/s | 45 °/s | full-stick steering while planted (a rate, unscaled; 0 = sticks ignored) |
| `windmillSpeed` | 1000 | 184.2 u/s | the omni-crystal windmill's sail speed (§2c) |

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
- **The chain** IS the gameplay chain (`chainLinks` **24** links since 2026-10-13, was 12; relaxed
  with `ThresherChainLinks.IterationsFor` passes so a long rope does not read stretched), DRAWN as
  a uniform **Catmull-Rom curve** through the links (`ThresherChainLinks.Smooth`,
  `chainRenderSubdivisions` 6 segments per link — 139 drawn points) so a slack chain bends smoothly
  instead of kinking at every link. The curve passes through every link exactly. It is a **fresnel tube** —
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
  energy gauge (its own matching `colorBlendRate`). A strobing READY flicker was tried and read
  as noise; the change itself is the signal. The skid trail keeps `lineMaterial` (vertex colour).
- **HUD** (`ThresherHUDController` / `ThresherHUDView`, `ThresherHUDVariant.prefab`, a variant of
  `VesselHUDPrefab`): the four-icon row Wrecking Ball · Mass Mode · Winch · Plant
  (`Tools/Build/author_thresher_icon_placeholders.py`, `--check`), a **ball-heat gauge** on the
  Charge card in the ball's colour and an **energy gauge** on the Space card (2026-10-13; it was the
  chain-out gauge): how much explosion RADIUS the chain's kills have banked
  (`ThresherExecutor.EnergyRadius01` = (energy scale − 1) / (`energyMaxDiameterScale` − 1)), iron
  when empty easing to lime when full, emptied by the blast that spends it. The READY cue now lives
  on the chain alone. (Field `energyGauge`, `[FormerlySerializedAs("chainOutGauge")]`; the
  GameObject is `EnergyGauge`.) Petal bars are authored (transplanted from the Squirrel's variant).
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
- **Spectate a spin — and AIM it from the launch point** (re-cut twice on 2026-10-13: the 1.8 rad/s
  threshold still made spectators sick, then the first aim camera watched from over the pivot).
  Once a planted orbit circles at `spectateSpinRate` **0.9 rad/s (~52 °/s)** or faster for
  `spectateEngageSeconds` **0.1 s**, the camera detaches (`CustomCameraController.Spectate`, blending
  over `spectateBlendSeconds` 0.6 s) to the **launch camera**:
  - **The launch point.** The aim names one point on the circle — the point whose tangent IS the aim
    (`ThresherChainSolver.LaunchRadial`: with travel normal N, the velocity at radial r is N × r, so
    the point is r = aim × N), on the circle the ship is ACTUALLY flying (its distance from the
    pivot, never the chain's length). The camera **follows behind that point ON the launch
    trajectory** (2026-10-13 round 4): the hull's own chase distance back along −aim
    (`aimCameraDistance` 0 = the camera settings' follow offset, 55 u) with **no height offset**
    (`aimElevationDegrees` 0), looking along the aim, up = the orbit's axis
    (`ThresherCameraFraming.LaunchCameraPosition`). The orbit is seen edge-on and the ship sweeps
    through the launch point in front of the lens every lap. The aim starts as the way the chase
    camera was facing, laid into the orbit plane, so the view direction carries across the cut.
  - **The sticks aim, and the whole spin turns with the camera.** `ThresherCameraFraming.TurnAim`:
    stick X swings the aim at `aimYawRate` 180 °/s — the launch point slides round the circle and the
    camera swings with it; stick Y pitches aim and axis together at `aimPitchRate` 120 °/s (the hull's
    nose-down sign) and the **orbit plane tilts with it** (`ThresherChainSolver.SteerOrbitPlane`
    lays each pivot step into the plane ⟂ the aim's axis, keeping the spin's sense). `PivotSteer`
    returns zero while `IsAiming`.
  - **The launch sight** (`ThresherLaunchSight`, round 4 — it replaced the dotted line, which
    degenerates to a point once the camera is ON the trajectory). A **crosshair** at screen centre:
    every point of the trajectory projects there. White while a release would wait; the palette's
    **CTA lime while the timing is right** — the ship will reach the launch point within
    `perfectWindowSeconds` 0.25 s (`IsTimingPerfect`, `ThresherChainSolver.SecondsToLaunchPoint`).
    Beside it, a **zoom window shaped like the TIME petal** (the Plant is the Time ability), in the
    Serpent eyepiece's slot (top-left, 45% of screen height): a second camera looking down the same
    trajectory from the launch point, with its own crosshair, **zoomed by LT's depth** like the
    Serpent's scope — `ThresherCameraFraming.ZoomTarget` (depth past `sightZoomDeadzone` 0.25, so a
    light hold plants without zooming; the Plant engages at 5%), eased at `sightZoomResponse` 12/s
    (only a binary key needs it), FOV `sightWideFieldOfView` 50° → `sightZoomFieldOfView` 12°. The
    flight view is never magnified. Shared with the Serpent rather than copied: `ScopePipView` (the
    offscreen camera; gained an explicit-eye `Tick` and a `RenderAspect`) and `ScopePetalImage`
    (gained three virtual shape members; `TimePetalImage` overrides only those).
    `TimePetalGeometry` is traced off `time_petal.png`: an exact rhombus, 72° at both tips,
    width / height = tan 36° (0.727), so its render target is that aspect too.
  - **The release is timed for you.** LT up ARMS the launch (`ThresherChainSolver.ArmLaunch`): the
    ship keeps circling and leaves from the chosen point, along the aim, on the frame it would
    otherwise carry past it (the angle still to go is within that frame's angular step) — at most one
    lap later (≈ 2 s at 3 rad/s, ≈ 7 s at the 0.9 threshold). Released inside the lime window the wait
    is little to none, and with **Time L5 "Perfect Release"** that launch leaves `perfectBoost` ×1.6
    faster than the orbit (`ThresherStepResult.Perfect`). The aim is held once armed. The ball is
    yanked along the aim; the transformer faces the hull along it (`TryConsumeReleaseDirection`) and
    grip puts the momentum on the nose. Re-pressing LT while armed does not cancel it (re-releasing
    re-arms with the same aim).
  - It re-attaches once the ship has launched, or if the spin falls under `spectateReleaseFraction`
    0.75 of the threshold (an armed launch still fires at its point). At the shipped dials every orbit
    at its cap spectates (reeled in at cruise 3.2 rad/s, let out at its cap 1.1); only a let-out orbit
    hooked on at the crawl floor (0.3 rad/s) is ridden until it spins up. Below the threshold the old
    behaviour stands: the sticks lean the orbit at `pivotSteer` and LT releases on the tangent at once.
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
| `_SO_Assets/VesselActions/Thresher/*.asset` | Config, Winch, Plant and Mass Mode actions |
| `_SO_Assets/Effects/Effect Containers/{Vessel,Skimmer}Containers/Thresher*.asset`, `Skimmer Prism Effects/ThresherSkimmerBoostPrismEffect.asset` | Forked containers (§3) |
| `Resources/ElementalAbilityMaps/Thresher.asset`, `_SO_Assets/Classes/SO_Class_Thresher.asset`, `_SO_Assets/Camera/ThresherCameraSettingsSO.asset` | Map, class, camera |
| `_Graphics/Icons/AbilityIcons/Thresher/` | Placeholder icons |
| `R_VesselActions/Executors/ThresherLaunchSight.cs`, `UI/View/TimePetalGeometry.cs`, `UI/View/TimePetalImage.cs` | The launch sight: crosshair + Time-petal zoom window (§5a); reuses `Utility/ScopePipView.cs` and `UI/View/ScopePetalImage.cs` from the Serpent |
| `Controller/Camera/CustomCameraController.cs` | + `Spectate` vantage seam (default null) and `MaxFollowTurnRate` follow-frame cap (default 0), §5a |
| `ImpactEffects/EffectsSO/Vessel Crystal Effects/ThresherWindmillByCrystalEffectSO.cs`, `_SO_Assets/Effects/Vessel Crystal Effects/ThresherWindmillByCrystalEffect.asset` | The omni-crystal windmill (§2c), wired into `ThresherImpactorDataContainer.vesselCrystalEffects` |
| `_SO_Assets/VesselActions/Thresher/ThresherMassModeAction.asset` | `ThresherActionSO` `control: MassMode`, bound to `Button1Action` in the prefab's shared map (§2b) |
| `_Graphics/Materials/Shaders/ThresherBallFresnelShader.shader`, `_Graphics/Materials/ThresherBallMaterial.mat` | The ball's fresnel look (§5) |
| `_Graphics/Materials/Shaders/ThresherChainFresnelShader.shader`, `_Graphics/Materials/ThresherChainMaterial.mat` | The chain's fresnel tube (§5) |
| `Tests/Editor/ThresherChainSolverTests.cs`, `Tools/Build/thresher_chain_harness/` | 56 tests (solver, planted steering, windmill, chain smoothing, camera framing, reach envelope, launch camera on the trajectory, sight zoom, armed + perfect launch, steered plane, magnet); offline runner + feel table. `Tests/Editor/TimePetalGeometryTests.cs`: the traced Time petal |

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
- **Vessel hits are decided on the owner of the Thresher**, from that machine's copy of the ball;
  a peer whose copy has drifted by a frame sees a hit it did not simulate (the score and the petals
  still arrive through the authoritative path). Untested in MPPM.
- **The sight window is a second full render of the arena** at 30 Hz (`ScopePipView`), as the
  Serpent's is, but only while a spin is spectated by the local pilot.
- **The aimed launch is decided on the OWNER.** The aim, the steered plane and the armed launch are
  the local pilot's camera state and do not replicate. The hull's pose does, but a remote peer runs
  the plain release the moment LT comes up, so its copy of the ball is let go while the replicated
  hull keeps circling to the launch point — the remote ball and chain can look wrong for up to a lap.
  Replicating the armed aim (one Vector3 with the release) is the fix. Untested in MPPM.
- Mass Mode is a toggle replicated by re-running the press on every peer; a press lost to a peer
  would invert that peer's Mass Mode until the next press (the same exposure every edge has).
- The windmill and Mass Mode have **no FMOD events of their own** yet: the trail and shield reuse
  nothing, a steal plays `crushEvent`, an energy blast `smashEvent`.
- Mass Mode's icon is still the Heavy Iron placeholder (a weight).
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
4. **Chain**: sweep the chain — taut OR slack — through rival prisms: they are sliced; through your
   own older trail: those prisms gain shields; a shielded rival loses its shield, then dies on the next pass.
5. **Plant**: hold LT — skid (trail), orbit, spin-up; RT up winds in. Release: fly off on the tangent,
   ball yanked after you. Planting straight at the ball still orbits. While planted, the sticks
   steer the orbit (45 °/s, under half the normal turn rate) without changing its speed.
6. **Upgrades** (FrogletTools element tools or crystals to L5): Charge (Magnet) — the free ball
   curves toward a nearby enemy vessel; Mass (Armoured Trail) — Mass Mode's trail prisms arrive
   shielded; Space (Breaker) — a red ball smashes a super-shielded prism (the Skim Race track's
   stellated ones are a good test); Time (Perfect Release) — release while the crosshair is lime
   and the ship launches visibly faster than the orbit. Levels below 5: Charge more petals per
   vessel hit, Mass bigger trail prisms, Space a bigger maximum blast, Time a faster spin-up.
16. **Enemy vessels**: hit an AI/rival hull with the ball or the chain — they lose petals and you
    score a Strike (once per second per victim); nothing else happens to them.
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
11. **Launch camera** (2026-10-13, round 4): plant and let it spin — the camera moves to follow
    behind a point on the circle at chase distance with NO height offset, looking along the launch
    path; a crosshair sits at screen centre and flashes lime each time the ship is about to pass
    the point (the ship visibly crosses in front of the lens there). A Time-petal (diamond) window
    top-left shows the same path magnified; squeeze LT deeper and it zooms, ease off and it widens;
    the main view never zooms. Hold RT while planted: the point stays on the ship's own circle. The ship sweeps through that point every lap. Stick right: the point slides round the
    circle and the camera swings with it. Stick Y: the whole orbit tilts with the camera. Release LT
    anywhere in the lap — the ship keeps circling and leaves exactly from the point along the line;
    then the camera eases back behind it.
12. **Smooth chain**: let the chain out and swing it slack — it curves smoothly, no visible kinks
    at link joints; taut it is a straight line.
13. **Chain energy**: slice a few rival prisms with the chain — the ball's rim leans lime and the
    SPACE card's gauge fills toward lime. Tow
    the SLOW ball (domain colour, below smash) into a rival prism — it explodes, bigger the more
    you sliced; the rim and the Space gauge empty. Bounce off your own trail — no explosion, rim stays lime.
14. **Mass Mode**: press A — the ball turns your lit colour. Swing it through a rival trail: prisms
    change to your domain (no explosions, even red-hot); through your own trail: they gain shields;
    it lays a trail of your prisms behind it. Slice rival prisms with the chain meanwhile (bank
    grows). Press A again, then hit a rival prism: one big blast.
15. **Windmill**: fly through an omni crystal — the ball whips round the ship across your flight
    path, red-hot, for ~12 s, smashing anything it crosses; RT changes its radius. An elemental
    crystal does NOT do this.
10. **Ball and chain look**: the ball is a dark body with a bright rim in your domain colour (no
    ring round it); its studs flash as it rolls, and stay legible (no backwards strobe) when white
    hot; it brightens toward smash speed, turns red and blooms when hot.
    The chain reads as a round iron tube with a domain-coloured rim, brighter while taut, easing to
    lime at READY — no colour ever flickers; each change is a quick blend.

Tuning order if it feels wrong: `maxLen` vs the hull's turn rate (whether the ball swings or falls
slack), then `smashSpeed`, `explosionDiameter`, then `crack` / `reelSpinCap`. Camera: `spectateSpinRate`
(when it detaches), `aimYawRate` / `aimElevationDegrees` (how the aim feels), then `cameraZoomInRate`
(how lazily it comes home), `perfectWindowSeconds` / `perfectBoost` (the timing reward). Energy:
`energyDiameterPerUnit` / `energyMaxDiameterScale`. Vessel hits: `vesselHitPetals`, `magnetRange`. Mass Mode:
`massTrailSpacing` (trail density). Windmill: `windmillSeconds`, `windmillSpeed`.

## Follow-ups

### Refactor rows (2026-10-10 ship pass — rows only, nothing here was changed)

Each row carries its measurement; none blocks building on this branch.

- **Stale overrides inherited from the Squirrel (debt CREATED by cloning).** `check_generated_assets.py`
  (after `unity_refcompile`) reports **68** findings, all on `Thresher.prefab`, of which 29 are
  modifications on its nested `Skimmer.prefab` instance naming fields `Skimmer` no longer has
  (`NotifyNearbyBlockCount`, `alignWithTrail`, `blockImpactEffects.Array.data[0]`, …). A/B'd on
  2026-10-08 against an untouched Squirrel copy: identical list, so they are the Squirrel's too.
  Fix both hulls together through `PrefabDriftFixer` (never by hand-editing YAML); blocking question:
  is any of those overrides still load-bearing on the Squirrel after its field renames?
- **`lineMaterial` now serves only the skid trail (debt CREATED).** Since the chain got its own
  `chainMaterial` (round 5), `ThresherExecutor.lineMaterial` is the skid trail's material and the
  chain's fallback; the name and tooltip describe its old job. Rename to `skidMaterial` with
  `[FormerlySerializedAs("lineMaterial")]`. Inconsistency → a fix, not a report.
- **Two colour-blend rates for one cue (debt CREATED).** `ThresherConfigSO.colorBlendRate` drives the
  world colours and `ThresherHUDView.colorBlendRate` the HUD gauge; both are 15 and must be tuned
  together (the HUD view also keeps its own `chainColor`). Pass the rate through
  `ThresherHUDController` from the config instead. Measure first: `grep -n colorBlendRate` → exactly
  two serialized definitions today.
- **The hook-on is a 91° velocity snap in the SOLVER (debt MADE VISIBLE).** The camera cap (§5a)
  hides it from the camera, but the hull still turns onto the orbit's tangent within a few frames
  (`pivotFacingRate` 25/s), and a ship flying radially away from the ball has its heading replaced
  outright. Whether hooking on should instead blend the velocity over ~0.2 s is a DESIGN call (it
  changes where the orbit starts), not a cleanup. Measured by the scratch driver described in vessel
  rule 43: 91.4°/frame reeled in, 67.3° let out.
- **`ThresherCameraFraming` lives in `ThresherChainSolver.cs` (minor).** It is there so the
  harness compiles it from its shipped path with no new wiring; 9 call sites, all in the executor.
  Split it into its own file only together with a `run.sh` change.
- **Other hard-attached hulls with hull-driven snaps (debt WALKED PAST, unmeasured).** Every vessel
  camera is `FixedCamera`; any transformer that rotates the hull itself (a facing snap, a tug, a
  dash) whips the camera the same way. Not measured here — the measurement is the vessel rule 43
  scratch driver per transformer that overrides `RotateShip`
  (`grep -ln "override void RotateShip" -r Assets/_Scripts --include=*.cs`: 3 on 2026-10-10 —
  Thresher, SingleStick, Command), and `MaxFollowTurnRate` is the fix.

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
