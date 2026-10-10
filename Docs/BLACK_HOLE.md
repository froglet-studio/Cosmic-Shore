# BLACK HOLES — gravity wells in the HyperSea

> A black hole is spawned with ONE number — its strength — by `BlackHoleRegistry.Spawn`; in the game
> today only the Stoat's field dipole lays them (`StoatDipoleExecutor`, two at a time). It pulls every prism in reach into orbit or into
> the singularity, pulls vessels (which can out-run it), drags space around with its spin, and
> spaghettifies what comes near its horizon. **Everything it does is physics, approximated — no
> painted glow, no disc, no invented swirl**: a Paczyński–Wiita pull, Lense–Thirring frame
> dragging, the general-relativistic tidal tensor, and the Vessel Studio's lens (a shadow at the photon-capture
> radius, Einstein bending, a thin photon ring), every hole in one screen pass. The
> motion is a simulation (live gameplay data under the movers contract); the stretch is a §4.7
> global-uniform vertex map (photons only). Status: **built 2026-10-07, made physical 2026-10-08 —
> nothing here has been run in the editor**; the gates it passed and the playtest it still needs
> are in §9.

> **Naming (2026-10-08): player-facing these are WORMHOLES.** A black hole is an **attractor
> wormhole**, a white hole a **repulsor wormhole**, and a black–white pair an attractor–repulsor
> wormhole pair — the Stoat's sling, the tool's buttons ("WORMHOLE TOOL", Attractor / Repulsor),
> the console (`wormhole attractor|repulsor|pair`, alongside the old `blackhole` verbs). **The code
> keeps its black-hole names** (`BlackHole`, `HolePolarity.Black/White`, `BlackHoleRegistry`): the
> real wormhole mechanics — the dipole joined by a Butterfly-fold throat — are being built on
> `cece/charming-cerf-alf1j1` on top of these same types, so a rename here would only collide with
> it. What this file built is the PLACEHOLDER the Stoat flies with until that lands.
> **Ownership:** a hole a vessel slung (`BlackHole.OwnerVessel`) pulls only that vessel — a vessel may
> not move an opposing vessel (`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). Tool/console/cell holes pull everyone.

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
| The lens — what the holes LOOK like (the Vessel Studio's lens, every hole in one screen pass; no painted disc) | `BlackHoleLens.cs` (the per-hole marker + `ScreenWells`, the studio's `setLensUniforms`), `_Graphics/Materials/Graphs/BlackHoleLens.shader` + `BlackHoleLens.hlsl`, `Resources/BlackHoleLens.mat`, `Tools/Shaders/verify_black_hole_lens.py` (§5.1) |
| The ECS component every prism's companion entity carries | `_Scripts/Controller/ECS/Components/GravityBodyComponents.cs` (+ the prototype addition and the `SetGravityBody` / `ClearGravityBody` / `TryGetGravityBodyLookup` API in `PrismRenderService`) |
| The lens's own render pass — copies the camera colour AFTER the transparents and draws every hole in one full-screen triangle from it (Render Graph, injected from script; §5.1) | `BlackHoleLensPass.cs` |
| Who lays holes | The Stoat's field dipole only (`StoatDipoleExecutor`; `R_VesselActions/STOAT_DIPOLE.md`). Its settings are the studio's, ported (`StoatDipoleConfig.asset`) |
| Keys, console, the Black Hole tool, the test scene | **Retired 2026-10-10** (§6). The Vessel Studio's Stoat page is where a pair is tried now |
| Tuning (the only tuning surface) | `BlackHoleConfigSO` → `Assets/Resources/BlackHoleConfig.asset` |
| The crystal wormhole that grew out of the Black Hole cell (attractor + repulsor, smooth wells) | `Docs/CRYSTAL_WORMHOLE.md` |
| Wirer / proof / gates | `Tools/Shaders/wire_prism_gravity_warp.py`, `Tools/Shaders/verify_prism_gravity_warp.py`, `PrismClockWiringValidator` (Specs + edges), `BlackHoleTests`, `BlackHolePhysicsTests` |
| See the lens offline (the studio's GLSL and the SHIPPED lens side by side, to a PNG) | `Tools/Shaders/render_black_hole_lens.py` (§5.1); proof: `Tools/Shaders/verify_black_hole_lens.py` |
| Log channel | `CSLogChannel.BlackHole` (FrogletTools ▸ Toolbox ▸ Logging), one line per second while a hole is live, including the idle case |

## 0.1 Where it stands (2026-10-08) — read this first

> **Two branches, one engine (merged 2026-10-08).** `cece/charming-cerf-alf1j1` took the black hole
> from this branch file by file (at `1eb1d0b5f`) and grew it into the CRYSTAL WORMHOLE (§12,
> `Docs/CRYSTAL_WORMHOLE.md`); this branch grew it into white holes, drifting pairs and the Stoat
> (§11). The merge took that commit as the true base for every copied file, so neither side's work
> was re-derived, and kept BOTH pair styles behind one switch (§13). Names are charming-cerf's:
> `HolePolarity.Sink` (attractor) / `.Source` (repulsor), the sign carried in `GM`, `Throat` for the
> other pole.

Everything below was built on `claude/peaceful-rubin-hhw49n` in one working session, in this order,
each step pushed with its offline proof. **None of it has been run in the Unity editor by the
author** (no `/verify-unity` in that session); the user playtested the lens, the tool and lava-lamp
spawning between steps, and every fault they reported is fixed and recorded in this file.

| # | Step | Where it is documented |
|---|---|---|
| 1 | Gravity: Paczyński–Wiita pull, ECS/Burst prism mover (three chained jobs), vessel pull, captures through `Prism.Consume` | §2, §3, §4 |
| 2 | Burst `MathF` errors in the substrate/swarm kernels fixed with `KernelMath` | §9.1 |
| 3 | The lens: a per-pixel Schwarzschild ray trace on a lens-sized sphere (replaced by the studio's one-pass lens, row 17) | §5.1 |
| 4 | Painted accretion disc built, then REMOVED at the user's request — the hole is its shadow and the bent scene | §5.1, §10 |
| 5 | Made physical: Lense–Thirring frame dragging (`spin`), GR tidal spaghettification (`tidalResponseSeconds`, `maxTidalStretch`) | §2, §5, §9.2 |
| 6 | The Black Hole tool (`B` / `blackhole tool on`): spawn rows, live holes, every config field (retired 2026-10-10) | §6 |
| 7 | Spawn placement: ahead of the camera ON SCREEN (in horizon radii) or a world position; Shift+B (retired 2026-10-10) | §6 |
| 8 | The lens sky is the scene's OWN skybox (`BlackHoleSky`), never URP's baked default (retired with the sphere, row 17) | §5.1 |
| 9 | Every camera gets the lens's depth texture; the on-screen camera, not `Camera.main`, places spawns | §5.1 |
| 10 | The lens bends TRANSPARENTS too (shards, particles): its own after-transparents pass | §5.1 |
| 11 | Merged with bleeding-edge (9,708 commits); `CSLogChannel.BlackHole` moved to bit 31 — the enum is now FULL | the merge commit |
| 12 | WHITE holes and black–white PAIRS: the radial law reversed, the horizon emitting, captured mass passed through to the other pole, the pair drifting apart and annihilating; tool buttons, N/M keys, `blackhole white|pair` | §11 |
| 13 | The STOAT (`VesselClassType.Stoat = 14`): a Squirrel-prefab clone whose triggers sling an attractor–repulsor pair across the hull, hold = size, attractor on the pressed side; the Sparrow's stop on X; generator + gates | §11, `R_VesselActions/STOAT.md` |
| 14 | (charming-cerf) The Black Hole cell, then the dipole — now the Crystal Wormhole: smooth wells, the graded lens, the felt pull, the seamless mouths, formation and annihilation | §12, `Docs/CRYSTAL_WORMHOLE.md` |
| 15 | (charming-cerf) The warp field | `Docs/WARP_FIELD.md` |
| 16 | The merge: both pair styles on one engine, a pair-style switch on the Stoat's sling and in the Black Hole tool | §13 |
| 17 | (2026-10-10) The lens IS the Vessel Studio's: every hole in one full-screen pass, ported line for line and proven against the page's own GLSL and JavaScript. The per-hole sphere, its sky (`BlackHoleSky`) and the ray trace are gone | §5.1 |

**Untested in the editor, in priority order:** the pair-style switch and a crystal-style sling (§13);
white holes and pairs (§11 — the repulsion on a vessel, the pass-through of captured prisms, the core
drawn over the now-diverging source lens, the drift and annihilation); the Crystal Wormhole cell end to
end (§12); the one-pass lens (`BlackHoleLensPass` + `BlackHoleLens.shader`, Render Graph) on every camera and the
Scene view, in lava lamp with a Stoat pair; Shift+B in lava-lamp freestyle with a
vessel flying (retired, §6); the edit-mode suites `BlackHoleTests`, `BlackHolePhysicsTests`,
`CrystalWormholeTests`, `WarpFieldTests` (written, never run).

**What builds on it next:** the Stoat vessel, whose ability spawns these holes (row 13; prototype on
the same branch, `R_VesselActions/STOAT.md`). Anything the Stoat needs from the black hole (per-hole spin and size, pulling more kinds of
mass, ownership so a hole does not pull the vessel that made it) is added HERE, to the black hole,
and recorded in this file — the vessel only spawns and despawns holes.

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

## 5.1 The lens — what the player SEES (`BlackHoleLens` + `BlackHoleLensPass` + `BlackHoleLens.shader` / `.hlsl`)

**The lens IS the Vessel Studio's (2026-10-10).** `Docs/Studios/StoatFlightStudio.html` draws every hole in ONE
screen-space pass (`lensMat`, its uniforms from `setLensUniforms`), and Unity now draws it the same way, ported line
for line. A black hole is invisible; what is visible is everything behind it bent toward it, and its shadow:

- **The shadow** is pure black at `shadowSize` × the horizon's angular radius (2.6: the photon-capture radius
  `b_c = (3√3/2) r_s ≈ 2.598 r_s`, so the black disc is the capture cross-section, not the horizon).
- **The bend.** Each pixel samples the scene displaced toward the hole by `θ_E² / max(θ, shadow)` — `θ` its angle from
  the hole on screen, `θ_E = f·tan √(2 r_s / D)` the Einstein angle (× `lensStrength`) — so something straight behind the
  hole becomes an Einstein ring. The bend fades out from `lensFadeStart` × the reach to the reach (`lensRadiusMultiplier`
  horizons), so it never ends at an edge.
- **The photon ring**: a thin warm glow `(1, 0.8, 0.55) · photonRingGlow · exp(−((θ − 1.03·shadow) / (photonRingWidth ·
  shadow))²)` hugging the shadow. It is not an accretion disc (§10): nothing orbits in it.
- **A white hole** bends the same way (`whiteLensStrength`), and inside its core (`whiteCoreSize` horizons) the light
  coming out of it: the bent scene × `whiteCoreSkyMix` plus a white-hot glow `whiteCoreBrightness · (1 − t)²`.
- **A smooth well** (the crystal pair, §12) is the graded lens `A · r_c · u · e^(−u²/2)`, `u = θ / r_c`, toward its centre
  for an attractor and away for a repulsor; no horizon, shadow or ring.
- An owned hole's domain tint (§14, `DomainTintAmount`) still colours its shadow and core; the Stoat ships it at 0.

**How it draws.** `BlackHoleLensPass` is injected from script into every base game camera and the Scene view while a
lens is live (no renderer feature, no asset edit). After the TRANSPARENTS it copies the camera colour
(`_BlackHoleSceneColor`: opaques, skybox and transparents — the snow shards are bent too) and draws ONE full-screen
triangle with `BlackHoleLens.shader`. The holes' screen numbers are worked out for that camera
(`BlackHoleLens.ScreenWells`, the nearest 8 in front of it: centre, depth, angular horizon, Einstein term, reach, margin)
and go with the draw in a `MaterialPropertyBlock`, so two cameras in one frame never read each other's. Each pixel sums
every hole's displacement, samples the copy ONCE, then lays the white core, the ring and the shadow over it; a pixel no
hole changes is discarded and keeps exactly what the camera drew. A camera that sees no hole skips the copy too.
**What is not bent:** for each hole, a pixel whose opaque depth is in front of the hole by more than 2.6 r_s (the
studio's `sceneZ < wC.z − wM.w`), so a vessel between you and the hole is drawn unbent over it. The depth texture is OFF
in `URP_Asset`; `BlackHoleLens.CameraSupport` turns it on for every enabled game camera while a hole is live and
restores each camera's own setting after. A ray bent off the screen shows the screen MIRRORED at its edge, as in the
studio.

**Why it changed (2026-10-10, the user's playtest of a Stoat pair in lava lamp).** Unity drew one lens SPHERE per hole,
30 horizons wide, each ray-tracing Schwarzschild spacetime from a copy of the scene taken before any lens. Two faults
came from that design, not from a tuning:
1. **Black occluding white.** A pair's spheres overlap (poles 60–200 u apart inside ~105 u lenses), and the sphere drawn
   last painted over its partner. The `_BHHoleBank` patch (each sphere drawing the OTHER holes' shadow and core) hid it
   only partly: the partner's own bending was still erased wherever the spheres overlapped.
2. **A large disc round the hole.** Every bent ray that landed on something in front of the hole was swapped for the
   skybox (`BlackHoleSky`, rendered in six faces), and a ray bent off the screen took the sky too. In lava lamp —
   prisms and the vessel in front, a cell membrane instead of an open sky — the swapped pixels drew the sphere's
   outline as a disc.
A sum has no order, and nothing is swapped in, so neither can happen. Retired with it: the lens sphere mesh,
`BlackHoleSky.cs`, the ray trace (`BlackHoleLensTrace`, `lensSteps`), `lensSkyResolution`, `lensSkyFacesPerFrame`,
`PublishHorizonHoles` / `PublishSmoothWells` and their global banks. Added to `BlackHoleConfig` (the studio's rows):
`shadowSize` (bhShadow 2.6), `lensStrength` (bhLensStrength 1), `whiteLensStrength` (whLensStrength 1), `whiteCoreSize`
(whCoreSize 2.6); `lensRadiusMultiplier` is the studio's bhLensReach (30) and `lensFadeStart` its bhLensFade (0.55). All
of them are rows in the artifact-to-game map (`Tools/Build/studio_to_unity/stoat.json`): the artifact owns their numbers and
`/artifact-to-unity` writes them (`studio_to_unity.py --check` reports drift).

**There is no accretion disc, by decision (2026-10-07).** What orbits and spirals into the hole is the REAL mass — the
prisms the gravity field moves (§3) and the tides spaghettify (§5). A synthetic disc was built, read in the editor as a
disc slicing through the hole, and was removed; `BlackHoleTests.Lens_HasNoPaintedAccretionDisc` keeps it out.

**Stated limits** (the studio's own): a bend that lands on a foreground object shows that object (the copy cannot see
behind it); a TRANSPARENT in front of the hole has no depth, so it is bent with the background; the gravitational-wave
and light-shell effects of the studio's pass are not in the game (the game has no waves yet), nor its crystal mouths
(the crystal pair carries its own mouth meshes, `Docs/CRYSTAL_WORMHOLE.md`). At most 8 holes per camera, the nearest.

**Proof** (`python3 Tools/Shaders/verify_black_hole_lens.py`, `--require-real` to make B2 and C mandatory). It reads the
studio's OWN code out of the page and runs it beside the shipped code:
- **A.** The studio's fragment shader (GLSL) and the shipped `BlackHoleLens.hlsl` + the shader's fragment function, both
  translated to C++ mechanically: 108,000 pixels over 400 random sets of 1–4 black holes, white holes and smooth wells
  agree to 1.2e-7; reversing the holes changes no pixel; past its reach the lens leaves the scene untouched and the
  largest step across the reach is 0.0002 (no edge); a pixel in front of a hole is left alone. Negative control: the
  photon ring moved 1.03 → 1.10 fails parity.
- **B.** The shader compiles: glslang against a per-file URP mock (negative control: without `DeclareDepthTexture.hlsl`
  it fails), and DXC against the REAL URP + core ShaderLibrary for D3D11, Vulkan and Metal.
- **C.** `BlackHoleLens.ScreenWell`, cut out of the C# and compiled with dotnet, against the page's `setLensUniforms`
  JavaScript run in node: 600 holes, worst relative error 6.5e-7. Negative control: the Einstein cap moved 1.2 → 1.3 fails.

`python3 Tools/Shaders/render_black_hole_lens.py --out pair.png` renders the studio's lens and the shipped one side by
side (`--gap 3` overlaps the pair; `--only black|white`). `BlackHoleTests` holds the rest in the Editor: the shader
compiles (`ShaderUtil.ShaderHasError`), `MaxWells` matches the shader's bank, `ScreenWell` is the studio's numbers, no sky
is ever sampled.

**Incident (2026-10-07): the first lens drew a MAGENTA quad.** The fragment stage called `DecodeHDREnvironment` without
the include that declares it; a one-blob mock passed and Unity substituted its error shader. The verifier's mock is per
file since, and because a shader that fails to compile still reports `Shader.isSupported`, `BlackHoleLens.IsDrawable`
also asks `ShaderUtil.ShaderHasError` in the Editor: a broken lens falls back to a plain black sphere with the compiler's
first error in a warning, never magenta. **Incident (2026-10-08): the snow shards were not bent** — the lens read URP's
opaque copy, taken before the transparents; `BlackHoleLensPass` copies after them. **Incident (2026-10-08): the vessel
camera's lens drew black** — only `Camera.main` (the menu's) had its depth texture on; `CameraSupport` patches every
game camera, and spawning measures from `BlackHoleLens.ViewCamera()`, the camera on screen.

Dials (`BlackHoleConfig`, Lens header): `lensEnabled`, `shadowSize`, `lensStrength`, `lensRadiusMultiplier`,
`lensFadeStart`, `whiteLensStrength`, `whiteCoreSize`, `whiteCoreBrightness`, `whiteCoreSkyMix`, `photonRingGlow`,
`photonRingWidth`. `lensEnabled` off falls back to the plain black sphere.

## 6. Tools, keys, console and the test scene — retired (2026-10-10)

The DiagnosticsHUD console verbs (`blackhole …`, `bh`, `wormhole …`), the **Black Hole tool** panel
(`BlackHoleTool` + `BlackHoleToolModel`, its offline proof `Tools/Build/black_hole_tool_harness` and
`BlackHoleToolTests`), the keys (**B**, **Shift+B**, **N**, **M** — `BlackHoleHotkeys`), the
`BlackHoleTest` scene with `BlackHoleTestHarness`, `BlackHoleTestConfigSO` / `BlackHoleTestConfig.asset`
and **Setup Black Hole Test Scene**, and the config's Spawn section (`spawnStrength`,
`spawnHorizonRadius`, `spawnAheadOfCamera`, `spawnDistanceHorizons`, `spawnPosition`, `spawnVelocity`,
`spawnSpinAxis`, `pairAheadHorizons`) were deleted at the owner's request, with the registry helpers
only they called (`SpawnFromConfig`, `SpawnPairFromConfig`, `SpawnStyledPairFromConfig`, `SpawnPoint`).

**Why.** They were bring-up instruments for a hole nobody flew. Two of them auto-spawned into EVERY
scene (`[RuntimeInitializeOnLoadMethod]`), so a stray N or M in the lava lamp laid a pair at the
camera, and the hotkeys and the debug HUD's "cmd: blackhole tool on" made a lava-lamp hole look like
something the menu did. The settings they edited are now the Stoat's, decided in the Vessel Studio
(`Docs/Studios/StoatFlightStudio.html`, the field-dipole rows) and ported to
`StoatDipoleConfig.asset`; that studio page is where a pair is tried, watched and tuned.

**What stays.** Everything a laid hole needs: `BlackHole`, `BlackHoleRegistry` (`Spawn`, `SpawnPair`,
`SpawnCrystalPair`, `LetGo`, `Annihilate`, the driver), the gravity field, the vessel pull, the warp,
the lens and its pass, the sky, `BlackHoleConfigSO` and its asset, and `BlackHoleTests` /
`BlackHolePhysicsTests`. Sections below that name the tool, its buttons or `blackhole …` verbs are the
record of how a behaviour was first tried, not a way to reach it today.

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
  specs, config sanity, residency budget; the test-scene wiring test was deleted with the scene, §6) and `BlackHolePhysicsTests` (eleven
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
- **One lens sphere per hole, ray-tracing Schwarzschild spacetime** (2026-10-07 to 2026-10-10). Exact bending, but each
  sphere drew from a copy of the scene taken before any lens, so overlapping holes erased each other, and its sky swap
  drew a disc round the hole in lava lamp (§5.1). The studio's one-pass sum replaced it. Do not go back to per-hole
  geometry for the lens: any per-hole draw has an order, and the pair overlaps by design.

- **Summing the tides of two holes in the shader.** The tensors do add, but two stretch axes do not make
  one stretch about a single axis, and the per-prism map would stop being a pure stretch. The larger
  tide wins, as the larger pull wins in the integrator.

## 11. White holes and black–white pairs (2026-10-08)

**A white hole is the same spacetime run the other way.** Outside the horizon the Schwarzschild
geometry is identical; what differs is the horizon: nothing can enter it and everything inside it
comes out. In this project that is one sign — `HolePolarity.Source` on `BlackHole`, a NEGATIVE `GM`
(and frame drag) in the physics `Well`. (This branch first carried it as a separate `Well.Polarity`;
the merge with charming-cerf, which had built the same thing as a signed GM, kept theirs. Their
source also flipped the tides and the lens — the antisymmetric twin rather than the time-reversed one;
the playtest preferred this branch's look, so a HORIZON source is drawn as below again, and only a
smooth well (§12) keeps charming-cerf's signed lens and tides.)

| | Sink (black, attractor) | Source (white, repulsor) |
|---|---|---|
| Radial law (§2) | `a = −GM/(r − r_s)²` toward it | the same magnitude AWAY from it |
| Frame dragging (§2) | ω about the spin axis | −ω (angular momentum flips under time reversal) |
| Capture | a body at r ≤ r_s is captured | never — a body inside the horizon is one it is emitting, and the pole guard's floor gives it the kick out |
| Tides (§5) | `GM·τ²/r³` stretch along the radial | the same (tides are even under time reversal; the bank publishes \|GM\|) — an emitted body starts a needle and relaxes as it leaves: the capture movie backwards |
| Lens (§5.1) | the same bending; inside the shadow radius, black | the same bending; inside the core radius, the white-hot core |
| Vessels (§4) | pulled; a vessel crossing a PAIRED horizon is carried to the white hole | pushed, through the same channel |

**The core** (the studio's, since 2026-10-10 — §5.1): inside `whiteCoreSize` horizons the bent scene × `whiteCoreSkyMix`
plus a glow white-hot at the centre and gone at the rim, `whiteCoreBrightness × (1 − θ/core)²`. (It used to sample the
skybox along the direction a backward ray trace crossed the horizon; that trace and its sky are retired.) A smooth well
(§12) has no horizon and so no core. With HDR on and no tonemapper, a brightness above 1 clips to pure white at the
centre.

**A pair** (`BlackHoleRegistry.SpawnPair`, `BlackHolePairMath`): a black hole and a white hole born
together, either side of a midpoint along one axis, the same size. A HELD pair (the Stoat's, while its
pilot orbits the black hole) stands still for up to its lifetime; once let go (`LetGo` — at birth for
a tool pair) the two FALL TOGETHER, accelerating from rest to `pairDriftSpeed` over
`pairCloseRampSeconds` (`s(t) = s0 − v·t²/2τ`, then linear), and ANNIHILATE where their horizons touch:
both ease out through their warp weight. (The first version drifted them apart and back on a
parabola; the playtest asked for a pair that only closes, 2026-10-09.) Paired holes have no velocity
of their own (the registry moves them), and if one is despawned alone the other goes too. From a
camera (the tool's Pair buttons, N/M, `blackhole pair`): the midpoint `pairAheadHorizons` ahead, the
holes on the camera's own horizontal to its left and right. A pair takes two of the four hole slots.

**Pass-through.** A prism captured by a black hole WITH a `Throat` (its pair's other pole) is not
consumed: it comes out of the white hole at the point reflection of its entry (relative to each
centre), with the velocity it went in with — which points outward there — at least 1.05 r_s from the
white centre, and stays a body under the field, now repelled. A SMOOTH well's core (§12) carries by a
pure translation instead (charming-cerf's rule, kept for their style); both go through the prism's
own notify. `BlackHoleGravityField.ThroatTransitsTotal` counts both. A lone black hole still
consumes. The prism's own notify (index + queued render matrix) moves it, the mover's contract.

**Vessels go through too** (`BlackHoleVesselPull.TryCarryThrough`, the playtest's ask): a vessel —
player or AI, by its own flying — whose centre crosses a paired black hole's horizon is put at the
same point-reflected exit, heading kept (outward there), through `VesselTransformer.SetPose`, so its
trail and camera are carried across the jump and a gate watcher counts a teleport
(`VesselTransitsTotal`). A let-go pair's white hole is moving (falling toward its partner at the
closing speed), so the vessel also leaves in the mouth's frame: it takes that velocity through
`ModifyVelocity` until the pair meets (`BlackHoleRegistry.TryGetMouthMotion`,
`BlackHolePairMath.SecondsLeft`). Without it, a hull that came out between the holes slower than they
close was run straight back down by its own white hole — the studio's portal probe showed exactly that.
Its drawn hull spaghettifies on the way in and relaxes the same way on the way
out (`BlackHoleWarp.VesselLogStretch`, the prisms' tide × `vesselTideScale`; drawn on the Stoat by
`StoatAnimation`, other hulls not yet).

**Proof.** `BlackHolePhysicsTests`: a white hole repels with the black hole's magnitude and turns its
frame the other way (the source's sign in GM and frame drag), a white hole never captures and pushes a body at its horizon past its influence sphere, a let-go
pair only closes and meets where its horizons touch (`SecondsToMeet`, and `SecondsLeft` after it), the exit is the point reflection with the entry
velocity outward. `render_black_hole_lens.py --only white` renders the core offline, the studio's beside the shipped one.

**The Stoat** (`R_VesselActions/STOAT.md` §1) is the vessel built on this. PRESS lays a HELD pair
beside the hull — the attractor perpendicular to the nose on the trigger's side at the orbit radius the
squeeze asks for, the repulsor mirrored — with the strength that makes that radius a circular
Paczyński–Wiita orbit at the hull's speed and a horizon a sixth of it. HOLD and the hull orbits the
black hole (the squeeze tightens the circle live, and the holes follow); RELEASE and it slingshots out
along the tangent while the pair falls together and annihilates. An owned drift pair pulls nobody
through `BlackHoleVesselPull` (the owner's pull is the orbit; opponents are §9's). In the crystal
style (§13) it still lays `SpawnCrystalPair` on release.

**Not yet:** a flash at annihilation and a thump at birth (the Stoat's feel pass, FMOD slots shipped
empty on `StoatSlingConfig`); the pair's AI/network replication (pairs exist on the machine that
spawned them, like holes — a remote Stoat's press/release edges do replicate, so each peer lays its
own copy at the replicated pose, but the copies are not one object).


## 12. The Crystal Wormhole (charming-cerf) — the Black Hole cell and the dipole, superseded

The Cell Selector world built here first as a "Black Hole cell", then as a black/white-hole dipole
joined by a wormhole, is now the **Crystal Wormhole** (`Docs/CRYSTAL_WORMHOLE.md`): an attractor and a
repulsor that are SMOOTH wells, not black holes — the HyperSea is far too big for black holes to make
sense. What it added to this engine stays and is documented there: polarity (`HolePolarity`, a signed
GM), smooth wells (`softening`: Plummer gravity, no frame dragging, softened tides, the graded lens),
`Amplitude`, the throat that carries captured mass through, and the felt pull on vessels. A lone black
hole (the console's, Shift+B) is unchanged.

## 13. Two pair styles, one switch (2026-10-08)

Both branches' pairs now run on one engine, and **`BlackHoleConfig.crystalPairs`** picks which one
every pair spawn lays: the Stoat's sling, the tool's Pair buttons, N/M and `blackhole pair`.

| | `crystalPairs` off: the DRIFT pair (§11, this branch) | `crystalPairs` on: the CRYSTAL wormhole (§12, charming-cerf) |
|---|---|---|
| Wells | horizon holes: Paczyński–Wiita, frame dragging, capture | smooth (Plummer) wells, no horizon, no frame dragging |
| Look | the attractor's shadow + Einstein ring; the repulsor bends light the same way and its horizon is a white-hot core | the graded lens (one throat wide), seamless mouths — no edge anywhere |
| The Stoat's sling | PRESS lays it, HOLD orbits the attractor (radius from the squeeze, the strength that makes it circular), RELEASE slingshots | laid on RELEASE, sized by the squeeze |
| Vessel pull | none from an owned pair (the owner flies the orbit); a tool pair's physical pull into the 90 u/s channel | the felt law in the hull's cruise (k 4, ceiling 1.3 × cruise, reach 12 throats) |
| Prisms through the pair | emitted at the point reflection just outside the repulsor | carried by pure translation into the repulsor's core |
| The pilot | any vessel crossing the black horizon comes out of the white hole (point-reflected), spaghettified in and out | carried through either mouth, by pure translation |
| Life | held while orbiting (≤ 12 s), then the two fall together at 40 u/s and annihilate where the horizons touch | form 0.6 s, stand 0.05 s, spiral and annihilate 3.35 s (`crystal*Seconds`) |

**Ownership is shared:** both styles' wells carry `OwnerVessel`, so nothing an owned pair does moves
an opposing vessel (`Docs/ELEMENTAL_ECONOMY.md` §9); a slung crystal pair's mouths carry only the
slinger's own player. A tool-spawned pair is environmental (a crystal one carries every pilot in the
scene when it opens). One pair per Stoat either way: a new press ends the last one (a crystal pair
annihilates over `crystalReplaceSeconds`, 0.4 s). The crystal throat is the size dial a drift pair's
horizon is (`horizonPerStrength × strength`), so a full squeeze opens a 24 u throat, close to the
crystal cell's ~26 u.

**Where the switch is:** the Black Hole tool's **Pair style** button (spawn section), the generated
row in its Config view, `blackhole style drift|crystal`, or the asset. It edits the config live, like
every field the tool edits, so the Stoat's next sling follows it. Ships OFF (the drift pair) until a
crystal sling has been flown in the editor.

**Built for it:** `BlackHoleRegistry.SpawnCrystalPair` (opens `CrystalWormhole` on a host object
that `CrystalPairHost` destroys once the pair is gone — the crystal cell's host is its environment,
so `CrystalWormhole` never tidies its host), `CrystalSettings` (the config's Crystal Pair section),
`SpawnStyledPairFromConfig` (the camera pairs), and a `Reference` field kind in the tool model so the
config can hold the mouth material (shown by name, edited on the asset). Tests: `BlackHoleToolTests`
`PairStyle_*` (ships as drift with the seamless mouth wired; equal sling lives; throat = horizon) and
`ToolModel_AnAssetReferenceIsShownNotUnsupported`; `black_hole_tool_harness` covers the 56 fields.

**Fly both before choosing:** the web studio (`Docs/Studios/StoatFlightStudio.html`, live in the one Vessel Studio with
the shared decision log, https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) flies the Stoat on a
gamepad through the game's own stick mix with either style, and compares them split-screen.

## 13a. The studio's look (2026-10-10)

The Vessel Studio's lens pass (`Docs/Studios/StoatFlightStudio.html`, `lensMat`) is the reference for how a
pair looks: a pure black shadow at 2.6 r_s, the Einstein bending, a thin warm **photon ring** at the shadow's
edge (`(1, 0.8, 0.55) · 0.55 · exp(−((b − 1.03 b_c) / (0.06 b_c))²)`), and on a white hole the same bending with
the white-hot core (×4, sky mix 0.8). The Unity lens matched all of it but the ring, which it now draws
(`BlackHolePhotonRing` in `BlackHoleLens.hlsl`, `_BHCore.zw` = `photonRingGlow`, `photonRingWidth`; black holes
only), and an owned hole's domain tint (§14), which tinted the shadow jade; the Stoat ships it at 0. The ring is
not an accretion disc (§10): it is a fixed thin glow at the photon sphere, nothing orbiting.

**Overlapping lenses (2026-10-10).** A pair's two lens spheres overlapped and the sphere drawn last erased its partner;
a first patch had each sphere draw the others' shadow and core (`_BHHoleBank`). Superseded the same day by the studio's
own one-pass lens (§5.1), which sums every hole and has no order at all.

## 14. Owner rules on a hole (2026-10-09, the Stoat's field dipole)

A hole can carry its OWNER's rules on top of the physics, set every frame by whoever spawned it
(today only `StoatDipoleExecutor`, `_Scripts/Controller/Vessel/R_VesselActions/STOAT_DIPOLE.md`):

| `BlackHole` member | Read by | Effect |
|---|---|---|
| `PrismCapture` = `Vanish` | `BlackHoleGravityField.ApplyVerdicts` | a captured prism is destroyed AT the horizon (`Prism.Vanish`: the ordinary destroyed event, scored to `CaptureOwnerName`, no debris, no SFX — it fell behind the shadow) |
| `PrismCapture` = `Steal` | the same | a paired sink carries the prism through AND recolours it (`Prism.Steal(CaptureOwnerName, CaptureDomain, superSteal)`) |
| `CrystalStripShare` | `BlackHoleCrystalStrip.Levy`, from `TeleportContinuity` | a rival vessel carried through the sink sheds that share of every element as crystals on the sink's side (the wormhole toll's shape: `AccrueElementalLoss`, conserving; the owner and teammates never pay) |
| `DomainTint` / `DomainTintAmount` | `BlackHoleLens` → `_BHTint` | a sink's shadow is drawn toward the colour, a white hole's core glows in it |

`Default` keeps everything above exactly as §3 and §11 describe. The dipole's pair is NOT a registry
`Pair` (its executor places both holes every frame and joins them as each other's `Throat`), so
`TickPairs` never closes it.
