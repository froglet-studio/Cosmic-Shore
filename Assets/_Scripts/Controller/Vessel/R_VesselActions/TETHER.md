# TETHER — a two-thumb flyer that runs on light tethers

`VesselClassType.Tether = 14`. **Prototype.** It plants its own anchor prisms ahead and keeps hooking
onto them, hand over hand; the pulls alternate sides, so it surges along a roughly straight line. A
held trigger fires a second, long tether to a distant prism and swings you round it in a big arc.
Every tether is a **light sword**: it cuts the hostile prisms it sweeps through.

It wears the Squirrel's hull, HUD, skimmer and effect containers (it is a clone, §9) — so it also
steals by skimming, jousts crystals and lays a Squirrel wake exactly as the Squirrel does. What is
new is everything on the triggers and everything the tethers do.

## 1. Controls

| Input | What it does |
|---|---|
| **Sticks** | Normal two-stick flight, roll included. **Roll sets the search plane** (§2). |
| **No trigger** | Auto-tethers run the straight-line flight (§3). |
| **LT / RT (hold)** | Fire the long tether to the best target on that side of the search plane and swing round it (§4). Holding before anything is in reach hooks the moment something is. |
| **Stick up while swinging** | Reel in — spins you faster. Stick down lets rope out. ("Up" is the nose-up command, so it honours InvertY.) |
| **Release** | Fling off along the tangent; past a half turn, with a boost. |
| Other trigger mid-swing | Lets go of the current swing (full fling) and arms the other side — alternate triggers to chain. |

Bindings follow the Squirrel's: `LeftStickAction`(2) / `RightStickAction`(1) on gamepad (keyboard and
both mouse schemes read this map), `OnlyLeftStickAction`(12) / `OnlyRightStickAction`(11) on touch.
The AI binds nothing (`AIPilot.abilities` is empty), so an AI Tether flies on auto-tethers only.

While the long tether holds: **pitch is the reel and yaw is the rope's** — the nose follows the
swing's tangent — but **roll stays live**, because roll is how you pick the plane of the next hook.
Auto-tethers pause while the long tether holds and resume with a fresh anchor on release.

## 2. The search plane

Both kinds of tether only look for anchors near the plane through the hull's **forward and right**
axes (normal = the hull's up). Auto-anchors are planted IN that plane; long-tether targets must sit
within `planeToleranceDegrees` (20°) of it, on the trigger's side (`TetherMath.TryScoreHook`).
Rolling the hull tilts the plane — roll 90° and the right trigger reaches straight "up". That is the
whole aiming system: no reticle, no extra input.

## 3. Straight-line flight on its own anchors (NEW — starting guesses, tune these first)

Not prototyped in the sandbox; every number is a dial on `TetherConfig.asset`.

- **Planting.** Every `anchorPeriod` (0.35 s) an anchor prism is planted `anchorLead` × speed
  (0.8 s) ahead, `anchorAngle` (20°) off the nose in the search plane, alternating sides
  (`TetherMath.AnchorPoint`). It is a **real prism of this hull's own trail**, laid through
  `VesselPrismController.LayAt` — the wake's own lay (same pool, owner, team, clearance wait,
  danger/shield rules, `OnBlockSpawned`) — into the controller's third ribbon, `AnchorTrail`, so a
  rider of the wake is never dragged zig-zagging through the anchors. Anchors stay in the world.
  No anchors below `minPlantSpeed` (3 u/s) or while the spawner is stopped / pen-up / tier-held.
- **Pulling.** Each anchor gets a short ELASTIC tether (`TetherMath.ElasticPull`): a spring that
  pulls only past its rest length (`autoRestFraction` × the planted distance, so a fresh line yanks
  at once), with damping on **stretching only** — a symmetric damper would brake the hull's own
  closing speed, which is cruise. Released when the hull draws level with the anchor
  (`PassedAbeam`).
- **Holding a little above cruise.** The pulls' speed GAIN is bounded at `autoSpeedTarget` (1.15)
  × the throttle's own cruise target (`TetherMath.LimitSpeedGain` — gain only, it never brakes a
  hull that is faster from a swing). The flight model's ordinary overspeed decay balances them.
- **Steering still works.** Anchors follow the nose. While a line is taut momentum re-aligns with the
  nose at `autoNoseGrip` (6/s) instead of snapping, so the alternating pulls are FELT as sway.

