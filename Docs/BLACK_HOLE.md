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
| The lens — what the hole LOOKS like (ray-traced background, shadow, lensed accretion disc) | `BlackHoleLens.cs`, `_Graphics/Materials/Graphs/BlackHoleLens.shader` + `BlackHoleLens.hlsl`, `Resources/BlackHoleLens.mat`, `Tools/Shaders/verify_black_hole_lens.py` (§5.1) |
| The ECS component every prism's companion entity carries | `_Scripts/Controller/ECS/Components/GravityBodyComponents.cs` (+ the prototype addition and the `SetGravityBody` / `ClearGravityBody` / `TryGetGravityBodyLookup` API in `PrismRenderService`) |
| Console commands | `BlackHoleConsole.cs` (`blackhole`, alias `bh`) |
| Tuning (the only tuning surface) | `BlackHoleConfigSO` → `Assets/Resources/BlackHoleConfig.asset` |
| The test scene | `Assets/_Scenes/Game_TestDesign/BlackHoleTest.unity`, `BlackHoleTestHarness`, the mouse camera `MouseOrbitCamera` (`_Scripts/Controller/Camera/`, + `MouseOrbitCameraConfigSO` → `Resources/MouseOrbitCameraConfig.asset`, §7.1), `BlackHoleTestConfigSO` → `Resources/BlackHoleTestConfig.asset`, FrogletTools ▸ Scene Setup ▸ **Setup Black Hole Test Scene** |
| Wirer / proof / gates | `Tools/Shaders/wire_prism_gravity_warp.py`, `Tools/Shaders/verify_prism_gravity_warp.py`, `PrismClockWiringValidator` (Specs + edges), `BlackHoleTests`, `BlackHolePhysicsTests` |
| See the lens offline (renders the SHIPPED HLSL to a PNG, any disc dial as a flag) | `Tools/Shaders/render_black_hole_lens.py` (§5.1); proof: `Tools/Shaders/verify_black_hole_lens.py` |
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
that keeps swimming, see §8). A body starts at rest; the frame dragging
gives it its swirl.

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

## 5.1 The lens — what the player SEES (`BlackHoleLens` + `BlackHoleLens.shader` / `.hlsl`)

A black hole is invisible; what is visible is everything behind it bent around it. The hole is drawn
by a per-pixel **Schwarzschild ray trace** of the scene behind it, not by a sphere and a disc mesh
(that was the first cut, and it read as a black ball with a yellow ring):

- **The background distorts.** Each pixel near the hole follows its light ray backwards from the
  eye through the hole's spacetime. A ray that escapes left along a BENT direction, and the pixel
  shows what the scene has in THAT direction — prisms and the skybox smear into arcs, and a point
  straight behind the hole becomes an Einstein ring.
- **The shadow is ~2.6× the horizon.** Rays closer than the critical impact parameter
  `b_c = (3√3/2) r_s ≈ 2.598 r_s` fall in, so the black disc on screen is the photon-capture
  cross-section, not the horizon.
- **The accretion disc is lensed.** A thin disc in the spin plane from the ISCO (3 r_s) outward. A ray
  crossing it picks up its glow on EVERY crossing, which is why its far side shows ABOVE and BELOW the
  shadow (its light bends over the top of the hole) and why a bright photon ring hugs the shadow. The
  gas follows the Shakura–Sunyaev temperature profile (`T⁴ ∝ r⁻³(1 − √(r_in/r))` — a dark gap at the
  ISCO, a hot inner ring), shifted by the relativistic Doppler factor of the orbiting gas
  (`v = √(r_s / 2(r − r_s))`, half light speed at the ISCO) and by gravitational redshift: the side
  turning toward the camera is bluer and ~9× brighter at 6 r_s, the side turning away redder — the
  lopsided glow of every real black-hole image. Differentially rotating spiral streaks (Keplerian,
  inner gas laps outer) make it visibly churn.
- **The disc forms from what the hole eats.** Its density is `diskBaseDensity + DiskFeed`, and every
  prism the hole consumes adds `diskFeedPerCapture` to `DiskFeed`, which halves every
  `diskFeedHalfLife` seconds. A hole sat in a prism field builds its disc in real time as the mass
  spirals in; a starving one fades back to a faint ring. (Emergent from the capture verdict the field
  already produces — no new state anywhere but one float on the hole.)
