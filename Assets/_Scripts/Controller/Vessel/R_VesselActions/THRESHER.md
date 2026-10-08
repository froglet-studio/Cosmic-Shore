# Thresher — the wrecking-ball prototype

**Status: PROTOTYPE, flyable from Toy Box > Vessel Changer, not yet opened in Unity by anyone.**
`VesselClassType.Thresher = 14`. A small, nimble two-stick ship towing a heavy wrecking ball on a
chain. Turn hard and the ball whips round; snap the turn back and wind the chain in, and it cracks
through prisms. Damage scales with how fast the **ball** is moving at contact, never the ship.

The mechanic was prototyped and tuned in a 2D browser sandbox (cruise 380 u/s). Every dial here is
authored in that sandbox's units and scaled to this game by one factor (§3), so the two can be read
side by side.

## 1. Controls (two-thumb flyer)

| Input | Gamepad event | Touch event | What it does |
|---|---|---|---|
| Sticks | — | — | Normal flight, exactly a Squirrel's (vector flight model, 120°/s pitch/yaw). Turn one way and snap back to whip; pitch and yaw snaps both work. |
| **RT hold** | `RightStickAction` (1) | `OnlyRightStickAction` (11) | Let chain out (`payOut`), up to `maxLen`: longer reach, slower whip. |
| **RT release** | | | Chain reels in fast (`reelIn`) conserving spin — **the crack**. The chain flickers gold (READY) while releasing now would smash. |
| **LT hold** | `LeftStickAction` (2) | `OnlyLeftStickAction` (12) | The ball skids to a stop (`skidSeconds`, still smashing) and becomes a pivot; the ship swings round it, faster every lap. RT up winds you in and spins you faster; RT held lets you out. |
| **LT release** | | | Fly off along the orbit's tangent; the ball is yanked after you (`yank`). |

The bindings are the Squirrel's slots (the Squirrel's drift is on LT and its tube on RT), so no input
plumbing changed. Keyboard: Left/Right Shift; one-thumb mouse: LMB/RMB (they raise the same events).
The triggers are read as press/release EDGES only — the analog depth is not used.

## 2. Physics (`ThresherChainSolver`, pure C#)

- **Ball**: a point mass with drag (`ballDrag`), integrated freely each frame.
- **Rope**: a position-based **max-distance** constraint between ship and ball. When the ball
  overshoots the chain length the overshoot is split by inverse mass with the ship's mass taken
  as 1 — the ship takes `tug × m/(1+m)` of it (0.057 at the defaults), the ball the rest — and each
  body's velocity is what its corrected position implies. A taut chain therefore cancels the ball's
  outward speed and keeps its tangential speed: turning the ship whips the ball round.