Measured (`TetherMathTests`, the shipped defaults, 5 s hands-off at cruise):

| | Result |
|---|---|
| Heading, shipped grip (6/s) | ≤ 0.63° off |
| Heading, **no grip at all** | ≤ 1.52° — the alternation alone holds it |
| Heading, no grip, every anchor on ONE side (negative control) | 18.8° and climbing |
| Speed | mean 1.135× cruise, surging 1.10× ↔ 1.15× on the plant beat |

## 4. The long tether (prototyped and tuned — behaviour kept)

Sandbox cruise was 380 u/s; this hull's cruise is **80 u/s** (neutral sticks:
`DefaultMinimumSpeed 20 + 0.5 × DefaultThrottleScaler 120`; 140 flat out, 20 pinched). Every speed
and length is the sandbox value × **80/380 = 0.2105**; rates, angles, fractions and seconds carry
over unchanged.

- **Hook choice** (`TetherMath.TryScoreHook`): within `range` (93; sandbox 440), not closer than
  `minHook` (27; sandbox 130), not behind, on the trigger's side, near the plane; ranked by side,
  ahead-ness, distance and plane. The window moves OUTWARD with speed
  (`WindowScale = 1 + windowGrowth × (v/cruise − 1)`, growth 0.5) so fast arcs stay readable.
  Prisms of any domain and crystals (`hookCrystals`) count; this hull's own LIVE auto-anchors do
  not (they are its propulsion, not targets). Ghost rings mark the best target on each side.
- **On hook** (`TetherMath.HookVelocity`): the same speed, redirected onto the tangent — hooking
  never costs speed. Then the line is RIGID: each frame the hull flies along the chord of its arc
  (`ArcVelocity`, the tangent rotated by half the swept angle, speed kept exactly) and is put back on
  the rope after integrating (`ConstrainToRope`). Outward radial velocity is what the constraint
  removes; inward is the reel.
- **Reeling** (`TetherMath.ReelLength`): stick up reels at `reelRate` (40; sandbox 190) plus
  `autoReel` (7.4; sandbox 35) always, down to `minLength` (19; sandbox 90). Angular momentum
  `v·L` is conserved, so reeling spins you up — and the reel **stops at the spin limit** `maxSpin`
  (0.6 rev/s): `L ≥ √(v·L/ω_max)`. A floor stops a reel and never pays a line out (the Gibbon
  prototype's chatter bug, held by `ReelFloor_NeverPaysTheLineOut`).
- **Release** (`TetherMath.ReleaseSpeed`): past a half turn, +`releaseBoost` (20%) shrinking as
  `1 − (v/cap)²`. Nose and course snap to the exit velocity (`TetherVesselTransformer.Fling`), or
  grip would eat the speed. For `glide` (1.6 s) overspeed decays at `glideDecayFactor` (0.15×).
- **Cap** `maxSpeed` 358 u/s (sandbox 1700, ≈ 4.5× cruise).

Measured from cruise (80 u/s), hooked dead abeam:

| Hook at | Stick | Spin limit reached | Half turn | Exit (with boost) |
|---|---|---|---|---|
| 93 u | full up | 1.02 s, 2.09× | 1.37 s | **2.42×** |
| 93 u | none (auto reel) | 6.6 s | 2.82 s, 1.29× | 1.53× |
| 60 u | full up | 0.50 s, 1.68× | 1.03 s | 1.97× |
| 40 u | full up | 0.22 s, 1.37× | 0.88 s | 1.62× |

So one swing pays ~1× → ~2.1× (2.4× with the boost), not the cap — the brief's "1x to 2.2x". The
sandbox's lap times (22.8 s no hooks / 18.5 s hooking every turn) and its ~4× chained plateau
were not re-measured here; they need a course and a pilot.

## 5. Light swords

Every live tether, short or long, cuts what it sweeps (`TetherExecutor.CutAlong`) — the swept quad
between last frame's line and this frame's is sampled (up to `maxSweepSamples` segments) through
`PrismSpatialIndex.QuerySegment`, refined by each prism's bounding sphere (the
`Projectile.SweepPrismsAlong` recipe). The Rhino sword's rules (`RHINO_ENERGY_SWORD.md`):

