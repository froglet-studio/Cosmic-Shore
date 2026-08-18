# Unity In-Editor Verification Checklist

> **Superseded for new work — see `Docs/QA/`.** The untested-development backlog is now
> generated and maintained by the `/qa-backlog` skill in `Docs/QA/QA_BACKLOG.md`, with a
> submission/result loop (`Docs/QA/README.md`) that archives passes and turns failures
> into dev tasks. The two entries below are kept until they are run; new unverified work
> does **not** get a section here — record it in the PR body's *Verification status*
> section and the scan will pick it up.

**Purpose.** Some changes land on shared branches (`bleeding-edge` and the
per-feature branches) without ever being opened in the Unity Editor —
authored and committed by a session that **cannot run the editor**, so no
compile, no play-test, no prefab/asset inspection happened on the author's
side. Those changes are correct on paper but carry editor-side risk: a prefab
import that didn't take, a Variant override that didn't serialize, a rig weight
that reads differently in-scene than in code.

This doc is where that risk gets **recorded once** instead of being re-explained
at the start of every session. When you next open the project in Unity, work the
open items below, tick what you confirm, and delete (or move to "Verified") what
holds up. When you commit code that you could not editor-verify yourself, add an
entry here rather than leaving it in a PR body or a chat message that scrolls away.

**How to use it**
- One `### ` section per unverified change set, newest first.
- Each has: what landed, the concrete **verify in editor** steps, and any
  **first-pass tuning** numbers (these are starting points, expect a balancing
  pass once the thing is observable in context — they are *not* settled).
- Status markers: 🔴 unverified · 🟡 partially confirmed · 🟢 verified in editor.

---

### 🔴 Urchin revival — chain-reaction spikes + trail rider (`claude/restore-urchin-vessel-9qacdk`, 2026-08-15)

Authored without a Unity compile or play-test. This one is **unusually editor-heavy**: the code
and the SO assets are complete, but the Urchin has been unplayable for long enough that its
prefabs never received the modern impact-effect wiring at all. Several of the steps below are
*authoring*, not verification — nothing in this feature runs until they are done, and none of it
fails loudly when they are not.

**ROUND 2 (same day, after the first live repro).** Playtest report: the Urchin showed in the
vessel changer toy, selecting it did not load the vessel. Root cause found and fixed, plus the
two structural gaps closed:

- **The swap crash.** `VesselCameraCustomizer` on the Urchin had BOTH fields null.
  `OnInitializePlayerCamera` is a SOAP event raised unguarded in `Initialize` (fail-loud policy),
  and that line runs only for the LOCAL USER — exactly the swap path. The NRE killed
  `vessel.Initialize` after the old vessel was already despawned, and `SwapVesselAsync` had no
  try/finally, so `_isSwapping` latched true and every later swap of ANY vessel was silently
  refused until scene reload. Fixed at the source per fail-loud: wired
  `OnInitializePlayerCamera` (the shared event asset) and `settings` →
  `UrchinCameraSettingsSO.asset` (salvaged byte-exact from the abandoned restore branch;
  `Configure()` dereferences `settings.mode` unguarded, so this was the second crash in line).
  `SwapVesselAsync` now runs in try/catch/finally — a failed swap costs one console error naming
  the vessel class, never a bricked changer. Same failure shape as upstream's Scarab fix
  (`5be5121cd`), different root: the Scarab's was the prefab lookup + duplicate network hash; the
  Urchin's lookup was fine (the mini model rendered, which proves it).
- **No trail.** `VesselPrismController._onPrismSpawnedEventChannel` was null — guarded in code,
  but a null channel means the vessel lays NO trail, fatal to a trail rider in a quieter way than
  a crash. Wired to the shared prism channel.
- **The attach Rigidbody fix landed** (the round-1 blocker below). Dolphin pattern: the 13
  per-part kinematic Rigidbodies are deleted and ONE kinematic root Rigidbody (Sparrow-exact
  values) added, so every hull collider's `attachedRigidbody` is the root and trigger events
  reach `VesselImpactor` — the trail ATTACH and the shell contact tier are now routable.
  12 hull BoxColliders (all solid) compound onto the root body; re-fit by eye if the editor shows
  a bad compound. **Verify: fly into a trail → the vessel latches and rides.**
- Found by a two-direction sweep of every component the Urchin shares with a working donor:
  fields the donor wires that the Urchin nulls, AND donor fields absent from the Urchin's
  documents (an absent key deserializes to the C# initializer — null for references). The
  remaining absences are benign (stale donor keys, warn-and-degrade petal bars, tuning
  defaults).

**ROUND 3 (2026-08-15, after the second live repro).** Playtest report: "I was getting attached,
but I was not able to slide" — plus the design directive that prismscapes span dimensions 0-3
(singleton / trail / surface / volume), trails are what players lay, and the Urchin should also
ROLL across the 2D prismscapes that gyroid and Schwarz flora make. What landed:

- **The freeze root cause was in `Trail.IndexSafetyCheck`**: on a NON-LOOP trail, an index
  stepping past the head ran `index %= maxRange` → index 0, the far tail — so `Project()` saw a
  phantom segment spanning the whole trail as the crow flies and advanced `finalLerp` by
  `1/chordLength` per frame. Attaching near the head (the common case — you attach where the
  trail is being laid) froze the ride. The non-loop branch now REFLECTS (bounce to `count-2`,
  incrementor flipped), mirroring the existing below-zero bounce. Loops keep the modulo.
- **Signed throttle around XDiff's ACTUAL rest.** XDiff rests at **0.5** on the current input
  scale (`GamepadInputStrategy`), not the 0.2 the 2023 formula assumed — hands-off read as 37%
  creep. `ReadThrottle` is now signed around `throttleRestPosition` (0.5): push slides the way
  the nose points along the ribbon, pull slides back. The look-over-the-shoulder reverse gesture
  is retired; direction = `sign(throttle × dot(forward, Course))`.
- **`PrismscapeDimension` (0-3, values = the dimension) + `PrismscapeTopology.DimensionOf`** —
  authored evidence first: `Trail.Dimension`, declared by the LAYER (default 1D = the vessel
  wake; the gyroid/Schwarz spawnables declare `Surface` — `Trail` is the general lay container
  that `PrismTrailBuilder` stamps on everything, so its presence is membership evidence, never
  shape evidence). Container-less prisms (flora growth) get a `PrismSpatialIndex.QuerySphere`
  census (shell fills a ball like r², solid like r³). Never physics, never per-frame. Enum
  values pinned by `EnumIntegrityTests`.
- **`BlockscapeFollower` finished as the 2D kernel**: face crawl (pilot keeps full steering —
  `Slide()` runs the protected `Roll()`/`Yaw()`/`Pitch()` passes), edge fold, and **prism hop**
  via QuerySphere with an `OnPrismCrossed` event. Its box math moved to local unit space — the
  old code compared `InverseTransformPoint` output (±0.5 space) against `localScale/2` (world
  half extents), so on a 4×4×1 block the fold fired 4× late and the snap parked the rider off
  the surface.
- **`GunVesselTransformer` routes by topology**: `prism.Trail != null` → TrailFollower slide,
  else BlockscapeFollower roll; ONE `ApplyPrismscapePayoff` (restore/grow+L5-shield/steal) serves
  both, fed by `FinalBlockSlideEffects` (1D callback) and `OnPrismCrossed` (2D event).

**ROUND 4 (2026-08-15, after the third live repro).** Playtest of round 3: "horribly unsmooth
... horribly jittery" on a trail — and the design spec stated plainly: a 1D ride is seamless
movement along the ribbon (trail prisms are authored with **Z parallel to the trail**), and a 2D
ride is smooth rolling on a continuous curved surface (gyroid/Schwarz prisms are authored with
**Z orthogonal to the surface**) — the rider must NEVER feel prism edges or gaps. What landed:

- **The jitter's root: direction recomputed per frame from a facing dot, against a hull that
  never rotates.** `Slide()` replaces `base.MoveShip()`, and `RotateShip` lives inside
  `MoveShip` — so during a ride nothing wrote `transform.rotation`, and round 3's
  `sign(dot(frozen forward, curving Course))` FLAPPED as the ribbon curved. Every flap ran
  `SetDirection`, which shifts the block index ±1 — a teleport per flap, up to every frame.
  The AI break-off lesson again: directional decisions are LATCHED, never per-frame geometry.
  Now `GunVesselTransformer` keeps a latched `_facingSign`, flipped only when `dot(transform.forward, RibbonAxis())` crosses `facingFlipThreshold` (0.35) the OTHER way — true hysteresis, so a bend sweeping the axis under a steady nose cannot flap it — and the signed throttle maps onto that sign before `TrailFollower.SetDirection` is told anything.
- **Attach snap killed**: the 2023 `percentTowardNextBlock = 0` TODO is implemented — the lerp
  seeds from the actual touch point projected onto the segment ahead.
- **The head parks instead of bouncing**: round 3's reflection stopped the freeze but bounced
  the rider at an open ribbon's end, and the throttle mapping flipped it back — oscillation at
  the exact place players attach. `RideTheTrail` now discards the bounced frame entirely (no
  snap, bookkeeping untouched) and resumes when the trail grows or the pilot reverses.
  `SetDirection` range-clamps the terminal-block flip that used to index off the list.
- **Ride attitude applied the free-flight way**: rides write `accumulatedRotation` (trail:
  forward eased onto the travel heading; surface: pilot steering + belly eased onto the
  smoothed normal) and apply it with `RotateShip`'s own `LERP_AMOUNT` slerp; ride boundaries
  sync `accumulatedRotation = transform.rotation` both directions so no backlog fires as an
  uncommanded turn.
- **The 2D roll is a NEW model**: round 3's face-crawl/edge-fold/hop kernel (per-prism boxes —
  structurally incapable of hiding edges) is deleted. `BlockscapeFollower` now rides a
  smoothed plane over the prisms' AUTHORED normals (`transform.forward`, sign resolved toward
  the ridden side): normal slerps at `normalTrackingRate`, hover is a soft spring at
  `hoverHeight`, ground is the nearest live prism (`QuerySphere`, one bounded query per frame
  per rolling vessel — replaced, never dropped, so gaps and shot-out ground are coasted).
  `OnPrismCrossed` still pays the shared payoff per prism visited.

Round-4 verify (detail in `URCHIN_TRAIL_RIDER.md` steps 7/8/8b): the slide is SMOOTH — hull
lies along the ribbon, no jitter, reverse turns the vessel around, the head parks and resumes;
the roll is CONTINUOUS — belly on the surface, steering live, no facets/bumps/edge feel, holes
coasted.

**ROUND 5 (2026-08-16, design pass on round 4).** Feedback: good start, but each ride needs its
OWN logic, keyed to its prismscape's z-axis relationship (trail prisms: z parallel; surface
prisms: z orthogonal) — and the 2D ride wants marble-madness vibes with wrap-around at edges.
What landed:

- **The 1D ride is a RAIL GRIND**: throttle = signed speed along the ribbon; forward/reverse
  from the pilot's FACING via `dot(forward, IndexOrderHeading)` — the original Urchin's
  dot-product scheme, stable because the index-order axis (unlike Course) never flips with
  travel, plus a `facingDeadband` hysteresis; **roll ORBITS the hull around the trail** at the
  attach radius (parallel-transported radial, handedness corrected by facing); **pitch/yaw stay
  free for aiming**; the only imposed attitude is a twist easing up radially out.
  `TrailFollower` became a pure centerline kernel (`CenterlinePoint`/`TravelHeading`, no
  transform writes) and the transformer composes `position = centerline + radial × radius`.
  Round 4's latched-at-attach throttle mapping and forward-onto-Course alignment are replaced.
- **The 2D ride is a MARBLE**: surface velocity now CHASES the steered target
  (`surfaceInertiaRate`) — release glides, turns carve, momentum re-projected onto the curving
  plane each frame, follower ticks every frame so the glide survives the deadband; and past a
  sheet's boundary the target normal blends toward the radial from the RIM POINT with the hover
  anchor moving to that rim point — the floor swings around the edge at hover distance and the
  rider **wraps onto the far side** (holes wrap around their lip the same way; the two frames
  meet continuously at the rim).

Round-5 verify (detail in `URCHIN_TRAIL_RIDER.md` steps 7/8/8b): grind = slide/aim independent,
facing decides forward, roll orbits the ribbon, head parks; marble = momentum glide + carve,
rolling off a sheet's edge wraps around the rim onto the other side. Feel dials:
`orbitDegreesPerSecond`/`minOrbitRadius`/`trailUpAlignRate`/`facingDeadband`/`surfaceAlignRate`
(transformer), `normalTrackingRate`/`hoverTrackingRate`/`hoverHeight`/`surfaceInertiaRate`/
`rimWrapMargin` (BlockscapeFollower).

**ROUND 6 (2026-08-16).** Playtest verdict: the gyroid roll "felt better than ever"; the 1D
ride still wrong, with the right question attached — "check whether vessels that leave two
trails are properly leaving two separate trails... it feels like it might incorrectly be 2
trails in 1" — plus: smaller trail prisms, wider twin gap, camera at half distance, chains to
generation 4, the ring-shotgun volley back, and a far denser omni barrage. What landed:

- **THE structural find: wake prisms were never members of their trail.** `CreateBlock` did
  `trail.Add(prism)` but nothing set `prism.Trail` (the only stamper was the spawnable
  builder), and **pool reuse preserved the stale reference** — so a wake block either had no
  container (the attach effect's null-Trail gate refused it, with an error) or a DEAD
  spawnable's container (the gate passed against the wrong ribbon, `GetBlockIndex` = −1, ride
  followed garbage; the census read the un-containered twin-ribbon blob as one *Surface* —
  the literal "2 trails in 1" feel, marble-rolling on trail prisms whose z points ALONG the
  ribbon). Fix is a set: `Prism.ResetState` clears membership (Trail + properties mirror);
  `Prism.AssignTrail` is the one stamping API, called AFTER Initialize by BOTH layers
  (builder reordered; wake spawner now stamps at all); the attach effect dropped its
  null-Trail refusal (container-less prisms are legitimate Singleton/Surface prismscapes —
  routing decides).
- **Wake geometry**: `BaseScale (10,5,5) → (10,2.5,4)`, `Gap 1 → 6` — two clearly separate
  2-wide lanes with a 6u clear gap (was 4.5-wide slabs 1u apart). **Camera**:
  `UrchinCameraSettingsSO` followOffset z −40 → −20, dynamic band 30/50 → 15/25.