- **Floor**: the rope can never drag the ship below `minShip ×` its current throttle target (the
  floor follows the pilot's own throttle; a pilot already slower than that by choice is not sped up).
- **Reel in** (RT up): `v_tangential × r` is conserved, the multiplier capped at `reelSpinCap` per
  change (per frame). **Pay out** (RT held) spends spin by `sqrt(oldL/newL)`. A slack chain is not
  touching the ball, so neither applies to it.
- **Crack**: on the frame a SLACK chain (more than 2% short of its length) snaps taut, `crack` of
  the radial speed the rope just cancelled is thrown sideways along the ball's swing. A chain riding
  taut does not crack — it has to go slack first.
- **Lock** (LT): `Plant()` adds `skid` to the ball's drag for `skidSeconds`, then the ball is a fixed
  pivot. The ship orbits at `max(|v| × lockKeep, minShip × cruise)` — the floor is what keeps a
  pilot who plants flying straight AT the ball from stalling — and spins up by `lockSpin`/s until
  `lockMax × cruise`. Reel conservation may carry the orbit past `lockMax` (that is the wind-in
  payoff); `maxBall` caps it. `Release()` sets the ball's velocity to `yank ×` the ship's.
- **Cap**: ball speed never exceeds `maxBall`.
- **Smash rules**: at or above `smashSpeed` a hit smashes and the ball keeps `plough` of its speed,
  rising to `ploughHot` when white-hot (`whiteHotSpeed`). Below, it **chips** (keeps `chipKeep`) and
  `chipsToBreak` chips break a prism.

### How the flight model is reached

`ThresherVesselTransformer : VesselTransformer` overrides **seams**, never `MoveShip`, so the danger
slow, knockback, the speed tunnel and every fleet-wide change still reach it. Two seams were added
to `VesselTransformer` for it, both **default-off and bit-identical for every other vessel**
(adding `Vector3.zero` and returning the extracted drift expression unchanged):

| Seam | Default | Thresher |
|---|---|---|
| `ComputeExternalAcceleration(velocity, dt)` | `Vector3.zero` | Steps the chain; returns `shipVelocity_after_rope − velocity`. While towing, the hull is ALSO turned by the same rotation — grip snaps momentum onto the nose every frame, so a tug that did not turn the nose would be erased next frame. |
| `NoseConvergence(dt)` | the drift-grip expression it was extracted from | 0 while pivoting (the solver owns the velocity) |

It also overrides `ComputeNoseAcceleration` (0 while pivoting — no thrust on the hook), `RotateShip`
(face the orbit tangent by parallel transport, and snap onto the velocity on the release frame so the
fling is not bent inward by the nose's lag) and `Update` (skip the frame during a multiplayer
hit-stop). These are the same two seams the Gibbon prototype (`cece/hopeful-bardeen-rmqhmd`, commit
720507e) introduced, re-cut here; that branch is unmerged, so whichever lands second should adopt the
other's copy verbatim.

## 3. Dials (`ThresherConfig.asset` → `ThresherDials`)

Scale factor `k = gameCruise / sandboxCruise = 70 / 380 = 0.1842`. Every speed and length is ×k;
rates (1/s) and shares are unscaled, which leaves every TIME in the mechanic — how long a reel
takes, how fast the ball cools, the skid — exactly as it was in the sandbox. `gameCruise` 70 is the
prefab's throttle target at neutral sticks (`DefaultMinimumSpeed 15 + 0.5 × DefaultThrottleScaler
110`, the Gibbon's retune of the same Squirrel hull); the top cruise is 125.

| Dial | Sandbox | Game | Meaning |
|---|---|---|---|
| `restLen` | 120 | 22.1 u | chain reeled in |
| `maxLen` | 340 | 62.6 u | chain let out |
| `payOut` | 520/s | 95.8 u/s | let-out rate (RT held) |
| `reelIn` | 1100/s | 202.6 u/s | reel-in rate (RT up) |
| `reelSpinCap` | 1.6 | 1.6 | spin multiplier cap per change |
| `ballDrag` | 0.35 | 0.35 /s | ball drag |
| `ballMass` | 0.4 | 0.4 | ball mass, ship = 1 |
| `tug` | 0.2 | 0.2 | share of the mass-weighted yank the ship feels |
| `minShip` | 0.6 | 0.6 × throttle target | tug floor |
| `crack` | 0.6 | 0.6 | share of cancelled radial speed thrown sideways |
| `smashSpeed` | 700 | 128.9 u/s | smash threshold |
| `whiteHotSpeed` | 1300 | 239.5 u/s | full glow, longest hit-stop, best plough (not in the sandbox spec — new) |
| `plough` / `ploughHot` | 0.9 / 0.97 | same | speed kept per smash, at smash speed / white-hot |
| `chipKeep` / `chipsToBreak` | 0.8 / 3 | same | speed kept per chip; chips that break a prism (`chipKeep` is new) |
| `ballR` | 17 | 3.13 u | swept-sphere radius (the drawn ball is ×`ballVisualScale` 1.25) |
| `skid` / `skidSeconds` | 7 / 0.3 s | same | plant brake; skid duration |
| `lockKeep` | 1 | 1 | speed kept on hooking on |
| `lockSpin` | 220/s² | 40.5 u/s² | orbit spin-up |
| `lockMax` | 2.2 | 154 u/s | spin-up stops at this × cruise |
| `yank` | 0.5 | 0.5 | ball gets this × ship velocity on unlock |
| `maxBall` | 2600 | 478.9 u/s | ball speed cap |

Feel dials on the same asset: hit-stop 30 → 90 ms by heat (`hitStopMin/MaxSeconds`), solo time
scale 0.05, shake 0.35 → 1.8 for 0.22 s, haptic floor 0.45, debris restitution 1/3 with a 200 u/s
ceiling, combo grace 0.4 s, READY margin 1.05 × smash, flicker 14 Hz, and the colours.

### Measured feel (from `Tools/Build/thresher_chain_harness`, 60 Hz, defaults)

Peak ball speed as a multiple of smash speed:

| steady turn, 4 s | reeled in | let out |
|---|---|---|
| 15°/s | 0.62 | 0.66 |
| 30°/s | 0.74 | 0.89 |
| 60°/s | 0.94 | **1.27** |
| 90°/s | **1.09** | **1.55** |
| 120°/s | **1.21** | 0.39 (turn circle tighter than the chain: the ball sits inside it) |

| wind-up, turn T at 120°/s, snap back 0.1 s, release RT | before reel | after reel |
|---|---|---|
| T = 0.3 s | 0.38 | 0.70 |
| T = 0.5 s | 0.38 | 1.00 |
| T = 0.8 s | 0.38 | **1.36** |
| T = 1.2 s | 0.49 | **1.69** |
| same turn, keep turning, release | 0.38 | **1.35** |

Lock from cruise, orbit speed × cruise (RT up / RT held): 0.5 s 1.02/1.11, 1 s 1.31/1.40,
2 s 1.88/1.98, 3 s 2.19/2.20.

**What the numbers say, before anyone flies it:**
1. **The reel is the crack, not the snap.** In 3D at this hull's turn rate the snap alone never
   reached smash speed in the grid swept; the release did. Long snaps (≥0.3 s) after a short turn
   land well under smash — a timing window the pilot has to learn. `SnapWithoutReel_StaysUnder…`
   pins this so a future tune that makes the snap itself smash is a visible decision.
2. **Circling with the chain out keeps the ball hot without much skill** (60–90°/s → 1.27–1.55×) —
   the known weakness, confirmed. A hard turn on a REELED chain also smashes from 90°/s.
3. At full stick (120°/s) the turn circle (r ≈ 33 u) is smaller than the let-out chain (62.6 u) and
   the ball falls slack into the middle. The sandbox's turn-radius-to-chain ratio is unknown; if it
   flew wider, `maxLen` or the hull's turn rate is the first thing to tune.

## 4. Smashing, feedback, look (`ThresherExecutor`)

- **Sweep, never colliders**: every frame the ball's path (start → end of the step) is swept as a
  sphere through `PrismSpatialIndex.QuerySegment` and refined against each prism's bounding sphere,
  nearest-first — the Sparrow projectile's sweep. Colliders would tunnel at speed, and prism
  collider LOD switches them off away from vessels.
- **Damage**: `Prism.Damage(ballVelocity × 1/3, domain, pilot, debrisSpeedLimit: 200)` — the
  fleet's proportional convention. This is the Rhino sword's `ContactVelocity` idea at its core: a
  point mass's contact velocity is its own velocity, so debris flies along the ball's path. Shields
  pop and super-shields hold exactly as for every other hull; each prism is hit once per contact,
  so a shielded one takes a second pass.
- **Own mass**: the fleet rule `SelfTrailContactConfigSO.SuppressesSkimContact` (the pilot's own
  prisms laid within the grace window are ignored). A sub-smash ball never chips its own DOMAIN's
  mass, so a lazy tow does not grind your team's trail away; a smash hits anything.
- **Hit-stop**: one per whip (on the combo's first smash), 30 → 90 ms by heat, local pilot only.
  SOLO sessions drop `Time.timeScale` to 0.05 (the AstroLeague ball's pattern, restoring to
  constants so a pause is never clobbered). MULTIPLAYER never touches `Time.timeScale`; the hull and
  its ball freeze instead (`ThresherVesselTransformer.Update` skips the frame).
- **Shake** (`CustomCameraController.Shake`) scaled by heat, full on the first hit of a whip and
  0.6× after; small on a chip. **Haptic**: `HapticController.PlaySkim(lerp(0.45, 1, heat))` — the
  reward feel; no new feel was added (Docs/HAPTICS.md).
- **Audio**: `smashEvent` (heavy thud), `chipEvent`, `plantEvent` (clunk), `yankEvent` — all
  **shipped EMPTY** per the LOCKED FMOD convention. Nothing plays until a sound designer wires them.
- **Combo**: one world-space counter `x2, x3, …` above the ball per whip, popping on each hit; a whip
  ends after the ball has stayed below smash speed for `comboGraceSeconds`.
- **Look** (runtime-built, prototype art, every peer draws its own): an iron sphere with six studs
  that rolls with its speed and glows iron → ember → gold at smash speed → white-hot (via a
  MaterialPropertyBlock); a 12-link cosmetic verlet chain pinned to the hull and the ball that goes
  slack and flickers gold for READY; a gauge ring around the ball that fills clockwise toward smash
  speed; a gold skid trail while planting.
- **Camera**: `ThresherCameraSettingsSO` sits at `(0, 8, -55)` (the Squirrel's is `-17`; the asset
  authors no `mode`, so `FixedCamera` places the lens at exactly this offset), so the
  reeled-in ball (22 u back) is in front of the lens. At full let-out the ball trails past the
  camera when flying straight and swings into view on a turn.

## 5. Networking

None of its own, the Gibbon's shape. Press and release round-trip through `R_VesselActionHandler`,
so both edges run on every peer, and every peer runs the same chain from the same inputs and the
hull's replicated motion. A hull whose transformer is switched off (a remote replica after a pilot
swap) is stepped by the executor's `LateUpdate` from its observed motion so its ball never freezes.
Peers can disagree on a marginal smash by a frame of drift. Untested in MPPM.

## 6. Files

| File | What |
|---|---|
| `Controller/Vessel/ThresherChainSolver.cs` | Pure physics + `ThresherDials` (the defaults, one source) |
| `Controller/Vessel/ThresherVesselTransformer.cs` | Seam overrides: tug, pivot, facing, hit-stop freeze |
| `Controller/Vessel/VesselTransformer.cs` | +`ComputeExternalAcceleration`, +`NoseConvergence` (default-off) |
| `R_VesselActions/Executors/ThresherExecutor.cs` | Sweep, smash, chip, feedback, visuals |
| `R_VesselActions/Data Containers/ThresherActionSO.cs` | Trigger routing (`Winch` / `Plant`) |
| `R_VesselActions/Data Containers/ThresherConfigSO.cs` | Every dial |
| `_SO_Assets/VesselActions/Thresher/*.asset` | `ThresherConfig`, `ThresherWinchAction`, `ThresherPlantAction` |
| `_Prefabs/Spacevessels/Thresher.prefab` | Squirrel clone (§7) |
| `_SO_Assets/Camera/ThresherCameraSettingsSO.asset` | Pulled-back chase camera |
| `Resources/ElementalAbilityMaps/Thresher.asset` | Four OPEN design slots |
| `Tests/Editor/ThresherChainSolverTests.cs` | 23 cases incl. the four specified |
| `Tools/Build/thresher_chain_harness/` | Runs the shipped solver + tests offline and prints §3's tables |
| `Tools/Build/check_network_prefab_hashes.py` | Gate from the Gibbon branch: no two prefabs share a `GlobalObjectIdHash` |

## 7. The prefab

`Thresher.prefab` is a YAML clone of `Squirrel.prefab` with: the transformer script swapped to
`ThresherVesselTransformer` (`singleTriggerDrift` off, cruise 15 + 110 × XDiff, `thresher` wired); the
Squirrel tube executor's GameObject repurposed as `ThresherExecutor` (the registry lists only it); the
drift-trail executor deleted; both override lists (gamepad 2/1, touch 12/11) rebound to the Thresher
actions; `vesselType 14`, `_name Thresher`; a fresh `GlobalObjectIdHash` (1887398589, computed as
`XXHash32("GlobalObjectId_V1-1-<guid>-<NetworkObject fileID>-0")`, the formula verified by
reproducing Squirrel's 2256742461) with `InScenePlacedSourceGlobalObjectIdHash` zeroed. Registered
in `Vessel Prefab Container.asset`, `DefaultNetworkPrefabs.asset` and `ToyVesselRoster.Default`.

**Crystal hull fusion**: `Resources/CrystalHullFusionConfig.asset` carries four `vessel: 14` entries
that are copies of the Squirrel's, pointing at the Squirrel's bakes with identical tuning. That is a
valid bake, not a placeholder: `CrystalHullFusionBakeSO.Matches` keys on the hull MESH and the
entry's `tileFill`/`surfaceLift`, and the Thresher draws the Squirrel's mesh. Re-running FrogletTools >
Vessels > Bake Crystal Hull Fusions writes `Thresher_*_HullFusionBake.asset` and repoints them, which is
equally correct. (`CrystalHullFusionConfigTests` requires four entries for every hull but the
Butterfly, so a new class with none fails it.)

**Still the Squirrel's**: the model, the skimmer and its effects, the HUD
(`SquirrelVesselHUDController` + `SquirrelHUDVariant`, whose icons describe drift and the tube — both
gone; its tube lookup is null-guarded), `SquirrelVesselTelemetry`, `DriftAudioController`, jets/tails.

## 8. Known weaknesses (from the sandbox, confirmed or not)

- **Hits land on the side away from the turn.** A turn throws the ball outward, which takes learning.
- **Circling with the chain out keeps the ball hot without much skill** — confirmed by §3 (1.27–1.55×
  at 60–90°/s).
- **READY underestimates during fast snaps.** `PredictReelCrackSpeed` projects the current relative
  tangential speed through a full reel and ignores the snap crack and the ship's ongoing turn.

## In-editor verification

1. **Toy Box > Vessel Changer → Thresher.** Expected: the hull swaps in with a dark ball 22 u behind on
   a chain, in front of the camera. Console clean.
2. **Lazy flying** (gentle turns, RT up): ball stays iron-dark, gauge ring under a quarter, no
   smashes; at most chips on rival mass. Own trail untouched.
3. **Whip**: hold RT ~1 s (chain lengthens), turn hard ~1 s, snap back briefly, release RT. Expected:
   the chain flickers gold before release when READY; on release the ball goes gold/white and smashes
   through prisms with a hit-stop on the first, shake, a growing `x2, x3` counter, and debris flying
   along the ball's path.
4. **Shielded prism**: first pass pops the shield, the second destroys it. Super-shield holds.
5. **Plant**: hold LT. Ball skids ~0.3 s (gold skid trail), then the ship orbits it, speeding up;
   with RT up it winds in tighter and faster; hold RT and it lets out. Release LT: fly off along the
   tangent with the ball yanked after you. The orbit never stalls, including planting while flying
   straight at the ball.
6. **Other vessels**: fly the Squirrel, Dolphin and Scarab (the vector-model hulls) — no change
   (the seams are identity by construction; this is the check that they are in practice).
7. **MPPM, two clients**: the guest sees the host's ball and chain; hit-stop on the host does not
   freeze the guest's world.

Tuning order if it feels wrong: `maxLen` and the hull's turn rate (the ratio decides whether the
ball swings or falls slack), then `smashSpeed`, then `crack` / `reelSpinCap`.

## Follow-ups

- **Element design (gate)**: four open slots. Candidate parameters, not approved: Mass → `ballMass`
  (heavier ball, harder tug, bigger plough), Space → `maxLen`/`ballR` (reach), Time → `reelIn` /
  `lockSpin` (rate), Charge → `smashSpeed`/`plough` (threat). Needs Garrett's call.
- **HUD**: a Thresher HUD (gauge = ball speed / smash, READY pip, combo) to replace the Squirrel's.
- **Art/audio**: real ball/chain art; the four FMOD events.
- **Virtual (swarm) prisms** are not swept (`QuerySegmentVirtualIds` + `ResolvePrism`), so the ball
  passes through swarm fauna members.
- **Sticks while planted** are ignored; tilting the orbit plane with the sticks is the obvious next
  verb.
- **AI**: no AI abilities are authored (both triggers are holds).
- **Random spawns**: `VesselSpawner.SpawnShip(Random)` picks from every enum member with a prefab, so
  the single-player Random path can now hand out the Thresher prototype (as it would any new hull).
- **Debt walked past (not fixed here)**: the Squirrel prefab this was cloned from carries 152
  `check_generated_assets.py` findings (stale nested-prefab overrides, unresolved script guids,
  `R_VesselElementStatsHandler.cs` declaring `R_ShipElementStatsHandler` — on all 14 vessel
  prefabs), and `SquirrelCameraSettingsSO` carries seven keys `CameraSettingsSO` no longer has.
  Measured by auditing an untouched Squirrel copy in a base worktree: the Thresher's 152 are
  byte-identical, so none were introduced here. The camera copy was cleaned; the prefab was not.
- **Gibbon id clash**: the Gibbon branch still claims `VesselClassType = 13`, which the Butterfly
  took; it will need 15 (or whatever is next) when it is rebased.