- **Ungated.** No cooldown, no stance.
- **Hostile only.** Own-domain mass, this hull's live anchors and the hooked prism are never cut.
- **Shields:** a shielded prism loses its shield and survives (`Prism.Damage`'s own rule); a
  super-shielded prism BINDS (`AbsorbSuperShieldHit`) — the sword's behaviour with an un-energized
  blade, since a tether has no energy meter.
- **Enter-only contact**, like the sword's trigger: a prism already touching the beam last frame is
  not hit again, so a shield the beam just popped is not cut through by the same contact.
- **Contact velocity:** a tether is a rigid segment pivoting on its anchor, so a point `t` of the way
  from anchor (0) to hull (1) moves at `t × v_hull` (+ the anchor's own velocity at the far end, for
  a drifting crystal) — `TetherMath.RopePointVelocity`. It feeds the slice plane
  (`Cross(beam, v)`, falling back as the sword does) and the debris (`× restitution 1/3`,
  `debrisSpeedLimit 200`) exactly as `RhinoSkimmerDamagePrismEffectSO` does.

## 6. Looks and feel

All runtime `LineRenderer`s (`TetherVisuals`, the `SniperBeam` approach — `Sprites/Default`, one
shared material per session, a soft-edged procedural texture so a wide halo reads as glow). Colours
stay ≤ 1: gameplay bloom clamps at 0.5, so glow comes from WIDTH (`Docs/PALETTE.md`).

| Element | Where | Driven by |
|---|---|---|
| Bright core in a soft team-colour halo | every peer | domain signal colour; width + brightness rise with tension (auto: stretch; long: spin ÷ spin limit) |
| Auto-tethers thinner and calmer | every peer | `autoCoreWidth/autoHaloWidth/autoAlpha` vs the `long*` set |
| Sparks running along the beam on a slice | every peer | `TetherVisuals.Spark` |
| Bracket flash where a beam clamps on | every peer | `TetherVisuals.Bracket` |
| Retract with a flicker on release | every peer | automatic: a beam not drawn this frame retracts |
| Hook snap: flash along the beam, shake, haptic | flash everywhere; shake + haptic local pilot | scaled by the hook's yank (`TetherMath.HookYank01`); `HapticController.PlayBind` |
| Swept-arc indicator, gold at a half turn | local pilot | the swing's own path, drawn just inside the rope |
| Half-turn chime | everywhere it's audible | `halfTurnChimeEvent` |
| Dashed release line | local pilot | exit speed × `releaseLineSeconds`, gold once the boost is live |
| Auto-tether surge audio | everywhere it's audible | `autoSurgeEvent`, once per anchor |

**Audio ships EMPTY** (the LOCKED FMOD convention): `hookSnapEvent`, `releaseEvent`,
`halfTurnChimeEvent`, `autoSurgeEvent` on the `TetherExecutor` component. Each is silent until an
event is authored; there is no placeholder.

## 7. How it reaches the flight model — default-off seams, no fifth move step

`TetherVesselTransformer` overrides seams on `VesselTransformer`'s VECTOR model and has no
`MoveShip` of its own, so the danger-prism slow, knockback, the speed tunnel and external Course
writes all reach it. Three seams are new on `VesselTransformer`, ported from the Gibbon prototype
(`cece/hopeful-bardeen-rmqhmd`, 720507e) plus one of this branch's; **each defaults to the exact
expression it replaced, so every other vessel is bit-identical**:

| Seam | Default | Tether |
|---|---|---|
| `ComputeExternalAcceleration(v, dt)` | `Vector3.zero` (adding a float zero is an identity) | the tethers' Δv |
| `NoseConvergence(dt)` | the drift expression, verbatim | 0 hooked; `autoNoseGrip` while auto-taut |
| `PostVectorIntegrate(dt)` *(new here)* | no-op | put the hull back on the rigid rope |
| `AnalogTriggerDrift` | `true` (GetTriggerSum unchanged) | `false` — both triggers are the long tether |

It also overrides the already-virtual `ComputeNoseAcceleration` (off while hooked; overspeed decay
× 0.15 in the glide), `ShapeSpeed` (the cap) and `RotateShip` (nose follows the tangent while
hooked, roll live).

`VesselPrismController` gains `LayAt` / `CanLay` / `AnchorTrail`: `CreateBlock`'s tail moved
unchanged into a private `Lay` that both call, so the wake's lay is byte-for-byte what it was.

## 8. Networking

No network code of its own, by the same argument trail prisms make:

- Trigger press/release replicate through `R_VesselActionHandler` (ServerRpc → ClientRpc), so
  `Begin`/`Commit` run on every peer.
- Planting, hooking, the abeam release, cutting and drawing run on **every peer** from the hull's
  replicated pose and speed. Trail prisms are not network objects; every peer lays every vessel's
  trail locally, and the anchors are trail prisms.
- The forces run only where the transformer runs (the machine flying the hull); everyone else sees
  them as replicated motion.
- **Known divergence, shared with the Rhino sword:** cuts resolve per peer against each peer's own
  prism copies, and a long-tether hook is chosen per peer from its own copies — two peers whose
  interpolated hull differs by a frame can disagree about a prism at the very edge of a beam or a
  hook window. Fix if it matters: send the chosen anchor in the press RPC.

## 9. Files

| File | Role |
|---|---|
| `Controller/Vessel/TetherMath.cs` | All tether maths as pure functions — hook scoring, rigid rope, reel, release, auto spring, cut velocity. |
| `Controller/Vessel/AutoTetherRig.cs` | The auto-tethers: cadence, sides, attach, abeam release, tension, force. Plain C#. |
| `Controller/Vessel/LongTetherRope.cs` | One rigid swing: hook, track (swept angle), step (reel + arc). Plain C#. |
| `Controller/Vessel/TetherVesselTransformer.cs` | The seams' overrides. No move step. |
| `R_VesselActions/Executors/TetherExecutor.cs` | Planting, hooking, release, cutting, feedback; the flight model's door. |
| `R_VesselActions/Executors/TetherVisuals.cs` | Beams, sparks, brackets, retract, ghosts, swing arc, release line. |
| `R_VesselActions/Data Containers/TetherLongLineActionSO.cs` | One trigger: `StartAction → Begin`, `StopAction → Commit` (the Squirrel tube's shape). |
| `R_VesselActions/Data Containers/TetherConfigSO.cs` | Every dial. |
| `_SO_Assets/VesselActions/Tether/` | `TetherConfig`, `TetherLongLineLeft` (side −1), `TetherLongLineRight` (side +1). |
| `_Prefabs/Spacevessels/Tether.prefab` | A clone of `Squirrel.prefab` (the dual-stick hull on the vector model): transformer swapped, `TetherExecutor` added under `ShipActions` and made the registry's only executor, triggers rebound, cruise 80, `vesselType 14`, own `GlobalObjectIdHash`. |
| `Resources/ElementalAbilityMaps/Tether.asset` | Four OPEN design slots (§10). |
| `Tests/Editor/TetherMathTests.cs` | 12 tests incl. 3 negative controls. |
| `Tools/Build/tether_harness/run.sh` | Compiles the three shipped maths files + the shipped tests against Unity-shaped stubs and RUNS every `[Test]`, headless. |
| `Tools/Build/check_network_prefab_hashes.py` | Fails on two prefabs sharing a `GlobalObjectIdHash` (from the Gibbon branch, 1c2c997). |

Registered in `Vessel Prefab Container.asset`, `DefaultNetworkPrefabs.asset` and
`ToyVesselRoster.Default`, so it is offered by **Toy Box ▸ Vessel Changer**.

## 10. Tuning knobs (`TetherConfig.asset`)

| Knob | Ships | Sandbox | What it does |
|---|---|---|---|
| `cruiseSpeed` | 80 | 380 | Reference cruise; the window starts moving out above it |
| `planeToleranceDegrees` | 20 | — | How far off the search plane a target may sit |
| `anchorLead` | 0.8 s | — | Anchor distance ahead, as flight time |
| `anchorAngle` | 20° | — | Anchor angle off the nose |
| `anchorPeriod` | 0.35 s | — | **The beat** |
| `anchorScale` | 3³ | — | Anchor prism size |
| `autoSpeedTarget` | 1.15 | — | Tethers never pull past this × throttle cruise |
| `autoStiffness` | 0.8 /s² | — | **Surge strength** |
| `autoDamping` | 4 /s | — | Damps stretching only |
| `autoRestFraction` | 0.5 | — | How stretched a fresh line starts |
| `autoNoseGrip` | 6 /s | — | Sway vs snap while auto-taut |
| `range` / `minHook` | 93 / 27 | 440 / 130 | Hook window at cruise |
| `windowGrowth` | 0.5 | — | Window moves out with speed |
| `reelRate` / `autoReel` | 40 / 7.4 | 190 / 35 | Reel speeds |
| `minLength` | 19 | 90 | Shortest line |
| `maxSpin` | 0.6 rev/s | 0.6 | The spin limit |
| `maxSpeed` | 358 | 1700 | Cap |
| `releaseBoost` | 0.2 | 0.2 | Boost past a half turn |
| `glide` / `glideDecayFactor` | 1.6 s / 0.15 | same | Post-release carry |
| `cutRadius` | 2.5 | — | Beam's cutting capsule |
| `restitution` / `debrisSpeedLimit` | 1/3 / 200 | — | The sword's debris numbers |
| look + feel block | — | — | widths, alphas, spark/bracket/retract timings, shake/haptic |

## 11. Elements

The map's four slots are **open design slots** (`Input: 0`, no upgrade) — per the design-approval
gate, no element→ability mapping is invented for a prototype. Proposals, NOT implemented, pending
sign-off: **Space → reach** (`range`), **Time → the swing's rate** (`reelRate`/`maxSpin`),
**Mass → anchor size** (`anchorScale`), **Charge → the blade** (`cutRadius`, or popping
super-shields at L5 — the sword's energized gate). Because the hull is a Squirrel clone, the
Squirrel's own elemental effects (skimmer reach, steal, crystal joust) and their level-5 gates are
live on it, unchanged and undeclared by this map (`element_ability_table.py Tether` shows them).

## In-editor verification

1. **Compiles; edit-mode suite green** — `TetherMathTests` (12), `EnumIntegrityTests`,
   `OneThumbVesselCoverageTests`, `ToyVesselRosterCoverageTests`.
2. **Flyable** — Menu_Main → freestyle → **Toy Box ▸ Vessel Changer** → Tether. Squirrel hull, no
   console errors on swap.
3. **Auto-tethers** — fly hands-off: thin calm beams flick out ahead, alternating left/right on a
   ~0.35 s beat, a bracket flash at each new anchor; the hull holds its line and sits a little above
   cruise with a felt surge. Anchors stay behind as prisms. Steer: anchors follow the nose.
4. **Search plane** — roll 90°: anchors and ghost rings move into the new plane.
5. **Long tether** — with a prism ~30-90 u out to the right, ghost ring on it; hold RT: snap (flash,
   shake, rumble scaled by how much the hook turned you), a thick bright beam, auto-tethers stop.
   Speed held. Hold stick up: tighter, faster, stopping at ~2× cruise. Swing arc fills and goes gold
   at a half turn (chime once audio is authored). Dashed release line shows the throw. Release: nose
   snaps to the line, +~20%, speed lingers ~1.6 s. Hold RT with nothing in reach, fly toward a
   prism: hooks the moment it enters the window.
6. **Chaining** — RT swing, press LT mid-swing: flings and re-hooks left.
7. **Cutting** — fly beams through an opposing trail: prisms slice with sparks along the beam;
   shielded → shield pops, prism survives that contact; super-shielded → jiggles, survives. Own
   trail and anchors untouched.
8. **MPPM two clients** — the other client sees beams, anchors and cuts in the same places.
9. **Regression** — fly the Squirrel and two other vector/scalar hulls: unchanged.

## 12. Follow-ups

- **Tune the auto-tether rhythm** (§3) — the top playtest question.
- Its own hull / HUD / telemetry / camera settings (it wears the Squirrel's; the HUD's four icons
  describe the Squirrel). The two Squirrel executors are left on the prefab (unbound, out of the
  registry) because the Squirrel HUD reads the tube executor's cooldown.
- Element mapping (§11) — design sign-off first.
- AI: nothing drives the long tether yet.
- The ghost search and the armed hook search both run on a held trigger with nothing in reach —
  share one result.
- **Vessel id coordination**: the unmerged Gibbon branch (`cece/hopeful-bardeen-rmqhmd`) also claims
  13, which the Butterfly holds; if it merges after this it must take **15**, and its copy of
  `check_network_prefab_hashes.py` and the three `VesselTransformer` seams are the same changes as
  here (resolve the add/add by keeping one).