- **Chains at generation 4** (both spike assets' `generationsAtFullCharge`), with
  `ChainReactionBudget.VolleysPerFrame` 4 → 6 (frame ceiling ≤ 84 chain spikes) so depth
  reads as a rolling cascade; territory conversion stays the primary brake.
- **The volley is a ring SHOTGUN again**: `FiringPatterns.ConcentricRings` +
  `Gun.FireRingBlast` — 3 staggered rings in a 25° cone + center spike = 37/pull, one blast
  per pull from the hull, zero RNG (peer-identical by construction).
- **The barrage is dense**: `barrageSpikeCount` 36 golden-spiral points for the ship's own
  volley at ANY depth (was 4 tetrahedral at depth 0); chain children keep energy-derived
  counts. The 2023 hull's 18 authored ShootPoint port vectors were recovered from the old
  prefab and are recorded in `URCHIN_CHAIN_SPIKES.md`; the spiral supersedes them.

Round-6 verify: fly the Urchin, lay trail — two visibly separate thin ribbons; attach to one
and grind IT (not a phantom blob); camera noticeably closer. Volley: shotgun rings that chain
deep at high Charge. Barrage: a dense full sphere. Watch the console for chain-budget drop
reports under sustained deep cascades — expected under stress, but constant reporting at
casual play means the budget wants another look.

**ROUND 7 (2026-08-16).** Playtest: shotgun needed real velocity inheritance + tighter spread;
the grind still jerked — "a jump to another prism and back again very quickly... at a
periodicity that matches the prisms along the trail." That description was the diagnosis:

- **`Trail.Project` lerped along a CHORD on every crossing frame.** The walk loop advanced
  `startIndex`/`nextBlock` but never re-read `currentBlock`, so a frame that crossed a block
  boundary measured — and lerped along — a two-segment chord from the frame's ORIGINAL block,
  cutting the corner at a parameter computed against the wrong length; the next frame
  re-derived cleanly and snapped back. One bad frame per crossing = the per-prism jerk. Fixed
  with the missing `currentBlock = TrailList[startIndex]` inside the loop.
- **The centerline is now a Catmull-Rom arc** through the block centres (position + heading;
  bookkeeping stays segment-linear; outer control points wrap on loops, clamp at open ends) —
  a straight lerp kinks direction at every block centre, the opposite of the rail-slide feel.
- **Spikes always inherit the vessel's live velocity** — the executor passed inherited
  velocity only while attached, so free-flight volleys fired as if from a standing gun and
  dropped behind the ship at cruise. And the shotgun cone tightened 25° → 15°.

Round-7 verify: grind a trail at full throttle — NO per-prism tick, position AND heading sweep
smoothly through block centres like a rail slide; fire the shotgun at cruise speed — the fan
travels with the ship (rings hold shape relative to the pilot) in a visibly tighter cone.

**ROUND 8 (2026-08-16, "this was a huge improvement").** Two features on the now-working grind:

- **Trail integrity over missing prisms.** Destroyed-in-place prisms already rode (transforms
  intact, payoff restores them) — but a real hole (null at teardown, pooled-away object) broke
  the walks, and `DestroyedTerrainSpeed: 10` halted the slide to a crawl over every destroyed
  stretch. Now: `Trail.IsRidable` + `TryStepRidable` bridge holes in `Project`/`LookAhead`
  (same wrap/reflect end semantics, so parking still works; spline control points fall back to
  segment endpoints; a walk with no survivors parks instead of throwing), and
  `DestroyedTerrainSpeed` is 150 on both followers — holes keep pace and get rebuilt under
  the payoff as you cross.
- **Junctions.** On every block crossing the ride probes (`QuerySphere`,
  `junctionSearchRadiusScale`×extent) for a DIFFERENT 1D container passing nearby; if its
  heading runs more along the pilot's forward than the current ribbon's axis (by
  `junctionSwitchMargin` hysteresis), the ride FORKS onto it (`Attach` + `SeedTrailRide` —
  positionally continuous), then the grind radius settles in (`orbitRadiusSettleRate`). Aim
  down the branch you want; the ride takes it. Ribbons only — a nearby gyroid is not a fork.

Round-8 verify: blow holes in a ridden trail (spike it, fly a Dolphin through it) then grind
across — full pace, no snap, prisms restore under you. Attach to a trail your wake meets: ride
back to the meeting point aiming down your wake — the ride forks onto it; aim along the
original trail instead — it keeps straight. Linger at a junction — no flip-flopping.

**ROUND 9 (2026-08-16).** Riding a SQUIRREL trail was still wrong ("strange axes and between
both trails"). Its trails are correct — `BaseScale.x 20` / `Gap 18.5` = two 0.75-wide ribbons
19.25 apart in two separate `Trail` objects — and every fault was in the ride, all from the
same blind spot: **the Urchin's own wake is not a representative trail**.

- **A parallel ribbon is not a fork.** A vessel's second ribbon runs alongside the first for
  its whole length, so it was a junction candidate at every crossing and the rider hopped the
  pair's 19u gap repeatedly. `junctionParallelThreshold` (0.9): a junction is a DIVERGENCE.
- **Probe radius keyed off block size → 160u on a Squirrel.** Its block scale is dynamic
  (`SetNormalizedXScale` from skimming, `SetDotProduct` from drift, `maxBlockScale: 5` →
  blocks ~40u wide), so `4 × largest extent` swept the arena. Now a flat
  `junctionSearchRadius` (12 world units).
- **Unbounded grind radius**: a fork onto a 19u-distant ribbon seeded a 19u orbit. Clamped by
  `maxOrbitRadius` (8).
- **A gapped wake's block CENTRES are not its spine.** The lay holds the inner edge at a fixed
  offset (that is what keeps the gap constant as blocks widen), so the centres swing sideways
  ~20u as a Squirrel skim-widens — in straight flight. `Trail.LateralAnchor` (declared by the
  layer; a prism cannot tell which face points at its sibling) + `Trail.RidePoint` recover the
  width-independent line, used by every geometry read in the walks. 0 for ungapped wakes and
  all spawnable lays.

**Finding, NOT changed — a drifting Squirrel's prisms are not axis-aligned to their trail.**
Prisms lay with `blockRotation` = the vessel's rotation, so mid-drift they point where the ship
was AIMED, not down the ribbon. The platform already has the fix mechanism
(`BlockRotationOverride`, used by `BarrelRollController`/`ScarabJukeController` to lay
travel-aligned bridging prisms); drift does not use it. The ride is immune (it takes its axis
from the curve of block positions, never prism rotation), so this is a visual/design call —
but it does mean the stated invariant "trail prisms have z down the trail" is currently false
for the drift vessel.

Round-9 verify: ride a Squirrel trail — no hopping between the pair, no lateral swerve as the
Squirrel's blocks change width, no fling on attach. Ride an Urchin trail — unchanged from
round 8.

**ROUND 10 (2026-08-16) — junctions REMOVED, single-trail ride polished.** Junction forking is
gone by design call: it worked, but every block crossing carried a chance of leaving the rail
you were on, and two rounds went into stopping it firing when it shouldn't. A ride has to be
excellent on ONE trail before choosing between two means anything. Everything the junction work
left behind is kept and still earning its place — hole bridging, `Trail.HeadingAt`,
`SeedTrailRide`, the orbit-radius settle. (If it returns: a junction is a DIVERGENCE, and the
probe radius must be in world units — both recorded in `URCHIN_TRAIL_RIDER.md`.)

The polish, all on the 1D grind:

- **Speed is smoothed in the follower** (`speedTrackingRate`), where terrain changes happen: a
  friendly→hostile boundary is a 15× cliff (150→10) and the old walk re-read terrain per block
  *within* a frame, publishing each value in turn. This replaced the per-block time-accounting
  walk entirely — including `LookAhead`'s "<2 blocks" early-out, which fought hole bridging by
  refusing to move on a sparse ribbon.
- **Throttle has inertia** (`trailInertiaRate`) and direction only re-latches outside the
  deadband, so a reversal coasts through zero instead of an about-face at speed. The follower
  is ticked EVERY frame now (it owns the coast); gating it at the deadband made release a hard
  stop.
- **The orbit frame rides the continuous spline tangent** (`RibbonAxis()`), not the per-block
  `IndexOrderHeading` step function — the same class of once-per-block tick as the round-7
  chord bug, one layer up.
- **Attach carries your speed and never pops**: `_rideSpeed` seeds from arrival speed, the
  grind throttle seeds from the stick, and the orbit radius seeds at the hull's ACTUAL distance
  and eases in (clamping the seed — what the junction work did — teleported the hull sideways
  on contact).

**ROUND 11 (2026-08-16) — the ride restored to what shipped years ago.** Playtest: attaching to
a Squirrel trail and pushing forward "swung me around"; backward worked better; *"we had
something years ago on the main branch that felt better than this."* Went and read it —
`GunShipController.Slide` at `d895f329a`, intent named by `023d53cc7 "When attached move down
the direction you are looking"`. The original is three lines: position is a lerp between block
centres (**ON** the trail), the rotation lerp is **deliberately commented out** (attitude is
never touched while sliding), and direction flips on `dot(forward, segment) < 0`.

- **The positional orbit and the up-twist are REMOVED.** Both came from an over-literal reading
  of round 5's "roll should rotate them around the trail" — but while riding, the hull's
  forward IS the rail, so ordinary `Roll()` already spins the pilot around it. The imposed
  twist fought the stick every frame and, on a curving ribbon (a Squirrel drift line — exactly
  the test case), swung the hull bodily as the radial turned. `Roll()`/`Yaw()`/`Pitch()` now
  all run exactly as in free flight, and position is the centreline plus a contact offset that
  decays (`railSettleRate`).
- **The forward/backward asymmetry was the facing seed.** It latched from `dot(nose, axis)` —
  but you fly INTO a trail, so at contact the nose is across the ribbon, that dot is ~0 and its
  sign is noise: push forward and you were as likely to be sent back the way you came. It now
  seeds from the direction `Attach` latched, which comes from the vessel's **Course**.
- Kept, because none of it fights the original: Catmull-Rom centreline (a smoother version of
  the original's segment lerp), hole bridging, speed/throttle inertia, `RidePoint`, the payoff.

Round-11 verify: attach to a Squirrel trail and push forward → you continue the way you flew
in, no swing; roll → the view spins around the rail and nothing fights the stick; pitch/yaw →
free aim while the rail carries you.

**ROUND 12 (2026-08-16) — "still orbiting like crazy": the RAIL was a helix; ride the SPINE.**
Round 11's transformer was right and the ride still orbited, because the geometry it rode was a
corkscrew. Every gapped block is laid at `spine + vesselRight × (width/2 + halfGap)` with the
vessel's right AT LAY TIME — **roll included** — so a rolling layer (the Squirrel, constantly)
braids each of its two ribbons into a HELIX around its flight path, radius ≥ ~9.6u (up to ~30u
skim-widened). Every ride line at a fixed offset along each block's lay-time right inherits the
helix — block centres (pre-round-9) and the inner edge (round 9's `RidePoint`) alike. Riding a
9u+ helix at 150 u/s IS "orbiting like crazy", in every round so far, under every transformer.

`Trail.RidePoint` now undoes the ENTIRE lay offset — `blockPos − blockRight × (width/2 +
halfGap)` — recovering the SPINE the laying vessel's centre actually flew. Exact under full
roll (the block's rotation preserves the lay-time right). `Trail.LateralHalfGap` joins
`LateralAnchor` (both stamped by the spawner). Both ribbons of a pair map to the SAME spine —
the pair is one wake, and you slide down the corridor between the twins, prisms streaming past
on either side. `Attach` seeds via `RidePoint`/`HeadingAt` too (raw-position seeding would snap
a lay-offset on the first moving frame). Accepted approximation: `LateralHalfGap` is
trail-level, so a per-block `ApplyBoostGap` variance (boosting Sparrow) gets a slightly
approximate spine mid-boost.

Round-12 verify: lay a ROLLING trail with the Squirrel (hold roll while flying), then attach
and ride it — the ride runs straight down the corridor between the braided ribbons, no orbit,
no swing; attach to either ribbon of the pair → same road; the Urchin's own trail rides as
before.

**ROUND 13 (2026-08-16) — the spine cannot be reconstructed; the lay STAMPS it.** Playtest of
round 12: rode a Squirrel, swapped to Urchin, attached to the Squirrel trail → "immediately
bobbing up and down and over to the other trail, shielding both trails". Round 12's premise —
"the block's rotation preserves the lay-time right vector" — is FALSE for exactly the blocks a
DRIFTING Squirrel lays: the lay offset rides the SHIP's right, but drift/barrel-roll bridging
sets `BlockRotationOverride` so those blocks are rotated to the TRAVEL direction. `block.right`
is then the wrong axis, and subtracting ~10u along a wrong, per-block-varying direction is the
bob-and-swing (the flailing hull then contacted and paid both ribbons — the shielding).

Reconstruction of the lay offset from block geometry has now failed twice (fixed offset along
right → helix under roll; undo along block.right → wrong axis under the override), so it is
dead as a concept: **`Prism.TrailLayOffset`** — the exact world-space vector the spawner added
to the spawn position — is stamped per block (after `Initialize`, cleared on pool reuse) and
`Trail.RidePoint` subtracts it. Immune to roll, to the rotation override, to per-block boost
gap variance (round 12's approximation note is void — per-block exact), and to the payoff
GROWING ridden blocks (no live width read). `Trail.LateralAnchor`/`LateralHalfGap` deleted.

Round-13 verify: fly the Squirrel and DRIFT + roll while laying, swap to the Urchin, attach to
that trail — the ride runs clean down the wake's spine, no bobbing, no crossing to the sibling
ribbon, payoff only on the ribbon you ride. Straight-flight trails unchanged.

**ROUND 14 (2026-08-17) — the REAL Squirrel bug: `OnDisable` orphaned the wake; plus true bend
hysteresis.** Playtest: the Urchin's own trail rode well (bend jitter aside); the Squirrel's
was still chaos even on DEAD-STRAIGHT sections — which ruled out geometry (the 1D follower on
a straight ribbon cannot wander). The chaos was the MARBLE: `VesselPrismController.OnDisable`
called `ClearTrails()`, so the vessel-changer swap EMPTIED the despawned Squirrel's Trail
containers while every prism still pointed at them → `DimensionOf` read Count ≤ 1 → Singleton
→ surface follower, whose along-z "normal" on trail prisms flung the hull everywhere, hopped
nearest-ground between BOTH ribbons, and paid/shielded every hop. All three rounds of Squirrel
symptoms were this one line; the geometry fixes were real but only ever reached the live-trail
ride.

- **The wake OUTLIVES its vessel** (mass is conserved — so must bookkeeping be): `OnDisable`
  no longer clears; the Trail objects live as long as their prisms reference them. Explicit
  resets (turn reset, cell drain) still clear — and `Trail.Clear()` now un-stamps membership
  first, so even they leave honest container-less prisms, never members of an empty list.
- **`IsRidable` requires membership** (`p.Trail == this`): persistent trails can accumulate
  pool-REUSED entries over a session; membership tells a survivor from a phantom parked at its
  new life's position — phantoms bridge as holes.
- **`facingFlipThreshold` (0.35) replaces the re-latch band**: TRUE hysteresis — the
  forward/reverse mapping flips only when the aim crosses well past broadside the OTHER way,
  so a bend sweeping the axis under a steady nose holds direction instead of flapping.

**ROUND 15 (2026-08-17) — rings loop; 0D unattachable; 2D stops fighting aim.** Playtest: "best
yet — I could ride both my own and the Squirrel's trail great", with three follow-ups.

- **Six lay paths were stamping trail membership BEFORE `Initialize`**, which round 13's
  pool-reuse clear wipes — so their prisms came out container-less, censused as 0D Singletons,
  and routed to the MARBLE. That is the ring's "strange behavior", and it was a **regression
  beyond the Urchin**: `SpawnableWaypointTrack` and `SpawnableRaceTrack` (HexRace) lost their
  `Trail` too, which `Skimmer` and `SkimmerAlignPrismEffectSO` read for trail alignment. All
  six — `BoostRingBuilder`, `SpawnableFlower`, `SpawnableCord`, `SpawnableDartBoard`,
  `SpawnableRaceTrack`, `SpawnableWaypointTrack` — now call `AssignTrail` after `Initialize`.
  **Regression-check the HexRace track and any skimmer trail-alignment.**
- **Rings are LOOPS**: `SpawnableRings` + `SpawnableDartBoard` build `new Trail(isLoop: true)`,
  so walks wrap by modulo and a rider circles indefinitely either way. Ray-shaped AOEs stay
  open (a spoke has two ends).
- **0D is not rideable**: `TryBeginRide` refuses Singleton; the vessel flies on.
- **2D no longer fights aim**: the belly-onto-normal ease is REMOVED (with `surfaceAlignRate`).
  The surface constrains POSITION, never attitude — the round-11 rule, now applied to both
  dimensions. Motion still follows the plane (crawl direction is the steered forward projected
  onto it).

**ROUND 16 (2026-08-17) — ride the prism SURFACE; roll walks you around it; camera to 1/3.**
A SPARROW trail is a single ribbon (no gap — that vessel flies with one thumb), so its block
centres sit exactly on the ride line and riding the line bare put the Urchin INSIDE every
prism. The ride now offsets out to the prism's surface, both halves derived:

- **Which way**: the hull's own UP flattened across the trail. Rolling sweeps up around
  forward — which while riding IS the trail axis — so the ship walks around the prism's z axis,
  belly toward the rail. No new state, and it cannot fight the stick (attitude stays the
  pilot's; position follows it).
- **How far**: the exact box cross-section `min(halfX/u, halfY/v)` from the prism's authored
  `TargetScale`, so a wide flat trail rides close on its broad faces and further at its edges.
  `rideSurfaceClearance` (1.5) adds the hull's half-thickness.

Gapped wakes are unchanged in feel (their ride line is the corridor spine, so the offset is a
small clearance within it). Camera: `UrchinCameraSettingsSO` followOffset z −20 → −6.67,
dynamic band 15/25 → 5/8.33.

**ROUND 17 (2026-08-17) — ribbons ride separately; shielded/skewed prisms ride their envelope;
Urchin hull opacity.**

- **A gapped wake is TWO SEPARATE SINGLE TRAILS.** The spine ride (rounds 12-13) is retired by
  design call — consistency beats the corridor. `RidePoint` is the block's own centre;
  `Prism.TrailLayOffset` + its spawner stamp are deleted. Accepted: a wake laid by a rolling
  vessel is ridden as the helix it genuinely is.
- **Shielded/super-shielded → the cuboid the shell nests in**: half-extents × 3, read from
  `OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE` (both shield meshes are the box's
  circumscribing dual, so one factor covers both tiers).
- **Skewed prisms → the SUPPORT envelope in the trail's frame**: `Σ halfᵢ·|axisᵢ·radial|`, so a
  drift block yawed off the ribbon reads as effectively wider and the rider clears it. Support
  rather than a trail-aligned AABB because an AABB about an axis needs an arbitrary "up" choice
  and changes with it; support needs none, is continuous under roll, and reduces exactly to the
  box half-extent for a square-on prism (Sparrow ride unchanged).
- **Urchin hull opacity**: `VesselHelper.ApplyShipMaterial` paints slot **[1]** on a
  MeshRenderer, so slot [0] keeps its authored material — and on the Urchin that was the
  TRANSPARENT `BlueBaseVesselMaterial` on 12 of 13 renderers. Only `ShroudLeft` had the pair
  reversed (opaque `GreenAccent` in slot 0), which is why one submodel looked right and the
  rest looked see-through. All 13 now match ShroudLeft. **Materials were NOT edited** —
  `BlueBaseVesselMaterial` is shared by nine vessels; this is a per-renderer slot-order fix on
  the Urchin prefab only.

**ROUND 21 (2026-08-17) — reorient merge; the steal chain was abortable; round 20's material
conclusion reversed by the Squirrel's own material map.**

- **Merged `origin/bleeding-edge`** (43 commits: Dolphin elemental re-cut, self-trail contact
  grace, logging channels, ecology fixes). One docs conflict, both sides kept.
- **"Spikes stick but nothing steals or chains"** — the container is `[Embed, Steal, ChainFire]`
  with no per-effect isolation, so a throw inside Steal killed both the flip (it sat upstream)
  and the chain volley, while Embed had already landed. Fixed four ways: wired the 9 authored
  `{fileID: 0}` holes on `TrailRing.prefab` / `GreenDartBlock.prefab` (`onPrismStolen`,
  `_themeManagerData` — an unstealable prism family under the fail-loud SOAP policy);
  `PrismTeamManager.Steal` now flips BEFORE it reports (capture payload → `ChangeTeam` →
  `Raise`), so reporting can never veto gameplay; `ProjectileImpactor` runs every effect
  through `ImpactorBase.RunEffectIsolated` (throw = ONE named console error with stack,
  siblings still run); and `Gun.FireSingle` authors projectile scale in WORLD terms — the
  Urchin's muzzles sit under guns at scale 1.75, which had been multiplying into every
  round's size, collider and sweep radius since round 19.
- **Materials: round 20 reversed.** The Squirrel FBX's `externalObjects` map names the roles:
  `Body` = BlueBase (dark glass, fixed), **`Domain` = GreenAccent (the placeholder the runtime
  REPLACES — authored jade because jade is the menu default)**, `Window` = Screen (fixed).
  Squirrel proportions: Domain 26% / Body 64% / Window 2% / Engine 6% — the domain is an
  ACCENT. The Urchin's authored mapping matches exactly, so `_domainReplacesMaterials` now
  names ONLY `GreenAccentVesselMaterial` and the vessel is two-tone again: domain accents +
  dark body + screen. Rendered the FBX submeshes offline (three orthographic views): the left
  gun is clean, every part is Body-majority + Domain-accent, and the "Window" is the authored
  front dome (33 polys, a third of the silhouette — left as authored; if it should be dark
  hull, the change is one slot on the Body renderer).

Round-21 verify: change domain to Ruby on the Urchin → the ACCENTS (ribs, rings, gun rings) go
ruby, the body stays dark navy glass, the front dome stays the screen material, **no jade
anywhere, and NOT one uniform material**. Fire the aimed volley into a hostile trail → prisms
recolour to your domain (the steal) and child volleys erupt from converted prisms (the chain);
fire at a Squirrel crystal RING → it steals and chains too (this was structurally impossible
before — the ring prefab's steal event was unwired). Shotgun spikes are back to their normal
size (they were 1.75× oversized since round 19). If anything in the chain still fails, the
console now names the exact effect and stack — paste that line back.

**ROUND 20 (2026-08-17) — the jade that survived a Ruby swap: BOTH of the Urchin's authored
materials are domain-bearing.** *(Conclusion REVERSED by round 21 — GreenAccent is the Domain
placeholder by fleet convention; painting both erased the two-tone read. The colour measurements
remain valid.)*

- **`GreenAccentVesselMaterial` IS Jade, hardcoded.** Its `_Color2` (the fresnel rim, the part
  that glows) is `(0, 0.7765, 1.4980)` = `JadeColors.ShipColor2 × 2` **exactly, to 7 decimals**;
  `_Color1` matches ×2 on blue and one 8-bit step off on green. `ThemeManager` drives the live
  ship material through those same two properties, so any slot wearing this material stays jade
  on every domain — which is precisely what a Ruby pilot photographed.
- **`BlueBaseVesselMaterial`'s rim is pure black** `(0,0,0)` — a base with no rim, which is the
  round-17 "too transparent" report.
- So neither authored material is a neutral the vessel should keep. `_domainReplacesMaterial`
  became the **list** `_domainReplacesMaterials`, and the Urchin declares BOTH; every slot on all
  13 renderers takes the domain colour. `Body`'s third material (`ScreenVesselMaterial`, a neutral
  cockpit screen shared with Rhino/Dolphin) is untouched. No other vessel changes.
- **The left gun has no model error.** Parsed `Urchan_Test.fbx` directly (binary FBX 7400): all 14
  geometries declare materials in the SAME order `[Material.004, Material.009]` (`Body` adds
  `Material.002`), mapped `ByPolygon`; mirrored halves match poly-for-poly (276/276, 400/400,
  370/370 ×2, 324/324, 281/281); each of the 13 prefab renderers points at its own distinct mesh,
  none shared. The one oddity is `ShootPoints` (`Sphere.041`) — a zero-polygon holder for the 18
  historical firing ports, not wired into the prefab. The real anomaly was prefab authoring
  (`ShroudLeft`'s two materials reversed vs its twelve siblings), already fixed; painting every
  slot makes slot order unable to matter again.

Round-20 verify: on the Urchin, change domain at the toy → **no cyan/jade survives anywhere on the
hull** on Ruby or Gold; the whole ship reads in the new domain's colour, and nothing is
see-through. Change back to Jade → it reads jade again (that one is not a no-op check: compare
against Ruby first). Console: no `[VesselCustomization] '<part>' wears none of the vessel's domain
materials` warning. Other vessels' hulls unchanged.

**ROUND 19 (2026-08-17) — the domain colour is painted by IDENTITY, not by index; the shotgun
fires from both guns with a tighter spread.** *(The identity mechanism is right; round 20 found it
needed to name TWO materials, not one.)*

- **Round 18's domain fix was a no-op, and the reason generalises.** It restored the authored
  material order (`BlueBase` back to slot 0) *and* moved the index 1 → 0 in the same commit;
  those cancel exactly, so the domain colour landed on the same submesh as before and the
  cockpit reading was "changing domain did not swap the correct material." **Never move an
  array order and the index that reads it in one change** — one of the two is the fix, and
  doing both is a rename.
- **The durable repair drops the index.** New optional
  `VesselCustomization._domainReplacesMaterial` names the AUTHORED material the domain colour
  replaces; `ResolveDomainSlots` finds every slot wearing it per renderer (whatever index),
  and `ShipHelper.ApplyShipMaterialToSlots` paints those. Empty = the existing slot-index path,
  so **no other vessel changes**. The Urchin declares `BlueBaseVesselMaterial` — which is also
  the transparent one, so this removes the see-through hull and the mis-coloured hull together,
  and it is correct on `ShroudLeft` (authored in the opposite order to its twelve siblings) and
  on `Body` (three materials) alike, which no single index can be.
  The slot map is resolved ONCE and cached — after the first paint the slot holds the domain
  material, so re-resolving would find nothing and the vessel would stop responding to its
  domain. It reads `sharedMaterials` (not `materials`, which clones and would never match the
  asset by identity). A geometry wearing none of the named material warns once, by name.
- **The shotgun fires from every muzzle** (`ResolveMuzzles`), each fan spun by
  `360/spikesPerRing · i / muzzleCount` about the aim axis via the new
  `Gun.FireRingBlast(phaseOffsetDegrees)`, so two guns interleave into one denser cone instead
  of drawing the same spokes twice. `spikesPerRing` is now authored PER MUZZLE and halved
  6 → **3**, so the pull still throws ~38 spikes (2 × 19) rather than 74 into a 160-deep pool.
- **Tighter spread**: `coneHalfAngleDegrees` 15° → **9°**.

Round-19 verify: fly to the domain-changer toy on the Urchin and change domain → the **hull**
recolours immediately (this is the check round 18 appeared to pass and did not); trim stays
accent; nothing see-through. Other vessels' hulls unchanged. Pull the aimed trigger → spikes
leave **both gun muzzles**, not the hull centre, and the fan is visibly narrower than before;
count reads about the same density as round 18, not double. Console: no
`[VesselCustomization] '<part>' wears none of 'BlueBaseVesselMaterial'` warning.

**ROUND 18 (2026-08-17) — spike dwell x3; the omni barrage chains; domain colour on the right
submesh.** *(Superseded in part by round 19 — the domain-colour item below did not take effect.)*

- **Embedded spikes persist 3x**: `ProjectileEmbedPrismEffect.dwellSeconds` 1.25 → **3.75**.
  Pure look — the steal and the child volley have both already happened by then.
- **The omni barrage never chain-reacted.** `FireSpherical` decremented `energy` before
  spawning while the ring volley's `FireSingle` did not, so the barrage came out one tier
  shallower from the same authored number — at its resting depth its spikes landed terminal.
  The decrement is right for a chain HOP (it IS the depth ladder), so it is now conditioned,
  not removed: `if (pointsOverride <= 0) energy--`. Only the ship's own volley authors a point
  count; a hop never does. Barrage `generationsAtRestingCharge` 0 → **1** so both triggers
  chain from rest.
- **Domain colour was on the wrong submesh.** Platform contract (`ScarabHullBuilder`): a
  MeshRenderer hull is painted on **slot 1** (submesh 0 = shared body, submesh 1 = domain). The
  Urchin's FBX authors its submeshes the other way round. New
  `VesselCustomization._domainMaterialSlot` (default 1 — **no other vessel changes**), passed
  through to `VesselHelper.ApplyShipMaterial` and clamped to the renderer's slot count; the
  Urchin declares **0** and its round-17 material swap is REVERTED to the authored order. Net:
  domain colour on the hull, opaque accent showing elsewhere, nothing transparent.

Round-18 verify: shoot a prism → spikes stand in it noticeably longer before fading. Fire the
ALL-DIRECTIONS trigger at enemy mass → it chains like the aimed one does (raise Charge → deeper
on both). Look at the Urchin → the HULL wears the domain colour (change domain at the toy and
the hull changes), trim stays accent, nothing see-through. Other vessels' hulls unchanged.

Round-17 verify: attach to ONE ribbon of a Squirrel wake → you ride that ribbon's own prisms,
not the corridor; the other ribbon is a separate trail you can attach to independently. Ride a
SHIELDED trail → the ship clears the octahedron shells instead of passing through them. Ride a
drift-laid (yawed) trail → the ship goes around the wider effective prism, no corner clipping.
Look at the Urchin in the hangar/menu → the whole hull reads solid, matching the left shroud;
no part is see-through. Other vessels' hulls unchanged.

Round-16 verify: ride a SPARROW trail — the Urchin sits ON the prisms, not in them; roll and
it walks around the trail's axis, belly always to the rail; roll continuously and it circles
smoothly with no fight. Ride a wide flat trail — it hugs the broad face and stands off at the
edges. Squirrel twin trail — unchanged. Camera sits noticeably closer.

Round-15 verify: Squirrel-hit-crystal ring → the Urchin rides it as a LOOP, forward and
backward, round and round, never rolling onto it as a surface. Fly at an isolated prism → no
attach. Ride a gyroid → pitch/roll/aim are completely free, camera never fights, and you can
shoot where you please while rolling. HexRace: skimmer trail alignment on the waypoint track
still works.

Round-14 verify: fly the Squirrel STRAIGHT to the vessel changer, swap to Urchin, attach to
the Squirrel trail — it now rides as a TRAIL (clean 1D slide down the spine, no marble
flailing, no ribbon-hopping, no shielding both trails). Ride the Urchin's own trail around a
tight bend — direction holds through the apex, no rapid forward/back swapping. Turn-reset
modes (any minigame turn end) still clear trails cleanly.

Round-10 verify (`URCHIN_TRAIL_RIDER.md` steps 7/8/8a): release mid-grind → coasts to a stop;
grind onto an enemy trail → brakes rather than snaps; latch on at speed holding forward →
carries the speed, eases onto the rail, no sideways pop; full-speed run down a long ribbon → no
tick in position, heading OR the orbit frame; reverse → swings through zero.

Full mechanics, historical record and follow-ups:
`_Scripts/Controller/Vessel/R_VesselActions/URCHIN_CHAIN_SPIKES.md` and `URCHIN_TRAIL_RIDER.md`.
Element map: `Docs/ElementalAbilitySystem/FLEET_MAPS.md` §2 Urchin.

**What landed**

- **Chain-reaction spikes.** A spike lands on a prism, **embeds**, **steals** it (domain flip —
  mass conserved, nothing destroyed), then fires its own `LoadedGun` to spray the next generation
  out of the converted mass. Three effect SOs run in list order —
  `ProjectileEmbedPrismEffectSO`, `ProjectileStealPrismEffectSO` (pre-existing),
  `ProjectileChainFirePrismEffectSO` — which is the modern form of the 2023 enum list
  `[Stop(12), Steal(7), Fire(13)]` still sitting as orphaned YAML on three of the four spike
  prefabs. **Order is load-bearing**: steal before fire, or every child re-converts ground the
  parent already took.
- **Three brakes, in order of authority.** (1) *Territory conversion*, emergent and **PRIMARY** —
  `Projectile.DisallowImpactOnPrism` refuses a prism already wearing the firing domain, so the
  wavefront extinguishes as it eats its own frontier. This was the only brake in 2023 and is
  deliberately kept primary. (2) *Depth* — `Projectile.ChainGeneration`, zero terminal, scaled by
  CHARGE, **shipping at 2** (3 and 4 supported and deliberately unshipped, on collider budget).
  (3) *Load shedding* — `ChainReactionBudget.VolleysPerFrame` = **4**, which **drops** excess
  volleys (never queues) and warns on a 5 s throttle. At depth 2 a volley is 10 spikes, so one
  frame's chain contribution is bounded at **40** live trigger colliders.
- **Two 2023 defects fixed rather than reproduced.** The original had **no depth cap** (its base
  tier fired children of the same tier, so only territory ever stopped it) and **leaked its pool
  permanently on success** (`Stop` killed the coroutine whose terminal statement was the only
  cleanup call, so every spike that actually hit something was immortal — after the pool port that
  drains the pool). `Projectile.EmbedAndRetire` now owns the retirement explicitly and **fades**
  the spike rather than popping it.
- **Determinism.** `Gun.FireSpherical` uses `Gun.DeterministicOrientation(origin, depth)` — a
  quantized position hash — instead of `UnityEngine.Random.rotation`, so every peer's cascade
  agrees, **and** so a gun firing dozens of times a second cannot perturb the global RNG stream
  that deterministic systems seed (the HexRace track calls `Random.InitState`).
- **Spike domain paint** moved out of `Start()` (which ran before `Initialize` on a fresh instance
  and never again on pool reuse) into `LaunchProjectile`, via `sharedMaterial` + a
  `MaterialPropertyBlock`.
- **Trail rider.** Attach is two flags (`IVesselStatus.IsAttached` + `.AttachedPrism`) set by
  `VesselAttachPrismEffectSO`; `GunVesselTransformer` edge-detects them and `Slide()` replaces
  `base.MoveShip()`. Crossing a prism boundary fires `FinalBlockSlideEffects()` — restore if
  destroyed, **grow** if friendly, **steal** if hostile. Riding recharges ammo (doubled on
  shielded prisms).
- **Three ways it was broken, all fixed.** The transformer resolved `BlockscapeFollower` (a
  surface-crawl experiment) instead of `TrailFollower` (the maintained along-the-trail kernel) —
  identical member names, so it compiled, but only `TrailFollower` calls back into
  `FinalBlockSlideEffects`, so the entire payoff never ran. The throttle was hardcoded
  `var throttle = 0;`, so a vessel could attach and then sit motionless forever. And the ride never
  fed `AdvanceSpeed`, so detaching snapped the pilot back to a stale cruise speed.
- **PLATFORM: `VesselDamagePrismEffectSO` declines while `IsAttached`** (new `skipWhileAttached`,
  default **on**). Riding and ramming are the same collision through one flat effect list with no
  ordering guarantee, so **any** attaching vessel would destroy the prism it latched onto — the
  2023 "urchin destroying first block when attaching to a trail" bug, whose fix lived in a
  collision path that no longer exists. Guarded once, for the fleet. This is the change most
  likely to have unintended reach: it touches an effect asset every vessel lists.
- **Element map (APPROVED this session).** SPACE → Spike Volley (`RightStickAction`), CHARGE →
  Spike Barrage (`LeftStickAction`), MASS → Trail Rider (**passive**, `Input 0`), TIME → Slip
  (`Button2Action`). All four L5 upgrades gate on
  `R_VesselElementalAbilityHandler.IsUpgradeActive(element)` — the replicated unlock bit — never a
  raw local level read.
- **Scoring.** `Player.ReportPrismStolen_ServerRpc(float volume)` + `StatsManager.CreditPrismSteal`.
  `StatsManager.PrismStolen` opened with `if (!_allowRecord) return;` and `_allowRecord` is false
  on clients, so **a client's steals scored nothing** — a gap that predates the Urchin and affects
  every steal source in the game. Only the stealer's half travels (identity comes from RPC
  ownership); the victim's remaining-mass tally drifts on a client-side steal, a deliberate trade
  (an untrusted client-supplied name is worse than a soft tally).
- **Registration.** `Urchin.prefab`'s `VesselStatus.vesselType` was `Random(0)`, so
  `TryGetShipPrefab` could never match it even once listed. Now `Urchin(4)`, and added to
  `SO_Classlist_Classes.asset` and `Vessel Prefab Container.asset`.

**Wiring — confirm it landed, and close the one that has not**

*State below is as of commit `b3bc963bc`. The branch was still moving while this was written, so
re-check each line against the prefab rather than trusting the tick.*

1. **Spike prefabs — authored, needs an import check.** Three new fully-wired prefabs exist:
   `_Prefabs/Projectile/UrchinSpikeProjectile{,Energized,SuperEnergized}.prefab`, each a trigger
   `SphereCollider` (r 0.25) + `Rigidbody` + `Projectile` + `ProjectileImpactor` (container
   pre-wired to `UrchinSpikeProjectileImpactContainer.asset`) + `ImpactCollider` + `LoadedGun`.
   They were written as YAML **outside the editor**, so open all three and confirm no missing
   scripts and no unassigned container — a spike with no impactor passes through everything,
   silently.
   The four **2023** prefabs under `_Prefabs/Environment/*SpikeProjectile.prefab` are superseded
   and referenced by nothing; three still serialize the orphan keys
   (`trailBlockImpactEffects: 0c000000070000000d000000`, plus `Team`, `Ship`, `Velocity`,
   `ProjectileTime`) that the script no longer declares. Leave or delete, but do not wire them.
2. **Projectile pools — landed, sanity-check the ceilings.** `Urchin.prefab` now carries its own
   `ProjectileFactory` with three `ProjectilePoolManager`s: Normal → `UrchinSpikeProjectile`
   (capacity 120, **max 400**), Energized → `…Energized` (40 / **160**), SuperEnergized →
   `…SuperEnergized` (12 / **48**). `maxSize` is the real ceiling on live spike colliders — a
   drained pool is a hard stop (`No pool registered` / a factory miss), but an unbounded pool is a
   collider leak with no ceiling at all, so a bound is the right trade. Cross-check these against
   the **depth-2** row in `URCHIN_CHAIN_SPIKES.md` § "Collider budget" once the cascade is
   observable — and note this is a **per-vessel** factory, so four Urchins in a match multiply
   these numbers by four.
3. **Action wiring — landed.** `Urchin.prefab` now has `_executors` pointing at an
   `ActionExecutorRegistry` and `_inputEventShipActions` binding `RightStickAction(1)` →
   `UrchinSpikeVolleyAction`, `LeftStickAction(2)` → `UrchinSpikeBarrageAction`,
   `Button2Action(7)` → `UrchinSlipAction`. Trail Rider is correctly **unbound** — it is passive.
   The spike executor's `gun` and `barrageOrigin` are assigned; **confirm `muzzles` is populated**
   (it falls back to `barrageOrigin` if empty, which turns the aimed volley into a single-origin
   shot).
4. **Ammo resource — STILL OPEN, and it breaks two things.** `ResourceSystem.Resources` on
   `Urchin.prefab` is `[]` and every `ammoIndex` is 0. The volley (`ammoCost` 0.15) refuses to fire
   with `Invalid ammo index or ResourceSystem`, and `GunVesselTransformer.SlideActions` throws an
   **`ArgumentOutOfRangeException` every frame of a ride** (`ResourceSystem.ChangeResourceAmount`
   indexes without a bounds check). The barrage is free, so it will fire and mask this.
5. **`VesselCustomization._shipGeometries` — landed**, 13 entries. Without it
   `UrchinSlipActionExecutor` warns `found no ShipGeometries` and Slip detaches **without** phasing
   out, so the vessel re-latches on the very next prism — exactly the failure the ghost exists to
   prevent. Confirm those 13 objects actually carry `Collider`s; the executor collects only the
   ones that do.
6. **Vessel impact container — landed.** `vesselImpactorDataContainerSO` points at
   `UrchinImpactorDataContainer.asset`.
6b. **Netcode components are deliberately NOT wired** — no `NetcodeHooks`,
   `NetworkVesselClientCache`, `NetworkVesselImpactor` or `ClientNetworkTransform` on
   `Urchin.prefab`. Multiplayer spawn is its own pass, so the MPPM steps below cannot run until it
   lands. Do the single-player steps first and treat the MPPM block as blocked, not as failing.
7. **HUD row — STILL OPEN.** `UrchinVesselHUDView` exists (ammo fill + a deliberately binary riding
   indicator); the four `abilityIcons` bindings still have to be authored on the HUD prefab in
   charge → mass → space → time order. Run **FrogletTools > Vessels > Audit Vessel Ability Rows**
   afterwards — Trail Rider's `Input = 0` will look like an unset field and is **correct** (it is
   passive; the map cannot distinguish the two).
8. **Three `GunVesselTransformer` fields are not serialized on the prefab** — `throttleDeadband`
   (**0.1**), `throttleRestPosition` (**0.5**), `facingFlipThreshold` (**0.35**). (An earlier
   revision of this list named `throttleZeroPosition` and `reverseLookThreshold`; neither ever
   shipped.) They deserialize to their
   C# initializers, which is correct behaviour, but they will only appear in the inspector after a
   re-save. Never let `throttleDeadband` reach 0: `RideTheTrail` divides by `Throttle × speed`.

**Verify in editor**

9. Project compiles with zero errors, the `CosmicShore.Tests.EditMode` suite passes (it gained
   `_Scripts/Tests/Editor/UrchinChainReactionTests.cs`), and
   `python3 Tools/Build/author_urchin_assets.py --check` passes (it validates every YAML key
   against the serialized fields of the class each asset's `m_Script` points at — a key Unity does
   not recognise is silently dropped and the field reads its initializer forever).
10. **Urchin is selectable and spawns.** Menu_Main → vessel changer toy, or any mode's vessel
   select. It flies and lays a trail. **NO HUD APPEARS, and that is the known state** — there is
   no `UrchinHUDVariant.prefab`, `vesselHUDController` is `{fileID: 0}`, and the
   `UrchinVesselHUDController`/`View` pair is referenced by nothing. Do not treat a missing HUD
   as a failure of this step. (`vesselType` was `Random(0)`; if it fails to SPAWN,
   `TryGetShipPrefab` is not matching.)
11. **One spike, one steal.** Fire the volley (RT) at an **enemy** trail at Charge 0 (depth 1): the
    spike stops in the prism, the prism changes to your domain, **8** children spray out of it, and
    the original spike fades out ~3.75 s later rather than popping or standing there forever.
12. **The cascade dies by eating its frontier.** Same volley into a trail that is *already yours*:
    nothing beyond the spike stopping — no steal, no children. Into open space: the spike expires
    at 2 s and returns to its pool.
13. **Depth scales with CHARGE.** At Charge 10 the first landing should spray **10** and each of
    those should spray again. Still 8 means the level read is not reaching `ResolveGenerations`.
14. **Reach scales with SPACE, and decays.** Space 0 vs Space 10 should visibly differ (muzzle
    speed × 0.4 vs × 2.5), and within one cascade each generation should reach shorter than the
    last (falloff 0.75). At Space 5 ("Deep Cascade") the last generation reaches as far as the
    first.
15. **The barrage is free and shallow.** LT at Charge 0: a golden-spiral burst from the hull that
    steals and does **not** chain, costing no ammo. At Charge 5 ("Overcharge") the same press
    should chain one generation.
16. **Shielded mass costs no generation.** Spike a shielded prism: the shield sheds, the prism
    stays hostile, **no** children fire. Super-shielded: nothing happens at all.
17. **Pool integrity — the 2023 regression.** Fire ~50 volleys into dense enemy mass, then keep
    firing. The gun must not go quiet. A silent stop means a spike that HIT (step 10's fade) or a
    spike that MISSED (`projectileEndEffects` on the container) failed to return.
18. **The frame brake reports itself.** Drive a deep cascade into a wall of enemy trail; the
    console should show `[ChainReactionBudget] Shed a chain volley - ceiling is 6/frame`. Seeing it
    is correct — it is how you tell a short cascade caused by brake 1 from one caused by brake 3.
19. **Domain paint.** Fire as Jade, change domain at the domain-changer toy, fire again: spikes
    must wear the **new** colour. The previous domain's material means the paint drifted back to
    `Start`.
20. **Attach — and the prism survives.** Fly into your own trail: the vessel snaps onto the ribbon,
    the camera pulls in, the stick stops steering. **The prism you touched must still be there.**
    A prism that explodes on contact means `skipWhileAttached` is not taking effect — that is the
    2023 bug, and it is the single most important observation in this list.
21. **The ride moves, and the payoff runs.** On the throttle the vessel travels the ribbon; off it,
    it holds still. Your own trail's prisms visibly **grow** as you cross them; an enemy's change
    to your domain. Moving but no grow/steal means the transformer resolved `BlockscapeFollower`.
22. **Reverse.** Riding, look back past ~126° from your course while on the throttle: direction
    flips.
23. **Destroyed prisms restore**, and the ammo meter climbs while riding — visibly faster over
    shielded prisms.
24. **Hostile trail is a slog, until Time 5.** Ride an enemy ribbon at Time 0 (10 vs 150 — a
    fifteenth of the speed). At Time 5 ("Slipstream") it should run at full pace while still
    stealing.
25. **Reinforced Wake.** Mass 5, ride your own trail: grown prisms come up **shielded**
    (octahedron shells), and riding back over them pays double ammo.
26. **Slip.** Riding, press B: you let go and can fly out through the trail for ~0.6 s without
    re-latching. Fly back in — you must re-latch, i.e. the colliders came back.
27. **Interrupted slip (the dangerous one).** Slip, then immediately swap vessels / end the turn /
    respawn. The hull must be **solid** afterwards. A vessel that can now fly through the whole
    prismscape means the `finally` restore did not run.
28. **Detach speed.** Ride at full pelt, then slip: the vessel carries a sensible speed out instead
    of snapping back to the cruise it had when it latched on.
29. **Refused attach.** Touch a prism with **no trail** (an environment/flora prism, a fauna body
    prism). The vessel must keep flying normally. Freezing in place means `IsAttached` stayed set
    on a refusal.
30. **A domain change mid-ride.** Ride your own trail, change domain at the toy, ride the *same*
    trail: it must now read as **hostile** (slow, and it steals). Still growing means the live
    `Domain` property got snapshotted again.
31. **REGRESSION — the rest of the fleet still rams.** `skipWhileAttached` lives on an effect asset
    every vessel lists, so spot-check a Squirrel and a Rhino destroying prisms by hull contact, and
    a Rhino sword swipe. They never set `IsAttached`, so the guard must be a no-op for them.
32. **REGRESSION — the HexRace track is unchanged.** Load `MinigameHexRace` twice at the same
    intensity and confirm the track is identical, then do it again after an Urchin has fired
    several hundred spikes in a prior match in the same session. This is the
    `Random.rotation` → `DeterministicOrientation` fix; a track that differs means something still
    perturbs the global stream.

**MPPM — two clients** (the replicated unlock bits and the steal RPC)

33. **Same cascade on both screens.** Client A's Urchin fires a depth-2+ volley into a trail; host
    and client must see the same fan and the same set of converted prisms. A visibly different
    spray is `Random.rotation` creeping back into `FireSpherical`.
34. **Space 5 on the CLIENT.** Take the client's Urchin to Space 5 and fire a deep cascade — the
    **host** must see the same non-decaying reach and the same converted prisms.
35. **Charge 5 on the CLIENT.** Fire the barrage — both peers must see it chain.
36. **Mass 5 on the CLIENT.** Ride the client's own trail — the **host** must see the grown prisms
    arrive **shielded**. Unshielded on the host means the gate is reading a local level instead of
    `IsUpgradeActive`.
37. **Time 5 on the CLIENT.** Ride a hostile ribbon — the host must see it move at the fast speed.
38. **Steal RPC — the client scores.** With the client's Urchin, convert ~20 prisms by spike and
    ~20 by riding, then read the client's own `PrismStolen` / `VolumeStolen` on the scoreboard.
    Both must be non-zero; before `ReportPrismStolen_ServerRpc` they were zero. Then check the
    **victim's** `PrismsRemaining` / `VolumeRemaining`: it will **not** have been debited for the
    client-side steals. That is the deliberate trade, not a bug — but confirm it does not put a
    scoreboard into a nonsensical state (negatives, a total that exceeds the cell's mass) in the
    modes that display remaining mass.
39. **Do the same from the HOST's Urchin** and confirm the server does not **double-credit** —
    `StatsManager.OwnsAttacker` is what stops the server also recording a steal a remote player
    already reported for itself.

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Cascade depth | `UrchinSpikeVolleyAction.asset` `generationsAtRestingCharge` / `generationsAtFullCharge` | **1 → 2.** Linear in level, anchored at 0 and 10, extrapolated across `[-5, 15]`, clamped `[0, 4]`. Worst case per seeded hit: depth 1 → 8 spikes, **depth 2 → 90 (what ships)**, depth 3 → 1,092, depth 4 → 15,302. Depth 3 was authored first and pulled: it is indefensible against a per-cell collider budget already at 3–4k against a 1,500 target. 3 and 4 stay supported by the SO and deliberately unshipped. |
| Barrage depth | `UrchinSpikeBarrageAction.asset` same pair | **0 → 1**, plus `chainsOnChargeUpgrade: 1` (the L5 floor). The barrage is free and pays for it with shallowness. |
| Per-generation reach decay | both spike assets `generationRangeFalloff` | **0.75**, clamped `[0.05, 1]`. 1 is the SPACE-5 upgrade. Lower makes a deep cascade read as a *wave*; 1 makes it read as an expanding sphere. |
| Frame ceiling | `ChainReactionBudget.VolleysPerFrame` (public static, **code**) | **4**, global across every Urchin in the match. If cascades feel truncated, check the console warning first — this drops, it does not queue. |
| Volley cost / rate | `UrchinSpikeVolleyAction.asset` | 0.15 ammo @ 3/s, muzzle speed 60 × SPACE (0.4 … 2.5), flight 2 s |
| Barrage cost / rate | `UrchinSpikeBarrageAction.asset` | **free** @ 1/s, muzzle speed 40 × SPACE, flight 2 s |
| Spike dwell / fade | `ProjectileEmbedPrismEffect.asset` | **3.75 s** / 0.35 s — pure look; the steal and the volley already happened. `fadeSeconds` must stay > 0 (continuity of existence). |
| Ride speeds | `Urchin.prefab` `TrailFollower` | Friendly **150** / Hostile **10** / Destroyed **10**. The 15× gap is what Slipstream buys, and it is the biggest single number in the vessel. Note `BlockscapeFollower` on the same GameObject serializes an identical trio that nothing reads. |
| Growth per ridden prism | `Urchin.prefab` `GunVesselTransformer.growthAmount` | `ElementalFloat`, element Mass, 0.6 → 1.2 (`Value` 1) |
| Ride ammo recharge | `Urchin.prefab` `GunVesselTransformer.rechargeRate` | 0.1/s, **×2** on a shielded prism |
| Throttle shaping | `GunVesselTransformer` (C# defaults, **not yet serialized on the prefab**) | `throttleDeadband` **0.1** · `throttleRestPosition` **0.5** · `facingFlipThreshold` **0.35**. A deadband of 0 means the ride never parks at rest (there is no divide by throttle — an earlier revision of this row claimed one). |
| Slip ghost | `UrchinSlipAction.asset` | 0.6 s → 1.6 s, `detachImpulse` **0** (off) |
| Steal→score weight | mode `ScoringRuleSO` | unchanged; the RPC only makes a client's steals *count*, it does not weight them |

**Known gaps at time of writing** — no audio is authored on any Urchin surface (per the FMOD
convention the spike embed/steal/chain-fire and the attach/ride/slip each want their own
`EventReference`, shipped **empty**); `AIPilot` has no notion of either ability, so an AI Urchin
neither fires nor rides and will sit at zero throttle if it attaches by accident; edit-mode
coverage is narrow — `UrchinChainReactionTests` pins the depth curve, the ghost window and volley
determinism, but nothing covers the effect trio or the budget; the Urchin lists no
`VesselChangeSpeedByPrismEffectSO`, so it takes **no speed penalty from any prism** (danger prisms
included), joining Rhino and Serpent in that gap; `StatsManager.CreditPrismSteal` is called only
by the RPC while `PrismStolen`'s server branch still hand-rolls the same four lines its docstring
says it was extracted to share; the steal-scoring trade is **not** recorded in
`Docs/ScoringSystem/BUGS.md` despite `Player.cs` saying it is; and the four 2023
`_Prefabs/Environment/*SpikeProjectile.prefab` are now superseded by the three under
`_Prefabs/Projectile/` — `RecursiveSpikeProjectile.prefab` in particular is an artefact of the
same-tier recursion loop with no tier in `ProjectileFactory`.

---

### 🔴 Dolphin elemental map re-cut around one weapon (`claude/dolphin-elemental-upgrades-umokil`)

Authored without a Unity compile or play-test. Every element on the Dolphin now owns one
orthogonal dimension of its single offensive act, and the HUD row was re-cut to match. Full
record: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` §8.

**What landed**

- **Charge → Echo Sight + blast THICKNESS.** `_coreMultiplierAtRestCharge 0.75` /
  `_coreMultiplierAtFullCharge 1.5` on `DolphinVesselExplosionByCrystalEffect`, via the new
  `ElementalScaling.MultiplierFromRest` (the fleet's first multiplier NOT anchored at 1 at rest —
  so the authored core is now what a mid-Charge Dolphin fires and a fresh pilot's beam is
  deliberately thinner).
- **Charge 5 = Pilot Echo.** Vessels inside the blast volume brighten in their own domain colours
  (`EchoSightVesselHighlighter` → `_ColorMultiplier` MPB per material index; `BlastVolume.Contains`
  is the CPU twin of the sweep job's predicate).
- **Mass → crystal seeding.** Recharge multiplier renamed `cooldownMultiplierAtFullMass`
  (`[FormerlySerializedAs]`). **Twin Seed retired** — one crystal per cycle. **Mass 5 = Claimed
  Seed**: the seed is an omni crystal (`Crystal.prefab`, `ownDomain = Blue`, lime CTA) below the
  upgrade and a team crystal (`TeamCrystal.prefab`, own domain) above it.
- **Mass no longer touches the trail.** `trailVolume` disabled, `massUpgradeShieldsTrail 0` on
  `Dolphin.prefab`.
- **HUD row re-cut**: Charge = new procedural `BlastProfileGraphic`; Mass = crystal recharge (pips
  deleted); Space = jaws + a widened prism tally; Time = the boost ring. Prefab YAML was
  hand-authored, and the row wirer was re-cut to the same layout and generalized fleet-wide as
  `VesselAbilityRowWirer` (`FrogletTools > Vessels > Wire Vessel Ability Row`).
- **Second pass (colour + subtraction).** The Charge profile crosses the shared palette's
  **grey → white** (idle → in use) instead of a bespoke warm colour. The Space **reach bar was
  dropped entirely** — that slot says angle and amount only. And `BlastProfileGraphic`'s outline
  winding was fixed: it was measuring the end caps from the wrong basis vector and rendering a bowtie.
- **Third pass (all three from playtest).** (a) The Mass slot rendered **black** — `DullCrystalColor`
  is authored (0,0,0) on Jade/Ruby/Gold in the live palette. Replaced with the new
  `SO_ColorSet.GetDomainSignalColor` (domain UI colour, brightest channel driven to 1);
  `Docs/PALETTE.md` §2.4. (b) **Pilot Echo was indistinguishable from the lit prisms in Rampage.**
  Brightness was the one channel the ability itself had already saturated, so the hull is now driven
  to its **saturated domain colour** (`_Color1`/`_Color2`) AND gets an additive **halo** — a disc with
  a hard ring at the silhouette, drawn `ZTest Always` so it reads through mass and in empty space
  (`_Graphics/Materials/Graphs/EchoSightHalo.shader` + `Resources/EchoSightHalo.mat`, both new,
  hand-authored). (c) The Charge slot now reports **pilots debuffed** and **creatures killed** after
  each blast, two stacked bare numbers in the Space tally's grammar, told apart by palette colour.
- **Fourth pass.** The halo no longer shrinks with distance: its radius is
  `max(world size at this depth, vesselHaloMinScreenRadius)` computed in the vertex shader, so past
  ~750 u it holds a constant angular size (measured: 59 px diameter at 1080p, vs ~20 px before, at the
  2400 u max reach). The offset moved from view space to CLIP space to do it. Also recorded: the
  sight's range gate was already Space-driven and already covers fauna/flora — no change needed
  there — with crystals the one thing it does not reach (`DOLPHIN_CRYSTAL_SEEDING.md` §11).
- **Fifth pass — the PRISM half.** The sight now samples its volume **once per prism** (object
  origin) instead of per fragment, so a prism lights up WHOLE. That matches
  `AOEConicSweepQueryJob`, which tests one point per prism and destroys the whole prism — the
  per-fragment version was painting a shape the blast does not operate on. Colour went warm amber
  → pale cool blue `(0.45, 0.70, 1.0)` and gain 1.15 → 0.70 (`DOLPHIN_CRYSTAL_SEEDING.md` §12).

**Verify in editor**

1. **Compile.** Two new scripts ship with hand-authored `.meta` GUIDs
   (`BlastProfileGraphic` = `3d1c8a7e…`, `EchoSightVesselHighlighter` = `7a4e2f9c…`); the HUD prefab
   references the first by that GUID. If Unity re-mints either GUID on import, the Charge slot's
   profile binding breaks — check the `Profile` object on `DolphinHUDVariant` still has its script.
2. **Run FrogletTools > Vessels > Audit Vessel Ability Rows.** Dolphin should report map complete,
   4/4 icons, order ✅.
3. Open `DolphinHUDVariant.prefab`: four slots left→right must be ProfileButton, CrystalButton,
   JawButton, DriftButton. `CrystalPip0/1` should be **gone**.
4. Freestyle on the Dolphin: the Charge slot draws a **solid capsule** (not a bowtie, no hollow
   wedges) that grows with banked energy; raising Charge fattens and shortens it without changing its
   overall extent. It sits at the palette's grey when RT is released and crosses to white while held.
5. **Hold RT** — prisms in the blast volume light warm as before. At Charge 5, another vessel that
   enters the cone brightens in its own domain colours and fades back out when the cone leaves it.
   Release RT and confirm every vessel returns to its exact prior brightness (no lingering glow).
6. Swap vessel while holding RT — no vessel is left over-bright (`HardReset` → `ClearAll`).
7. Idle a cooldown below Mass 5: the seeded crystal wears the **lime CTA**, the Mass slot is lime,
   and a rival-domain vessel can collect it. Raise Mass to 5: seeds arrive in your domain's colours,
   the Mass slot crosses to that same domain colour, and a rival cannot collect them. Then use the
   freestyle domain-changer toy — the slot must follow the new domain.
8. Lay a drift trail: prisms are **not** shielded and do **not** grow with Mass.
9. Fire a blast at a dense wall and confirm the tally under the jaws renders a 4–5 digit number at
   full size without auto-shrinking.
10. **MPPM two clients** — Pilot Echo is local-only presentation, but the unlock bit is replicated;
    confirm a remote Dolphin holding RT does not brighten anything on the other client.
11. **The halo shader is the highest-risk item on this branch** — a hand-written ShaderLab pass that
    no compiler here can check. Confirm `EchoSightHalo` compiles with no errors, and that
    `Resources/EchoSightHalo.mat` resolves it (a magenta or invisible quad means it did not). Its
    `.shader.meta` GUID is hand-minted (`6c2b9d4a…`) and the material references it by that GUID; if
    Unity re-mints it, re-assign the shader on the material.
12. **Halo through mass.** At Charge 5 in **Rampage** (the case that motivated it — ~9,800 cactus
    prisms all lit at once): hold RT with a rival in the cone and confirm the halo reads (a) in open
    space, (b) surrounded by lit prisms, (c) fully behind prisms. Confirm the ring lands on the hull's
    silhouette rather than inside or well outside it, and that it never occludes anything (ZWrite is
    off) or darkens the ship (additive).
13. **Halo at range.** Mark a rival across the arena (≥1000 u) and confirm the halo is clearly
    visible and roughly the same on-screen size as one at 800 u — that is the floor working. Up close
    the ring should still trace the hull's silhouette; at range it becomes a reticle around the ship,
    which is intended. Check it stays CIRCULAR at a non-16:9 aspect (the shader carries an explicit
    aspect correction) and that it does not jitter or pop at the crossover depth.
14. **Two rivals of different domains in one cone** must be tellable apart — each halo and hull wears
    its OWN domain colour, never a shared highlight colour.
15. **The prism half.** Hold RT near a wall of prisms: each prism must be lit ALL-OR-NOTHING with a
    jagged prism-granular boundary, never a smooth cut across a prism's face. This is the item most
    likely to fail to compile — it reads the object matrix in the FRAGMENT stage, which is supported
    but is not proven elsewhere in this project (the one precedent, `PrismClockAnimation`'s jiggle, is
    vertex-stage). A silent failure mode to watch for under DOTS instancing: if the instance ID is not
    set up in the fragment, EVERY prism would light off one instance's origin — i.e. all or none light
    together regardless of where the cone points. If that happens, set `PRISM_SIGHT_WHOLE_PRISM 0` to
    fall back and report it.
16. **Sight brightness/hue.** Prisms should read as *lit*, not washed to white, with their tier
    colours still visible through the cast. Check a SHIELDED prism specifically — the frosty tier is
    the one the new cool hue could be confused with. If it reads as a tier change, lower
    `PRISM_SIGHT_GAIN` rather than changing the hue.
17. **The living tally.** Fire a blast that catches a rival and some fauna: the Charge slot shows a
    white pilot count and a blue creature count, both fading after ~2.5 s. Fire one that catches
    neither and confirm both stay blank (no "0"). Then fire two blasts back to back inside the 0.15 s
    cooldown — the fauna count may be shared between them; that is the documented window limitation,
    not a bug to chase.

**Verification matrix (what was actually verified, and how)**

| System changed | Verified how | Result |
|---|---|---|
| `PrismDestructionSight.hlsl` (whole-prism sampling, colour, gain) | **clang compile + run of the shipped file** | Compiles. Deep-inside adds exactly `gain × blue × CORE_FILL`; outside-gape / behind-vessel / past-reach all add exactly 0 |
| The sight's SPACE range gate | **clang run** — same prism at z=1500 with cone height 2400 vs 1200 | Lights at full reach, dark at half. The gate is real and Space-driven (was previously argued from reading the code) |
| `EchoSightHalo.shader` | **clang compile + run of the extracted HLSLPROGRAM** | Compiles (found and fixed a non-portable `0.0h` half-literal). Crossover at 756 u, then a constant 59 px diameter to the 2400 u max reach; x/y pixel extents equal at every depth (circular, not elliptical) |
| `BlastProfileGraphic` outline | **Off-engine Python sim** over 5 (L, R, rotation) cases | Convex, non-self-intersecting; area within 2% of the exact stadium at 10 segments/cap; max vertex step = 2L (the straight edge), proving no jump across the interior |
| All 7 hand-authored YAML component blocks | **Mechanical key↔field parity** vs the C# classes and their bases | Zero unknown keys (the only "unknowns" are `MaskableGraphic`'s own package-side fields) |
| `EchoSightHalo.mat` ↔ shader | **Set comparison** of Properties / material entries / CBUFFER members | All three sets identical — no property that fails to upload, none MPB-only |
| New assets (4 GUIDs) | **GUID uniqueness + meta pairing + m_Script resolution sweep** | Each GUID appears in exactly one `.meta`; no orphan/missing metas; every unresolved `m_Script` in the changed prefabs is a pre-existing package script |
| Deleted prefab sub-objects (pips ×2, ReachBar) | **Repo-wide fileID sweep** | Zero references anywhere |
| Removed/renamed public members (12) | **Repo-wide grep per member** | Zero stragglers; every re-signed member's call sites migrated |
| `VesselAbilityRowWirer` idempotency | **Tool constants diffed against the shipped prefab** | All four band pairs match to 1e-6; Dolphin slot names match the prefab — running it is a no-op |
| Conditional-compilation guards | `Tools/Build/check_conditional_compilation.py` | OK (1676 files) |
| **All C# on the branch** | **NOT COMPILED — impossible in this container** (no Unity managed DLLs, no `dotnet`/`mcs`/`csc`). Verified instead by mechanical symbol checking: every interface member dereferenced, every namespace, every call site, every serialized field name | Consistent, but **the editor is the compile gate** |
| **Everything in play** (look, feel, tuning, DOTS-instanced fragment matrix) | **NOT VERIFIED** — steps 1–17 above are the gate | Human required |

**First-pass tuning (not settled)**

| knob | asset | value |
|---|---|---|
| `_coreMultiplierAtRestCharge` / `AtFullCharge` | `DolphinVesselExplosionByCrystalEffect` | 0.75 / 1.5 |
| `_minCoreMultiplier` | " | 0.5 |
| `vesselHighlightGain` | `Dolphin.prefab` ▸ `EchoSightActionExecutor` | 4 |
| `vesselHighlightSaturation` | " | 0.85 |
| `vesselHaloScale` | " | 2.4 (halo radius ÷ hull radius, close range) |
| `vesselHaloMinScreenRadius` | " | 0.055 (≈59 px diameter at 1080p; the distance floor) |
| `vesselHaloIntensity` | " | 1.4 |
| `vesselHighlightFadeSeconds` | " | 0.18 |
| `_RingWidth` / `_RingGain` / `_GlowFalloff` | `Resources/EchoSightHalo.mat` | 0.12 / 1.6 / 2.5 |
| `maxExtentFraction` / `minRadiusFraction` | `DolphinHUDVariant` ▸ `Profile` | 0.86 / 0.06 |

**Known gap.** `_coreExplosionScale` (320) was authored as the blast's true thickness; it is now
the mid-Charge value, so at rest the beam is 240 and at Charge 10 it is 480 (clamped to the base
diameter). If the resting blast reads too thin in play, raise `_coreExplosionScale` rather than
moving the 0.75 endpoint — the endpoints are the design.
### 🔴 Self-trail contact grace (`claude/vessel-self-trail-collision-tp01j3`)

Authored without a Unity compile or play-test. A pilot's hull and skimmer now ignore a prism
**that pilot laid** for `hullGraceSeconds` / `skimGraceSeconds` (both 1.0 s) after it was laid —
owner-scoped and time-boxed, never domain-scoped, so another player's *and* a teammate's trail
stay interactable from the frame they appear. New config `SelfTrailContactConfigSO` +
`Assets/Resources/SelfTrailContactConfig.asset` (**both the .asset and its .meta were hand-authored
as YAML — Unity has never imported them**). Guards added at the top of the prism branch in
`VesselImpactor.AcceptImpactee` and `SkimmerImpactor.AcceptImpactee`, above the shell-ownership
check. Companion geometry fix in `VesselPrismController.CreateBlock`: the `waitTillOutsideSkimmer`
clearance delay measured `TrailZScale` (= `BaseScale.z`), omitting both `ZScaler` and the MASS
volume multiplier, so upgraded vessels' colliders came on while the prism was still inside the
ship; it now measures `scale.z` with a speed floor and a 2 s ceiling.

**Verify in editor**

1. Project imports clean — confirm `SelfTrailContactConfig.asset` resolves its script (not
   "missing MonoBehaviour") and shows both grace fields at 1. A hand-written GUID pairing is the
   single most likely thing to have gone wrong here.
2. **Squirrel, freestyle:** drift a tight circle — Charge/boost must NOT climb off the ribbon you
   are laying. Cross trail older than a second — skim energy resumes.
3. **Dolphin:** bank skim energy, then drift the hull across your own fresh ribbon. Energy and
   charged boost must NOT halve and there must be NO `VesselImpact` SFX. Against an *older* stretch
   of your own trail it must still ram, sound, and cost you.
4. **MASS 5 on the Squirrel** (Heavy Trail → shielded drift prisms) and repeat (2). This is the
   case that proves the guard sits above the shell-ownership check.
5. **MPPM, two clients:** trailing pilot skims the leader's trail from the frame it appears and
   reaches joust range. Repeat with both on the SAME domain — still skims. (A domain-scoped fix
   would have broken this; it is the regression to watch for.)
6. **Rhino (regression check):** cutting your own older trail must STILL bank sword energy at the
   signed-off 0.04/prism. The grace is not expected to reach this vessel — it cannot turn onto its
   own freshest ribbon inside the window — so any change here is a bug in the grace.
7. Delete the asset once and confirm the code defaults still apply and the rule still holds.

**First-pass tuning** (starting points, not settled)

| Knob | Value | Notes |
|---|---|---|
| `hullGraceSeconds` | 1.0 | Raise if vessels still clip their own ribbon mid-drift. |
| `skimGraceSeconds` | 1.0 | Lower if self-skim feels dead coming out of a drift. |
| `MaxClearanceWaitSeconds` | 2.0 | Const guard, not a dial — only reached at very low speed. |

Full record: `Assets/_Scripts/Controller/ImpactEffects/SELF_TRAIL_CONTACT.md`.

---

### 🔴 Shield morphs → GPU; the last CPU prism ticker deleted (`claude/octahedron-shield-gpu-morph-4nnw1s`)

Authored without a Unity compile or play-test. The octahedron shield's engage bloom and its
disengage shatter — and the stellated super-shield's pair — were the last per-frame CPU prism
animation: `PrismOctahedronShieldManager` ticked every morphing shield and each one REBUILT A
MESH per frame, on the un-batched GameObject renderer. Both are now `f(clock, stamp)`
(`Docs/PRISM_ANIMATION.md` §4.8, §5 B4) and **the manager is deleted**, which completes Phase B
of the clock-material migration.

The mesh generators bake each vertex's own **face centroid** into TEXCOORD1, so the cache-shared
**settled** shield mesh is also the morph mesh: `SetRenderMeshOverride(sharedMesh)` at engage is
now the only render call, `SetExoticVisualActive` is never driven true again, and same-size
shields stay in ONE batch through the whole animation. The shatter overlay's per-prism child
GameObject is replaced by batched pure-entity debris (`PrismShieldShatter`), and is deliberately
no longer cancelled on re-engage — deleting visible shards mid-flight breaks continuity of
existence. `AnimationCurve.EaseInOut(0,0,1,1)` is exactly `smoothstep` (zero end tangents), so every
shield whose component is added at runtime is reproduced exactly and the curve fields are
retired. **Two prefabs are a deliberate exception**: `BlueBlock.prefab` (live in three
multiplayer scenes) and `OctahedronShieldTest.prefab` serialize a hand-altered curve with end
tangents 2 — fast-slow-fast, up to 0.192 from smoothstep — and now ease like the fleet. If
BlueBlock's shield bloom reads differently from the others, that is this, and it is expected.

**Editor risk specific to this change**: the graphs were edited as JSON out of the editor
(`Tools/Shaders/wire_prism_shield_morph.py`), so the first thing to confirm is that BlockGraph
and ExplodingBlockGraph still import clean. A shield that appears full-size with no bloom is
un-imported wiring, and it now says so via `WarnUnwiredMaterial` on `_ShieldMorphDuration`.

**Verify in editor** — the six-step playtest is written out in
`Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` ▸ **Phase 9**, with a symptom→cause table. In short:

1. Asset-only gates first: `python3 Tools/Shaders/wire_prism_shield_morph.py --check`, the
   `PrismShieldMorphTests` edit-mode suite, and `FrogletTools > Ecology > Prism Animation >
   Validate Clock Wiring` (both graphs now require the four `_ShieldMorph*` properties +
   the `PrismShieldMorph` node).
2. Open both graphs — no import errors; `ShieldMorphStartTime/Duration/Direction/Offset` on the
   Blackboard. Recovery: `git checkout` the graphs and re-run the script.
3. Skim a trail to shield a prism (bloom), let the shield expire (shatter). Then the Skim Race /
   Astro League track for the stellated tier.
4. Watch draw calls with many shields morphing at once — the whole point is that they do not
   scale with the number of *animating* shields.
5. Pool reuse and the birth snap (pre-shielded environment prisms) are steps 5–6 of Phase 9;
   a stale stamp on a reused prism is loud and unmistakable.

`OctahedronShieldTest.prefab` + its tester still work as the isolated rig: the host has no
`Prism`, so the bloom stamps onto the MeshRenderer's MaterialPropertyBlock instead of an entity
— one write, same shader.

---
### 🔴 Rhino Energy Sword v3 — energize ritual as the supershield key + authored FX pass (`claude/energy-sword-v3-rework-20q3uh`)

Authored without a Unity compile or play-test (mcs-compiled with stubs, capsule crackle HLSL
clang-compiled and sanity-run, prefab YAML machine-validated — but nothing SEEN). Full mechanics,
knob table, and the numbered verification list:
`_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md` § "In-editor verification".
The load-bearing checks, in risk order:

1. **The FresnelGraph fix renders** (carried from v2, still never seen): the blade must read as
   a solid WHITE-hot capsule with the animated Voronoi pattern, brightening as energy banks.
   Magenta or grayscale = the graph edit didn't import; `git checkout` the graph and re-wire
   in-editor.
1a. **It reads as a SWORD at every length** — hilt pinned at the mount, growth going out the
   tip only. Bank energy from empty to full (30 → 120) and watch the near end stay put. If the
   blade still grows out of both ends, the hilt anchor isn't applying (check
   `ShieldSwipeActionExecutor.bladeHalfExtentLocal` = 1 and that the blade mesh really is the
   built-in capsule).
1b. **Colour states are unmistakable**: resting/charging = white-hot (never a domain tint —
   the sword friendly-fires), ENERGIZED = danger red. If white→red doesn't read at a glance in
   the heat of play, deepen `energizedColor` or raise `visibilityMultiplier`; don't reintroduce
   a team hue.
1c. **Tip debris got faster** — `lengthScale` 2 + hilt anchoring roughly double the modelled
   lever arm, so more tip strikes saturate `debrisSpeedLimit` (200). Judge a tip strike vs a
   hilt graze on a trail wall; if tip hits read too hot, lower `swingVelocityScale` on
   `RhinoSkimmerDamagePrismEffect.asset` (do NOT put `lengthScale` back).
2. **The energize ritual**: hold both triggers centered ~1 s → anticipation arcs → white-hot
   ignition + whole-blade crackle burst; ~5 s lit tail after release; ~5 s cooldown. Energized
   contact pops Stella-Octangula prisms; non-energized contact bounces with a dim spark.
3. **The resting-prism edge**: bounce off a super-shielded prism, KEEP touching it, energize —
   it must pop the instant ignition lands (exercises the new shell-tier
   `RedispatchPairsForOwner` + box `ReapplyPrismEffectsToOverlapping`).
4. **The capsule crackle looks right**: arcs ride the blade through swings, ripples proportioned
   along the stretched capsule (not squashed at the tips). Tune on
   `RhinoBladeCrackleMaterial.mat` live (`[ExecuteAlways]`).
5. **Tracers — five hairlines, yours to tune in the inspector.** `Rhino.prefab` →
   `RhinoSwordBladeTracer0..4`, spread tip→hilt down the blade. Nothing in code writes their
   size: set `widthMultiplier` / `time` (and the curve / gradient) per component; the controller
   only re-seats each emitter across ONE evenly-divided span, inset at each end by half the
   width of the streak sitting there so widening an end one grows it into the blade, never past
   the point — spacing stays even even when their widths differ. Authored hairline: 0.5 / 0.15. Add or
   remove array entries on `RhinoSwordFXController.bladeTracers` — the spread follows the count.
   All five tint from the live blade colour, so they should change state with the sword.
5a. **Home position lowered** — the sword mount is now local y **−1** (was 9.38, ~3 units above
   the hull top; −1 is about the hull's own vertical centre). Judge where the grip sits; if the
   sword still reads as towering, the stronger lever is the rest PITCH on the same transform
   (~20° from vertical, was 41.8° historically), not y.
5b. **Swipe recovery**: after a swipe releases, that direction should pause ~0.35 s before it
   can sweep again (each side independent) — and that pause should VANISH while energized. Two
   things must stay true throughout: the blade keeps cutting everything it touches (this gates
   the pose, never damage), and you can still chop/energize while a swipe is recovering.
6. **Base-skimmer non-regression**: any other vessel's skimmer crackle still shows the red
   sphere look (surface mode + material overrides live on the Rhino variant only).

**First-pass tuning** (all on `ShieldSkimmerScaleConfig.asset` unless noted — expect a balance
pass): energize hold 1 s / tail 5 s / cooldown 5 s / cost 0.1 · stance thresholds 1.5 & 0.4
(`RhinoShieldSwipeConfig.asset`) · ignition intensity 2.5 × 5 sites · spark 1.6 @ 14 wu ·
denied spark 0.7 · `restingBladeColor`/`fullEnergyColor` white (energy reads as BRIGHTNESS)
vs `energizedColor` = `SO_ColorSet.Danger` red (state reads as HUE) · `visibilityMultiplier`
1.2 × `fullEnergyBrightness` 1.8 (was 2×2.5 = a 5× HDR white that bloomed into a blob —
raise cautiously) · tracer size is NOT in the config: tune `widthMultiplier` / `time` on the
`RhinoSwordBladeTracer0..4` TrailRenderers (hairline 0.5 / 0.15) · swipe recovery 0.35 s @
engage threshold 0.4 (`RhinoShieldSwipeConfig.asset`) · sword mount y 9.38 → −1.

---

### 🔴 Sparrow rounds grow as they fly + MASS-5 shield restore (`claude/sparrow-spread-haptics-qizbwf`)

Authored without a Unity compile or play-test. Answers *"the only thing that has felt fun was
huge projectiles"* by making huge projectiles **earned**: rounds now leave the muzzle at their
authored size and **swell across the flight** — 3× at resting Mass, 6× at Mass 10, linear in
level and extrapolated to 1.5× / 7.5× at the ends of the [-5, 15] band. Bullets and turret shots
alike (the turret adopts it through `bulletAction`). The visual and the swept hit radius are
scaled by the same factor every frame, so the round-6 honesty rule (hit radius = visible
cross-section +10%) holds at every instant.

**Also an element re-split, by design sign-off:** the Shielded Prisms upgrade returns from
SPACE 5 to **MASS 5** (`FiredPrismState.ShieldedAtSpace5` → `ShieldedAtMass5`, same enum value 3
so the asset is unchanged), leaving SPACE 5 as **pierce only, on both fire modes**. MASS owns the
substance of what you fire; SPACE owns its reach. The Sparrow's map is 4/4 upgrades again.
Full record: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md` ▸ "Round 3".

**Verify in editor:**

1. **Rounds visibly fatten in flight.** Fire at an empty stretch and watch a tracer from muzzle
   to end of life — it should leave thin and arrive noticeably fat. This is the headline.
2. **Length does NOT grow.** Only the cross-section scales; if tracers turn into enormous
   needles, the growth is being applied uniformly.
3. **What you see is what you hit.** A round should destroy prisms at about its *visible* width
   at that moment in flight — not a swath wider than the tracer, and not a thread through the
   middle of a fat bolt.
4. **Mass changes it.** Collect Mass crystals and re-fire: the swell should get dramatically
   stronger (6× at Mass 10 ≈ the old oversized-collider feel). Drain Mass and it should get
   punier.
5. **MASS 5 now shields turret prisms.** Below Mass 5 fired prisms are plain; at 5+ they arrive
   with the octahedron armour and the wider hit sphere. **Space 5 must no longer shield them** —
   it should only make shots pierce.
6. **Pierce is still SPACE 5**, on both fire modes.
7. **No other projectile changed.** Manta rounds, skyburst missiles: `SetFlightGrowth` defaults
   to 1 and only the Sparrow's two fire paths pass anything else.
8. **Asset import.** `FullAutoAction.asset` shows **Round Growth (MASS)** with 3 / 6;
   `FullAutoBlockShootAction.asset`'s *Fired Prism State* still reads the 4th enum entry, now
   labelled **Shielded At Mass 5**.

**First-pass tuning:** `growthFactorAtRestingMass` **3**, `growthFactorAtFullMass` **6** — both on
`FullAutoAction.asset`. Author both to 1 to disable growth entirely.

---

### 🔴 Projectile tunneling — swept prism collision (`claude/sparrow-spread-haptics-qizbwf`)

Authored without a Unity compile or play-test, and the headline change of the branch's second
pass. **A projectile is a teleport, not a sweep**: `Projectile.MoveProjectileAsync` writes
`position += Velocity·Δt` and PhysX samples the discrete trigger once per physics step, so a
Sparrow round at its base 375 u/s tested only **~26% of its own flight path** (~3% at high SPACE,
and it halves again at 30 fps). Prisms in the gaps were passed straight through, silently. That is
why the guns could not clear a small area, and why oversizing the collider "fixed" it — a
12-diameter ball closes a 6.25 u per-frame step.

Fixed with `PrismSpatialIndex.QuerySegment` (the swept counterpart of `QuerySphere`) driving
`Projectile.sweptPrismDetection`, which takes **sole** ownership of prism contact — the trigger's
prism case is suppressed so nothing double-dispatches. Hits dispatch nearest-first, and the round
is moved to each contact point before its impact fires. Opt-in per prefab, enabled on
`SparrowProjectile.prefab` and `Sparrow Projectile Prism.prefab` only. Full record:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md` ▸ "Round 2".

**Verify in editor — this is the item that matters most:**

1. **A held burst now clears a small area.** Point at a dense patch of prisms and hold fire:
   everything in the beam's path should die, not a scattered subset. This was the whole report.
2. **No double damage.** Prisms must not die at ~2× the expected rate or show doubled hit VFX —
   that would mean the trigger suppression missed and both paths are dispatching.
3. **Pierce still gates on SPACE.** Below SPACE 5 a round must stop at the **first** prism along
   its path (not one further down the line) and, in Turret Stance, leave its prism right there.
   At SPACE 5+ it must cut through several in a line.
4. **Range is unchanged.** The sweep must not make shots die early — a round that hits nothing
   still travels its full ~72 u at SPACE 0.
5. **Turret prisms anchor at the impact point**, not at max range, when a shot is stopped early.
6. **Profiler.** ~54 concurrent bullets each run one `QuerySegment` per frame. Watch for a new
   cost in the projectile path under a sustained hold; the segment AABB is thin so it should be
   small, but it is new per-frame work.
7. **Nothing else changed.** Manta / missile / other projectiles have `sweptPrismDetection` off
   and must behave exactly as before.

---

### 🔴 Sparrow spray accuracy — fire rate, decaying-accuracy cone, escalating haptic (`claude/sparrow-spread-haptics-qizbwf`)

Authored without a Unity compile or play-test. The Sparrow's full-auto guns now fire at **90
volleys/s (180 rounds/s)** and lose accuracy while the trigger is held: a cone opens from 0° to a
**1.5° half-angle cap** over ~1.6 s after a **0.12 s** grace window, each round deflected to a
hash-sampled point inside it, and a **new fourth haptic feel** buzzes with rising strength and
cadence as it opens. Releasing the trigger resets accuracy completely. Both fire loops were also
converted from a frame-quantized `UniTask.Delay` to a time accumulator, without which the authored
rate would silently have been `min(rate, framerate)`. The Turret Stance inherits all of it through
the existing `bulletAction` parity. Full mechanics + files + tuning:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md` (which also carries the
full 13-step verification list); `Docs/HAPTICS.md` records the policy exception.

**Hand-authored asset YAML — check these import clean first:**

- `_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset` — new **Accuracy** foldout with 7 fields;
  `Firing Rate` reads **90**.
- `_Prefabs/Spacevessels/Sparrow.prefab` — a **GunSprayAccuracy** child under `VesselActions` with
  the script resolved (not "missing"), listed as the **5th** entry in
  `ActionExecutorRegistry._executors`; the two pool managers show the resized capacities.
- Three new `.cs.meta` + one `.md.meta` were hand-written with generated GUIDs — Unity must not
  report duplicate-GUID or re-import them as new assets.

**Verify in editor (headline items — the full list is in the design doc):**

1. **Tap vs hold.** Tapped bursts are a tight line; a held burst visibly fans into a narrow cone
   (1.5° is subtle by design now) and then **stops** widening. Release and re-pull → dead-on again immediately.
2. **Stance flip is not a free reset.** Open the cone fully while flying, then toggle Turret Stance
   (input 6) **without releasing fire** — prisms must start laying at the *open* cone. This is the
   one-frame deferred reset in `GunSprayAccuracy.LateUpdate`; if it regressed, the prisms come out
   in a tight line.
3. **Frame-rate independence.** Cap the editor to 30 fps and confirm both the stream density AND
   the destruction rate are unchanged. Before this pass both would have halved.
4. **Haptic ramp — needs a gamepad or a device.** A bare desktop editor has no motors, so "I feel
   nothing" there is *not* evidence about the wiring. With a pad: light buzz from round one,
   climbing in strength and rate for ~1.6 s. Ramming a prism mid-spray must still produce a clean
   punish thud through it.
5. **No hitching on a 10 s hold**, either fire mode, profiler open.

**First-pass tuning (starting points — expect a balancing pass):**

| Knob | Asset | Value |
|---|---|---|
| `firingRate` | `FullAutoAction.asset` | 90 volleys/s (was 30) |
| `spread.onsetSeconds` | `FullAutoAction.asset` | 0.12 |
| `spread.growthDegreesPerSecond` | `FullAutoAction.asset` | 1.0 |
| `spread.maxHalfAngleDegrees` | `FullAutoAction.asset` | 1.5 |
| `spread.distributionBias` | `FullAutoAction.asset` | 0.5 (uniform disc; 1.0 = dense core) |
| `spread.hapticFloor01` | `FullAutoAction.asset` | 0.15 |
| `spread.hapticIntervalAtRest / AtMaxSpread` | `FullAutoAction.asset` | 0.10 / 0.045 s |

**Two knock-on effects to judge, not bugs:**

- **Turret stance now lays ~180 prisms/s** of permanent mass (was 60). `firingRate` is the single
  lever and it moves the guns too — do **not** add a turret-only divisor.
- **Dog Fight pace changes a lot** — 3× the rounds downrange *and* each one now tests its whole
  path, so landed hits/s rise by considerably more than 3×. Expect the 120-point target to need
  raising; it is authored in FrogletTools ▸ Game Modes ▸ End Game Conditions, so that needs no
  code change.
### 🔴 Dolphin drift holds its velocity — throttle disabled for the drift (`claude/dolphin-drift-velocity-e62z2c`)

Authored without a Unity compile or play-test. The Dolphin's drift already locked the velocity's
DIRECTION (`DolphinDriftAction.driftDamping: 0`); its magnitude kept tracking the throttle. New
`VesselTransformer.holdSpeedWhileDrifting` (authored **on** in `Dolphin.prefab`, off everywhere
else) latches the cruise speed at drift start and pins it in `AdvanceSpeed` until the drift is
released, so the throttle is inert for the drift's duration. Mechanics + what is deliberately
left outside the hold: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` §2.

**Verify in editor (Menu_Main freestyle on the Dolphin, or any Dolphin scene):**

1. **The field is there and on.** Dolphin prefab → `VesselTransformer` → **Hold Speed While
   Drifting** is ticked. Squirrel / Rhino / Manta prefabs show it **unticked** (the field is new,
   so their drift must be unchanged).
2. **Magnitude locks.** Fly at part throttle, start the drift, then sweep the throttle stick end
   to end: `VesselStatus.Speed` (DiagnosticsHUD, or a debug watch on the transformer) must not
   move. Heading still swings with the stick.
3. **It holds what you had, not a constant.** Repeat from a crawl and from full throttle — the
   held value differs each time and equals the speed at the moment the drift engaged.
4. **Release restores authority.** Let go: speed resumes tracking immediately, and the boost
   discharge accelerates as before (~357 peak off a full meter, ~2.5 s decay).
5. **Danger prisms still bite.** Ram a danger prism mid-drift — the vessel must still slam to the
   danger slow. `throttleMultiplier` is outside the hold by design; if a drifting Dolphin shrugs
   it off, the hold has been applied one layer too late.
6. **No stuck lock.** Drift → end the turn / replay / swap vessels mid-drift → fly again: the
   throttle works. (`ResetTransformer` clears the latch; this is the "cancelled UniTask never
   runs its tail" failure class, so it wants an explicit check.)
7. **Squirrel regression.** Swap to the Squirrel and drift: its racing drift must still be
   throttle-modulated exactly as before.
8. **MPPM two-client.** Host + one client, both flying Dolphins: a remote peer's drift must look
   the same on both machines (the action replays on every peer, so the hold runs on the replica
   too — a divergence here shows as the remote ship's speed visibly disagreeing during a drift).

**First-pass tuning:** none — the hold has no numbers. The one open balance question is the
boost carry recorded in `DOLPHIN_ENERGY_ECONOMY.md` §2: re-drifting at the peak of a discharge
now pins the vessel near 357 while it banks the next boost. If that ratchets in play, clamp the
captured value to the unboosted cruise target (78) in `RefreshDriftSpeedHold`.

---

### 🔴 Sparrow Skyburst Missile Bay — bay-open animation + bay-anchored missile launch (`claude/sparrow-missile-bay-78fxi4`)

Authored without a Unity compile or play-test. The Sparrow's skyburst now fires the model's
own bay missiles: press → bay-open clip on a new additive animator layer, 0.2 s later the
projectile (now the extracted `Sparrow Missile.fbx` model, not the wedge polyhedron) spawns at
the live `b_Missile.R`/`.L` bone pose. Right bay fires first (`Missile Launch 1`), left bay
last. Full mechanics + files + tuning:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md`.

**Verify in editor (any Sparrow scene — DogFight or Wildlife Liberation; also fine in Menu
freestyle after swapping to the Sparrow):**

1. **Import sanity.** `Assets/_Models/Sparrow Missile.fbx` and
   `Assets/_Models/Vessel Models/SparrowModel4.fbx` import clean (no console errors).
   `SparrowAnimatorController` shows a second layer **Missile Launching** whose two states
   reference model4's `Missile Launch 1/2` clips (not `None (Motion)`).
2. **Donor clip binds to model1's rig.** Enter play mode as Sparrow, fire the skyburst
   (right trigger ability), and watch the hull: the bay doors under the fuselage open and a
   bay missile visibly ejects, then the bay closes. If nothing moves, the cross-FBX path
   binding failed — open the two Missile Launch clips in the Animation window on the
   SparrowModel1 hierarchy and check for yellow (missing) bindings. That is the one piece of
   this change that only the editor can prove.
3. **Seam.** The live projectile should appear just as the animated missile clears the hull
   (launchDelaySeconds 0.2 on `SkyBurstGunAction.asset`). If the projectile pops before the
   doors part, raise toward 0.26; if the animated missile visibly retracts before the
   projectile exists, lower toward 0.16.
4. **Sides alternate.** With full ammo (2 missiles): first shot ejects the RIGHT bay missile
   and the projectile emerges from the right bay; second shot the LEFT. Console must show no
   `could not find missile bay bone` warning (that warning = name lookup failed, spawn fell
   back to the old Gun Point).
5. **Projectile look.** The skyburst in flight is the missile model, nose along velocity
   (not broadside, not the wedge). Its exhaust particle sizing may need a pass — it was
   tuned against the ~15 u wedge.
6. **No flight-feel drift.** Normal flying, boosting, pitch/yaw/roll animation identical to
   before (the component swapped `MantaAnimationContoller` → `SparrowAnimationController`
   with the same driving math; `hasBoost` stays 1).
7. **No puppetry fights.** While the bay clip plays during hard maneuvers, wings/tail must
   not snap (the layer is additive and the takes hold rest values on every other bone).
8. **Turn end / vessel swap mid-delay.** Fire and immediately end the turn (or swap vessels
   in menu freestyle): no projectile appears afterward, no NRE (pending launch is cancelled;
   ammo stays spent — the missile was committed at the press).
9. **Gameplay hitbox unchanged.** SkyBurstProjectile root scale is still 1 and the
   SphereCollider still 0.85 — only the visual moved to the `MissileVisual` child.

**First-pass tuning:** `launchDelaySeconds` 0.2 · `MissileVisual.localScale` 2 (≈1.7 u world
missile at ProjectileScale 10 — sized to the bay missile) · animator state speed 2.5.

**Flagged, deliberately NOT changed:** the skyburst direct-hit sphere (world radius 8.5) now
visibly dwarfs its ~1.7 u visual; the old 15 u wedge masked it. `0.85 × ProjectileScale 10`
looks emergent rather than authored — DogFight balance call for Garrett.

---

### 🔴 Vector flight model — Squirrel drift fix, Dolphin migration, Scarab refactor (`claude/astro-league-vessel-design-r5q2a8`)

`VesselTransformer` now carries TWO movement models, selected per vessel by `vectorFlightModel`
(default **off**). The scalar model integrated a speed SCALAR along `Course`, so mid-drift the
engine pushed along the SLIDE — squeezing the throttle dug you deeper into it, which is why the
drift read as ice rather than as driving. The vector model integrates a world-space velocity and
applies thrust along the **NOSE**. Design + proof: `R_VesselActions/SQUIRREL_DRIFT.md`.

**Outside a drift the two models are the same computation**, verified numerically over 4000 frames
at 60 Hz with a wandering scissor throttle, periodic slow modifiers and turn rates 0→8°/frame:
max |Δspeed| **5.7e-14**, max |ΔCourse| **2.2e-16**, max |Δposition| **1.7e-13**. That is the whole
safety argument for not retuning the fleet — but it is arithmetic, not an editor observation, so
step 2 below is the one that must actually be seen.

| Vessel | flag | policy | what changed |
|---|---|---|---|
| **Squirrel** | on | Live | Thrust direction only. Throttle semantics UNCHANGED (`XDiff` scissor, `ThrottleScaler 60`, AI `XDiff`) |
| **Scarab** | on | Live (own policy) | Refactor only — same feel, its `MoveShip` copy deleted |
| **Dolphin** | on | **Locked** | Entering a drift no longer costs speed — the velocity vector freezes (grip 0 + zero thrust). This is a **deliberate behaviour change**; the pre-existing slowdown was the boost being cancelled on drift entry |
| everyone else | off | — | untouched |

**Rounds 2–3 (2026-08-15) — fixes after play-testing:**

- **The drift overshoot ceiling was braking, not bounding.** It clamped `|v|` to
  `ComputeThrottleTarget() × 1.25` outright, so a vessel that ENTERED a drift fast was slammed to
  its current cruise target on the first drift frame. On the Dolphin that meant a boosted 357 u/s
  meeting a ~55 u/s ceiling (`ChargeBoostAction.BeginCharge` clears the boost on drift entry, so the
  target collapses), and because the ceiling tracks `XDiff` the scissor throttle read as a speed
  dial mid-drift. **Both reported symptoms, one cause.** The ceiling now takes the pre-thrust speed
  as a floor: it bounds GAIN and never brakes. **This also affected the Squirrel** — 180 → 75 on the
  first frame of any drift entered above cruise; now 177 and decaying naturally.
- **`MissingReferenceException` from `AstroLeagueBall.SampleVesselVelocities`.** `GameDataSO.Vessels`
  is a `List<IVessel>`, and `==` on an **interface** reference is a plain C# comparison that never
  reaches `UnityEngine.Object`'s overload — so a destroyed `VesselController` sailed through
  `vessel == null` and threw on `vessel.Transform`. Fixed at both ends: `VesselController.OnDestroy`
  now leaves the roster (only the despawn path removed before, so the freestyle vessel-changer swap
  leaked destroyed hulls), and the ball routes every roster walk through a `LiveTransform` helper
  that tests the Unity object behind the interface. Sample dictionaries are pruned of dead keys.

⚠ **Correction to the brief this was built from:** `holdSpeedWhileDrifting` (and
`RefreshDriftSpeedHold` / `_driftSpeedHeld` / `_heldDriftSpeed`) **has never existed in this
repository** — absent from `VesselTransformer.cs`, from that file's git history, and from
`Dolphin.prefab`. The Dolphin's throttle was LIVE during drift, i.e. it had the same raw defect as
the Squirrel.

⚠ **The Dolphin's drift-entry slowdown is PRE-EXISTING and is not fixable on the scalar path.**
Its drift calls `ChargeBoostActionExecutor.BeginCharge`, which clears `BoostMultiplier` /
`IsBoosting` / `IsChargedBoostDischarging` (it must — a cancelled discharge would otherwise leave a
permanent free multiplier). That collapses `ComputeThrottleTarget()` from the boosted 357 to plain
cruise 78 the instant you drift, and on the scalar model `speed` is a value that CHASES the target,
so it is dragged down with it (357 → 350 on frame 1, ~139 within a second). No tuning reaches it.
Under the vector model speed is STATE, so `Locked` freezes it at 357 for the drift's duration. That
is why the Dolphin is on the vector model rather than reverted — a round-2 revert to scalar
reinstated exactly this slowdown and was undone.

**Verify in editor (in order):**
1. **Squirrel — drift recovery (the point).** HexRace or freestyle. Get to speed, hold LT into a
   hard drift until the course visibly separates from the nose, then **aim the nose out of the
   slide and squeeze the throttle**. The vessel must pull ONTO the nose direction. Before this
   change it accelerated further along the slide.
2. **Squirrel — no-drift identity (the claim).** Fly with no drift at all: accelerate, brake, turn
   hard, take a danger-prism slow, ride the tube, boost. It must feel *exactly* as on `main`. If
   anything reads different outside a drift, the identity broke and that is a stop-ship.
3. **Squirrel — enter a drift at BOOST speed (the round-1 regression).** Speed must decay
   smoothly toward the cruise target. It must NOT snap down on the first drift frame, and the
   scissor throttle must not read as a speed dial while drifting. This is the exact failure that
   was reported on the Dolphin; the Squirrel had it too.
4. **Dolphin — drifting at max speed costs nothing (the reported bug).** Fly straight, build the
   boost, and pull the drift trigger at top speed. Speed must **hold flat** for the drift's
   duration — no dip at all, and the scissor throttle must not move it. Heading holds too (grip 0).
   Release: the discharge resumes from the speed you kept.
5. **Dolphin — danger prism while drifting.** Clip a danger prism mid-drift; the slow MUST land.
   `throttleMultiplier` stays live through the lock — a drifting vessel that shrugs off danger
   prisms is a locked-design violation, not a feel win.
5a. **Dolphin — boost discharge on release.** Hold the drift to bank charge, release. Acceleration
   must be immediate; you start from the speed you kept, so there should be less to make up than
   before, never more.
6. **AI drift still locks course on the objective.** HexRace, watch an AI approach a crystal. At
   drift entry its trail must continue toward the crystal while the hull swings off-axis. If the
   trail follows the nose, the `Course` re-aim in `SyncExternalWrites` regressed — this was a live
   bug in the Scarab's first-pass transformer and is the reason that method exists.
7. **Menu vessel swap preserves speed** on Squirrel, Dolphin and Scarab. Freestyle at speed →
   vessel changer → swap. The new hull must inherit the speed, not drop to a stop
   (`SetInitialSpeed` → the external-write re-seed).
8. **Squirrel — drift overshoot plateau.** From CRUISE (not boost), hold a long clean drift at full
   throttle: speed may rise above the straight-line cruise but must plateau at **1.25×** the
   throttle target. Set `driftOvershootCeiling` to 1 and confirm the plateau disappears.
10. **No `MissingReferenceException` from the ball.** Astro League or freestyle with a forged ball
   live: swap vessels via the changer toy several times, and let an AI vessel despawn. The console
   must stay clean — previously `AstroLeagueBall.SampleVesselVelocities` threw every physics tick
   once any vessel in the roster had been destroyed.
9. **MPPM two-client.** A drifting vessel's trail and heading must match on the remote peer.
   `n_Speed`/`n_Course` are owner-write and the transformer does not run on non-owners
   (`ToggleActive(false)` for `IsNetworkClient`), so nothing structural changed — confirm it.

**Tuning:** `driftOvershootCeiling` 1.25 (Squirrel/Dolphin/Scarab) · `driftThrottlePolicy`
Live/Locked/Live · Squirrel grip 0.5 (tier 1) / 0.25 (sharp) · Dolphin grip 0 · grip convergence is
now frame-rate independent (`1 − e^(−k·dt)`; ~0.4% from the old `k·dt` at 60 fps).

**Known gaps:** the Dolphin cannot accelerate at all while drifting now (that is what `Locked`
means — verify it reads as a commitment rather than a stall when drifting from low speed); no
edit-mode test guards the identity (the model lives on a MonoBehaviour needing a
live vessel — `SQUIRREL_DRIFT.md` §9 names the factoring that would make one cheap); the Manta is
the remaining scalar-path vessel that drifts (two-trigger, `singleTriggerDrift: 0`) and still has
the raw defect — its flag flip is a one-line change plus a feel pass, deliberately not taken here.

---

### 🔴 Scarab vessel foundation — new VesselClassType 12, out-of-editor prefab clone (`claude/astro-league-vessel-design-r5q2a8`)

Authored entirely without Unity: `Scarab.prefab` is a programmatic clone of `Sparrow.prefab`
(Sparrow weaponry excised, transformer/juke/telemetry retyped in place, switch executor and
cavitation blast added), plus 14 new SO/prefab assets and three registrations (`Vessel Prefab
Container`, `DefaultNetworkPrefabs`, `ArcadeGameAstroLeague.Vessels` — Rhino deliberately kept at
index 0). Design: `SCARAB.md`. All YAML machine-validated (field parity vs live classes, zero
dangling fileIDs, guid uniqueness); C# stub-compiled under mcs. None of it has been imported.

**Element map (authored 2026-08-15, no longer proposal):** Charge = cavitation blast **cooldown**
(2.5s → 1.25s at L10) + **Cavitation Shear** at L5 (blast destroys shielded prisms) · Mass = switch
size (×1 → ×2.5) + **Armored Switch** at L5 (switch prisms arrive shielded) · Space = forged **ball
size** (×1 → **×4** at L10; the map's own multiplier is the carrier) + L5 open · Time = throttle top
speed (×1 → ×1.5) + **Snap Dash** at L5 (double-tap RT). The right-stick dash is **base kit with no
cooldown**; only the blast riding it is paced.

⚠ **IF THE SCARAB SPAWNS NOTHING AND SHOWS AS A PLAIN SPHERE IN THE VESSEL CHANGER — check
`_SO_Assets/Vessel Prefab Container.asset` slot 7 first.** Both symptoms are ONE cause:
`VesselPrefabContainer.TryGetShipPrefab(Scarab)` returning false. `VesselChangerToy.BuildStation`
falls back to `ToyFactory.AddSphereBody` when the lookup fails, so the "big ball" IS the
lookup failing, not a broken model. The asset's text is correct (guid, fileID, root transform,
VesselStatus with `vesselType: 12` — all verified), so the remaining cause is editor-side: a
reference authored outside Unity can resolve to null, and the slot then shows
**None (Transform)**. Open the asset and re-drag `Scarab.prefab` into the empty slot. The
container now LOGS the empty slot by index instead of skipping it in silence, so the next
occurrence names itself.

**Verify in editor (in order):**
0. **The hull and the camera (new 2026-08-15).** Open the prefab: a `ScarabHull` child now carries
   `ScarabHullBuilder`; right-click the component ▸ **Rebuild Hull** to see the mesh without
   entering play mode. In flight the Scarab must read as a BEETLE — domed shell with a seam down
   the middle, a forward horn, six legs — and the inherited Sparrow mesh must be invisible (its
   renderers are disabled at build time; its GameObjects stay, because the vessel's BoxCollider and
   ImpactCollider live on them, so collisions must still work). The domain colour must land on the
   CARAPACE and horn (submesh 1), not the underside. Camera sits directly behind with **no vertical
   lift** — `followOffset {0, 0, -50}`; the old `y: 10` was inherited from the Sparrow, the only
   vessel that carries one.
0a. **Puppetry, roll and blast (new 2026-08-15).** Fly the Scarab and watch the hull: the wing
   cases must crack open under yaw (wider on the outside of the turn), the legs tuck as you speed
   up and splay as you slow, the horn swings against the nose. A rigid hull means
   `ScarabAnimation` resolved no parts — check the console for its unresolved-part report.
   Right-stick dash: the whole visible ship must spin 360° (it previously rolled the hidden FBX).
   And the dash must now throw a **visible spherical blast** ~45u ahead — if nothing appears,
   `Detonate()` regressed.
1. **Open `Assets/_Prefabs/Spacevessels/Scarab.prefab` and SAVE it** — this is load-bearing, not
   a smoke test: the clone carries Sparrow's `NetworkObject.GlobalObjectIdHash` until the editor
   re-serializes it, and two registered network prefabs sharing a hash collide. Open, confirm no
   missing-script rows, save.
2. Console clean on import (no `Broken text PPtr`, no unresolvable guids).
3. Menu_Main → freestyle → vessel-changer toy now shows a 7th model → swap to Scarab. Fly:
   RT = accelerating analog throttle, thrust always along the NOSE, holding it must NOT decay
   (full throttle ≈ 90 u/s after 1s, 180 ceiling by 3s; release drops 180 → 0 in ~1.5s, never a
   dead stop below MinimumSpeed 10); LT = analog drift, course visibly decoupling from the nose,
   speed retained; right stick to the perimeter = lateral dash + 360° visual roll (camera must NOT
   roll) — **repeatable immediately, there is no dash cooldown**;
   A / Space = a low-poly toy RING blooms ~150u ahead on the COURSE with its interior filled by a
   Vogel-spiral prism disc (drift then place — the ring should appear where you're going, not where
   you're pointing), second+third presses spend the remaining charges, fourth refuses.
4. **Crystals → a ball.** **EVERY omni crystal forges a ball** — the energy gate is authored OFF
   (`_requireEnergy: 0`) by request. Fly through one: a ball appears ahead of your nose carrying
   your speed and domain colour, and the console prints `[ScarabBallForge] … forged a {domain}
   ball … @ N u/s`. Each crystal also brightens the **Switch icon** one step (0→3 charges).
   ⚠ If a ball spawns but sits still, the freeze/velocity ordering in `LaunchServer` regressed; if
   TWO balls appear per crystal in MPPM, the server gate regressed.
   ⚠ **Balls accumulate without bound** while the gate is off and freestyle has no arena boundary
   — nothing despawns them, and each live ball costs a per-tick prism scan plus a sweep over every
   vessel. Fine for a short session; if a long one degrades, that is the population cap (§15.5),
   not a new bug.
   *(The energy economy still exists behind that one flag: turn `_requireEnergy` on and the meter
   gates forging again — four crystals fill the ring, the fifth forges. While it is off the HUD's
   energy ring fills but gates nothing.)*
3b. **The cavitation blast.** Every right-stick dash that finds the blast off cooldown throws a
   small SPHERICAL explosion ~45u ahead along the dash direction (diameter 90). It must:
   destroy prisms in that volume (fly at your own trail and dash into it); **kill fauna** caught in
   it (a creature dies when its body prisms go — dash through a swarm in a populated cell); and
   **debuff an opposing pilot** it engulfs — all four of their element flowers drop ~half a level
   and recover over 4s. It must NOT hit your own domain's mass or teammates (`affectSelf 0`).
   ⚠ Dash again immediately: the DASH must still fire even while the blast is recharging — if the
   dodge is blocked by the cooldown, the split regressed.
4a. **Gauges**: the CHARGE icon (leftmost) is bright orange when the blast is ready and dims for
   the cooldown after each blast; the SWITCH icon (second) dims one step per placement and refuses
   (staying dim) at zero; the SPACE icon (third) plus the energy ring flip to the READY colour when
   the meter fills. Nothing reads the dash itself — it has no cooldown to show.
5. Astro League: the configure modal's carousel now offers Rhino + Scarab; pick Scarab, 2 players
   + AI → AI must all spawn as RHINOS (list order — if an AI spawns as a Scarab, `Vessels[0]`
   got reordered); play a rally, hull-strike the ball, place a ring in front of your goal.
6. MPPM two-client: remote peer sees the Scarab hull, its trail, and placed rings (both peers lay
   the ring via the replicated A-press; positions may differ slightly under latency — expected).
7. Elemental seeding (debug), one per row:
   - **Charge L10** → blast cooldown halves (2.5s → 1.25s), visible on the Charge icon's dim time.
     **Charge L5** → dash into a SHIELDED prism wall: the blast now DESTROYS it instead of only
     shedding shields. Super-shielded mass must still survive and kill the blast.
   - **Mass L10** → bigger rings + a wider interior disc. **Mass L5** → newly placed switches bloom
     in already SHIELDED (shield geometry on every ring prism at birth, not popped on afterwards).
   - **Space L10** → a forged ball is **4× the size** of one forged at rest. Balls already in flight
     keep the size they were born with (stamped once) — that is correct, not a bug.
   - **Time L10** → higher throttle ceiling (~270). **Time L5** → double-tap RT dashes forward.
8. **Dash-into-crystal parity** (the trajectory check): hold a heading, dash sideways, and clip an
   omni crystal *during* the dash. The forged ball must leave along the DASH-blended heading, not
   the throttle line — the same trajectory a stationary ball would take if you dashed into it.

**First-pass tuning (expect a balancing pass):** accel 90 u/s², coast drag 120 (release-only —
holding the trigger must never decay), top speed 180 (×1.5 at Time 10), dash 80 u/s / 0.5s /
**no cooldown**, Snap Dash 100 u/s / 0.4s / 0.3s double-tap window, cavitation blast scale 90 /
offset 45 / 2.5s cooldown (×0.5 at Charge 10) / duration 0.85s / `proportionalDebris` with
restitution 1/3 × Inertia 1.8 (the Dolphin's shipped group), blast debuff −0.5 on all four
elements over 4s with 1s per-victim anti-spam, ring radius 20 (×2.5 at Mass 10), 28 interior +
44 burst prisms (2.5, 1.5, 8), place distance flat 150 (Space no longer scales it), 3 switch
charges, crystal grants +0.334 charge / +0.25 energy, ball size ×1 → ×4 at Space 10.

**Known gaps (deliberate, tracked in SCARAB.md):** the HUD gauges are live but ride the cloned
Sparrow variant's ART — the four icons still draw Sparrow weapon glyphs, so the row reads wrong
even though every binding is correct (art pass, not wiring); the switch ring is body-only (no ball
deflection or energy trigger — mode work, and the ball cannot bounce off prisms at all, SCARAB.md
§5); a forged ball has no boundary in freestyle so it coasts away forever (the documented §15.6
candidate, not a bug) and keeps the mode's last-striker recolouring until permanent ownership
lands (§4.2); **Space's L5 upgrade is the one deliberately open slot** (the notes assign Space the
ball's size and name no upgrade — do not invent one); the blast's optional cooldown sweep ring
(`blastCooldownRing`) is unwired, so the Charge readout is tint-only until an art pass adds it;
AI never flies the Scarab (list order); touch cannot place switches (no Button1 raise site).

---
### 🔴 Sparrow Turret Stance — two flight visualizations, still-nothing hardening (`claude/sparrow-prism-attack-hg6n78`)

Authored without a Unity compile or play-test. The stance STILL showed nothing after the
Initialize fix; three surviving silent failure modes are closed or screaming, and the flight
visual now ships in **two live-switchable forms** for A/B judgment. Full mechanics + the
verification list: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md`.

**The A/B.** `FullAutoBlockShootAction.asset` → **Flight Visualization**, read per volley (flip
it in the inspector during play mode; the next shot switches):

- **TranslateAndGrow** — the prism scales up and translates out of the gun into place
  (`PrismFlightClock` vertex offset + grow bloom at the fastest clock rate).
- **ReverseSuction** — the fauna suction shader in reverse: faces stream out of the MOVING shot
  point into the anchored shape (`PrismImplosion.StartGrow` tracking the carried projectile —
  `PrismType.Grow`'s first producer). The real prism is created when the shot lands, so mass
  becomes tangible at arrival (the mid-flight-collider "wart" does not exist in this mode).

**Why nothing showed, most likely:** un-imported flight graph wiring makes viz 1's prisms
teleport straight to ~286 u downrange (that stamp now SCREAMS via `WarnUnwiredMaterial` on
`_FlightStartTime`), and the authored bloom rate had the prism at a few percent of its size on
arrival (executor now pins `GrowthRate = 8`). Also confirm the editor is actually on this
branch — none of this is on `bleeding-edge`.

**Verify in editor:**

1. Asset-only gates first: `python3 Tools/Shaders/wire_prism_flight_clock.py --check`, and
   `FrogletTools > Ecology > Prism Animation > Validate Clock Wiring` (now requires the three
   `_Flight*` properties + `PrismFlightClock` on both graphs).
2. Open BlockGraph + ExplodingBlockGraph — no import errors, `FlightStartTime/Duration/Velocity`
   on the Blackboard. Recovery: `git checkout` the graphs + run Auto-Wire Clock Properties.
3. Stop, hold fire, in **TranslateAndGrow**: prisms visibly leave the muzzles, scale up in
   flight, anchor at ~286 u. `[PrismClock]` errors in the console mean the graph wiring — see
   step 2. Prisms popping in at range with no flight = the same, now with an error naming it.
4. Flip to **ReverseSuction** live: faces stream from the moving shot point into place; the
   real prism appears as the stream completes. This mode uses only long-shipped shader wiring,
   so it doubles as the control: viz 2 working while viz 1 doesn't isolates the new graph edits.
5. Pierce (SPACE 5) / attribution / MASS stretch / MASS-5 shield / MPPM — as documented in
   SPARROW_TURRET_STANCE.md's list.
6. Judge the two visualizations and pick (or keep both). Also judge viz 1's mid-flight collider
   at the destination vs viz 2's tangible-at-arrival.

**Playtest round 2 (2026-08-10, same branch):** shots were very hard to see — three changes on
top, all data + one curve retune:

- **ReverseSuction is now the default** (`flightVisualization: 1`), slowed to **5× the flight
  time** (`suctionDurationMultiplier: 5`): the shot lands and pierces on the bullet clock, the
  faces keep streaming into place for ~1.5 s after it, and the real prism is created 0.2 s
  before the stream completes (tangible at assembly completion — the mid-flight-collider wart
  is gone in this mode).
- **Turret prisms are DANGER prisms** (`fireDangerPrisms: 1`) — danger material, so they stand
  out. Locked-law consequences to verify: they bite the shooter too, and MASS-5 Shielded
  Prisms is suppressed while danger is on. Known cosmetic seam: the stream renders domain
  colors, the revealed prism wears the danger material.
- **Gun range re-anchored, both modes**: base speed 1500 → **750**
  (`FullAutoAction.speedValue.Value`), SPACE curve 2.5 → **4.667**
  (`Sparrow.asset` MultiplierAtFullLevel) — SPACE 0 range halves (~143 u), SPACE 15 unchanged.
  Verify with a Space crystal binge that range visibly stretches toward the old reach.

**Playtest round 3 (2026-08-10):** now SHIELDED full-size shots on the plain flight, range
quartered from the original:

- `firedPrismState: Shielded` (enum replaces the round-2 `fireDangerPrisms` bool — Plain
  restores the MASS-5 gate, Danger restores round 2), `spawnFullSize: 1` (no grow-in; the
  flight is the transition), `flightVisualization: 0` (suction off but kept as the alternate).
- Range: base speed 750 → **375**, SPACE curve 4.667 → **9** — SPACE 0 ≈ 72 u, SPACE 15
  unchanged (~931 u). Verify shots are close-in, LARGE, and octahedron-armored from the
  muzzle; verify a Space binge stretches the reach ~13×.
- Verify the shield birth-snap renders ON THE FLIGHT: the flying shot must be the octahedron,
  not a plain box that armors on arrival — if it flies plain, the birth rule regressed.

**Round-3 follow-up (spread-at-distance):** the flight moved vertices but the spread chain's
distance read the PIVOT (parked at the anchor), so shots rendered with max-range spread from
frame one. `PrismFlightSqrDistance` now feeds `Prism Sub Graph.SqrDistance` on BlockGraph from
the displaced pivot, and the `SqrDistanceSubGraph` node is retired. Verify: a fired prism's
spread/near-look must now be identical to a trail prism laid at the same visible distance,
tightening as it flies; ordinary prisms (trail/environment) must render unchanged. Re-run
`wire_prism_flight_clock.py --check` + Validate Clock Wiring (BlockGraph now requires
`PrismFlightSqrDistance`).

**Playtest round 4 (2026-08-10):** shield onto SPACE 5, bullet-sized hit spheres.
**⚠ The shield half of this entry is SUPERSEDED** — it returned to MASS 5 on 2026-08-13
(`ShieldedAtMass5`); verify against the newest entry at the top of this file, not this one:

- `firedPrismState: ShieldedAtSpace5` — regular prisms below SPACE 5, shielded at 5+, same
  gate as pierce. Verify the flip at the SPACE-5 unlock: below, plain prisms that stop at
  first impact; at 5+, armored octahedra that pierce. MASS-5's map slot is now open (label
  records the move) — the HUD's Mass icon should no longer show an upgrade state change
  affecting the turret.
- The carried hit volume is now the BULLETS' sphere: unit SphereCollider on
  `Sparrow Projectile Prism.prefab`'s ProjectileCollider child (was a thin box, ~1/24th the
  bullet's cross-section — the round-3 "missing lots" report), scaled in code to
  `collisionDiameter: 12` / `shieldedCollisionDiameter: 18`. Verify prism shots now connect
  on the same aims that bullets connect on, and that shielded shots feel distinctly easier
  to land. Prefab was hand-edited (BoxCollider → SphereCollider, same fileID) — confirm the
  prefab opens clean with the sphere on the child.

**Playtest round 5 (2026-08-10):** friendly fire always on; CHARGE 5 spares only the skyburst:

- Turret prism carried projectile `friendlyFire: 0 → 1` on `Sparrow Projectile Prism.prefab`.
  Verify a turret shot fired into YOUR OWN domain's prisms now damages them (and stops there
  below SPACE 5) — previously it flew straight through friendly mass. Bullets already had
  `friendlyFire: 1`; confirm they still damage own-domain prisms unchanged.
- `ProjectileDetonatorSO` now stamps `AffectSelfOverride = !SpareOwnDomain` on every skyburst
  detonation. Verify: below CHARGE 5 a skyburst blast destroys your own domain's prisms;
  at CHARGE 5+ the blast (and the direct hit) spares them — hit, timeout, and mine
  detonations all flip together. The shared `AOEExplosion.prefab` was NOT edited — confirm
  the Manta crystal explosion still spares own domain as before.
- Placement immunity (round-5 follow-up: shots destroyed their own output — first their
  own delivery, then, with a 12-u hit sphere, the previous prism even at full spin):
  `Prism.ProjectileImmuneUntil` window checked in `DisallowImpactOnPrism`; turret stamps
  flight + `placementImmunitySeconds` (0.2, on `FullAutoBlockShootAction.asset`). Verify:
  a single shot lands and STAYS; a full-speed spin leaves a RING of prisms (not one);
  holding fire on one spot still churns (each prism outlives its window between shots
  arriving >0.2 s later — expected); a deliberate later shot into an old fired prism
  destroys it (friendly fire intact); enemy fire during the brief window is ignored but
  lands normally after (~0.5 s from fire). Tune `placementImmunitySeconds` to taste.

**Playtest round 6 (2026-08-11):** hit spheres shrink to the projectile they draw:

- `SparrowProjectile.prefab` `SphereCollider.m_Radius` **0.3 → 0.04125**. The collider
  scales by the LARGEST lossy-scale component, and the tracer is scaled `(1.5, 1.5, 20)`,
  so the old radius gave a 6.0-world-radius (12-diameter) ball around a dart whose visible
  cross-section radius is 0.75. Now `0.04125 × 20 = 0.825` world radius = 1.65 diameter =
  the visible projectile +10%. Verify in the Scene view during play that the bullet's
  gizmo sphere now hugs the tracer instead of dwarfing it.
- `collisionDiameter` **12 → 1.65**, `shieldedCollisionDiameter` **18 → 2.475** on
  `FullAutoBlockShootAction.asset` (the ×1.5 shielded ratio is preserved).
- **Feel check, the point of the round:** both fire modes lose a lot of aim forgiveness
  (~53× smaller frontal cross-section). Verify bullets and prism shots still connect on a
  deliberate aim and that they now MISS on a sloppy one — that is the intended result. Also
  verify a spray still leaves multiple prisms (placement immunity is doing less work at
  this size, so `placementImmunitySeconds` 0.2 may now be reducible — tune only after
  flying it).

**First-pass tuning:** fire rate 30/s + speed 375 (SPACE ×9 at full) + flight 0.3 s on `FullAutoAction.asset`
(shared with the guns); `blockScale (0.8, 0.5, 5)` + `flightVisualization` on
`FullAutoBlockShootAction.asset`; reveal overlap 0.2 s (`RevealOverlapSeconds` in the executor);
turret prism pool 40/90/8 on the Sparrow prefab.

---

### 🔴 Dolphin speed + charged-boost retune (`claude/dolphin-speed-boost-tuning-qgnojw`)

Authored without a Unity compile or play-test. **Two authored numbers changed in
existing serialized assets** — no new keys, no new components, no hand-built YAML
structures — so the import risk is low, but nobody has flown the result.

**What landed.** Four requested deltas, all data, no code:

| quantity | before | after | delta |
|---|---|---|---|
| max cruise speed | 60 | **78** | +30.0% |
| max boost speed (peak of a full discharge) | 210 | **357** | +70.0% |
| boost charge fill rate | 0.250 /s | **0.275 /s** | +10.0% |
| boost drain rate | 0.500 /s | **0.400 /s** | −20.0% |

- `Dolphin.prefab` → `VesselTransformer.DefaultThrottleScaler: 50 → 68`.
  `DefaultMinimumSpeed` deliberately left at **10** — the request was max speed, so the
  throttle top moved and the drift/idle floor did not.
- `ChargeBoostAction.asset` → `maxBoostMultiplier: 2 → 2.259`,
  `chargeTimeToFull: 4 → 3.636`, `dischargeTimeToEmpty: 2 → 2.5`. That asset is
  referenced only by `Dolphin.prefab`, so no other vessel moves.

**The peak multiplier is squared, and that is why 2.259 is not a round number.**
`VesselTransformer.CurrentBoostAmount()` multiplies `BoostMultiplier` (decaying live)
by `ChargedBoostCharge` (pinned at the charge-end value), so the authored peak lands as
`maxBoostMultiplier²`: the real ceiling was `50 × 2² + 10 = 210`, not the 110 the design
doc implied. **This was NOT changed** — it is shipped behaviour on both the executor and
the legacy `ChargeBoostAction`, and "fixing" it inside a tuning pass would halve the
Dolphin's boost unasked. It is now documented in `DOLPHIN_ENERGY_ECONOMY.md` §2. If it
should become a single factor, that is a one-line change plus its own retune — see
Follow-ups there.

**Verify in editor** (Menu_Main, freestyle, Dolphin)

1. **Full throttle, no boost** — `VesselStatus.Speed` settles at **78** (was 60).
2. **Hold drift from an empty meter** — the boost ring fills in **~3.6 s** (was 4).
3. **Release a full meter** — speed peaks near **357** and takes **~2.5 s** to fall
   back (was 210 over 2 s). This is the number most likely to want a balancing pass;
   357 is a big jump and the speed tunnel amplifies how it reads.
4. **Drift → release → drift again** — speed returns to normal, no stuck multiplier
   (the `BeginCharge` clear is untouched, but this is the regression it guards).
5. **The speed tunnel tracks it.** FOV should narrow noticeably harder at the new top
   speed. That coupling is the platform law (`Docs/SPEED_TUNNEL.md`) — absolute and
   fleet-wide, no per-vessel window — so it is the intended consequence, not a bug.
6. **Nothing else moved.** Fly any other vessel; `ChargeBoostAction.asset` and the
   Dolphin prefab are the only things touched.

**Collider budget:** unchanged — no spawning, geometry, or query change.

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Throttle top | **68** (+ `MinimumSpeed` 10 = 78) | `Dolphin.prefab` → `VesselTransformer.DefaultThrottleScaler` |
| Speed floor | **10** (unchanged) | `Dolphin.prefab` → `VesselTransformer.DefaultMinimumSpeed` |
| Boost peak multiplier | **2.259** (**squared** in use → ×5.103) | `ChargeBoostAction.maxBoostMultiplier` |
| Charge time to full | **3.636 s** | `ChargeBoostAction.chargeTimeToFull` |
| Discharge time to empty | **2.5 s** | `ChargeBoostAction.dischargeTimeToEmpty` |

Max boost speed is `DefaultThrottleScaler × maxBoostMultiplier² + DefaultMinimumSpeed` —
recompute it after touching **either** of the first two rows, they are not independent.

---

### 🔴 Dolphin crystal blast — capsule sweep along the jaw gape (`claude/dolphin-echobliteration-capsule-a0vs26`)

Authored without a Unity compile or play-test. **Unlike most entries here this one
DID hand-author asset YAML** — a `SphereCollider` was rewritten into a
`CapsuleCollider` in place (class id `135` → `136`) — so the first check below is a
genuine import check, not a formality.

**What landed.** The Dolphin's crystal-impact blast no longer sweeps a circular
cone whose radius grows with skim energy. Its cross-section is now a **capsule**
(a 2D stadium): the radius is pinned to a fixed width *across the beam*, and what
energy buys is capsule **length**, extended along the axis the vessel's jaws open
across (container-local up = ship up). A charged blast is a fan — wide in the jaw
plane, narrow across it.

- `AOEConicSweepQueryJob` (Burst) tests point-to-**segment** instead of
  point-to-axis. Same cost class, no extra sqrt.
- `AOEConicExplosion.prefab`'s trigger is a `CapsuleCollider` driven per frame by
  `UpdateCapsuleTrigger`, so the vessel-impact volume and the Burst volume are the
  same shape by construction. A dev-build warning fires if a conic blast opens a
  gape without a capsule trigger.
- `InitializeStruct.CoreScale` / `_coreExplosionScale` carry the capsule diameter,
  authored separately from the empty-charge length so the blast can rest as a
  short capsule instead of a sphere. `0` collapses everything back to the plain
  circular cone — that is what every non-conic caller and the spherical blast get,
  so **no other vessel's blast changed**.
- The jaws (hull + HUD icon) were re-measured against the new geometry and their
  linear approximation retired: both now call one shared
  `RiptideAnimation.GapeAngleAt(t, min, max)`, exact at every charge.
- `AOEExplosion._sphereCollider` → `_triggerCollider`, typed `Collider`, since the
  shape is now the subclass's business.

**Verify in editor**

1. **The hand-authored collider imported.** Open `_Prefabs/Projectile/AOEConicExplosion.prefab`.
   The root must show a **Capsule Collider** (Is Trigger ✓, Radius 0.0667,
   Height 1, Direction **Z-Axis**, Center 0/-0.5/0) — *not* a missing component, a
   Sphere Collider, or a second collider alongside it. If Unity rejected the YAML
   this is where it shows.
2. **It compiles.** Nothing in the branch is `#if`-guarded (the conditional-compilation
   gate passes), but no C# compiler ran on the author's side at all.
3. **Empty-energy blast is unchanged in feel, slightly lozenge-shaped.** Fly to a
   crystal with no banked energy. The blast should look and destroy about as
   before (it is 400 long × 320 wide instead of a 400 sphere).
4. **Charged blast is a FAN.** Bank energy to full, then hit a crystal while flying
   at a dense prism wall. Destruction should be wide in the jaw plane and narrow
   perpendicular to it — roll 90° and fire again to confirm the fan rolls with the
   ship (it is bound to ship-up, not world-up).
5. **The jaws never read fully shut.** At zero energy both the hull's jaws and the
   HUD's Time icon should sit slightly open (4.76°/side), and they should agree
   with each other at *every* charge step, not just at the ends.
6. **Nothing else regressed.** Fire a Manta / Rhino / Squirrel / Serpent crystal
   blast (all spherical) and a Sparrow skyburst — they take the `CoreScale == 0`
   fallback and must be identical to before.

**Collider budget:** unchanged — the conic blast still carries exactly one trigger
collider, swapped sphere → capsule.

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Capsule length, empty → full | **400 → 2080** | `DolphinVesselExplosionByCrystalEffect._min/_maxExplosionScale` |
| Capsule diameter (fixed) | **320** (radius 160) | `DolphinVesselExplosionByCrystalEffect._coreExplosionScale` |
| Gape half-angle, empty → full | **4.7636° → 23.4287°** | `RiptideAnimation.MinJawAngle` / `MaxJawAngle` (derived from the two above over the prefab's `height: 2400` — **change a scale and these must follow**) |
| Gape axis | **(0,1,0)** container-local = ship up | `AOEConicExplosion.gapeAxis` |

The length/diameter pair is the whole feel: length is reach along the gape,
diameter is how forgiving the aim is across the beam. The jaw angles are *derived*,
not independent — recompute them as `atan((scale / 2) / height)` after any retune.

---

### 🔴 Sparrow stationary stance — roll works stopped, pitch/yaw 3× (`claude/sparrow-strafing-roll-stopped-d2yc7g`)

Authored without a Unity compile or play-test. **Code only — no prefab, scene or
SO asset was touched**, so the editor-side risk is a compile check plus feel.

**What landed.** The strafing roll (`BarrelRollController`) no longer bails out
while the Sparrow is in its stationary/turret stance. Stopped, the boost still
gives no speed, but the roll arms on the same boost press, triggers on the same
full stick deflection, spends the same charge pip, and **strafes the same
distance** — it is the stopped Sparrow's dodge. Rolling does not change the
stance.

- The displacement survives the restriction via a new per-modifier opt-in,
  `ShipVelocityModifier.ignoresTranslationRestriction` (default **false**; only
  the roll sets it, every other `ModifyVelocity` caller is untouched and stays
  fully held while restricted).
- `VesselTransformer` grew a restricted branch: `ApplyVelocityModifiers(
  translationRestricted: true)` + `MoveRestricted()`. It deliberately does not
  write `VesselStatus.Speed` or `Course`.
- Two incidental fixes fall out of that branch — velocity modifiers now **age**
  while restricted (previously they froze and lurched out on stance release),
  and the `StopFlareBody` material write is now edge-triggered instead of
  per-frame (it writes through `renderer.materials[0]`, which clones).
- The roll projects its nudge on current **facing** while stopped, because
  `Course` is stale there (`MoveShip` is what refreshes it).

**Also landed: pitch and yaw run at 3× while stopped.** New serialized
`VesselTransformer.restrictedTurnMultiplier` (default **3**), applied through a
shared `TurnScalar` property to the whole pitch/yaw rate whenever
`IsTranslationRestricted` is set. Applied in **`SingleStickVesselTransformer`**
as well as the base class — that subclass overrides `Pitch`/`Yaw` and is what
both the Sparrow and the Serpent actually run, so a base-only change would have
reached neither. Roll is deliberately unscaled (it is the bank into the turn,
not a turn rate). **The Serpent inherits the same default** — one inspector
field on `Serpent.prefab` to opt out.

**Verify in editor**

1. Project compiles with zero errors. Run the `CosmicShore.Tests.EditMode`
   suite — `ShipModifierTests` gained two cases pinning the new flag.
2. `MinigameFreestyleMultiplayer_Gameplay` (or Menu_Main freestyle), Sparrow.
   **Flying** roll first: boost + full left stick → rolls and strafes, once per
   press. This must be **unchanged** — it is the regression risk.
3. Toggle the stationary/turret stance. Boost + full left stick → **rolls and
   strafes**. Speed does not change. Still once per press (hold boost + hold the
   stick at max = exactly one roll). Charge ring arms and wipes as when flying.
4. After the stopped roll: still stopped, still in stationary fire mode, and no
   trail/bridging prisms were laid.
5. **Stale-course check.** Stopped, rotate to aim well away from the heading you
   had when you stopped, then dodge. The strafe must go where the stick points
   relative to your **current** facing — a skew toward the old heading means the
   projection plane is wrong.
6. **No banked lurch.** Stopped, take a knockback (a Rhino ram, or clip a danger
   prism): you must not move. Release the stance: you must not lurch.
7. **Stopped turn rate.** Flying, time a full 180° yaw. Toggle the stance and
   repeat: roughly **a third** the time. Pitch likewise. Release the stance and
   the rate must drop straight back (the scalar is read per frame — a rate that
   stays fast means it got cached). The bank into the turn is unchanged by
   design, so the stopped turn reads flatter than a flying one.
7b. **Other vessels.** Serpent — stop into its weave stance, take a knockback,
   release: no movement while stopped, no lurch after. Note its pitch/yaw are
   **also 3×** while stopped (same transformer, same default); set
   `restrictedTurnMultiplier` to `1` on `Serpent.prefab` if that is unwanted.
   Any vessel: boosts/bounces/deviation nudges still displace normally while
   flying, and flying turn rates are unchanged everywhere.
8. **MPPM two clients.** Roll while stopped on client A; client B must see the
   same displacement (it replicates through the owner-authoritative
   NetworkTransform, same as the flying roll — no new networked state).

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Dodge distance | `Sparrow.prefab` `BarrelRollController.nudgeSpeed` / `rollDurationSeconds` | 60 / 0.6 — **one number for both stances**. If the stopped dodge should reach further or less far than a flying strafe, that is a new serialized field, not a rescale of this one. |
| Stopped turn rate | `Sparrow.prefab` `VesselTransformer.restrictedTurnMultiplier` | 3 (pitch + yaw only). Sparrow authors PitchScaler/YawScaler 80 with RotationThrottleScaler 0.1, so ~82 °/s flying → ~247 °/s stopped. |

**Open question the author could not resolve** — whether a stopped dodge should
cover the same ground as a flying strafe. Shipped as identical (the simplest
reading of "the same way"); it is one field to split if it plays too strong in
turret stance.

---

### 🔴 Sparrow boost redesign — no overheat, base strafing roll, Elemental Ward (`claude/sparrow-ability-redesign-norbgz`)

Authored without a Unity compile or play-test. Touches a **platform** surface
(`ResourceSystem.ApplyElementalEffect`) plus two vessel prefabs edited as YAML,
so the editor-side risk is real: hand-written prefab documents, a removed
GameObject, a removed resource slot, and renamed serialized fields.

**What landed**

- The Sparrow's overheat mechanic is **deleted** — `OverheatingActionSO`,
  `OverheatingActionExecutor`, the legacy `OverheatingAction`, the
  `OverheatingAction.asset`, the `Heat` resource on the Sparrow prefab, and
  `VesselStatus.IsOverheating`. Input event 7 binds straight to the shared
  `BoostAction.asset`; boost is now unlimited in duration.
- The **strafing roll dropped to base kit** — `BarrelRollController` lost its
  `IsUpgradeActive(Element.Time)` gate. Still one roll per boost press.
- **TIME-5 is now "Elemental Ward"** — a general, source-keyed
  elemental-debuff immunity on `ResourceSystem`
  (`SetElementalDebuffImmunity` / `IsElementallyImmune`), gated in one place:
  the negative branch of `ApplyElementalEffect`. Driven declaratively by the new
  `VesselElementalImmunity` component: **Sparrow** `WhileBoosting` + Time gate,
  **Serpent** `WhileTranslationRestricted` ungated.
- The Sparrow boost icon's radial gauge became a **binary roll-charge pip**
  (`SparrowHUDView.SetRollCharge`), driven by
  `BarrelRollController.OnRollChargeChanged`.
- `SquirrelVesselHUDController` lost its `OverheatingActionExecutor` lookup — it
  compiled against a Sparrow-only component and always resolved to null on a
  Squirrel, so the Squirrel's heat gauge never moved. Pure dead-code removal.

**Verify in editor** (full steps + expected observables:
`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` §
"In-editor verification")

1. Project compiles with zero errors; no new console warnings on Sparrow or
   Serpent spawn. **Known pre-existing, not from this branch:** the Sparrow's
   `ElementalBarsController.view` reference (`fileID 7416581124810081342`) is
   already dangling on `bleeding-edge`.
2. **Prefab integrity is the top risk.** Open `Sparrow.prefab` and confirm: no
   missing-script slots; the `OverheatingBoostActionExecutor` child is gone; the
   `ResourceSystem` list reads Missiles / FullAuto / ExhaustBarrage (3 entries,
   no Heat); `SparrowHUDController.barrelRollController` points at the root's
   `BarrelRollController`; `VesselElementalImmunity` is on the root reading
   `WhileBoosting` + `Time`. Then `Serpent.prefab`: `VesselElementalImmunity`
   on the root reading `WhileTranslationRestricted` + `None`.
3. Hold boost 60 s — no force-release, no danger trail, no self-slam.
4. Time at 0: boost + full stick deflection rolls **once** per press.
5. The boost (rightmost) ability icon's ring: full on press, wipes empty with a
   punch on roll, empty until the next press. Never a partial fill.
6. Time ≥ 5 (`ResourceSystem.TimeTestHarness = 0.5`): danger prism **while
   boosting** → element flowers do not dip; **not boosting** → they dip. Slow
   and input-mute land either way (by design).
7. Serpent stopped + danger prism → no flower dip, at any Time level.
8. **MPPM two clients**, both Sparrows, one at Time 5: both machines must agree
   on who resists the drain. This is the replicated-`NetElementUnlocks` path —
   a local level read would pass step 6 and fail here.
9. `FrogletTools > Vessels > Audit Vessel Ability Rows` — Sparrow still 4/4 in
   charge → mass → space → time.

**First-pass tuning** (starting points, not settled)

| Knob | Where | Value |
|---|---|---|
| Boost speed at Time 10 | `Sparrow.asset` Time `MultiplierAtFullLevel` | 1.5 (unchanged — but the hold is now unbounded, so this is the first balance lever) |
| Immunity window | `Sparrow.prefab` `VesselElementalImmunity.condition` | `WhileBoosting` (`Always` = passive ward at Time 5, one field) |
| Roll pip colours | `SparrowHUDVariant.prefab` | armed cyan `0.55/0.9/1`, spent dim grey `0.35/0.4/0.45 @ a 0.5` |
| Roll wipe / punch | same | 0.15 s / 0.3 |

**Open design question the author could not resolve** — whether the ward should
hold `WhileBoosting` (shipped, mirrors the Serpent's stopped stance) or `Always`
at Time 5. With an indefinite boost, `WhileBoosting` means a pilot willing to
fly permanently full-throttle is permanently warded. One inspector field either
way; no code change.

### 🔴 Dolphin elemental pass — skim feedback, drift boost, cone blast (`claude/dolphin-energy-crystal-cooldown-zpvc07`)

Authored without a Unity compile or play-test. Garrett play-tested the HUD/boost
rounds mid-branch, but **the final skim-feedback fix is unconfirmed** — the last
report was still "no skimming indication", after which the branch found (a) the
crackle needs three pieces the Dolphin had none of, and (b) all three skim signals
are individually invisible on desktop. Nobody has yet seen a Dolphin skim work.
Mechanics + full knob list: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md`.

**Verify in editor (highest risk first):**

1. **Run `FrogletTools > Vessels > Audit Vessel Skimmers`.** Expect
   `Dolphin  NearFieldSkimmer: 'EnergySkimmer' OK`. This is the branch's headline fix —
   `VesselStatus._nearFieldSkimmer` pointed at a DISABLED legacy skimmer, so
   `Skimmer.Initialize` never reached the object whose trigger fires and
   `SkimmerImpactor` dropped every contact silently. (Serpent is expected to FAIL —
   known, untouched.)
2. **Skim in Menu_Main freestyle.** Fly the Dolphin through cell mass: crackle arcs
   should sweep the skimmer sphere per prism, the HUD jaw icon should punch per skim,
   and the gape (icon + the model's own jaws) should widen toward 18.4° per side as
   energy fills. Watch the console — an unauthored `Prism.ParticleEffect` now logs one
   named warning per prefab instead of throwing per contact.
3. **The boost loop.** Hold drift → the ring steps up; release → speed rises and decays
   as it drains. Flying straight must NOT fill the ring (the passive `resourceGainRate`
   is gone). Drift → release → drift again must return to normal speed (the interrupted
   discharge used to leave `BoostMultiplier` stuck).
4. **Crystal impact.** The cone fires, energy empties, the jaws snap shut, and the Space
   icon flashes with a prism count. At Space L5 the cone must stop damaging your own
   domain's prisms.
5. **Charge L5.** ~~A second crystal pip appears.~~ *(Superseded TWICE. 2026-08-14: seeding
   went passive, so the pips became per-cycle yield rather than carried crystals. 2026-08-17:
   **Twin Seed and the pips are retired outright** — seeding moved to MASS, plants exactly one
   crystal per cycle at every level, and its L5 changes the crystal's TIER. Charge L5 is now
   "Pilot Echo". Verify against the newest Dolphin entry at the TOP of this file.)*
6. **MPPM two-client:** the L5 upgrade effects are gated on the replicated
   `IsUpgradeActive`, so confirm both peers agree — on Clean Blast and, since 2026-08-17,
   Claimed Seed and Pilot Echo (Twin Seed no longer exists).

**Hand-authored assets that have never had an editor import round-trip:** the Dolphin
HUD variant's four-icon row, the Dolphin prefab's crackle overlay + controller, and
`DolphinSkimmerChangeResourceByPrismEffect.asset`. Their YAML keys were machine-checked
against the scripts' serialized field sets, but Unity has not re-serialized them.

---

### 🔴 Fauna consumption v3 + shark jaw rig (fauna-consumption-behavior branch, merged)

Landed via PR #614 (`claude/fauna-consumption-behavior-*`) plus the shark-jaw
commit `438070a2`. None of it had a Unity compile or play-test from the author —
it is on the shared branch unverified. Design + mechanics reference:
`Docs/ECOSYSTEM.md` §7 / §7.3 (intentional consumption, the mouth-driven
predator, tiger-shark territoriality, centre focus).

**Verify in editor (the three things most likely to be wrong):**

1. **Jaw prefab import.** Open `Assets/_Models/Fauna/MassSharkFauna.prefab`.
   Confirm `SharkJawDriver` (`_Scripts/Controller/Environment/FloraAndFauna/SharkJawDriver.cs`)
   sits on `Shark_model` alongside the `Animator` + `RigBuilder`, that the two
   mouth `MultiAimConstraint`s and the `MawTarget` it aims at are all present and
   wired, and that weight `0` = FBX swim pose (mouth closed) / weight `1` = aimed
   at `MawTarget` (mouth open). Danger prisms are parented to the jaw bones — check
   the teeth actually gape with the mouth in a play-test (`NotifyBodyPrismsMoved`
   should keep their spatial-index positions honest as the jaw moves).

2. **Elemental Variant on the tadpole config.** Confirm the tadpole's
   `FaunaConfigurationSO` / prefab Variant carries its intended elemental setup
   (that the Variant override actually serialized and points at the creature
   prefab's `Boid`, not the dead `*Population`/manager prefab — see the §7 warning
   that the live spawn path is the cell config, not the scene-placed populations).

3. **Two feeding models coexist.** Confirm both consume paths still compile and
   run side by side without one having been collapsed into the other:
   `LightFauna` (brittlestar/shark) has **no** `_pendingMeals` grazing queue
   (intentional-feeding: approach → face → suction), while `Boid`'s **drone**
   path keeps its `_pendingMeals` burst-pacer (combat). Do not re-add the
   burst-pacer to the forager/intentional types or strip it from the drone path
   (`Docs/ECOSYSTEM.md` §7.3 explains why they differ).

**First-pass tuning (expect a balancing pass — observe in context first):**

| Knob | Value | Where it lives |
|---|---|---|
| Hunt pulse (window / cycle) | **10s open / 20s interval** | `LightFaunaDataSO.huntDurationSeconds` / `huntIntervalSeconds` |
| Tiger-shark territory radius | **r = 600** | `LightFaunaDataSO.territoryRadius` (+ `territoryAnchorDistance`) |
| Jaw open / close | **0.6s open / 1.8s close** | `SharkJawDriver` (open notably faster than close) |
| Herbivore/forager centre focus | **0.35** | `FaunaConfigurationSO.CenterFocusBias` (per-deployment) |

These four are the ones the author flagged as guesses. The jaw transition is
~2.4s total per 20s hunt cycle; the driver early-outs on a single float compare
whenever the mouth is settled, so re-tuning the timings has no perf cost.

---

## 🔴 Dolphin skim economy + jaw CTA + fleet silhouette removal (2026-08-07)

Branch `claude/dolphin-prism-energy-5e4hbq`. None of this was editor-verified — the
prefab surgery was done out-of-editor and machine-validated (no new dangling fileIDs,
no surviving references, C# compiled against a stub harness), but Unity has not
reimported any of it yet.

**What landed**

1. `DolphinSkimmerChangeResourceByPrismEffect._resourceAmount` **0.1 → 0.006666667**
   (15× less energy per skim; ~150 skims to arm the blast, 50 on a danger trail).
2. `DolphinVesselHUDView` blends the Time-slot jaw pair white → `ElementalBarsConfigSO.limeColor`
   across the top 15% of energy (`jawArmingThreshold: 0.85`).
3. The dead vessel **silhouette** removed from 13 vessel + HUD-variant prefabs, plus
   dead `silhouette`/`silhouetteContainer`/`trailContainer` keys and their overrides
   in 13 more files.

**Verify in editor**

1. Open each of the 15 edited prefabs — no *"Missing (Mono Script)"* row that was not
   already there, no broken hierarchy, HUD still lays out. (Sparrow, Rhino, Squirrel,
   Serpent, Manta and the six vessel prefabs lost real GameObjects; the rest lost keys.)
2. Play Menu_Main → freestyle on the **Dolphin**: no `[ElementalBarsController]` runtime
   warning that was not there before, and **no `[DolphinVesselHUDView]` warning at all** — it
   fires once if the shared bars config is missing or the jaw refs carry no Graphic, either of
   which means the lime CTA is silently dead. The four ability icons still bind (FrogletTools >
   Vessels > **Audit Vessel Ability Rows** → Dolphin 4/4, order ✅).
3. Fly the other vessels' HUDs briefly (Sparrow, Squirrel, Rhino, Serpent, Manta) and
   confirm nothing visually disappeared *except* the ship outline.
4. Skim a long time → jaws blend to lime near full; ram a prism → they drop back to white.

**Known pre-existing issues surfaced, NOT fixed here (own branch):**

- `SerpentHUDVariant.prefab` and `VesselHUDPrefab.prefab` carry a component whose script
  guid `57dc27a3f7264d548b51007c0615f701` resolves to **no script in the project** — an
  existing *Missing (Mono Script)* component, unrelated to this change.
- `Dolphin.prefab`'s `ElementalBarsController` has **no `elementBars` key**, so the element
  flowers are created at runtime via `CreateDefaultElementBars()` (which logs a warning).
  Fix with FrogletTools > Vessels > *Bake Elemental Petal Bars Into All Vessel HUDs*.

---

## ✅ Dolphin — passive crystal seeding + Echo Sight (2026-08-14, VERIFIED IN EDITOR)

Branch `claude/dolphin-crystal-spawn-rework-feqrxc`. **Play-tested by Garrett on 2026-08-14 —
seeding, the highlight and the HUD all confirmed working in the editor.** The steps below are
retained as the regression list for anyone touching this again. Full detail + tuning table:
`Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md` §6.

Still UNVERIFIED, because a single-editor play-test cannot reach them:
**the MPPM two-client row (9)** — that a remote Dolphin's sight stays local and that the L5 upgrade
agrees across peers — and **the live-cap row (3)**, which needs ~4 minutes of uninterrupted
seeding at the shipped 30 s cooldown to reach `maxLiveSeeded: 8`.

**What landed.** Charge's crystal seeding became PASSIVE (a cooldown loop seeding team crystals
into the cell's cytoplasm), freeing the right trigger for Space's new **Echo Sight** — hold it and
every prism inside the crystal blast's live destruction volume lights up. The sight touches nothing
but photons: no camera write, no FOV change, nothing replicated. (A zoomed first-person view was
built alongside it and cut; the speed tunnel is untouched by this branch.)

**Import first** (kept for anyone re-running this from a clean checkout). Two shader graphs were edited out-of-editor
(`Tools/Shaders/wire_prism_destruction_sight.py` → BlockGraph, ExplodingBlockGraph). They need a
Unity import pass to regenerate their shaders. An unimported graph shows **no highlight and no
error**, so check this before concluding the sight is broken.

1. **Compile clean**, then FrogletTools > Vessels > **Audit Vessel Ability Rows** → Dolphin still
   map complete, 4/4 icons, order ✅.
2. Freestyle on the Dolphin, idle one cooldown → a team crystal blooms in somewhere in the
   cytoplasm and the Charge slot punches. Over several cycles they should spread through the
   shell, **not** cluster near the nucleus, and **never** land inside it.
3. Let `maxLiveSeeded` (8) fill → seeding stops and **no crystal disappears**. Collect one →
   seeding resumes. (A crystal vanishing here is a conserved-mass violation, not a tuning issue.)
4. Charge to L5 → the mini crystal pip appears and each cycle plants two.
5. **Hold RT** → prisms in the blast volume light warm, and the camera does **not** move and the
   FOV does **not** change. Release → the highlight fades over ~0.3 s rather than snapping off.
   Swap vessel mid-sight → no stuck highlight.
7. Skim to full, hold RT → the highlighted volume is a **fan** (wide across the jaw plane, narrow
   across the beam), matching the hull's jaws. Ram a prism → it narrows to match.
8. Take a crystal while sighting → the blast destroys what the sight was showing.
9. MPPM ×2 → a remote Dolphin holding RT looks normal; the sight is local-only.

**If holding RT does nothing at all**, check `blastEffect` is assigned on the Dolphin prefab's
`EchoSightActionExecutor` — unassigned is silent.

**Known limitations shipped deliberately** (see the doc's §7): seeded crystals are local-only
(`TeamCrystal.prefab` has no NetworkObject, matching the previous hold-to-plant scope), and the
sight ignores Space L5 "Clean Blast" friendly fire — it highlights everything geometrically inside
the volume.