- **The disc is translucent where it is cool, and its colour survives to the screen.** Opacity is
  Beer–Lambert through the gas column, `α = 1 − e^−τ`, with `τ ∝ density × clump × √flux`: the hot
  inner disc is optically thick, the cool outer disc thin, and no feed level turns it into a solid
  plate (the clump noise is contrast-stretched, so a fed disc shows gaps and dense streaks). The
  disc's light goes through `BlackHoleDiskTonemap`, which rolls the brightest channel off toward 1
  above a knee (0.6) and scales the other two with it — the project has NO tonemapper
  (`DefaultVolumeProfile`: Tonemapping None), so without it the final blit clips HDR per channel,
  which drags every colour toward yellow and then white.
- **Its colour is its temperature.** `diskPeakTemperature` (default **10,000 K**: white-hot, the
  approaching side blue-white) sets the hottest ring; the Shakura–Sunyaev profile cools the outer
  edge to ~0.56× that. Real accretion discs are far hotter than 10,000 K, and every blackbody that
  hot looks white to blue-white, so that is the realistic range; 6,500 K gives the warm white /
  orange of *Interstellar* (an artistic choice there, too).

**Incident (2026-10-07): the "yellowish light disc".** The first in-editor look showed a large,
flat peach-to-tan oval around the hole. It was the disc, rendered wrong three ways at once: its
opacity was a clamp, `saturate(density × noise)`, so a disc fed to the cap (2,518 captures pin
`DiskFeed` at `diskFeedMax`) sat at α = 1 everywhere — measured 1.000 all round its hottest ring and
0.62 even at 12 r_s — which erased its streaks and let its dim ~3,700 K orange outer ring cover the
scene; the HDR emission (brightness 4) was clipped per channel, so 3–8 r_s became a flat white plate
and the dim orange read as beige/tan (`#efcbae` → `#ab8564`, exactly the screenshot); and the peak
temperature was 6,500 K, warm by choice. All three are fixed above, and `Tools/Shaders/render_black_hole_lens.py`
renders the shipped HLSL offline — it reproduced the screenshot from the pre-fix file before the fix
was written, which is how the diagnosis was confirmed.

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
stand-in background (procedural sky, a dark-blue prism field). Every disc dial is a flag
(`--temp`, `--density`, `--brightness`, `--doppler`, …), and `--hlsl` renders another version of the
file — the way to compare a change before and after.

**Proof.** `Tools/Shaders/verify_black_hole_lens.py` compiles the SHIPPED HLSL with clang++ and runs
it: the shadow edge at 2.594 r_s (exact 2.598), Einstein deflection at b = 20/40/80 r_s within 0.8%
of Schwarzschild's second-order value, rays passing above the shadow parallel to the disc (which never
cross its plane in flat space) picking up the far side of the disc, Doppler asymmetry 9×, the ISCO gap,
no light from inside the horizon, 4,000 random rays finite and in range, a seamless fade; a fed disc
that keeps its streaks (α 0.33–0.91 round the hottest ring, never a plate), an outer disc less than
half as opaque as the hot ring, a roll-off that keeps hue and stays ≤ 1 (3,700 K × 4 shows G/R 0.57,
orange, where the per-channel clip gives 1.00, yellow) — and negative controls: a coarse step fails
six of them, a roll-off that never engages fails the hue property, and the pre-fix disc fails all
three disc properties. Both shader stages are then compiled twice:
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
Dials (`BlackHoleConfig`, Lens / Accretion disc headers): `lensRadiusMultiplier`, `lensFadeStart`,
`lensSteps`, `diskInner/OuterMultiplier`, `diskBaseDensity`, `diskFeedPerCapture`, `diskFeedMax`,
`diskFeedHalfLife`, `diskBrightness`, `diskPeakTemperature`, `diskDoppler`, `diskSpinSpeed`,
`diskNoiseScale`; `lensEnabled` off falls back to the plain black sphere.

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
  they ARE warped (the map is correct on any mesh), so a burst near a hole visibly leans into it.
- **No replication.** Each peer that spawns a hole runs it locally; prism bodies move on the machine
  that simulates them. Spawning is a console/test action today, not a networked game event.
- **One worker per root** for the transform write-back only (above); the read and the integration
  are parallel. **Cell volume accounting** is not re-filed as prisms
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
