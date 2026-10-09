# CRYSTAL WORMHOLE — two necks of one warp field, glued at their throats

> Space crystals fold the HyperSea into a **wormhole**: an **attractor** and a **repulsor** whose throats are
> one place. Fly into one and you come out of the other. Nothing is a surface. The pair is a WARP FIELD (every
> length a pilot observes shrinks toward each pole), and in the pilot's own lengths each pole is a NECK. The
> throat spheres at the two necks are glued, and **light and vessels both follow that one geometry**. From
> afar the attractor is a crystal ball holding the far side's whole sky, with this side's sky wrapped round it
> as an Einstein ring. Up close it opens into a tunnel. The pair drifts together over a minute and
> annihilates when the poles meet. Status: **rebuilt 2026-10-09 and simulated offline end to end; not yet
> run in the editor.**

## 0. Where everything is

| What | Where |
|---|---|
| The pair: poles, life, throats, transits | `_Scripts/Controller/Environment/CrystalWormhole/CrystalWormhole.cs`: `Open`, the pure `Life`, `Glue`, `TurnThrough`, `Through`, `CameraThrough`, `TryResolveTransit`, `Annihilate` |
| What is seen: the lens sphere, the far eye, the panoramas | `CrystalWormholeView.cs` (same folder) |
| Where light goes | `_Graphics/Materials/Graphs/CrystalWormholeLens.hlsl` (the field's optics, both throats glued) and `CrystalWormholeLens.shader` (what a traced ray is coloured from); material `Resources/CrystalWormholeLens.mat` |
| The field | `ThroatWarp.cs` (`_Scripts/Controller/Environment/WarpField/`), the cell's `Crystal Wormhole Warp Field.asset`, composed over the two poles by `WarpFieldRuntime` (`Docs/WARP_FIELD.md`) |
| Vessels follow the field's geodesics | `VesselTransformer.ApplyWarpGeodesicTurn` (uses `WarpFieldRuntime.LogGradientAt`) |
| The camera follows the ship through | `CustomCameraController.CarryThrough` (a rigid map with a turn; the camera crosses at its own point), called by `TeleportContinuity` |
| The cell's environment (a stand-in for the crystals that will open one in play) | `SpawnableCrystalWormhole.cs` → `_Prefabs/Spawnables/SpawnableCrystalWormhole.prefab`; `BuildSettings()` holds every number a crystal mechanic would pass |
| The cell | `_SO_Assets/Cell Configs/Crystal Wormhole Cell/`, listed in Menu_Main's `Cell.CellConfigs[13]` (the Cell Selector's *Crystal Wormhole*) |
| Gravity on prisms, the felt pull on vessels | the gravity-well engine (`Docs/BLACK_HOLE.md`): smooth `BlackHole` wells with no lens of their own, `BlackHole.Amplitude`, `BlackHoleVesselPull.FeltAcceleration` |
| The offline flight simulator and the gate | `Tools/Shaders/simulate_crystal_wormhole.py`: images by default, `--check` is the gate |
| Edit-mode gates | `CrystalWormholeTests`, `WarpFieldTests` |
| Dev | console `blackhole annihilate [seconds]` brings the meeting forward (Editor / development builds) |

## 1. Why it was rebuilt (2026-10-09)

The first version (2026-10-08) drew each mouth as a sphere whose view of the far side was alpha-faded toward
its silhouette. It was drawn from OUTSIDE the sphere (front faces) and dropped once the camera was inside. A
separate "graded bulge" lens was summed over the wells, and the warp field was `RadialWarp` (s ∝ r). Playtest
verdict: *"it looked like an effect with a blurry edge … at least the black hole looked like an interesting
lens."* Flying it in the simulator showed the rest:

- **The ball interior was a crawl.** The old translation-model mouth put a pilot INSIDE the far ball. In the
  felt geometry that is down the far pole's tube and through its floor, roughly 1,900 felt units at the
  floor scale: tens of seconds of crawling, with the repulsor pushing you back.
