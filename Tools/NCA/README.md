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
