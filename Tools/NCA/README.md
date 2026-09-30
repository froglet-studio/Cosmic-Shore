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