- **The visual and the geometry were unrelated.** A faded disc says "portal". It does not say "warped space".

The rebuild makes the warp field the single source of truth. Light follows it, vessels follow it, and the
throats are where its two necks join.

## 2. The geometry

### 2.1 The field is a neck

A pilot at a point where the field reads `s` is `s` times their own size. Measured in their own lengths, a
world length `dx` is `dx / s`: a metric. Around a pole, the sphere at distance `r` then has felt radius
`F(r) = r / s(r)`.

- `RadialWarp` (s ∝ r) makes `F` constant: an endless tube. Light skimming a tube winds round it the whole
  way. The first simulation of that showed the attractor as a kaleidoscope of endlessly wound images, far
  too noisy.
- **`ThroatWarp`** shapes it into a NECK:

  `F(r) = r + λ·e^(−u − u²/2)`, with `u = (r − throat)/λ` and `λ = throat/throatScale − throat`.

  `F` is smallest at the throat and stationary there (`F′ = 0`): a catenoid-like neck, which is the optics
  of an Ellis wormhole. A smootherstep on `ln s` makes the field exactly 1 beyond `throat + 4.5 λ`. Inside
  the throat (only an eye just carried through is ever there) the scale holds at its floor.
- **Shipped:** throat 30, throatScale 0.2, so a pilot is one fifth of their size going through. The ring
  is `throat/scale` = 150 u across in impact parameter, and the field is flat by 570 u from a pole.
- `ThroatWarp.cs` and `CrystalWormholeLnS` carry the same lines. `--check` §2 and
  `ThroatWarp_NarrowestAtTheThroat_FlatPastItsReach_AndNoCrease` hold them together.

### 2.2 The throats are glued

A point on one throat comes out at the **antipodal** point of the other: `x′ = attractor + repulsor − x`,
the point reflection through the pair's midpoint. Everything that goes through is **turned 180° about the
throat normal** `n` (`CrystalWormhole.TurnThrough`, `CrystalWormholeTurnThrough`):

- The radial part is kept. Falling in along `−n` is climbing out along `−n`, which is the far sphere's
  outward normal at the antipode.
- The sideways part reverses. That is exactly how the antipodal map carries the sphere's own tangent
  vectors.

So the gluing is an **isometry of the two necks**. The joined geometry is smooth, a ray's angular momentum
about its pole carries straight through, and both sides' images compress into one continuous ring instead
of meeting at an edge. Because the product field is point-symmetric about the midpoint, the scale is the
same on both sides of a transit.

**The gluing that was tried first, and rejected.** "Position antipodal, heading kept" (no turn) leaves a
pilot's heading untouched, but the bundle of rays it sends through CONVERGES. Near mass on the far side
comes out depth-inverted, the camera has to jump with the ship, and the simulator measured a visible hitch
at every transit (frame change 31 against 3.4 for an ordinary frame). With the turn, the transit is smaller
than an ordinary frame.

### 2.3 Light follows the field

In the felt metric, light travels through a medium of index `n = 1/s`. A ray bends by
`d(dir)/dl = ∇⊥ln n = −∇⊥ln s`, toward the pole.

- `CrystalWormholeTrace` integrates that per pixel with RK4. Each step is 0.2 × the distance to the nearer
  pole, because the neck is scale-free. The trace steps exactly onto a throat when it meets one, glues
  there, and carries on until the ray leaves the lens sphere.
- **No speckle, by construction:**
  - A step that ended part-way across a throat used to throw its remainder away. That made a kink that
    moved from pixel to pixel.
  - The bend used to jump at the throat sphere, so the first stage of a ray glued onto the far sphere landed
    on either side of the jump, ~2° apart.
  - Both showed as sparkle, and both are fixed. The bend now ramps to zero across the inner 10% of the neck
    (`CRYSTAL_WORMHOLE_NECK_RAMP`). `--check` §4 asserts a smooth scanline, with the ramp collapsed as its
    negative control.

### 2.4 Vessels follow the field

