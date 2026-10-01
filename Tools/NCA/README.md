# NCA — learned cellular automata, starting from a faithful reproduction

Offline research tooling. Nothing here ships, nothing here touches the game yet.

The goal is new flora behaviour grown by a **learned local rule** instead of an authored
growth rule. Earlier attempts at that were crude and did not work, so this folder starts
from the one result that is known to work and reproduces it exactly before changing
anything: the lizard emoji of **Mordvintsev, Randazzo, Niklasson & Levin, "Growing Neural
Cellular Automata", Distill 2020** (<https://distill.pub/2020/growing-ca/>).

```
python3 Tools/NCA/growing_nca.py selftest                         # sparse update == paper's dense update
python3 Tools/NCA/growing_nca.py train --experiment regenerating  # also: growing, persistent
python3 Tools/NCA/growing_nca.py figures --run Tools/NCA/runs/lizard_regenerating
python3 Tools/NCA/verify_js.py --run Tools/NCA/runs/lizard_regenerating
python3 Tools/NCA/build_viewer.py                                 # -> Tools/NCA/viewer.html
```

`runs/` is scratch and ignored by git. A finished run is promoted into `results/<run>/`
(config, weights, loss, figures), which is what `build_viewer.py` reads and what is
committed.

```
```

Requires `torch` (CPU is fine), `numpy`, `pillow`; `node` for `verify_js.py`.

## What is reproduced, and how faithfully

Every hyper-parameter is the reference Colab's:

| | paper | here |
|---|---|---|
| grid | 40px target + 16 padding = 72×72 | same |
| state | 16 channels: RGBA + 12 hidden, premultiplied alpha | same |
| perception | depthwise [identity, Sobel x, Sobel y], Sobel = outer([1,2,1],[-1,0,1])/8, zero padded | same |
| update | 1×1 conv 48→128, ReLU, 1×1 conv 128→16, last layer zero-init | same (8,336 params) |
| stochastic update | each cell fires with p = 0.5 | same |
| alive mask | 3×3 max-pool of alpha > 0.1, before AND after | same |
| loss | MSE of RGBA vs target after 64–96 random steps | same |
| optimiser | Adam 2e-3 → 2e-4 at step 2000, per-variable gradient normalisation | same (eps 1e-7, the Keras default) |
| training | batch 8, 8000 steps | same |
| pool / damage | pool 1024, worst sample reseeded; 3 samples get a random disc (r 0.1–0.4) erased | same |

Two deliberate differences, neither of which changes the maths:

1. **Target source.** The Colab downloads `noto-emoji/png/128/emoji_u1f98e.png` from
   GitHub, which this build environment cannot reach. `load_emoji` renders the same
   artwork from the Noto Color Emoji font (the PNGs are generated from it), centres it on
   the same 128×128 canvas and thumbnails it to 40px exactly as the Colab does. Any RGBA
   image path also works as a target.
2. **Sparse update.** A cell that is dead before the update is zeroed by the alive mask
   whatever its update was, so the MLP runs only on cells that are alive AND fire. This
   is exact — `selftest` holds the value equal to the paper's dense formulation to 0 and
   the gradient to 4e-7, with a negative control that fires — and makes CPU training
   ~2.7× faster (0.67 s/iteration on 4 cores against 1.8 s dense).

## The three experiments

| experiment | what the paper shows |
|---|---|
| `growing` | grows the lizard by step ~64–96, then overgrows or decays: nothing in training asked it to stop |
| `persistent` | the pool makes the lizard an attractor; it holds for thousands of steps |
| `regenerating` | pool + damage; cut the lizard and it grows back |

