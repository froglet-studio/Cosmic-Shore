# BLACK HOLES — gravity wells in the HyperSea

> A black hole is spawned with ONE number — its strength — anywhere in any scene, from the
> DiagnosticsHUD console (`blackhole spawn 10`). It pulls every prism in reach into orbit or into
> the singularity, pulls vessels (which can out-run it), and bends what is drawn around its
> horizon. The physics is a simulation (live gameplay data under the movers contract); the bend is
> a §4.7 global-uniform vertex map (photons only). Status: **simple version, 2026-10-07 — nothing
> here has been run in the editor**; the gates it passed and the playtest it still needs are in §9.

## 0. Where everything is

| What | Where |
|---|---|
| The equations (pure, Burst, tested) | `_Scripts/Controller/Environment/BlackHole/BlackHolePhysics.cs` |
| One hole (the spawned object: strength, velocity, spin, visual, ease) | `BlackHole.cs` (same folder) |
| The registry + the ONE driver (`LateUpdate`, order 29500) | `BlackHoleRegistry.cs` |
| The prism MOVER — admission, the job, the bulk writes, captures | `BlackHoleGravityField.cs` |
| The vessel pull | `BlackHoleVesselPull.cs` |
| The warp's CPU half — the bank + high-poly residency | `BlackHoleWarp.cs` |
| The warp's GPU half | `_Graphics/Materials/Graphs/PrismGravityWarp.hlsl` |
| The ECS component every prism's companion entity carries | `_Scripts/Controller/ECS/Components/GravityBodyComponents.cs` (+ the prototype addition and the `SetGravityBody` / `ClearGravityBody` / `TryGetGravityBodyLookup` API in `PrismRenderService`) |
| Console commands | `BlackHoleConsole.cs` (`blackhole`, alias `bh`) |
| Tuning (the only tuning surface) | `BlackHoleConfigSO` → `Assets/Resources/BlackHoleConfig.asset` |
| The test scene | `Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity`, `BlackHoleTestHarness`, `BlackHoleTestConfigSO` → `Resources/BlackHoleTestConfig.asset`, FrogletTools ▸ Scene Setup ▸ **Setup Black Hole Test Scene** |
| Wirer / proof / gates | `Tools/Shaders/wire_prism_gravity_warp.py`, `Tools/Shaders/verify_prism_gravity_warp.py`, `PrismClockWiringValidator` (Specs + edges), `BlackHoleTests`, `BlackHolePhysicsTests` |
| Log channel | `CSLogChannel.BlackHole` (FrogletTools ▸ Toolbox ▸ Logging), one line per second while a hole is live, including the idle case |

## 1. What it is, in one paragraph

A `BlackHole` is a record with a position, a velocity, a spin axis and a strength `S`. The config
turns `S` into the two numbers the physics needs — the gravitational parameter `GM = S × gmPerStrength`
and the event-horizon radius `r_s = max(minHorizon, S × horizonPerStrength)` — and a third it derives
from them, the influence radius (where the pull falls below an acceleration floor). Every frame the
registry's driver does three things with the live holes, in this order: moves the prism bodies
inside their influence spheres (§3), pulls the vessels (§4), publishes the warp bank (§5). A prism
whose centre crosses a horizon is consumed into the singularity; a vessel inside one is held. That is
the whole feature; everything below is how each part keeps the project's laws.

## 2. The physics (`BlackHolePhysics`)

**Radial law — the Paczyński–Wiita pseudo-Newtonian potential**, `Φ = −GM / (r − r_s)`, the standard
way to put Schwarzschild gravity into a Newtonian integrator. Acceleration `a = −GM / (r − r_s)² r̂`.
It reproduces the innermost stable circular orbit at `3 r_s` and the plunge inside it, which is what
makes near-horizon mass SPIRAL IN rather than orbit forever the way `1/r` would let it. The pole at
`r_s` is floored (`PoleGuardFraction`, a quarter of the horizon): a body crossing the horizon is
captured on that step, so the floor only bounds its last kick before it vanishes.

**Frame dragging — a Lense–Thirring-style coupling.** A rotating hole drags the inertial frame around
it. The dragged frame at distance `d` turns at `frameDragging × Ω_circular(d)` about the hole's spin
axis (and travels with the hole), and every body is viscously coupled to it at `frameDragCoupling` per
second. With the fraction near 1, mass at rest is swept into orbits and the far field "keeps turning";
below 1 it spirals in. Without it every body at rest falls on a radial line — correct and dull. With
more than one hole the DOMINANT well's frame is used (the one pulling hardest at that point), for the
same reason the warp picks one slot: two frames about two centres do not add up to a frame about
anything.