`VesselTransformer.ApplyWarpGeodesicTurn` turns a vessel's whole frame (commanded rotation, hull and
momentum) at `|∇⊥ ln s|` radians per world unit flown. That is the bend light takes, so what you see dead
ahead is where you go, and flying hands-off past a throat looks like flying straight. It is a no-op without
a field.

A pilot aimed outside the ring swings round the neck and out again, exactly as the light they were looking
along does (simulated: `attractor_offaxis`).

## 3. What is seen (`CrystalWormholeView`, `CrystalWormholeLens.shader`)

- **One sphere, drawn from the inside.** One sphere surrounds the whole pair: half the separation plus the
  field's reach. Only its far faces are drawn (`Cull Front`), so exactly one layer covers every pixel whose
  ray crosses the warp, from any viewpoint, including from inside it, which is where a pilot always is when
  it matters. Past the sphere the field is exactly flat, so the sphere is never seen. It is drawn by
  `BlackHoleLensPass` after the transparents. It does not discard: the mip choice needs every lane's
  derivatives, so it blends by alpha.
- **What a traced ray shows:**
  - **This side** (no throat, or two): the frame copy at the bent direction, if that is on screen and lies
    beyond the bend. Otherwise the near pole's panorama, otherwise the sky.
  - **The far side** (one throat): the **far eye**. This camera sits at the gameplay camera's pose taken
    through the near throat (`CrystalWormhole.Through`: the rigid map at the throat point facing the
    camera), and an oblique near plane on the far throat removes everything on the wrong side. It renders
    colour and depth. Mass on the far side that lies in front of that side's own bend (your own ship while
    the camera follows it through) is looked up along the direction the ray LEFT the throat, by that depth.
    Outside the far eye's frame, the ray uses the panorama of the mouth it came out of.
  - **Mass in front of the bend** (your hull, a prism beside you) stays where it is: the pixel blends back
    to the frame by depth (`CrystalWormholeBendDistance`, never nearer than half the eye's distance to its
    nearest pole).
- **Anti-aliasing.** Every picture is sampled at a mip chosen from how fast the ray's direction changes
  across the pixel (`ddx`/`ddy` of the leaving direction). The far eye and the panoramas carry mips, so the
  wound rings at the crystal ball's edge read as bands of colour instead of sparkle.