`figures` renders, per run: the growth strip (steps 0 → 4000), a growth GIF, four cuts at
step 200 and their repair, the rotated-perception result (rotating only the Sobel kernels
rotates the grown shape — the paper's "rotating the perceptive field" section), and a
`summary.json` of target error at each checkpoint.

## The browser runner

`nca_core.js` is a ~100-line inference-only step in plain JavaScript over a channels-last
`Float32Array`. `verify_js.py` runs it under Node against the PyTorch model with the same
weights at fire rate 1 (deterministic) and asserts the whole 16-channel state agrees
(measured 1.4e-7 relative); a sign-flipped Sobel negative control must disagree.
`build_viewer.py` bakes every trained run into one self-contained page: live automaton
(drag to cut, double-click to seed, rotate the sensing, view any hidden channel),
training curves, a measured-results table and the figures.

The JS step is written as the reference for an eventual C#/HLSL port: it is one
per-cell function of the 3×3 neighbourhood, which is exactly a compute-shader kernel.

## Adding time: an animated target (`animated_nca.py`)

The same 8,336-parameter cell, trained on a LOOP instead of a still: by default an 8-frame
travelling body wave generated from the emoji itself (head still, tail widest, one
wavelength per loop), or any animated GIF via `--gif`. It has to become a limit cycle with
no clock — each cell fires at random and sees only its neighbours — so the cells must keep
time in their hidden channels and keep each other in step.

```
python3 Tools/NCA/animated_nca.py target --out swim.gif                    # preview the loop
python3 Tools/NCA/animated_nca.py train --init Tools/NCA/results/lizard_regenerating/model.pt
python3 Tools/NCA/animated_nca.py train --gif some_animation.gif --frames 12
```

Two-stage training (the docstring has the detail):

1. **Clock from birth.** Seeds only; the checkpoint at step t must show frame ⌊t/8⌋ mod 8.
2. **Pool + damage, phase-free.** Five checkpoints 8 steps apart must match consecutive
   frames from whichever start frame fits best, so each lizard keeps its own phase, keeps
   moving, and heals when cut.

### Result (8000 steps: 1500 clock + 6500 pool, warm-started from the regenerating lizard)

| measured over steps 200–3000 of one rollout from a seed | |
|---|---|
| tempo (target 8 steps / frame) | **8.75** steps / frame |
| frames visited | 8 of 8, in order, indefinitely |
| error vs best-matching frame | 10⁻³·²³ |
| error vs the frame its own fitted clock predicts | 10⁻³·¹⁹ |
| best any still image can do against the loop | 10⁻²·⁷² |
| tail quarter cut at step 400, error 600 steps later | 10⁻³·²⁷, tail regrown by ~50 steps |
| tempo after the cut | 8.78 steps / frame |

It runs about 9% slow, which is expected: with each cell firing half the time, the
consensus clock is a noisy average and nothing penalises a slightly long period across
the 32-step checkpoint window. The spots are softer than the static lizard's, which is
the price of 12 hidden channels now also carrying a clock.

**Stage 2 alone collapses, and that is the finding worth keeping.** A blurred average
lizard is equally close to every frame, so a phase-free loss gives the gradient no
consistent direction to start oscillating; from scratch it settled on one frame and never
moved (kept as `runs/lizard_swim_collapsed_v1` locally, measured with a 0.13 log10
best/worst frame margin). Pinning the phase to birth for the first stage breaks that
symmetry. The yardstick for "is it really moving" is the best any STILL image can do
against the loop — 10⁻²·⁷² for the swim — and training goes under it within 250 steps.

## One more spatial dimension (`nca3d.py`)

The same cell with one more axis: identity plus x, y **and z** Sobel gradients over the
3×3×3 neighbourhood (/32, so a unit ramp reads 1, as the 2D /8 does), 16 channels,
64 → 128 → 16 (10,384 parameters), stochastic update, 3×3×3 alive mask. The target is
the emoji INFLATED into a body (half-thickness ∝ √distance-to-edge, so the spine is thick
and the toes thin) on a 22×44×44 grid, swimming a **helical** travelling wave: displaced
sideways by sin(phase) and in depth by cos(phase), so the tail traces a circle — a motion a
flat lizard cannot make. Same two-stage training as the 2D swim, with spherical cuts.

```
python3 Tools/NCA/nca3d.py selftest
python3 Tools/NCA/nca3d.py train --init2d Tools/NCA/results/lizard_swim/model.pt --clock-steps 1000
python3 Tools/NCA/nca3d.py train --resume --out Tools/NCA/runs/lizard3d_swim   # after a restart
python3 Tools/NCA/verify_js3d.py --run Tools/NCA/results/lizard3d_swim
```

**Making it tractable on CPU** took three exact reductions, each held equal to the dense
formulation by `selftest`: perception evaluated only at alive-and-firing cells (a 27-voxel
gather times the fixed stencil as a 27×4 matrix); every step cropped to bbox(alpha > 0.1)
+ 2 (outside it the state is zero before and after, so computing there is waste); and a
separable alive test instead of `max_pool3d`. Plus per-step activation checkpointing to
keep memory at one state per step. 19 s → 4 s per training iteration.

**Warm start from the 2D swimmer.** `--init2d` copies a 2D model's identity/gx/gy weights
into the 3D layout with the z weights at zero — the 2D swimmer embedded as a point in the
bigger space. In a 100-step A/B it reached 10⁻³·⁰³ against 10⁻²·⁵⁹ from zero init, already
under the 10⁻²·⁸⁴ still-volume floor: the 2D clock transfers.

**Checkpoint/resume.** A container restart killed the first attempt at step 1425; training
now saves model, Adam moments and step every 50 steps, and `--resume` rebuilds the (unsaved,
~2.8 GB) pool by growing seeds with the checkpoint model for random lengths.

### Result (3500 steps: 1000 clock + 2500 pool, warm-started from the 2D swim)

| measured over steps 200–2000 of one rollout from a seed | |
|---|---|
| tempo (target 8 steps / frame) | **8.49** steps / frame |
| frames visited | 8 of 8, in order |
| error vs best-matching frame | 10⁻³·⁵¹ (10⁻³·⁶¹ at step 2000) |
| error vs the frame its own fitted clock predicts | 10⁻³·⁴⁹ |
| best any still volume can do against the loop | 10⁻²·⁸⁴ |
| best / worst frame gap | 1.16 (log₁₀; the 2D swim's was 0.95) |
| ball cut through the tail half at step 400, error 600 steps later | 10⁻³·³⁸, regrown by ~50 steps |
| tempo after the cut | 8.47 steps / frame |

The browser runner (`nca3d_core.js`) matches torch to 5.6e-7 over 60 steps, with a
z-derivative sign flip as the negative control (5.2e-1).

## From cellular to collision (`particle_nca.py`)

The grid NCA is a *cellular* automaton: every cell of a fixed lattice runs the rule. Boids
is a designed *collision* automaton: free agents that only ever respond to the neighbours
they are near. This step trains the NCA's cell as a collision automaton. A particle has a
continuous position and the same 16 channels; each step it perceives the particles inside
radius `R` and the learned rule returns a state change **and a velocity**.

```
python3 Tools/NCA/particle_nca.py selftest
python3 Tools/NCA/particle_nca.py train --experiment regenerating --world '{"capacity": 900, "corrected": true}'
python3 Tools/NCA/particle_nca.py figures --run Tools/NCA/runs/particle_regenerating
python3 Tools/NCA/verify_particle_js.py --run Tools/NCA/runs/particle_regenerating
```

What is learned vs designed:

| | learned (the MLP, 10k parameters) | designed physics (`World`) |
|---|---|---|
| state | `ds` from [own, neighbour mean, neighbour gradient, crowding] | alive iff a neighbour within `R` has alpha > 0.1, before and after |
| motion | a velocity, capped at `vmax` | collisions push apart pairs closer than `r0` |
| numbers | — | a visible particle short of `k_bud` neighbours **buds** a dormant (all-zero) child `r_bud` away from its neighbours' centroid — exactly an empty grid cell next to life |

The loss reads the particles through a differentiable Gaussian splat onto the same 72×72
target, so every experiment of the grid NCA is directly comparable. Kernels are the
standard SPH ones, `w = (1-r²/R²)³` for the mean and `(1-r²/R²)²` for the gradient.

### Why the first cut only ever grew a blob, and the fix

The first two runs are recorded because the way they failed is the finding. One exploded
(loss 0.011 → 1e24 in 50 steps, then extinct), and gained the texture-NCA *overflow loss*,
a rollback-to-last-healthy guard and numbered snapshots. The other trained stably and grew
a pale diagonal ellipse — the right colour and heading, no legs — for a thousand steps.
Each ablation below removes one suspect:

| run (log10 loss) | step 250 | 500 | 750 | what it rules in or out |
|---|---|---|---|---|
| free particles, `vmax` 0.6 | -1.84 | -1.91 | -1.91 | the blob |
| free particles, `vmax` 0.15 / 0 | -1.81 / -1.83 | -1.87 / -1.90 | — | **not motion** |
| direct fit of the splat to the lizard (no automaton) | | | | -3.5 fixed lattice, -4.6 free: **not the renderer** |
| fixed jittered lattice, slots persist (grid semantics, particle perception) | -1.79 | -1.93 | **-2.27, legs** | perception can learn it |
| the same lattice re-jittered per sample and rollout | -1.83 | -1.99 | -2.04 | a random arrangement slows it |
| **free particles, corrected perception** | **-1.96** | **-2.13** | | the blob is gone |

The cause was the gradient feature. `Σ g·dx·(sⱼ−sᵢ)/(1+Σg)` reads a field differently
depending on where the neighbours happen to sit: on a random arrangement it is wrong by up
to **91 %** (2D) / **94 %** (3D) *for a linear field*. On a fixed lattice that error is the
same every rollout and gets learned around; on free particles it is noise, and the answer
to noise is the average — a blob. The fix is the standard one from SPH, the moment-corrected
(least-squares) gradient `∇s = M⁻¹ Σ g·dx·(sⱼ−sᵢ)` with `M = Σ g·dx·dxᵀ`, plus a true
weighted mean, which is exact for linear fields on **any** arrangement (selftest: 4e-5 in
2D, 2e-4 in 3D, with the plain estimator as its negative control). It is `World.corrected`.

### Result: the lizard, as a collision automaton (4000 steps, corrected perception)

`results/particle_regenerating`: grow from one particle, persist, regenerate. Zero
rollbacks. Same metric as the grid run (mean squared error over the 72×72 RGBA target):

| | grid NCA (4000 steps) | particle NCA (4000 steps) |
|---|---|---|
| training loss | 10^-3.45 | 10^-2.68 |
| grown, step 96 | 10^-3.20 | 10^-2.74 |
| held, steps 200–3000 | 10^-4.38, pinned | shape 10^-2.63 to -2.86 (3 seeds), drifting ~1 px / 850 steps |
| quarter cut at step 400 | regrows | back to its pre-cut error (10^-2.80) by 200 steps later |
| particles | 5184 cells | ~430, stable from step 96 to 3000 |

It is the lizard: legs, curled tail, back stripe, and it heals. It is about 20× less
precise than the grid, which trained twice as long here and has no renderer in the way (a
free fit through this splat bottoms out at 10^-4.6). The one thing a grid cannot do and
this does is **move**: at step 1250 the whole body slid sideways at 0.006 px/step, a
treadmill of budding at one edge and dying at the other (with the learned velocity switched
off it drifts *faster*, so the rule was already fighting it). More training anchored it —
2 px over 3000 steps by step 2500 — without any designed restoring force.

The browser runner (`particle_core.js`) matches torch to 7.6e-6 over 20 steps on a grown
colony, with the collision push and the corrected perception each flipped as negative
controls, **and** on a lone seed beside one dormant child (1.8e-7). That second case is
there because the first shipped runner passed the grown-colony check while killing 11 of
40 colonies in their first 3–7 steps: it wrote each particle's own alpha into a
`Uint8Array` before taking the neighbourhood max, truncating 0.96 to 0. A grown colony
never notices (some neighbour is always visible); a seed next to its first bud is kept
alive by nothing but its own alpha. *A verifier that starts from a mature state cannot see
bugs that only matter at birth.*

A result nobody trained for: at step 500 some colonies bud a **second lizard** off the
first. The loss punishes it (the target is one lizard), but it is behaviour a particle
system has and a grid cannot.

### The swim, as a collision automaton (`--experiment swim2d --amp 16`)

The same particles, trained on the eight-frame body wave. At the grid's amplitude (6 px) it
could not learn to move, and the reason was measurable rather than mysterious: neighbouring
target frames differ by 10^-2.78 while the particle lizard's own precision is about 10^-2.7, so
there is no signal to follow. At amplitude 16 a still image can do no better than 10^-2.28,
which leaves the same half-decade of headroom the grid swim had. Warm-started from the particle
lizard, 1500 clock steps from birth then pool training, three fresh seeds per batch held to the
birth clock, learning rate dropped 10x at step 3000.

| checkpoint | tempo (target 8) | error vs best frame, in place | drift / 1000 steps |
|---|---|---|---|
| 1750 | 10.5 / 9.9 / 16.6 (3 seeds) | 10^-1.75 | ~6 px |
| 3250 (after the lr drop) | 8.2 / 8.2 / 8.2 | 10^-2.32 | ~1 px |
| 4000 | 8.9 / 8.9 / 8.9 | 10^-2.54 | 0-1 px |
| **4250 (shipped)** | **8.34-8.48 (4 seeds)** | **10^-2.57** | **~1 px** |
| 4500 (final) | 8.66-8.72 | 10^-2.32 | |

Step 4250 is the shipped result, picked by `particle_nca.score_snapshot` (4 seeds rolled out
together, 1000 steps; no colony died): on the long figures rollout it swims at 8.48 steps per
frame, holds its shape at 10^-2.66 (step 1000) and 10^-2.49 (step 3000) with the drift removed,
drifts 3 px in 3000 steps, and after a quarter cut (106 particles) is back near its pre-cut
error within 100 steps, still at 8.58 steps per frame. Before the learning-rate drop the rule
kept trading tempo against shape; after it, both held. Scoring the whole batch at once also
surfaced something the three hand-picked seeds had not: at step 3250, one of two colonies started
under `torch.manual_seed(0)` died in infancy (a batch of four under the same seed lost none) -
early extinction is rare, but it happens, so the scorer counts it and `gpu_run.py` rejects any
snapshot that shows it.

## Prisms: the collision automaton in the game's vocabulary (`prism_render.py`, `--experiment prism3d`)

3D particles whose visible state can only say what a Cosmic Shore prism can say. The 16-channel
layout is kept (alpha is still channel 3, so the alive and budding rules are unchanged) and 16
channels are added:

| channels | meaning |
|---|---|
| 16-18 | domain logits: Jade, Ruby, Gold |
| 19-22 | tier logits: plain box, danger box, shield octahedron, super-shield stella octangula |
| 23-25 | half-extents, `0.9 * exp(0.4 * tanh(c))`: 0.60-1.34 voxels per axis |
| 26-31 | rotation, 6D two-column representation (zero = identity) |

- **Colours are the live palette** (`OriginalColorSetSO`): each (domain, tier) is the base face
  lifted 35% toward its fresnel rim, linear -> gamma; danger is the shielded base under the
  danger rim, exactly as `Docs/PALETTE.md` §2.1 composes it. Twelve appearances.
- **Discrete by construction.** Domain and tier are straight-through one-hots: the loss renders
  the argmax, the gradient goes through the softmax. A trained rule can never show a colour or
  shape the game could not draw.
- **Shapes are the game's containment tests**, as gauges in half-extent units: box `max|u|`,
  octahedron `|u|_1 / 3`, stella the min over its two tetrahedra of the max of four face forms,
  both shields at the game's circumscribing scale 3. The stella gauge agrees with
  `StellatedOctahedronMeshGenerator.ContainsPointLocal` on 200,000 random points, and the Monte
  Carlo volumes come out 8 : 36 : 109 against the exact 8 : 36 : 108. Shielding a prism really
  does cost 4.5x its volume and super-shielding 13.5x, so the rule pays for a shield in the loss.
- **Rendering is solid-prism compositing.** Occupancy is a C1 smoothstep ramp one voxel wide
  across the surface (the exact box filter of a flat face; the first cut used a logistic edge,
  whose tail tripled a small prism's rendered volume), combined as a union
  `1 - prod(1 - k a)` with an occupancy-weighted colour, so overlapping prisms occlude rather
  than add. Each prism rasterises only the offset ball its own tier can reach.
- **The target** is the 3D helical swim with every voxel repainted in its nearest appearance
  (CIELAB) after a 36° hue rotation. Without the rotation every green lands on Gold. With it the
  lizard is Jade plain boxes (body), Gold shield octahedra and Gold super-shield stellae
  (spots, belly), and Jade stellae (pale features): colour and shape are coupled through the
  tier, so the target dictates where the big shapes go.
- **Amplitude 5**, from the same signal-to-noise argument as the 2D swim: at the 3D default
  (2.2 voxels) neighbouring frames differ by exactly the still-volume floor, 10^-2.7.
- **Scale.** The lizard is ~955 voxels, so prisms rest at 1.8 voxels (a couple of hundred
  particles); at 1.2 voxels the grid cannot tell a box from an octahedron anyway. The world is
  the swim3d world scaled 1.35x (R 3.5, r0 1.75), capacity 280.
- **Cost.** A rollout step backpropagated costs ~0.35 s at 8x400 particles, i.e. ~40 s per
  training step. The shipped run backpropagates only the last 48 steps (`--bptt 48`; the five
  loss checks span 32), batch 4, capacity 280.
- **Warm start** from the 2D particle swim: `lift_2d_to_3d` gives the gradient a zero z
  component, then `widen_channels` adds the 16 prism channels with zero output rows, so the
  widened model is bit-identical to the lifted one until the loss moves it (checked: one step,
  identical fire mask, zero difference in every position and channel).

`raytrace` renders figures as exact prisms (ray-convex-polytope intersection per piece, depth
buffered, base face lit Lambert plus the rim toward grazing angles). The browser bench draws
them as three.js instanced meshes from `prism_core.js`, which `verify_particle_js.py` holds
equal to `prism_render.prism_table` (decode error 2e-7, no domain or tier mismatches) on top of
the usual runner check.

## Running on a GPU (`gpu_run.py`)

The cloud session this was built in has four CPU cores and no GPU, which is why the prism run
backpropagates only the last 48 steps at batch 4. On a GPU it does not have to:

    git pull
    pip install -r Tools/NCA/requirements.txt      # torch: the CUDA build for your card first
    python Tools/NCA/gpu_run.py

That one command:

1. **checks** the device against the CPU on one step and one loss gradient from the same state
   (identical survivors, positions and states within 1e-3, gradient within 1%) and refuses to
   train on a device that disagrees;
2. **trains** the prism swim with full backprop, batch 8, capacity 360, 4500 steps
   (`--steps` to change), in `runs/prism_swim3d_gpu` - re-running resumes;
3. **picks** the best snapshot: every 250-step snapshot is rolled out from 4 seeds for 1000 steps
   (`particle_nca.score_snapshot`), and it keeps the lowest in-place error among snapshots with no
   extinctions and every seed within 20% of the target tempo;
4. **promotes** it to `results/prism_swim3d_gpu` (its own folder, so it never collides with the
   CPU run's snapshots in `results/prism_swim3d`; the viewer shows the GPU result when present),
   makes the figures, runs the JS verifier (skipped if `node` is missing), pulls the branch,
   rebuilds `viewer.html`, and **commits and pushes** (`--no-push` to stop before git).

`python Tools/NCA/gpu_run.py check` runs only step 1. `--device mps` for Apple silicon. Every
`particle_nca.py train` / `figures` call also takes `--device cuda` directly. Machines without
the Noto Color Emoji font use the bundled `assets/lizard.png` (the same 128x128 render; the
prism target built from it is byte-identical).

## Related work

The particle automaton itself is not new. **Kim, Pajouheshgar, Süsstrunk, Jakob, Park,
"Neural Particle Automata"** (arXiv 2601.16096, SIGGRAPH 2026) independently built the same base
model: NCA on free particles, SPH perception with a moment-corrected gradient, a learned
position update, a Gaussian-splat loss, regeneration, and a 3D version whose particles decode
rotated anisotropic Gaussians. What this directory adds, as far as a literature search finds:
growth by budding (their particle count is fixed), a designed collision force, a learned
periodic stroke on free particles (animated NCA exists only on grids: DyNCA and Mesh NCA as
textures, AnimNCA as an unreviewed walk cycle), and a discrete primitive vocabulary taken from a
game (the nearest precedent is Sudhakaran et al.'s Minecraft block types, on a voxel grid).
Other neighbours: Gala et al., E(n)-equivariant graph NCA (TMLR 2024); Grattarola et al.,
Learning Graph Cellular Automata (NeurIPS 2021); Hamon et al., Sensorimotor Lenia (Science
Advances 2025) for self-maintaining locomotion; Particle Lenia and Particle Life (hand-designed,
not trained); Deshpande et al. 2024 for differentiable cell division.

## Where this is meant to go (not started)

The reproduction is the floor. The obvious routes from it toward flora, roughly in the
order they cost:

- **Other targets, same model** — the paper's own gallery includes plant emoji
  (🌵 🌺 🌿 🌱 🌸 are all in `EMOJI`). Cheapest check of whether a plant silhouette is
  learnable.
- **3D.** Sudhakaran et al., "Growing 3D Artefacts and Functional Machines with Neural
  Cellular Automata" (2021) grow Minecraft structures, trees included, from one block with
  the same recipe and 3D perception. A voxel grid is the closest analogue to a prism lattice.
- **The flora growth law.** `/flora` says a plant grows the way it withers, run backwards:
  crystal, then limbs out of the crystal. An NCA seeded AT the crystal and trained with
  the regenerating objective is literally a rule that grows outward from the heart and
  repairs grazing damage — the food web's grazing is the "damage" the paper trains
  against. Mass is conserved in the game, so a port has to decide what an NCA "update"
  means for a prism (grow/place only; never delete — consumption stays the food web's job).

## Swarm targets: tadpole units

The next step after prism particles. Each particle is a whole tadpole fauna, the game's
`TadPoleFauna.prefab`:

- **Crystal (heart).** Fixed by the unit's element (Charge, Mass, Space or Time). Its world size
  is 2.298 for Charge and Space and 1.737 for Mass and Time.
- **Spindle.** Has a length, a bend (arc angle), a bend roll and a thickness.
- **Body prism.** Has a domain, a tier (plain, danger or shield octahedron), three half-extents
  and a roll. It rides the end of the spindle.

One population assembles into a swimming **whale**. After it loses units it switches to a second
learned rule whose target is a pulsing **jellyfish**.

- `tadpole_model.js` is the one generator for both targets, run by the designer page and in node.
  It includes an exact overlap check. Crystals are spheres, prisms are tested with the
  separating-axis test, and spindles are exempt (a limb may pass through a plate, as in the game).
- `tadpole_designer.src.html` is turned into `tadpole_designer.html` by `build_tadpole_designer.py`.
  The page has sliders, presets, a per-frame intersection check and a jelly-to-whale unit ratio.
  **Send both to Claude** writes `targets/tadpoles` to the page's database.
- `tadpole_target.py [targets.json]` runs the generator and writes
  `results/tadpole_targets/{whale,jelly}.json`. Each holds per-frame unit states (the state a rule
  must reach) and the rendered geometry, centred in that target's grid.

Defaults: a whale of 115 units and a jellyfish of 99 units. Both have zero intersections on every
frame and fit their grids.

The earlier all-prism whale (`whale_model.js`, `whale_designer.*`, `whale_target.py`) is kept as
the previous stage.

## Four elemental body plans: one rule kit, four seedings

The next stage keeps the tadpole as the unit and gives each element its own animated body plan.
The aim is ONE learned rule kit that grows a different creature depending on which element
dominates the swarm it is seeded into. Training scores the kit on four seedings, each with a
majority of a different element, against that element's plan. The reported loss is the SUM of
the four losses.

| Element | Plan | Units (default) | Majority | What the element does there |
|---|---|---|---|---|
| Mass | Whale | 192 | 66% | chunky near-cubic skin plates |
| Space | Jellyfish | 88 | 61% | long thin rods along the bell, tentacles and arms |
| Charge | Pufferfish | 179 | 73% | a shell that inflates; its plates turn shielded, then into danger spines |
| Time | Dragonfly | 76 | 70% | runners that circle the wing outlines; the fastest units in every plan |

Element identity is enforced in the generator, so no plan can ask a unit to break it:

- **Charge** is the only element that changes state (plain, shield, danger). Every shielded prism
  in every plan is a Charge unit.
- **Mass** prisms stay close to a cube (largest axis at most 1.6x the smallest).
- **Space** prisms are thin rods at every size (cross-section at most a quarter of the length).
- **Time** prisms are small and moderately long. Time units reposition: in every plan they run
  laps along a loop, so they really move between frames. Speed is measured per element.

All four hearts render the same blue and white; the element is shown by the crystal's shape.
(A dead tadpole's lime crystal is a later stage.)

**Domains are regions, not colours.** Each prism carries a slot (A/B/C). A plan uses two or three
slots, so no target is a single domain. The intended loss takes the minimum over slot-to-domain
assignments. A swarm is therefore rewarded for forming differently-coloured regions of the right
shape, whichever domains they turn out to be. The designer's **Domains** button cycles the six
assignments to show this.

- `swarm_model.js` is the single generator for all four plans, run by the designer and in node.
  `report()` returns the element mix, per-element speed, state counts, slot use, and the
  per-frame exact overlap check.
- `swarm_designer.src.html` is turned into `swarm_designer.html` by `build_swarm_designer.py`.
  **Send all four to Claude** writes `targets/swarm` to the page's database.
- `swarm_target.py [targets.json]` writes `results/swarm_targets/{mass,space,charge,time}.json`
  plus `params.json`. Units keep their index across frames.

All four defaults have zero intersections on all eight frames and fit their grids.

### Co-evolution: one rule, four seedings (`swarm_nca.py`)

`swarm_nca.py` trains ONE tadpole rule on four seedings at once and reports the sum of the four
losses. Each seeding is 16 tadpoles at one plan's element and domain mix.

- **Element and domain are fixed at birth.** An egg is always its parent's domain. It is its
  parent's element too, except for a 0.5% mutation that survives only if the rule hatches it.
- **The rule learns** where a tadpole moves, whether an egg hatches, its prism (mapped inside its
  element's identity), Charge's state (plain, danger or shield), its spindle, and when it dies.
  A death leaves a crystal and is penalised, so restraint has to happen at hatching.
- **Perception is domain-neutral.** A tadpole sees "same domain" vs "other domain", never which
  domain. It also senses the swarm's headcount and element mix, as a game Cell tracks its fauna.
- **The loss is translation-invariant.** It is a Sinkhorn divergence between centred swarms, with
  a Huber position cost, minimised over slot-to-domain assignments.

On a GPU, `python Tools/NCA/gpu_run.py swarm` runs the same co-evolution (see `swarm_gpu.py`):

1. It checks the GPU against the CPU on one step.
2. It trains with a larger batch and longer rollouts, warm-started from `results/swarm_coevo/warm_start.pt`.
3. Every 1000 steps it scores every grown plan against every target. If the result improves, it
   pushes `results/swarm_coevo_gpu/` only. It never touches `viewer.html`.

Re-running resumes. `python Tools/NCA/swarm_nca.py rollout --rule <rule.pt>` prints the
cross-score table and writes the data the viewer plays back.

#### Scoring against the current majority, and why switching failed at first

Headcount is not a goal. Each swarm is scored against **one** plan only: the plan of its current
majority element (Mass → whale, Space → jellyfish, Charge → pufferfish, Time → dragonfly). The
total is the sum over the four seedings. During training, some pool swarms lose enough of their
majority element (as if eaten) for another element to take over. From then on they are scored
against the new majority's plan. Every evaluation also runs a **switch test**: a grown swarm
loses its majority, then runs 240 more steps.

The first rule trained this way (`runs/swarm_try6`, step 250, shown in the viewer) grows each
seeding closest to its own plan (4/4: diagonals 31 / 33 / 26 / 37 against 55–86 off-diagonal),
but it never switches (0/4). A design review found five reasons:

1. **The label is read at the end of the rollout**, so a swarm that out-lays the new majority and
   returns to its old plan is scored as correct. The training signal rewards reverting.
2. **Laying is designed and element-blind, and it has no gradient.** The rule cannot change its
   own composition.
3. **`w_elem = 60` makes the score mostly a composition readout.** The plans' geometry differs by
   only 5–22.
4. **The hatch and death gradient is weak.**
5. **Switched samples do not live long enough** in the pool to restructure.

Three fixes, all behind `TrainCfg` flags that are off by default (a 3-step run reproduces the
earlier code exactly):

- **Sticky labels** (`sticky_plan`, `switch_cooldown`, `margin`, `p_ratio`): a switched swarm
  keeps its new plan as its label. A switch either leaves the new majority ahead by a margin
  ("tie") or culls every element toward the new plan's mix ("ratio"). Each swarm has a cooldown
  before it can switch again. The log's `rev` is the fraction of swarms whose majority no longer
  matches their label.
- **A learned laying gate** (`learned_lay`): the rule gates its own laying on channel 30, with
  `q = sigmoid(4·s + 3)`, so laying is about 0.95 when that channel is 0. Each child carries a
  zero-valued straight-through weight `dlog q_parent`, so the loss on a child reaches the
  parent's decision to lay it.
- **A composition-relative, contrastive loss** (`rel_elem`, `w_mix`, `w_con`, `con_margin`): the
  target's element marginal is reweighted to the swarm's own mix, so the divergence measures
  geometry given the composition. The mix gets its own squared-error term. A hinge requires the
  own plan to beat every other plan by `con_margin`.

Rollouts now also print a **geometry-only** cross table (element and domain costs zeroed): it
shows whether shape, not just composition, picks the plan.

Four overnight runs compare these fixes. All are warm-started from try6 and pushed to their own
branches, with results in `results/swarm_coevo_<tag>/`:

| Tag | Fixes | Branch |
|---|---|---|
| e1 | sticky labels | `cece/swarm-exp-e1` |
| e2 | e1 + learned laying gate | `cece/swarm-exp-e2` |
| e3 | e1 + composition-relative contrastive loss | `cece/swarm-exp-e3` |
| e4 | all three (stopped at step 260: diverging, see below) | - |
| e5 | e2 + scale-invariant loss | `cece/swarm-exp-e5` |
| e6 | e1 + scale-invariant loss | `cece/swarm-exp-e6` |
| e5b, e6b | e5 and e6 rerun on the NaN-guarded code | `cece/swarm-exp-e5b`, `-e6b` |
| e1b, e2b, e3b | e1, e2 and e3 rerun on the NaN-guarded code, with `NCA_NAN_DUMP` on | `cece/swarm-exp-e1b`, `-e2b`, `-e3b` |
| e7 | e4 + scale-invariant loss | this branch |

**A NaN hang.** The first E7 hung after about 185 steps. `sinkhorn_ot` halves its temperature
until it reaches the target value, a NaN temperature never does, and one non-finite cost matrix
kept the loop running forever. `py-spy` showed `e = NaN` inside the swarm's self-transport term.
Training now guards against this:

- The solver sanitises its costs and bounds its loop.
- States are clamped at ±1000, which no healthy rule reaches.
- A sample whose state or loss turns non-finite is dropped from the update and reseeded.
- A non-finite gradient skips the step.

The log counts these events as `nf`. Default runs are unchanged. E1–E3, E5 and E6 started on the
earlier code, so a run of theirs that goes quiet has most likely hung the same way.

E1 hung at step 500 and E2 at steps 200 and 220 (twice, deterministically from step 0). E1 has
none of the newer options, so the NaN is not specific to the laying gate or the scale-invariant
loss. The leading hypothesis is state runaway: nothing bounds the state (`s += ds`), and a rule
whose update grows with the state grows exponentially until it overflows. The ±1000 clamp stops
the overflow; `smax` in the log (largest |state|) shows whether it is happening. With
`NCA_NAN_DUMP=<path>`, the first step that turns a finite swarm non-finite is saved so it can be
replayed.

**Root cause: state runaway, confirmed.** In E7's step-250 pool nearly every sample sat at the
±1000 clamp on many channels, including the death channel (31); only fresh seeds stayed below
310. A tadpole dies when that channel passes 1, so a runaway rule kills its whole swarm. E2b's
first scored result (step 1000) was exactly that: every swarm extinct. The clamp only hides the
runaway, and a clamped state passes no gradient, so it cannot repair a pool that is already
pinned.

The fix is the overflow loss of the original NCA work:

- `w_over` adds the mean over live particles of `sum(relu(|s| - over_band))`, with
  `over_band = 5`.
- Started from the warm start (E8 = e6 + `w_over=1`), the largest |state| fell from 40 to under 10
  within 60 steps, with full-size swarms and lower loss. Resumed from an already-pinned pool, the
  penalty swamped the loss and the swarms collapsed.

So every run before the F series trained on runaway pools.

**Scoring fix.** An extinct swarm scores the sentinel 100 against every plan. The old scorer took
`min()` over that tie, landed on "mass", and published E2b's all-extinct result as "2/8 tests
pass". A single `tests_passed()` now requires the swarm alive, below the sentinel and strictly
closest to the wanted plan. The GPU job's publisher and the viewer's ranking both use it.

**F series** (all with `w_over=1`, branches `cece/swarm-exp-f*`):

| Tag | Fixes |
|---|---|
| f1 | sticky labels |
| f2 | + laying gate |
| f3 | + contrastive loss |
| f5 | + laying gate + scale-invariant loss |
| f7 | all four |
| e8 (local) | sticky labels + scale-invariant loss |

**First clean result: E8 at step 1000 passes 5 of 8 tests** (try6 baseline: 4 of 8).

- **Own plan, 4 of 4.** Each seeding grows closest to its own plan.
- **Shape now decides, not just the element mix.** In the geometry-only table (element and domain
  costs zeroed), every seeding is also closest to its own plan: 4.8 / 5.7 / 6.7 / 8.8 against 9–30
  off-diagonal.
- **First switch.** The dragonfly that loses its Time majority ends closest to the whale (41.5
  against 52.9–63.8).
- **The other three switches fail on composition, not shape.** The evaluation's cull leaves the new
  majority barely ahead, and laying is element-blind, so after 240 more steps the majority has
  drifted: whale → dragonfly instead of jellyfish, a 128 vs 126 tie for the jellyfish, pufferfish →
  dragonfly as intended but still pufferfish-shaped. Composition control is what the learned laying
  gate (f2, f5) is for.

The divergences are scale-invariant, so they are not on try6's scale; compare runs by tests passed.

**F results at step 1000** (strict scoring; a passing swarm must be alive with at least 32
tadpoles):

| Run | Tests passed | Own plan | Switches | Swarm size |
|---|---|---|---|---|
| f1 (sticky labels) | 5/8 | 3/4 (the whale grows closer to the dragonfly, 40.3 vs 45.1) | 2/4: jellyfish → pufferfish, pufferfish → dragonfly | 280 |
| f2 (+ laying gate) | 0/8 | geometry right, 4/4 by divergence | 1/4 by divergence | 16–17 |
| f5 (+ laying gate + scale-invariant) | 0/8 | geometry right, 4/4 by divergence | 1/4 by divergence | 18–20 |

F1's two switches are genuine: full 280-tadpole swarms that ended closest to the new majority's
plan. Its whale → jellyfish switch scores 16.3 against the whale itself, i.e. it kept its old body.

**The learned laying gate found a shortcut.** F2 and F5 pick the right plan, but they never lay:
they keep the 16-tadpole seed. A sixteen-point cloud in roughly the right place matches any plan's
spread well enough, since the divergence carries no size term (and with `scale_inv`, deliberately
none). Their 4/4 own-plan diagonals are against bodies that never grew, so they are not counted.
At step 2000 nothing had changed: F2 and F5 still ran 16–20-tadpole swarms. Their publishers,
which use the old scorer, report 5/8; the strict scorer gives both 0/8. Two fixes:

- **The scorer** requires `MIN_TEST_BODY = 32` tadpoles before a plan test can pass.
- **`min_body` / `w_body`**: a one-sided body floor, `w_body · relu(1 - n/min_body)²`. It is the
  same for every plan, so it tells the rule to grow without telling it which plan to grow. The floor
  is set to 76, the smallest plan. This is not a headcount goal: any swarm at or above the floor pays
  nothing.

**G series** (F plus the body floor, branches `cece/swarm-exp-g*`):

| Tag | Fixes |
|---|---|
| g2 | f2 + `min_body=76`, `w_body=20` |
| g5 | f5 + `min_body=76`, `w_body=20` |

**G2 at step 1000 passes 5 of 8 with full-size swarms** (G5: 4/8). The body floor worked: the rule
now controls its own laying and still grows every seeding to 280 tadpoles. Its own-plan margins
are the widest of any run so far (13.9 / 17.5 / 15.3 / 20.7 against 42–84 off-diagonal), and one
switch passes (jellyfish → pufferfish). The other three switches drift: 240 steps after the cull,
the majority has moved on again (the whale that lost Mass to Space ends with a Charge majority).
That is the composition problem `learned_egg` targets. G2 ties E8 on tests passed; it is shown in
the viewer because its rule decides its own laying. F1 at step 2000 is still 5/8; F7 at step 1000
kept 19–32-tadpole swarms (1/8). E8 at step 2000 fell to 4/8.

**A new behavior: a parent may choose its egg's element** (`learned_egg`, `p_cross`). Training only
adjusts the weights of one fixed rule; the rule cannot invent an action it has no actuator for. Until
now an egg was always its parent's element, except for a rare random mutation, so the only way to
change a swarm's mix was to lay more of one element or let another die. That is why the switch tests
drift. With `learned_egg`, four hidden channels (26–29) become the parent's preference over its egg's
element. A share `p_cross` of eggs (default 0.1: "here and there") take the element the parent picks;
the rest breed true. Domain always breeds true. The pick gets a gradient the same way the laying gate
does: a zero-valued straight-through score of the chosen element rides on the child. Off by default,
so every earlier run is unchanged.

| Tag | Fixes |
|---|---|
| h1 | e8 (sticky + scale-invariant + overflow) + `learned_egg` |
| h2 | g2 (sticky + laying gate + body floor + overflow) + `learned_egg` |

**E4 diverged, and the logs said why.** Its loss rose from about 100 to 450. The contrastive hinge
(0–11) and the mix error (0.00–0.42) stayed small; the divergence itself grew to 50–97, with every
sample at the 280-slot cap. Grown swarms run 200–280 tadpoles against plans of 76–192, and
collision fixes their spacing, so a big swarm cannot take a small plan's geometry. Switching to a
smaller plan would then need deaths, which are penalised. Two facts point at size rather than shape:

- With element and domain costs zeroed, the four targets are still 12–27 apart, so geometry alone
  can tell the plans apart.
- The grown swarms' own-plan divergence (25–37) is larger than those gaps.

The fix is `scale_inv`: before matching, the swarm is centred and rescaled to the plan's RMS
radius, so a body plan is a shape, not a size. Uniform scale costs 0, a 1.3x stretch still costs
0.35–0.70, and cross-plan separation is unchanged. This matches the design brief: a plan is chosen
by element ratios, never by headcount. Runs with `scale_inv` are also evaluated with it
(`summary.json` carries `scale_inv: 1`), so compare them with the others by tests passed, not by
divergence values.

```
python Tools/NCA/gpu_run.py swarm --device cpu --tag e1 --steps 6000 --set per_kind=2 --set pool=24 \
  --set seed_every=6 --set roll_min=48 --set roll_max=96 --set bptt=28 --set sticky_plan=1 \
  --set p_switch=0.25 --set switch_cooldown=240 --set p_ratio=0.5 --set margin=0.15   # (+ the e2/e3 flags)
```