**Every body is a test particle.** No mass appears in any equation because trajectories do not
depend on it (the equivalence principle). So a heavy prism and a light one fly the same path, there is
nothing to author per prism, and a pure-entity prism of the future is a body with nothing to add.

**Integration** is semi-implicit Euler (symplectic — a circular orbit wobbles but does not drift, which
`BlackHolePhysicsTests` holds over three periods), substepped per body from its closest horizon gap
up to `maxSubsteps`, so the pole is integrated rather than jumped.

**Release.** A body outside EVERY influence sphere is damped (`releaseDamping`) and, once slower than
`releaseSpeed`, released back to static mass — so mass a hole flung clear settles instead of coasting
across the arena forever, and a field with no holes left goes quiet. There is no decay, TTL or culler:
the only ways a prism leaves the simulation are a horizon or settling.

Design numbers at the shipped config (`gmPerStrength 20000`, `horizonPerStrength 2`): a strength-10
hole has `GM = 2×10⁵`, `r_s = 20`, circular speed ~45 u/s at 100 u and escape speed ~70 u/s there — a
cruising Squirrel (54 u/s) is caught at that range, a boosting vessel is not. The influence radius is
`r_s + √(GM / a_floor)` = ~600 u at the 0.6 u/s² floor, capped at 900. Strength 50 is a cell-eater.

## 3. The prism mover (`BlackHoleGravityField`) — live gameplay data, not animation

Where a prism is next frame depends on where the holes are and where every previous step put it;
the GPU could not have known it at any stamp. That is the class the clock-material law leaves alone
— `Docs/PRISM_ANIMATION.md` §1 "Animation vs. live gameplay data", §3.6 movers — so the field is a
per-frame transform write under the **movers contract**, the same contract fauna locomotion, the Ark
and the builders keep: the transform, the spatial index and the render entity all see the prism where
it actually is, and so does every collider and gameplay query.