- **Cost.** One full-screen trace inside the lens (RK4 at 2 field evaluations per pole per stage, typically
  20–40 steps; `lensSteps` 96 caps the ring), one far-eye render at 0.75 × screen (capped by the device
  tier's `FoldGateWindowMaxRenderScale`), and one panorama face per frame alternating between the poles
  (256² each). The far eye and the panoramas never draw a lens themselves (`BlackHoleLensPass.Exclude`).

### 3.1 The transit, frame by frame

1. **The owner's ship enters a throat.** The owner sees its step enter the attractor's ball
   (`CrystalWormhole.Transits`) and calls `vessel.SetPose`: the antipodal point, rotation turned through.
2. **Ribbons and camera.** `TeleportContinuity` resolves the jump (`TryResolveTransit`), cuts the ribbons at
   the two throat points, and starts the camera's carry (`CustomCameraController.CarryThrough`).
3. **The camera follows the ship through.** It keeps framing the ship taken back through the pair, by the
   rigid map at the ship's crossing point: position AND orientation, so the camera never tries to roll after
   it. The pilot sees their ship through the throat, drawn by the far eye.
4. **The camera crosses at its own point.** When the camera itself reaches the throat, it crosses there
   (`CameraThrough`). That is exactly where the far eye was rendering from, so the hand-over is a change of
   frame with nothing on screen to show it. The camera's smoothing velocity turns with it.

Measured by the simulator, as the frame change across each moment against an ordinary frame near it:
**ship transit 3.4, the camera's own crossing 2.2, ordinary frame 4.4** (`--check` §5).

## 4. The felt pull

Vessels feel `Sign · k · cruise² · R_t · s · r / (r² + R_t²)^1.5` (× amplitude) in their own frame. It is
measured in each hull's cruise speed, because the fleet's cruise speeds span 35 → 216 u/s. The ceiling is
`1.3 × cruise`. Shipped values: `k = 4`, reach 12 throats (360 u).

`FeltLaw_TheAttractorCarriesYouIn_TheRepulsorMustBeBoostedThrough` asserts, on the ThroatWarp numbers for
cruise 35/60/180:

- the attractor carries a hull in at ~2.3× its cruise;
- the repulsor holds off any hull at cruise;
- a hull boosting at 2.5× its cruise gets through.

Prisms the attractor takes into its core (half the throat) come out at the antipodal point of the
repulsor's core and are driven on out (`BlackHoleGravityField`). Nothing is destroyed.

## 5. Life: a minute from opening to meeting

One clock, `CrystalWormhole.Life(p, form, touch, turns, beats, depth)`, pure and tested. `p` runs 0 → 1
over `lifeSeconds` (60).

- **Separation** `√(1−p)`: slowly, then all at once. The poles are still 71% of the way apart at 30 s.
- **Orbit angle** `2π·turns·(1 − separation)`: two turns, quickening as they close.
- **Formation** eases each pole's amplitude in over `formSeconds` (4) where it opened.
- **Annihilation** `v = 1 − separation/touch`: 0 until they touch (separation 0.45, about the last 12 s),
  1 as they meet. The envelope is `form·(1−v)^1.5`. The attractor's amplitude is
  `env·(1 + depth·v·sin(2π·beats·v²))` and the repulsor's has the beat reversed: anti-phase and quickening,
  so the two necks' warping and unwarping convolute.
- **At `p = 1`** every amplitude is 0, the field is exactly 1 everywhere and space is flat.
- **Throats.** Each throat's radius is `throat·envelope`, never more than 0.4 × the separation, so the glued
  spheres never overlap. Below 0.5 u the throats are closed.
- **Looping.** `reformSeconds` (8) after meeting, the pair opens again at its starting points, so it can be
  watched again. When the Cell Selector retires the world, the pair annihilates inside the suction (0.9 s)
  and stays gone. `Annihilate(seconds)` brings the meeting forward from wherever the pair is.

## 6. What is open

- **Untested in the editor.** Cell Selector → *Crystal Wormhole*, then:
  1. Watch two crystal balls form, drift together while orbiting, touch, merge into one distortion that
     beats, and vanish at 60 s (and again 8 s later).
  2. Fly at the attractor: the ball grows into a tunnel, you shrink to a fifth, your ship never pops, and
     you come out of the repulsor and are pushed away.
  3. Fly at the repulsor at cruise (held off), then boost (through).
  4. Lay trail near the attractor and watch it come out of the repulsor.
  5. Check the frame rate inside the lens on a mid-tier GPU: the trace is full-screen there.
- **Simulated, not rendered by Unity.** `simulate_crystal_wormhole.py` runs the shipped HLSL and reproduces
  each source the shader samples. The far eye and panoramas are modelled as exact casts, the scene copy as
  the unbent render, and point sampling stands in for mips (the approach images are supersampled). The
  `CrystalWormholeLens.shader` compile is glslang against a declarations mock. DXC against the real URP
  library was not available here.
- **Stated limits.**
  - The lens has no geometry inside it. Mass inside the lens sphere is seen by bent rays only through the
    frame copy, the far eye or the panoramas (each a picture from one point), so its parallax under a strong
    bend is approximate.
  - A transparent in front of the bend is in the frame copy and is bent with the background.
  - The Scarab's cruise reads 25 (its speed is not throttle-driven), so it feels gentler poles.
  - The scale model is not to scale.
- **Forming it with crystals.** The fiction is that players open a wormhole with space crystals, and the cell
  is a stand-in. `CrystalWormhole.Open(host, attractorPos, repulsorPos, settings)` is the whole API a crystal
  mechanic needs: which crystals, how many, where the two ends land and who ends it are design decisions not
  taken yet.
