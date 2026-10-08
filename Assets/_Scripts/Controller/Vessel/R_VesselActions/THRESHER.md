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
| **RT release** | | | Chain reels in fast (`reelIn`) conserving spin — **the crack**. The chain flickers **lime** (the palette's CTA colour) while releasing now would reach smash speed (READY). |
| **LT hold** (Plant) | `LeftStickAction` (2) | `OnlyLeftStickAction` (12) | The ball skids to a stop (`skidSeconds`, still destructive) and becomes a pivot; the ship swings round it, faster every lap. RT up winds you in and spins you faster; RT held lets you out. |
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
  through `ExplosionHelper`, 16 u diameter × the Charge scaling, `AffectSelfOverride` **on**
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
- **Bounce** reflects only the inward velocity component and puts the ball at the contact point.

Scale `k = gameCruise / sandboxCruise = 70 / 380 = 0.1842` on every speed and length; rates and
shares are unscaled, so every timing matches the sandbox. `gameCruise` 70 is the prefab's neutral
throttle target (`DefaultMinimumSpeed 15 + 0.5 × DefaultThrottleScaler 110`).

| Dial | Sandbox | Game | Meaning |
|---|---|---|---|
| `restLen` / `maxLen` | 120 / 340 | 22.1 / 62.6 u | chain reeled in / let out (× Space) |
| `payOut` / `reelIn` | 520 / 1100 /s | 95.8 / 202.6 u/s | winch rates |
| `reelSpinCap` | 1.6 | 1.6 | spin multiplier cap per change |
| `ballDrag` / `ballMass` / `tug` | 0.35 / 0.4 / 0.2 | same | drag (1/s), mass (× Mass), ship's share of the yank |
| `minShip` / `crack` | 0.6 / 0.6 | same | tug floor; crack share |
| `smashSpeed` / `whiteHotSpeed` | 700 / 1300 | 128.9 / 239.5 u/s | red-hot threshold; hottest (longest hit-stop, best plough) |
| `plough` / `ploughHot` / `crushKeep` | 0.9 / 0.97 / 0.85 | same | speed kept per kill: hot, white-hot, below smash |
| `bounce` | 0.6 | 0.6 | restitution off own-domain prisms |
| `ballR` / `maxBall` | 17 / 2600 | 3.13 u / 478.9 u/s | swept radius (× ∛Mass); speed cap |
| `skid` / `skidSeconds` / `lockKeep` | 7 / 0.3 s / 1 | same | plant brake, duration, speed kept |
| `lockSpin` / `lockMax` / `yank` | 220 /s² / 2.2 / 0.5 | 40.5 u/s² / 154 u/s / 0.5 | spin-up (× Time), cap (× Time), unlock yank |

Measured feel (`Tools/Build/thresher_chain_harness`, 60 Hz, rest levels; peak ball speed × smash):
steady turns reeled in 15/30/60/90/120 °/s → 0.62/0.74/0.94/1.09/1.21, let out → 0.66/0.89/1.27/1.55/0.39
(at 120 °/s the turn circle, ~33 u, is tighter than the let-out chain and the ball falls slack).
Wind up, turn at 120 °/s for T, snap back 0.1 s, release: T 0.3/0.5/0.8/1.2 s → 0.70/1.00/1.36/1.69.
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
- **Look** (runtime-built prototype art): a studded ball that rolls, coloured as in §2; the drawn
  chain IS the gameplay chain, iron-grey, flickering lime at READY; a gauge ring round the ball
  filling toward smash speed in the ball's colour; a skid trail while planting.
- **HUD** (`ThresherHUDController` / `ThresherHUDView`, `ThresherHUDVariant.prefab`, a variant of
  `VesselHUDPrefab`): the four-icon row Wrecking Ball · Heavy Iron · Winch · Plant
  (`Tools/Build/author_thresher_icon_placeholders.py`, `--check`), a **ball-heat gauge** on the
  Charge card in the ball's colour and a **chain-out gauge** on the Space card that turns lime at
  READY. Petal bars are authored (transplanted from the Squirrel's variant).
- **Camera**: `ThresherCameraSettingsSO` at `(0, 8, -55)` so the reeled-in ball is in front of the lens.

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
| `Tests/Editor/ThresherChainSolverTests.cs`, `Tools/Build/thresher_chain_harness/` | 28 tests; offline runner + feel table |

Registered in `Vessel Prefab Container`, `DefaultNetworkPrefabs` (own `GlobalObjectIdHash`
1887398589), `ToyVesselRoster.Default`, `CrystalHullFusionConfig` (reuses the Squirrel's bakes —
same hull mesh) and `SO_Classlist_All` / `_Classes`. Still the Squirrel's: the model, the skimmer
prefab, `DriftAudioController`, jets and tails.

## 9. Known weaknesses

- A turn throws the ball outward, so hits land on the side away from the turn.
- Circling with the chain out keeps the ball hot without much skill (1.27–1.55× smash at 60–90 °/s).
- READY underestimates during fast snaps (it projects the current spin through a full reel and
  ignores the snap crack).

## In-editor verification

1. **Toy Box > Vessel Changer → Thresher.** The hull swaps in with a ball 22 u behind on a chain, in
   front of the camera; the HUD shows the four Thresher icons and petal bars. Console clean.
2. **Slow ball**: tow gently into a RIVAL trail — prisms are destroyed, no explosion, the ball is in
   your domain colour. Into your OWN older trail — the ball bounces off, nothing breaks.
3. **Whip**: hold RT ~1 s, turn hard ~1 s, snap back briefly, release. The chain flickers lime first;
   on release the ball goes red, destroys everything including your own trail, each hit a blast
   (string, not per prism), first hit with a hit-stop, debris along its path, `x2, x3…` counter.
4. **Chain**: fly so the taut chain sweeps through rival prisms — they are sliced; your own are not;
   shielded rival prisms are skipped.
5. **Plant**: hold LT — skid (trail), orbit, spin-up; RT up winds in. Release: fly off on the tangent,
   ball yanked after you. Planting straight at the ball still orbits.
6. **Upgrades** (FrogletTools element tools or crystals to L5): Charge — hot ball lit, blasts spare
   your trail, bounces off it; Mass — a hot ball keeps its speed through a row; Space — chain cuts
   slack and pops shields; Time — releasing a slow orbit still throws a red ball.
7. **Other vessels**: Squirrel, Dolphin, Scarab fly unchanged.
8. **MPPM two clients**: the guest sees the host's ball and chain; a host hit-stop does not freeze
   the guest.

Tuning order if it feels wrong: `maxLen` vs the hull's turn rate (whether the ball swings or falls
slack), then `smashSpeed`, `explosionDiameter`, then `crack` / `reelSpinCap`.

## Follow-ups

- **Art and sound**: real ball/chain art and icons; the six FMOD events.
- **Hull**: its own model, skimmer and jets (still the Squirrel's).
- **Virtual (swarm) prisms** are not swept by the ball or chain.
- **Sticks while planted** are ignored; tilting the orbit plane is the obvious next verb.
- **AI**: no AI abilities are authored (both triggers are holds).
- **Random spawns**: `VesselSpawner.SpawnShip(Random)` can now hand out the Thresher.
- **Gibbon id clash**: the Gibbon branch claims `VesselClassType = 13` (the Butterfly's); it needs 15.
- **Pre-existing debt walked past (not fixed here)**: `R_VesselElementStatsHandler.cs` declares
  `R_ShipElementStatsHandler` and `SkimmerBoostPrismEffect.cs` declares `SkimmerBoostPrismEffectSO`
  — `check_generated_assets.py` reports both as "script can not be loaded" on every vessel prefab
  and on the Squirrel's live boost asset. The Thresher prefab's remaining audit findings are its
  inherited Squirrel ones (stale nested-prefab overrides, `ShowTopMostFoldoutHeaderGroup` keys).