**The data is ECS, the loop is one job.** `GravityBody` (`IComponentData`, `IEnableableComponent`:
velocity, a capture index, two one-shot verdict flags) is on every prism's companion-entity
PROTOTYPE, disabled — so admitting a prism is a non-structural `SetComponentData` +
`SetComponentEnabled`, never an archetype move (the prototype pattern's whole point), and every
prism outside a hole's reach is byte-for-byte what it was before the component existed. The per-frame
work is ONE Burst `IJobParallelForTransform` over the admitted prisms' transforms — read the pose,
step it through `BlackHolePhysics`, write the pose back, emit the render matrix and the index point —
then ONE bulk render write (`PrismRenderService.SetTransformsBatch`, scheduled on the job) and ONE
bulk index write (`PrismSpatialIndex.UpdatePositionsBatch`): the two halves of
`Prism.NotifyPositionChanged` done for N prisms at once, exactly as the swarm and the builders do.
Nothing per prism is managed except admission and the verdicts.

**Admission** runs every `admissionInterval` (0.1 s): each hole asks `PrismSpatialIndex.QuerySphere`
for the prisms inside its influence sphere, new ones are ranked nearest-first and admitted up to
`maxBodies` (6,000 across every hole). Nearest first because the field DECAYS with distance — the
budget goes to the mass the hole moves most. Never admitted: super-shielded mass (structure; nothing
can destroy it, so pulling it would pile it unkillable at the singularity), a prism still growing in
(its transform is not final), one with no index slot, one whose entity was born on a non-Prism
prototype (debris, husks — their flight is a clock stamp), and a creature's or a plant's body prism
(`HealthPrism` — posed by its rig every frame; pulling it alone would tear the body off a creature
that keeps swimming, see §8). A body starts at rest; the frame dragging
gives it its swirl.

**Two verdicts leave the set.** CAPTURED (the centre crossed a horizon): the prism is consumed through
`Prism.Consume(hole.transform, Domains.Blue, "Black Hole", devastate: true)` — devastate because a
shield is not an answer to a singularity, and the sink is the hole's transform, so the implosion
debris converges on the hole as it moves (the one moving-target implosion is already a documented
exception). RELEASED: the body is disabled and the prism is static mass again. Pooled reuse clears
the component (`ClearPrismStamps` → `ClearGravityBody`), so a reused prism never inherits the velocity
of the one it replaced.

Stated limitation: a `TransformAccessArray` job parallelises over ROOT transforms, so a field whose
prisms all share one parent (the test lattice, a cell environment) integrates on one worker. Burst
makes that cheap at the body budget; recorded, not fixed.

## 4. Vessels (`BlackHoleVesselPull`)

A vessel is not a body: it has an engine that works against the pull, and its position belongs to
the peer that drives it. So the hole carries a per-vessel GRAVITATIONAL velocity (`v_g += a·dt`, the
same acceleration a prism feels, times `vesselPullScale`, clamped at `maxVesselPullSpeed`) and hands
it to the vessel's own transformer each frame through `VesselTransformer.ModifyVelocity` — the one
sanctioned way anything outside a vessel moves it (`VesselDeviationByPrismEffectSO` and the Rhino
bounce use the same per-frame form). The engine's own travel adds on top, which IS the escape rule:
faster than the local escape speed gets away, bent — the pull is still a lateral push on its path;
slower is drawn in. The transformer clamps its whole shift channel at 100 u/s and the config keeps
the hole's share under that (90), so a knockback still registers on a falling ship, and inside the
horizon a ship is HELD rather than destroyed — a simple version's stated limit (§8).

Applied only on the machine that drives the vessel (`!IsNetworkClient`: the owner, or a
non-networked vessel) and never on a stationary one (whose transformer does not age its modifiers,
so entries would pile up). Vessels are enumerated once a second while a hole is live — there is no
vessel registry, and this is not a hot path. `ModifyVelocity` weights a fresh entry at 1.5×, and an
entry given this frame's `dt` as its duration is consumed exactly once on steady frame times, so the
shift handed over is `v_g / 1.5`; on a jittery frame an entry can survive into a second application
at ~0.5× — bounded noise, the price of the sanctioned channel over a direct position write.

## 5. The warp (`BlackHoleWarp` + `PrismGravityWarp.hlsl`) — photons only, a §4.7 global

Mass near a horizon is drawn tidally stretched toward the singularity: the face of a prism nearer
the hole is pulled harder than the face farther from it, so the prism elongates along the radial
and squeezes across it — spaghettification. It is the hole's signature on screen (the horizon is a
black sphere; what the player SEES is the mass bending around it), it sits on top of the field,
and it changes nothing a gameplay query can read.

**Legality, in the two lines a reviewer will ask for.** (1) "Where is the hole relative to this
prism" is live, per-frame data — the hole moves and the prisms move — so it can never be a per-prism
stamp, and a per-prism CPU material write is what the law forbids; the sanctioned shape is a GLOBAL
uniform (§4.7): O(1) writes per frame that every prism reads. (2) The high-poly residency swap near a
horizon is a STATE CHANGE (final at the instant it is applied, like a shield engaging), performed
strictly outside the volume the warp can move anything (`warpResidencyMargin`), budgeted
(`warpMaxResidentPrisms` 32 × `warpSubdivision` 12 → 55k triangles, nearest-to-horizon first, since
the field decays with distance), through the platform's shared-mesh handoff and `HighPolyPrismMesh`.
The prism-morph skill's five admission questions: live data (yes — a global); bounded residency (yes);
smooth motion (a radial field on a dense mesh); closed-form map with an analytic derivative (below);
gameplay state unchanged (yes — the FIELD moves mass, the warp moves photons; they are separate
systems so the bend can be tuned or switched off without touching the physics).

**The map**, with `U` the centre, `r_s` the horizon, `d = |p − U|`, `s = d − r_s`:

```
p' = U + dir · f(d),      f(d) = d · (1 − g(d)),      g(d) = w · k(s)
k(s) = (1 − smoothstep(0, 1, s / reach))^e    (k = 1 inside the horizon, 0 at the reach)
```

