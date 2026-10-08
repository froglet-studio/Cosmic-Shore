# BLACK HOLES — gravity wells in the HyperSea

> A black hole is spawned with ONE number — its strength — anywhere in any scene, from the
> DiagnosticsHUD console (`blackhole spawn 10`). It pulls every prism in reach into orbit or into
> the singularity, pulls vessels (which can out-run it), drags space around with its spin, and
> spaghettifies what comes near its horizon. **Everything it does is physics, approximated — no
> painted glow, no disc, no invented swirl**: a Paczyński–Wiita pull, Lense–Thirring frame
> dragging, the general-relativistic tidal tensor, and a Schwarzschild ray trace for the lens. The
> motion is a simulation (live gameplay data under the movers contract); the stretch is a §4.7
> global-uniform vertex map (photons only). Status: **built 2026-10-07, made physical 2026-10-08 —
> nothing here has been run in the editor**; the gates it passed and the playtest it still needs
> are in §9.

## 0. Where everything is

| What | Where |
|---|---|
| The equations (pure, Burst, tested) | `_Scripts/Controller/Environment/BlackHole/BlackHolePhysics.cs` |
| One hole (the spawned object: strength, velocity, spin, visual, ease) | `BlackHole.cs` (same folder) |
| The registry + the ONE driver (`LateUpdate`, order 29500) | `BlackHoleRegistry.cs` |
| The prism MOVER — admission, the job, the bulk writes, captures | `BlackHoleGravityField.cs` |
| The vessel pull | `BlackHoleVesselPull.cs` |
| Spaghettification's CPU half — the global bank | `BlackHoleWarp.cs` |
| Spaghettification's GPU half — the tidal tensor, one affine map per prism | `_Graphics/Materials/Graphs/PrismGravityWarp.hlsl` |
| The lens — what the hole LOOKS like (ray-traced background and shadow; no painted disc) | `BlackHoleLens.cs`, `_Graphics/Materials/Graphs/BlackHoleLens.shader` + `BlackHoleLens.hlsl`, `Resources/BlackHoleLens.mat`, `Tools/Shaders/verify_black_hole_lens.py` (§5.1) |
| The ECS component every prism's companion entity carries | `_Scripts/Controller/ECS/Components/GravityBodyComponents.cs` (+ the prototype addition and the `SetGravityBody` / `ClearGravityBody` / `TryGetGravityBodyLookup` API in `PrismRenderService`) |
| Console commands | `BlackHoleConsole.cs` (`blackhole`, alias `bh`) |
| The Black Hole tool — one Spawn button at the configured position, live holes, every config field (§6.1) | `BlackHoleTool.cs` (uGUI) + `BlackHoleToolModel.cs` (pure: fields, bounds, switch); `blackhole tool on`; proof `Tools/Build/black_hole_tool_harness/run.sh`, `BlackHoleToolTests` |
| Tuning (the only tuning surface) | `BlackHoleConfigSO` → `Assets/Resources/BlackHoleConfig.asset` |
| The test scene | `Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity`, `BlackHoleTestHarness`, the mouse camera `MouseOrbitCamera` (`_Scripts/Controller/Camera/`, + `MouseOrbitCameraConfigSO` → `Resources/MouseOrbitCameraConfig.asset`, §7.1), `BlackHoleTestConfigSO` → `Resources/BlackHoleTestConfig.asset`, FrogletTools ▸ Scene Setup ▸ **Setup Black Hole Test Scene** |
| Wirer / proof / gates | `Tools/Shaders/wire_prism_gravity_warp.py`, `Tools/Shaders/verify_prism_gravity_warp.py`, `PrismClockWiringValidator` (Specs + edges), `BlackHoleTests`, `BlackHolePhysicsTests` |
| See the lens offline (renders the SHIPPED HLSL to a PNG from any viewpoint) | `Tools/Shaders/render_black_hole_lens.py` (§5.1); proof: `Tools/Shaders/verify_black_hole_lens.py` |
| Log channel | `CSLogChannel.BlackHole` (FrogletTools ▸ Toolbox ▸ Logging), one line per second while a hole is live, including the idle case |

## 1. What it is, in one paragraph

A `BlackHole` is a record with a position, a velocity, a spin axis and a strength `S`. The config
turns `S` into the two numbers the physics needs — the gravitational parameter `GM = S × gmPerStrength`
and the event-horizon radius `r_s = max(minHorizon, S × horizonPerStrength)` — and a third it derives
from them, the influence radius (where the pull falls below an acceleration floor). Every frame the
registry's driver does three things with the live holes, in this order: moves the prism bodies
inside their influence spheres (§3), pulls the vessels (§4), publishes the tidal bank (§5). A prism
whose centre crosses a horizon is consumed into the singularity; a vessel inside one is held. That is
the whole feature; everything below is how each part keeps the project's laws.

## 2. The physics (`BlackHolePhysics`)

**Radial law — the Paczyński–Wiita pseudo-Newtonian potential**, `Φ = −GM / (r − r_s)`, the standard
way to put Schwarzschild gravity into a Newtonian integrator. Acceleration `a = −GM / (r − r_s)² r̂`.
It reproduces the innermost stable circular orbit at `3 r_s` and the plunge inside it, which is what
makes near-horizon mass SPIRAL IN rather than orbit forever the way `1/r` would let it. The pole at
`r_s` is floored (`PoleGuardFraction`, a quarter of the horizon): a body crossing the horizon is
captured on that step, so the floor only bounds its last kick before it vanishes.