A STRAIN form — the displacement is a fraction of the distance, not an absolute offset (the skill's
finding (c)): the centre is a fixed point, the map is singularity-free, and a hole twice the size
warps twice the mass twice as far, which is what "stronger hole" should mean on screen. The price is
`w < 1`, which the config clamps (`warpStrength` ≤ 0.95). It never folds (`f' = (1 − g) − d·g' ≥ 1 − g > 0`)
and never crosses the centre (`f > 0`). And the tidal claim is a theorem of the map, not a tuning:
the radial stretch `a = f'(d)` exceeds the tangential stretch `b = f/d = 1 − g` wherever the falloff is
not flat, because `g' ≤ 0`. The normal is the analytic inverse-transpose,
`n' = normalize(dir·(n·dir)/a + (n − dir·(n·dir))/b)`, proven by CONVERGENCE RATE (halving the patch
quarters the error) with a negative control that neuters the radial term and plateaus.

**Splice order.** The node sits IMMEDIATELY BEFORE the cradle on both live graphs (BlockGraph,
ExplodingBlockGraph; SuctionGraph excluded — consumed mass is drawn by the implosion carrier): the
cradle must stay LAST (its header says why), and the warp must see every earlier stage's position.
A separate node rather than a map kind inside `PrismCradle.hlsl`, with the skill's "one node, many
map kinds" rule weighed: the black hole is a world object, the cradle is one vessel's ride feel, the
two never legitimately fight over a vertex, and the structural morph walk
(`Tools/Shaders/prism_vertex_chain.py`) already lets every sibling wirer see past any number of
morphs — all sixteen `wire_*.py --check` pass with the warp in place. Cost: one integer compare per
vertex when no hole is live. The wirer finds its anchor STRUCTURALLY (the morph that feeds the vertex
blocks), never by name; `PrismClockWiringValidator`'s `CradleEdges` now assert the cradle is fed by
the warp and `GravityWarpEdges` assert the warp's own feeders.

**The bank**: `_PrismGravityWarpCentre[4]` (xyz centre, w horizon), `_PrismGravityWarpWeight[4]`
(x strain, eased by the hole's `WarpWeight`; y reach), `_PrismGravityWarpParams` (exponent, live count
— the master sentinel). File-scope, outside every CBUFFER, published once per frame by the one driver,
OFF state published at `BeforeSceneLoad` and on teardown. A despawning hole keeps a falling weight
until its ease completes, so the bend lets go instead of snapping; the field has already stopped
pulling by then (only the photons ease — the split every §4.7 global keeps).

## 6. Console

From any scene (the HUD and the console auto-spawn; editor and development builds only):

```
blackhole spawn <strength> [x y z] [vx vy vz]   spawn at (x,y,z) — default 300 u ahead of the camera
blackhole here <strength>                        spawn at the camera
blackhole move <id> <vx> <vy> <vz>               set a hole's velocity (drive it through mass)
blackhole strength <id> <value>                  retune a live hole
blackhole spin <id> <ax> <ay> <az>               set its frame-dragging axis
blackhole list                                   every live hole and its numbers
blackhole despawn <id> | all                     eased release, then destroy
bh ...                                           alias
```

The HUD's `BlackHole` section shows live holes, bodies, captures, warp residents and pulled vessels.

## 7. The test scene

`Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity` (not in Build Settings, like every
Game_TestDesign scene; regenerable by **FrogletTools ▸ Scene Setup ▸ Setup Black Hole Test Scene**,
a keeper that only repairs). Before Play: FrogletTools ▸ Scene Setup ▸ Testing Multiplayer ▸ **Do not
load Bootstrap Scene on Play**. Then:

1. **Spawn field** — a 25³ lattice at 24 u pitch cut to the inscribed spheroid (~8k prisms, jittered,
   randomly rotated; the Cuboid/Spheroid button toggles the cut, `side`/`gap` resize it). Wait for
   **ready** — prisms register with the spatial index behind a budget, and only indexed prisms can
   be pulled.
2. **Hole at centre** (strength from the field) — the field orbits (frame dragging), warps near the
   horizon, and the inner mass spirals in and is consumed. The readout shows bodies / captured.
3. **Fly-through** — a strength-8 hole starts 150 u outside the field's −X edge and crosses it at
   60 u/s, dragging mass along, bending what it passes, swallowing what it reaches, and retires
   itself past the far edge.
4. **Despawn holes** / **Clear**.

Console: `bhtest <total>` (near-cube field), `bhtest shape cuboid|spheroid`, `bhtest hole [strength]`,
`bhtest fly [strength] [speed]`, `bhtest despawn`, `bhtest clear`, `bhtest zoom <0..1>`; and the
global `blackhole` commands work here too (e.g. `blackhole spawn 10 200 0 0 -40 0 0`).