**Frame dragging — Lense–Thirring.** A rotating hole drags space around with it: a body with no
angular momentum of its own is carried around the spin axis at the frame's angular velocity

```
ω(r) = 2GJ / (c² r³) = a* · c · r_s² / (2 r³),      c = √(2GM / r_s)
```

— `a*` the dimensionless spin (`spin`, 0 = Schwarzschild, ≤ 0.998), `c` the light speed the
horizon implies (`r_s = 2GM/c²`), so `ω(r_s) = a*·c/(2 r_s)`, falling as `1/r³`. It is applied the
way GR applies it: the dragged frame ADVECTS the position (`p += (v + ω × r)·h`), it does not push
the velocity. A body at rest therefore falls almost radially far out and is WOUND around the hole
only as it nears the horizon — the infall twists the way the hole spins, and still ends inside it.
`ω` is held at its horizon value inside the horizon (where the body is captured anyway). With more
than one hole the DOMINANT well's frame is used (the one pulling hardest at that point): two frames
about two centres do not add up to a frame about anything. A hole's own velocity drags nothing — a
moving hole pulls; it does not tow the space around it. (The first version coupled every body
viscously to a frame turning at a fraction of the CIRCULAR speed, which swept mass at rest into
orbit from hundreds of units out. That is a fluid's swirl, not gravity; retired 2026-10-08, §10.)

**Orbits come from angular momentum, as they do in reality.** A hole spawned at rest in a field at
rest swallows a column of mass that falls straight in, wound near the end. Mass ORBITS when it has
sideways motion relative to the hole: a moving hole (`blackhole move`, the test scene's fly-through,
`spawnVelocity`) gives every body it reaches exactly that, so a hole driven through a field leaves a
wake of captured, orbiting and flung mass.

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

Design numbers at the shipped config (`gmPerStrength 2000`, `horizonPerStrength 2`, `spin 0.9`): a
strength-10 hole has `GM = 2×10⁴`, `r_s = 20`, the innermost stable orbit at 60 u (circular speed
27 u/s there), circular speed ~18 u/s at 100 u and escape speed ~22 u/s there — a cruising vessel
gets away; mass at rest does not. The influence radius is `r_s + √(GM / a_floor)` = ~200 u at the
0.6 u/s² floor, capped at 900. The implied light speed is `√(2 × gmPerStrength / horizonPerStrength)`
= 44.7 u/s for every strength (it is a property of the config's ratio, not of a hole) — a unit, not a
speed limit: nothing in the game is capped by it, and it enters only the frame-drag rate. The frame
turns at 1.0 rad/s at the horizon of a strength-10 hole, 0.13 rad/s at 2 r_s, 0.04 at 3 r_s; a
strength-20 hole turns half as fast at its own (twice as large) horizon.

## 3. The prism mover (`BlackHoleGravityField`) — live gameplay data, not animation

Where a prism is next frame depends on where the holes are and where every previous step put it;
the GPU could not have known it at any stamp. That is the class the clock-material law leaves alone
— `Docs/PRISM_ANIMATION.md` §1 "Animation vs. live gameplay data", §3.6 movers — so the field is a
per-frame transform write under the **movers contract**, the same contract fauna locomotion, the Ark
and the builders keep: the transform, the spatial index and the render entity all see the prism where
it actually is, and so does every collider and gameplay query.

**The data is ECS, the work is three chained Burst jobs.** `GravityBody` (`IComponentData`, `IEnableableComponent`:
velocity, a capture index, two one-shot verdict flags) is on every prism's companion-entity
PROTOTYPE, disabled — so admitting a prism is a non-structural `SetComponentData` +
`SetComponentEnabled`, never an archetype move (the prototype pattern's whole point), and every
prism outside a hole's reach is byte-for-byte what it was before the component existed. Per frame:

1. `ReadPoseJob` (`IJobParallelForTransform`, **`ScheduleReadOnly`**): every admitted prism's world
   pose into a `float4x4` array. Read-only transform access is NOT serialised per root hierarchy, so
   this runs on every worker even when the whole field shares one parent.
2. `IntegrateJob` (`IJobParallelFor`, batches of `IntegrateBatch` = 32): every body stepped through
   `BlackHolePhysics` — substeps × wells, the expensive part — emitting the render matrix (the read
   pose with its translation replaced), the index point and the verdict. Pure `Unity.Mathematics`;
   no `UnityEngine` or managed call, so Burst compiles all of it.
3. `WritePoseJob` (`IJobParallelForTransform`) writes the positions back to the transforms, scheduled
   beside the bulk render write (`PrismRenderService.SetTransformsBatch`, which depends on the
   integration only), then ONE bulk index write (`PrismSpatialIndex.UpdatePositionsBatch`).

The render and index writes are the two halves of `Prism.NotifyPositionChanged` done for N prisms at
once, exactly as the swarm and the builders do. Nothing per prism is managed except admission and
the verdicts.

**Admission** runs every `admissionInterval` (0.1 s): each hole asks `PrismSpatialIndex.QuerySphere`
for the prisms inside its influence sphere, new ones are ranked nearest-first and admitted up to
`maxBodies` (6,000 across every hole). Nearest first because the field DECAYS with distance — the
budget goes to the mass the hole moves most. Never admitted: super-shielded mass (structure; nothing
can destroy it, so pulling it would pile it unkillable at the singularity), a prism still growing in
(its transform is not final), one with no index slot, one whose entity was born on a non-Prism
prototype (debris, husks — their flight is a clock stamp), and a creature's or a plant's body prism
(`HealthPrism` — posed by its rig every frame; pulling it alone would tear the body off a creature
that keeps swimming, see §8). A body starts at rest; it orbits only with the angular momentum
it is given (a moving hole), and the frame dragging winds its infall (§2).

**Two verdicts leave the set.** CAPTURED (the centre crossed a horizon): the prism is consumed through
`Prism.Consume(hole.transform, Domains.Blue, "Black Hole", devastate: true)` — devastate because a
shield is not an answer to a singularity, and the sink is the hole's transform, so the implosion
debris converges on the hole as it moves (the one moving-target implosion is already a documented
exception). RELEASED: the body is disabled and the prism is static mass again. Pooled reuse clears
the component (`ClearPrismStamps` → `ClearGravityBody`), so a reused prism never inherits the velocity
of the one it replaced.

Stated limit: Unity serialises a transform WRITE job per root hierarchy, so a field whose prisms all
share one parent (the test lattice, a cell environment) runs `WritePoseJob` on one worker. That job
is one position store per body; the read and the integration run on every worker. (The first
version did everything in one `IJobParallelForTransform` and so integrated a one-parent field on a
single worker. It was split on 2026-10-07.)

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

## 5. Spaghettification (`BlackHoleWarp` + `PrismGravityWarp.hlsl`) — photons only, a §4.7 global

A body near a hole is pulled harder on its near side than its far side, so it is STRETCHED along the
line to the hole and SQUEEZED across it. The tidal tensor a freely falling body feels near a
Schwarzschild hole, in its own frame, is exactly

```
T = (GM / r³) · diag(+2, −1, −1)          radial, transverse, transverse
```

— a textbook GR result (the Riemann components in a radially infalling orthonormal frame): finite at
the horizon, falling as `1/r³`, trace-free. A body that yields to that tide for a response time `τ`
(`tidalResponseSeconds` — how soft it is) is drawn with the log-stretch

```
ε = GM · τ² / r³,      radial × e^ε,      transverse × e^(−ε/2)
```

applied to each prism as ONE affine map about its own centre (its object origin), along the line from
the hole to that centre. So:

- **Strongest at the horizon** (`ε_h = GM·τ²/r_s³`), 8× weaker at twice the distance.
- **Volume is conserved exactly** (`e^ε · e^(−ε/2) · e^(−ε/2) = 1`) — tides deform, they do not compress.
- **Small holes shred harder.** `r_s` grows with the mass, so `ε_h ∝ 1/M²`, as in reality — a
  stellar-mass hole spaghettifies you outside its horizon, a supermassive one swallows you whole.
- **It never folds**: every stretch is positive. **The centre stays put**: tides deform, and the
  gravity field (§3) is what moves mass — this is not a second pull.
- **One affine map per prism** keeps flat faces flat, so the authored 24-triangle prism is exact —
  **no high-poly mesh, no residency, no per-frame spatial query**. The normal is the map's inverse
  transpose, also exact.

At the shipped config (`τ` 0.9 s, strength 10, `r_s` 20) a prism is drawn ×6.4 long and ×0.40 wide at
the horizon (the law says ×7.6; the ceiling below takes the rest), ×1.8 long at 1.5 r_s, ×1.3 at
2 r_s and ×1.08 at 3 r_s. A strength-5 hole draws needles at its horizon (at the ×12 ceiling) and
×2.7 at 2 r_s; a strength-40 hole only ×1.13 at its horizon.

**Two shaping terms, both confined to where the physics is not visible.** The **ceiling**
`maxTidalStretch` eases ε into `C = ln(maxStretch)` through a 4-norm soft minimum,
`ε' = ε / (1 + (ε/C)⁴)^¼` — the law to 0.1% up to a quarter of the ceiling and 1.5% at half of it, and
never past it — so the horizon of a tiny hole draws a long needle, not a line to infinity. The
**reach** `warpReachMultiplier` bounds which prisms are evaluated at all: the tide is drawn exactly
across the inner half of the shell and fades (C1) to zero across the outer half, where it is already
≤ 1/43 of the horizon's at the shipped reach (6 r_s). The transverse squeeze always keeps ε'/2, so
volume stays conserved through both.

**Legality, in the line a reviewer will ask for.** "Where is the hole relative to this prism" is
live, per-frame data — the hole moves and the prisms move — so it can never be a per-prism stamp,
and a per-prism CPU material write is what the law forbids; the sanctioned shape is a GLOBAL uniform
(§4.7): O(1) writes per frame that every prism reads. The prism-morph skill's admission questions:
live data (yes — a global); residency (none needed — affine per prism); closed-form map with an
analytic normal (above); gameplay state unchanged (yes — the FIELD moves mass, this moves photons;
two systems, so the stretch can be tuned or switched off without touching the physics).

**Superseded 2026-10-08.** The first cut slid every VERTEX toward the singularity by a fraction of its
distance (`p' = U + dir·d·(1 − w·k(s))`, on a 32 × s12 high-poly residency). That was a second, invented
pull on top of the real one, and its falloff — flat at the horizon — put the stretch's maximum
mid-reach and ZERO at the horizon, the opposite of a tide (§10).

**Splice order.** The node sits IMMEDIATELY BEFORE the cradle on both live graphs (BlockGraph,
ExplodingBlockGraph; SuctionGraph excluded — consumed mass is drawn by the implosion carrier): the
cradle must stay LAST (its header says why), and the stretch must see every earlier stage's position.
A separate node rather than a map kind inside `PrismCradle.hlsl`, with the skill's "one node, many
map kinds" rule weighed: the black hole is a world object, the cradle is one vessel's ride feel, the
two never legitimately fight over a vertex, and the structural morph walk
(`Tools/Shaders/prism_vertex_chain.py`) already lets every sibling wirer see past any number of
morphs. Its signature `(Position, Normal) → (OutPosition, OutNormal)` did not change in the re-cut,
so neither did the wiring. Cost: one integer compare per vertex when no hole is live; with one live,
a few transforms, two square roots and two exps per vertex. The wirer finds its anchor STRUCTURALLY
(the morph that feeds the vertex blocks), never by name; `PrismClockWiringValidator`'s `CradleEdges`
assert the cradle is fed by the node and `GravityWarpEdges` assert the node's own feeders.

**The bank**: `_PrismGravityWarpCentre[4]` (xyz centre, w horizon), `_PrismGravityWarpWeight[4]`
(x `GM·τ²`, eased by the hole's `WarpWeight`; y reach), `_PrismGravityWarpParams` (ln of the stretch
ceiling, live count — the master sentinel). File-scope, outside every CBUFFER, published once per
frame by the one driver, OFF state published at `BeforeSceneLoad` and on teardown. A despawning hole
keeps a falling weight until its ease completes, so the stretch lets go instead of snapping; the
field has already stopped pulling by then (only the photons ease — the split every §4.7 global
keeps). With more than one hole, the hole with the larger tide at a prism's centre stretches it.

**Stated imprecision.** Entities Graphics culls by a prism's authored bounds, which a per-frame global
cannot grow, so a prism stretched ×N whose bounds are just off-screen can lose a needle tip that
should show. The tensor is the radial-free-fall frame's; an orbiting prism feels the same tensor to
the accuracy that matters on screen.

## 5.1 The lens — what the player SEES (`BlackHoleLens` + `BlackHoleLens.shader` / `.hlsl`)

A black hole is invisible; what is visible is everything behind it bent around it. The hole is drawn
by a per-pixel **Schwarzschild ray trace** of the scene behind it, not by a sphere and a disc mesh
(that was the first cut, and it read as a black ball with a yellow ring). It is two things and
nothing else — the shadow and the bent background:

- **The background distorts.** Each pixel near the hole follows its light ray backwards from the
  eye through the hole's spacetime. A ray that escapes left along a BENT direction, and the pixel
  shows what the scene has in THAT direction — prisms and the skybox smear into arcs, and a point
  straight behind the hole becomes an Einstein ring.
- **The shadow is ~2.6× the horizon.** Rays closer than the critical impact parameter
  `b_c = (3√3/2) r_s ≈ 2.598 r_s` fall in, so the black disc on screen is the photon-capture
  cross-section, not the horizon.

**There is no accretion disc, by decision (2026-10-07).** What orbits and spirals into the hole is the
REAL mass — the prisms the gravity field moves (§3) and the tides spaghettify (§5) — so the "disc"
a player sees is the hole's own swirl of matter, not a painting. A synthetic disc was built into the
lens (a thin Shakura–Sunyaev disc in the spin plane, relativistic Doppler beaming and redshift,
Keplerian spiral streaks, its density fed by every capture) and went through two rounds in the
editor: first as a flat peach-to-tan plate (its opacity was a clamp, so a fed disc sat at α = 1, and
with no tonemapper its HDR glow was clipped per channel — fixed with Beer–Lambert opacity and a
hue-preserving roll-off), then, correctly rendered, seen edge-on as a bright line slicing through
the shadow. That second look is what a real disc does, and it is not what this hole should be: it
was removed — shader, feed (`BlackHole.DiskFeed` / `NotifyCapture`), the eleven `disk*` config
fields, its verifier properties and its renderer flags. `BlackHoleTests.Lens_HasNoPaintedAccretionDisc`
keeps it from creeping back; if a disc is ever wanted again, the commit history has it whole.

**The equation** is the null-geodesic Binet equation `u'' + u = (3/2) r_s u²`, integrated in 3D as a
particle under the fictitious central force `x'' = −(3/2) h² x / |x|⁵` (units of r_s; it conserves `h`
and traces exactly the photon's orbit) with **velocity Verlet** — first-order Euler at the same step
put the shadow's edge 2% inside `b_c`, which the harness caught. Step = 8% of the current radius, so
rays are fine near the photon sphere and coarse far out; 128 steps per pixel by default.

**How it draws.** One SPHERE per hole — the lens volume itself, an icosphere (320 triangles) that
circumscribes the lens radius, scaled to the lens diameter (30 r_s by default) — in the transparent
queue, after URP copies the opaque scene. The shader draws its BACK faces (`Cull Front`): a convex
mesh shows exactly one back-face layer over every pixel whose ray passes through it, so the lens
covers its true screen footprint from any viewpoint — far away, close up, off to one side, or with
the camera INSIDE the lens (a strong hole's lens is hundreds of units wide, so flying into it is
ordinary). Its depth is pinned just inside the far plane, so a lens wider than the camera's far
distance is never clipped. **Only what is behind the hole is lensed**: the fragment reads the depth
texture first and discards wherever the opaque scene is in front of the hole's centre (`ZTest
Always`, the test made in the shader), so an occluded pixel costs one depth read and never runs the
trace. The hole grows in on spawn and shrinks out on despawn (its effective horizon rides the same
eased weight as the warp), so the shadow never pops. Per-hole numbers go through a
`MaterialPropertyBlock` each frame — one renderer per hole, at most four; the clock-material law
governs prisms, not this.

*Why not the first version's billboard:* a camera-facing quad at the hole's centre depth, as wide as
the lens, covers the lens's screen footprint only from far away. Measured on axis, it missed 0.6%
of the rays that should bend with the camera at 5 lens radii, 9.8% at 2, **22% at 1.5** (about the
test scene's framing), 40% at 1.2, and from inside the lens it could not cover the view — the lens
was cut off at a hard edge close up and lost entirely from inside. *Why not a full-screen camera
effect:* it would shade every pixel of every camera for every hole; the sphere shades only the
lens's own footprint, with the same per-pixel trace, and needs no renderer feature.

**Camera textures.** The lens reads URP's opaque-scene copy and depth texture, which are OFF in
`URP_Asset` (they cost a copy per frame). `BlackHoleLens.CameraSupport` turns them on for the MAIN
camera only while a hole is live, follows the main camera if it changes, and restores the camera's
own settings when the last hole goes. The project's opaque copy is 2× downsampled (asset-level, left
alone), so the lensed background is slightly softer than the unbent scene.

**Stated screen-space limits.** A bent ray that leaves the screen samples URP's sky reflection
cubemap instead of the scene, so off-screen prisms are not lensed in; a bent ray that lands on
something IN FRONT of the hole is rejected the same way (the copy cannot see past it). The bend is
faded to the straight ray over the outer 45% of the lens radius — light at impact parameter `b` is
really deflected by ~`2/b` at any distance, so a finite lens would otherwise draw a seam at its edge.

**Seeing it without the editor.** `python3 Tools/Shaders/render_black_hole_lens.py --out lens.png`
renders the SHIPPED HLSL (translated by the verifier's mechanical HLSL→C++ step, compiled with
clang++) through the shader's own composite and the project's display transform, against a
stand-in background (procedural sky, a dark-blue prism field), from any viewpoint (`--dist`,
`--yaw`, `--pitch`, `--fov`; `--dist` below the lens radius renders from inside it), and `--hlsl`
renders another version of the file — the way to compare a change before and after.

**Proof.** `Tools/Shaders/verify_black_hole_lens.py` compiles the SHIPPED HLSL with clang++ and runs
it: the shadow edge at 2.594 r_s (exact 2.598), Einstein deflection at b = 20/40/80 r_s within 0.8%
of Schwarzschild's second-order value, no light from inside the horizon, 4,000 random rays (eyes
inside and outside the lens) finite with unit escape directions, a seamless fade — and a negative
control: a coarse step fails five of them. Both shader stages are then compiled twice:
glslang against a URP mock laid out FILE BY FILE at the shader's own include paths, and DXC against
the REAL URP + core ShaderLibrary (the graphics checkout `Tools/Build/unity_refcompile` fetches) for
D3D11, Vulkan and Metal — each with a negative control that removes one include and must fail.

**Incident (2026-10-07): the first lens drew a MAGENTA quad.** The fragment stage called
`DecodeHDREnvironment`, which lives in core's `EntityLighting.hlsl`, while the shader included only
URP's `Core.hlsl` + the two Declare* files — none of which reach it. Unity failed the compile and
substituted its error shader. The offline check had passed because its mock was ONE blob that
declared every symbol the shader used, so a missing include could not fail it. Fixed by the include;
the verifier's mock is now per-file and the real-library DXC compile is the check that would have
caught it (`--require-real` makes its absence a failure). And because a shader that fails to compile
still reports `Shader.isSupported`, `BlackHoleLens.IsDrawable` also asks
`ShaderUtil.ShaderHasError` in the Editor: a broken lens now falls back to the black sphere with the
compiler's first error in a warning, never magenta, and `BlackHoleTests.Lens_ShaderCompilesAndTheLensIsDrawable`
fails the edit-mode suite on it. **Nothing here has been seen on screen**; the look is a playtest away.
Dials (`BlackHoleConfig`, Lens header): `lensRadiusMultiplier`, `lensFadeStart`, `lensSteps`;
`lensEnabled` off falls back to the plain black sphere.

## 6. Console

From any scene (the HUD and the console auto-spawn; editor and development builds only):

```
blackhole tool [on|off]                          open / close the Black Hole tool (§6.1); no word = toggle
blackhole config                                 open the tool on its config view
blackhole spawn                                  spawn from the config's Spawn section, at its spawn position
blackhole spawn <strength> [x y z] [vx vy vz]   spawn at (x,y,z) — default 300 u ahead of the camera
blackhole here <strength>                        spawn at the camera
blackhole size <id> <r_s>                        resize a live hole (event-horizon radius; 0 = from strength)
blackhole move <id> <vx> <vy> <vz>               set a hole's velocity (drive it through mass)
blackhole strength <id> <value>                  retune a live hole's pull
blackhole spin <id> <ax> <ay> <az>               set its spin axis (Lense–Thirring frame dragging)
blackhole list                                   every live hole and its numbers
blackhole despawn <id> | all                     eased release, then destroy
bh ...  /  black hole ...                        aliases ("black hole tool on" works as typed)
```

The HUD's `BlackHole` section shows live holes, bodies, captures, how many holes are stretching
prisms, and pulled vessels.

### 6.1 The Black Hole tool (`BlackHoleTool`, `BlackHoleToolModel`)

`blackhole tool on` opens a panel (top-right, drag it by its title bar; `blackhole tool off` or ×
closes it). **Its values live in the config ASSET, `Resources/BlackHoleConfig`, not in the tool**, so
what is on the asset is what spawns — from the tool, or from `blackhole spawn` with no strength.

- **SPAWN** rows edit the asset's Spawn section: **Spawn Strength** (the pull: `GM = strength ×
  gmPerStrength`), **Spawn Horizon Radius** (the SIZE — the event-horizon radius in world units;
  0 derives it from the strength), **Spawn Position** (WHERE the hole goes — its centre, world space),
  **Spawn Velocity** (u/s, world space; zero parks it) and **Spawn Spin Axis** (world). A caption
  shows what those make: r_s, the shadow (~2.6 r_s), the lens radius, the position and how far it is
  from the camera, GM and the influence radius.
- **Spawn** spawns from exactly those values at the spawn position (`BlackHoleRegistry.SpawnFromConfig`)
  — no camera involved, and no preset buttons: to put a hole somewhere else, change the position.
  **Despawn all**; **Save asset** (Editor) writes the asset to disk.
- **LIVE HOLES** lists each hole with **Retune** (apply the current spawn strength and size to it)
  and **Despawn**.
- **Config ▾** opens a second panel, docked to the left, with EVERY other field of the asset —
  physics, budgets, vessels, spaghettification, lens — grouped by its `[Header]`, a slider wherever the field has a
  `[Range]`, an input otherwise, clamped to its own `[Range]` / `[Min]`; hovering a label shows the
  field's `[Tooltip]`. It is generated from the SO by reflection (`BlackHoleToolModel.EditableFields`),
  so a field added to the config appears here with no change to the tool. **Select asset** (Editor)
  selects it in the Inspector.

Edits apply live. In the Editor they edit the asset itself and mark it dirty (the config is outside
`_SO_Assets/`, so `PlayModeSOProtector` does not revert it) — **Save asset** persists them now, the
project's next save otherwise.

**Size and strength are separate.** A hole's horizon radius is its own `Size` when set (> 0), else
`max(minHorizonRadius, horizonPerStrength × strength)`. So to change how big holes are: set **Spawn
Horizon Radius** in the tool (or `blackhole size <id> <r_s>` on a live one) for one hole, or
**horizonPerStrength** / **minHorizonRadius** for every hole that derives its size from strength.
What the player sees scales from r_s: the shadow is ~2.6 r_s, the lens bends out to
**lensRadiusMultiplier** r_s, and spaghettification is evaluated out to **warpReachMultiplier** r_s.
How hard a hole of a given size shreds is `GM·τ²/r_s³` — a bigger hole at the same strength stretches
LESS at its horizon (§5).

**Proof.** `Tools/Build/black_hole_tool_harness/run.sh` compiles the SHIPPED `BlackHoleConfigSO.cs`
and `BlackHoleToolModel.cs` (with a handful of `UnityEngine` stubs — the engine's reference DLLs have
no method bodies) and runs them: the tool reaches all 29 config fields, every number is bounded, the
asset, the SO and the tool agree key for key and the asset loads through the model sane, the size
helpers, the clamping, the labels and the switch — and two negative controls (a config with an
unsupported field, an asset with a renamed key) fire. `BlackHoleToolTests` (edit mode) checks the
model against Unity's own `SerializedObject` view of the asset, the spawn pose, the size helpers, the
clamping, and builds the real panel and closes it.

## 7. The test scene

`Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity` (not in Build Settings, like every
Game_TestDesign scene; regenerable by **FrogletTools ▸ Scene Setup ▸ Setup Black Hole Test Scene**,
a keeper that only repairs). Before Play: FrogletTools ▸ Scene Setup ▸ Testing Multiplayer ▸ **Do not
load Bootstrap Scene on Play**. Then:

1. **Spawn field** — a 25³ lattice at 24 u pitch cut to the inscribed spheroid (~8k prisms, jittered,
   randomly rotated; the Cuboid/Spheroid button toggles the cut, `side`/`gap` resize it). Wait for
   **ready** — prisms register with the spatial index behind a budget, and only indexed prisms can
   be pulled.
2. **Hole at centre** (strength from the field) — the mass at rest falls in, wound the way the hole
   spins as it nears the horizon, spaghettified on the way, and is consumed. The readout shows
   bodies / captured.
3. **Fly-through** — a strength-8 hole starts 150 u outside the field's −X edge and crosses it at
   60 u/s: relative to the hole, every prism it reaches is moving sideways, so this is where ORBITS
   happen — mass is swung around, captured or flung, stretched as it passes the horizon — and it
   retires itself past the far edge.
4. **Despawn holes** / **Clear**.

Console: `bhtest <total>` (near-cube field), `bhtest shape cuboid|spheroid`, `bhtest hole [strength]`,
`bhtest fly [strength] [speed]`, `bhtest despawn`, `bhtest clear`, `bhtest zoom <0..1>`, `bhtest frame`;
and the global `blackhole` commands work here too (e.g. `blackhole spawn 10 200 0 0 -40 0 0`).

### 7.1 The mouse camera (`MouseOrbitCamera`)

The scene's camera is a strategy-game mouse camera, Transport Fever style — it looks at a PIVOT from
a distance, and the mouse moves the pivot, turns around it and dollies toward it:

| Input | Does |
|---|---|
| **Right-drag** | Pan — the world moves with the cursor as if grabbed (exact at the pivot's depth: world-per-pixel is derived from the distance and the FOV) |
| **Left-drag** on empty space, or **Alt + right-drag** | Orbit — yaw and pitch about the pivot, pitch held short of the poles; the cursor hides and locks while orbiting |
| **Wheel** | Zoom toward the point under the cursor, which stays fixed on screen (exact: `pivot' = pivot + (p − pivot)(1 − new/old)`) |
| **Middle-drag** up / down | Zoom in / out about the pivot |
| **WASD** / **Q E** | Pan / turn; **Shift** = faster |
| **F** or **Home** (or the **Frame (F)** button, `bhtest frame`) | Back to the home view of the field |

A drag only starts from a press that is NOT on UI, so the panel's buttons and the DiagnosticsHUD still
click; keys are ignored while a text field has focus, so typing a console command never flies the
camera. Zoom is proportional (every notch is the same fraction of the distance at any range) and the
whole camera eases on unscaled time, so it works while paused. The harness never writes the camera's
transform — it says where home is (`SetHome` / `FrameHome`) and how far to sit (`SetDistance`, the
zoom slider, which follows the wheel back). Bindings and speeds: `MouseOrbitCameraConfigSO` →
`Resources/MouseOrbitCameraConfig.asset`; Transport Fever's own hand (left-drag pan, right-drag
orbit) is a two-field swap there. Not a gameplay camera — vessels fly on `CustomCameraController`,
which this does not touch. Math held by `MouseOrbitCameraTests` (pan anchoring, zoom-toward-cursor
fixed point, viewport round trip, wheel scale, shipped bindings).

## 8. Stated limits of the simple version

- **A vessel inside a horizon is held, not destroyed.** The pull is capped (90 u/s) so the game stays
  playable; a boost gets a ship out. Vessel death by singularity is a design decision, not taken here.
- **Lifeforms are not bodies yet.** A `HealthPrism` (fauna body, flora limb) is excluded from
  admission: the simple version would tear it off its rig. A creature or plant under gravity is a
  whole-body force on its locomotion — an ecology change (`/ecology`, the LOCKED principles) for a
  follow-up, not a per-prism one. Trails, cell environments and freestyle mass are all bodies.
- **Pure-entity debris is not a body.** Explosion fragments fly on the clock stamp and are not pulled;
  they ARE stretched (each fragment about the prism it came from), so a burst near a hole is drawn
  out toward it.
- **No replication.** Each peer that spawns a hole runs it locally; prism bodies move on the machine
  that simulates them. Spawning is a console/test action today, not a networked game event.
- **One worker per root** for the transform write-back only (above); the read and the integration
  are parallel. **Cell volume accounting** is not re-filed as prisms
  cross cells (the Ark's `NotifyCellChanged` cadence would be the pattern if a mode needs it).
- **The look is a playtest away.** The harness proves the stretch is the tidal tensor, the normal is
  its inverse transpose, nothing folds, nothing pops; whether spaghettification READS at these numbers
  is the playtest's answer. The dials, all physical: `tidalResponseSeconds` (τ — how soft a prism is;
  the stretch goes as τ²), `maxTidalStretch` (the needle a tiny hole draws), `warpReachMultiplier`
  (how far out the tide is evaluated — it is invisible past ~3 r_s anyway); `spin` (how hard the
  infall winds); `gmPerStrength` / `horizonPerStrength` (how hard it pulls and how big it is — and
  since tides go as `GM/r_s³`, a smaller horizon at the same pull shreds much harder).
- **Release damping is the one non-physical term.** Mass a hole flung clear is damped back to rest
  outside every influence sphere, so the field settles instead of coasting forever (§2).

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
- `Tools/Build/unity_refcompile/run.sh --config player-dev` over the final tree: **0 errors in
  project code, 0 unverified missing-type errors** across 91 assemblies (the ten new runtime files
  are in `Assembly-CSharp.rsp` and in no error bucket). Negative control: a planted call to a missing
  member lands in the "unverified" bucket (1), so that bucket reading 0 is the evidence, not the
  headline alone. `--config editor`: the four Editor-folder files (both tests, the setup tool, the
  validator) compile clean; its 4 reported errors are the README's known `CS0118 'Editor' is a
  namespace` false positives in untouched files. `check_generated_assets.py`: 4 added + 2 modified
  assets OK against the compiled schema.
- Edit-mode: `BlackHoleTests` (HLSL/bank/slot agreement, splice order on both graphs, validator
  specs, config sanity, residency budget, test scene wiring) and `BlackHolePhysicsTests` (eleven
  claims about what a hole does, run through the shipped integrator) — written, to be run in the
  editor with the rest of the suite.

### 9.1 Second round (2026-10-07): the first editor run's three faults

The first editor run reported three problems. All three are fixed, and nothing has been run in the editor
since (no `/verify-unity` in this session):

1. **`Unable to find internal function System.MathF::Sqrt`** (`::Pow`, `::Exp`, `::Sin`, `::Cos`,
   `::Acos`). These were not black-hole code: Burst was rejecting the substrate's `SubstrateAgentJob` and
   the swarm's `SwarmPoseJob`, whose kernels used `MathF`. They now go through `KernelMath`, which is
   `Unity.Mathematics` in Unity (`Docs/SUBSTRATE_FAUNA.md` §7.6).
2. **A magenta lens quad.** A missing `EntityLighting.hlsl` include (§5.1, Incident).
3. **The integration ran on one worker** for a field with one parent. It is now three chained jobs (§3).

Evidence, all run on the final tree:
- `verify_black_hole_lens.py --require-real`: A 1–10 hold. B1 (per-file mock) and B2 (DXC against
  the real URP 17.0 / core ShaderLibrary, D3D11 + Vulkan + Metal) compile, and each negative control
  fires. The prism warp custom function also compiles under DXC against the real library on all
  three APIs.
- `unity_refcompile` player-dev and player (release): **0 errors in project code, 0 unverified**.
  All five changed runtime files are in `Assembly-CSharp.rsp`, and `UNITY_5_3_OR_NEWER` is in its
  defines, so the `Unity.Mathematics` branch of `KernelMath` is the one that compiled.
- `unity_refcompile` editor: 0 errors in changed files. The 4 remaining are the README's known
  `CS0118` false positives in untouched files.
- `substrate_harness`: all groups pass, including group K's bit-match.
- `swarm_core_harness`: OK. It runs the burst pose gate and the new `check_kernel_math.py`.
- `ecology_lod_harness` and `showcase_cell_harness`: OK.
- Both glue typechecks: OK.
- The 8 Python C# gates: OK.
- The Burst gates' negative control: the pre-fix kernels fail on the new `MathF.` rule.

### 9.2 Third round (2026-10-08): made physical

The artificial frame-drag swirl became Lense–Thirring advection (§2) and the vertex slide became the
GR tidal stretch (§5); the slide's high-poly residency and its five config fields
(`warpStrength`, `warpExponent`, `warpSubdivision`, `warpMaxResidentPrisms`, `warpResidencyMargin`)
and the swirl's two (`frameDragging`, `frameDragCoupling`) are gone; `spin`, `tidalResponseSeconds` and
`maxTidalStretch` are new. Nothing has been run in the editor (no `/verify-unity` in this session).
Evidence, on the final tree:

- `verify_prism_gravity_warp.py`: thirteen properties hold on the SHIPPED HLSL under clang++ — identity
  ×2, centre fixed, the tensor measured number for number at 1.5–5 r_s (`+ε`, `−ε/2`, to 0.03%), `1/r³`
  and `ε_h ∝ 1/M²` (4.00×), volume to 4e-5 at every strength, no fold, the ceiling reached and never
  passed, the ceiling within 1.5% of the law at half of it, affine (flat faces flat), the normal
  perpendicular to every stretched face (4e-5), linear ease, no seam, dominant slot — and the negative
  control (normal correction off) fires at |cos| 0.95.
- `BlackHolePhysicsTests`: the frame-drag claims rewritten for Lense–Thirring — `ω(r_s) = a*·c/(2r_s)`,
  `1/r³`, held inside the horizon, zero with no spin and on the axis, and a body released at rest is
  wound the way the hole spins and still captured, where a non-rotating hole's infall stays radial.
  `BlackHoleTests`: the config's tidal law and its sanity. Written; to be run in the editor.
- The rest of this round's gates are listed in its commit message.

## 10. Rejected alternatives (so they are not proposed again)

- **A painted accretion disc** (thermal glow, Doppler beaming, fed by captures). Built, fixed once,
  and removed (§5.1): edge-on it is a bright line slicing through the shadow. The hole is its shadow
  and the bent scene; the matter around it is the real prisms it moves and spaghettifies.

- **A Rigidbody per prism / Unity physics gravity.** Prisms have no Rigidbody by design (the spatial
  index is THE spatial store; physics is structurally blind to fresh prisms). A force field through
  PhysX would move nothing.
- **Pure Newtonian `1/r`.** Every body with angular momentum orbits forever; nothing ever falls in.
  Paczyński–Wiita gives the plunge for one extra subtraction.
- **Writing `VesselStatus.Course` to bend a vessel.** `Course` is the pilot's and the AI's heading;
  re-aiming it reads as the ship steering itself. The velocity-shift channel is the sanctioned push.
- **A map kind inside `PrismCradle.hlsl`** — considered against the skill's rule; a separate node won
  on ownership (world object vs. one vessel's feel) now that the morph walk is structural (§5).
- **A per-vertex slide toward the singularity** (the first cut, a radial strain `p' = U + dir·d·(1 − g)`,
  and before it an absolute-offset form `f = d − A·k`). Both are a second pull that gravity does not
  exert, and the strain's falloff put the stretch's maximum mid-reach and zero at the horizon. The tide
  is affine per prism, exact on 24 triangles, and strongest where it should be (§5).
- **A viscous swirl as "frame dragging"** (every body coupled to a frame turning at a fraction of the
  circular speed). It swept mass at rest into orbit from far out — a whirlpool, not a spinning hole.
  Lense–Thirring falls as `1/r³` and advects position; orbits come from angular momentum (§2).
- **Summing the tides of two holes in the shader.** The tensors do add, but two stretch axes do not make
  one stretch about a single axis, and the per-prism map would stop being a pure stretch. The larger
  tide wins, as the larger pull wins in the integrator.