## 8. Stated limits of the simple version

- **A vessel inside a horizon is held, not destroyed.** The pull is capped (90 u/s) so the game stays
  playable; a boost gets a ship out. Vessel death by singularity is a design decision, not taken here.
- **Lifeforms are not bodies yet.** A `HealthPrism` (fauna body, flora limb) is excluded from
  admission: the simple version would tear it off its rig. A creature or plant under gravity is a
  whole-body force on its locomotion — an ecology change (`/ecology`, the LOCKED principles) for a
  follow-up, not a per-prism one. Trails, cell environments and freestyle mass are all bodies.
- **Pure-entity debris is not a body.** Explosion fragments fly on the clock stamp and are not pulled;
  they ARE warped (the map is correct on any mesh), so a burst near a hole visibly leans into it.
- **No replication.** Each peer that spawns a hole runs it locally; prism bodies move on the machine
  that simulates them. Spawning is a console/test action today, not a networked game event.
- **One worker per root** for the job (above). **Cell volume accounting** is not re-filed as prisms
  cross cells (the Ark's `NotifyCellChanged` cadence would be the pattern if a mode needs it).
- **The look is a playtest away.** The harness proves the map is the map, the normal is its
  derivative, nothing folds, nothing pops; whether spaghettification READS at these numbers is the
  playtest's answer. If it comes back too subtle, the three budgets are `warpStrength` (how far),
  `warpReachMultiplier` (how much mass) and `warpExponent` (where in the shell it concentrates) — and
  the physics has its own: `frameDragging` for how much it orbits vs. plunges, `gmPerStrength` for
  how hard it pulls.

## 9. Verification record (2026-10-07)

Offline, in this order; nothing was run in the editor:

- `Tools/Shaders/wire_prism_gravity_warp.py` wired both live graphs; every one of the sixteen
  `Tools/Shaders/wire_*.py --check` passes with the warp in place; `check_shadergraph_custom_function_signatures.py` OK.
- `Tools/Shaders/verify_prism_gravity_warp.py`: ten properties hold on the SHIPPED HLSL compiled
  with clang++ (identity ×2, pull, no fold over 200k samples, radial purity, tidal `a > b`, normal
  convergence 1.7e-4 → 4.2e-5 → 1.0e-5 → 2.8e-6, no seam, affine strain, dominant slot); the negative
  control plateaus at 0.04.
- `check_conditional_compilation`, `check_console_logging`, `check_enum_member_references`,
  `check_switch_label_collisions`, `check_self_referential_locals`, `check_duplicate_attributes`,
  `check_abstract_member_implementations`, `check_using_directives`: all OK.
- `Tools/Build/unity_refcompile/run.sh --config player-dev`: see the PR for the run's verdict.
- Edit-mode: `BlackHoleTests` (HLSL/bank/slot agreement, splice order on both graphs, validator
  specs, config sanity, residency budget, test scene wiring) and `BlackHolePhysicsTests` (eleven
  claims about what a hole does, run through the shipped integrator) — written, to be run in the
  editor with the rest of the suite.

## 10. Rejected alternatives (so they are not proposed again)

- **A Rigidbody per prism / Unity physics gravity.** Prisms have no Rigidbody by design (the spatial
  index is THE spatial store; physics is structurally blind to fresh prisms). A force field through
  PhysX would move nothing.
- **Pure Newtonian `1/r`.** Every body with angular momentum orbits forever; nothing ever falls in.
  Paczyński–Wiita gives the plunge for one extra subtraction.
- **Writing `VesselStatus.Course` to bend a vessel.** `Course` is the pilot's and the AI's heading;
  re-aiming it reads as the ship steering itself. The velocity-shift channel is the sanctioned push.
- **A map kind inside `PrismCradle.hlsl`** — considered against the skill's rule; a separate node won
  on ownership (world object vs. one vessel's feel) now that the morph walk is structural (§5).
- **An absolute-offset warp** (`f = d − A·k`): the displacement would not scale with the hole, and a
  strong hole's horizon would look no different from a weak one's. The strain form scales (§5).
- **Summing the fields of two holes in the shader.** Not a radial field about anything; the analytic
  normal stops being the derivative. One authority per vertex, like the cradle.
