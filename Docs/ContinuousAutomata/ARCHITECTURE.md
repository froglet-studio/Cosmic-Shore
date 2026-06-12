# Continuous Automata — Learned Life for the HyperSea

This document covers the **Continuous Automata Exploration** initiative: using
*learned* automata — update rules trained by gradient descent rather than
hand-designed — to create unique, self-organizing, self-healing life in
Cosmic Shore.

The reference point is Google's Growing Neural Cellular Automata
([distill.pub/2020/growing-ca](https://distill.pub/2020/growing-ca/)), which
trained a tiny neural network update rule, run identically and locally on
every pixel of a grid, to grow 2D emojis from a single seed cell, hold them
stable indefinitely, and regrow them after damage. The premise of this
initiative: the same methodology generalizes off the grid — boids are
already simple continuous automata, just with a hand-written rule — and a
**learned continuous automaton can converge to a specified, sophisticated 3D
animation** the way those cellular ones converged to 2D emojis.

That centerpiece exists and is trained end-to-end: the **Particle NCA**
(`ParticleNCA/` below).

## The quadrant map

|  | **Hand-designed rule** | **Learned rule** |
|---|---|---|
| **Cellular (grid)** | Classic CA (Game of Life, …) | Growing NCA — 2D port shipped (`CellularNCA/`) |
| **Continuous (particles)** | Boids (`FloraAndFauna/Boid.cs`) | **Particle NCA — this initiative's centerpiece** (`ParticleNCA/`), Neural Boids (`NeuralBoids/`) |

Two distinct learned-continuous systems exist because they answer different
questions:

- **Neural Boids** keep the boid state vector (position + velocity) and learn
  the steering rule by regression from trajectory data (e.g. murmuration
  recordings). The learned rule replaces separation/cohesion/alignment.
  Behavior cloning — no shape objective.
- **Particle NCA** generalizes the *full* NCA construction: particles carry
  hidden state channels, perceive their neighborhood through a fixed
  differential operator, and are trained **through the rollout** (BPTT) so
  the *population* converges to a specified animated 3D shape — growing it
  from a seed, looping its animation, and self-healing after damage. This is
  the morphogenesis result, in continuous 3D space.

## Particle NCA — model

State per particle: position `p ∈ R³` plus `C = 8` hidden channels
(channel 0 is "vitality", used for rendering tint; the rest are free for the
rule to use as memory/signaling — the learned analog of morphogen gradients).

**Perception** is fixed-function (not learned), the continuous analog of the
NCA's identity + Sobel filters, computed over the `K = 12` nearest neighbors
with hat-kernel weights `w = max(0, 1 − dist/R)`:

| Component | Dim | Analog |
|---|---|---|
| own state `h_i` | C | identity filter |
| weighted mean of `(h_j − h_i)` | C | Laplacian |
| weighted sum of `(h_j − h_i) · dir_ij` | 3C | Sobel gradient |
| weighted mean offset `(p_j − p_i)` | 3 | center-of-mass direction |
| `Σw / K` | 1 | density |
| `sin(2πt), cos(2πt)` | 2 | **animation phase** |

Total perception dim: `5C + 6 = 46`.

**Update rule** (the only learned part): `dense(46 → 96, ReLU) →
dense(96 → 11, linear, zero-initialized)` producing `[Δp(3), Δh(8)]`.
Applied with a stochastic per-particle fire mask (rate 0.5 — the automaton
is asynchronous, in training and at runtime alike), `Δp` bounded by
`tanh × stepScale`, states clamped to ±4.

**Time conditioning** is what turns a static-shape NCA into an *animation*
NCA: the phase input advances `1/stepsPerLoop` per automaton step and the
training target is a function of phase. One rule therefore encodes the whole
looping animation — there is no keyframe playback anywhere; the motion is
re-derived every step from local interactions.

**Training** (`Tools/NCA_Training/train_particle_nca.py`) uses the Distill
sample-pool curriculum, generalized for animated targets:

- Loss: symmetric Chamfer distance between particle positions and the target
  point cloud at the rollout's last three checkpoints (final, −8, −16 steps,
  weighted 0.6/0.2/0.2 — the target moves during the rollout, so multi-
  checkpoint supervision teaches continuous *tracking*; end-only loss
  converges far too slowly at CPU iteration budgets), plus a small
  hidden-state overflow penalty. BPTT through 32–56 step rollouts.
- Pool of 64 persisted rollout endpoints (each tagged with its phase) keeps
  long-horizon stability trained across all phases of the loop.
- Each batch is ranked by current loss: the **worst** entry is replaced with
  a fresh seed (trains growth *and* continuously purges poisoned states); the
  **best** entry is damaged (trains regeneration). Damaging by rank matters —
  the original unconditional slot assignment diverged within 700 iterations.
- Damage = scatter: particles inside a random sphere are blasted to random
  nearby positions with state wiped. Particle count is conserved. The scatter
  radius must keep blasted particles within perceptual contact of the body
  (hat weights vanish beyond R; an informationally disconnected particle
  cannot learn to rejoin).
- Targets: procedural animated point clouds (`pulse_sphere`, `sphere_torus`
  morph, `jelly` swimmer, `helix`), or any `.npz` with `frames [F, M, 3]` —
  i.e. **any voxelized/sampled mesh animation can be a target**.

## Particle NCA — Unity runtime

```
Assets/_Scripts/Controller/Automata/ParticleNCA/
├── ParticleAutomataStep.compute     StepParticles / ScatterDamage / ResetToSeed kernels
├── ParticleAutomataSimulator.cs     buffers, dispatch, phase, public API
├── ParticleAutomataRenderer.cs      Graphics.DrawProcedural billboards, zero readback
├── ParticleAutomataRender.shader    URP unlit, vitality-tinted, additive
├── ParticleAutomataConfigSO.cs      deploy-time tunables (count, rate, scale, colors)
└── ParticleAutomataWeightAsset.cs   loads the self-describing PNCA .bytes export
```

- The compute shader replicates the training math **exactly** — verified to
  ~1e-10 by `Tools/NCA_Training/verify_export_parity.py`, which transliterates
  the shader algorithm in NumPy and diffs it against the PyTorch model. If
  Unity behavior ever diverges, suspect bindings/dispatch, not math.
- The `.bytes` weight file is self-describing (`PNCA` header carrying network
  dims and the trained simulation constants: perception radius, step scale,
  fire rate, steps per loop). `ParticleAutomataWeightAsset` parses and
  validates it; `ParticleAutomataSimulator` fails loud if the header doesn't
  fit the shader's compile-time constants (`CHANNEL_COUNT 8`,
  `PERCEPTION_DIM 46`, `OUTPUT_DIM 11`, `MAX_HIDDEN 128`, `MAX_NEIGHBORS 16`).
- Simulation runs in the unit-scale local space it was trained in;
  `config.WorldScale` + the simulator's transform place it in the world.
- Public API: `ResetToSeed()` (regrow from scratch), `DamageSphere(worldPos,
  radius)` (gameplay hook — shoot it and watch it heal), `Phase`,
  `ParticleBuffer` (for custom renderers).
- Brute-force GPU kNN is O(N²) per step: fine to ~4096 particles at 48
  steps/sec. Past that, port the neighbor search to a spatial hash. (This is
  a self-contained GPU sim — `PrismSpatialIndex` is for prism mass and does
  not apply here.)
- Trained weight assets live in `Assets/_SO_Assets/Automata/`.

Setup: GameObject + `ParticleAutomataSimulator` (wire a
`ParticleAutomataConfigSO`) + `ParticleAutomataRenderer` (wire a material
using `CosmicShore/Automata/ParticleAutomata`). The config wires the compute
shader and a `ParticleAutomataWeightAsset`.

## 2D Growing NCA & Neural Boids (supporting systems)

- `CellularNCA/` — faithful GPU port of the Distill 2D NCA (16 channels in
  4 RGBA RenderTextures, Perceive/Update/Apply kernels, alive masking,
  stochastic fire). Trained by `train_nca.py` (supports animated PNG-sequence
  targets). Useful for texture-space life: growing glyphs/sigils on surfaces,
  UI organisms.
- `NeuralBoids/` — GPU flock with a learned steering MLP, trained by
  `train_neural_boids.py` from `[T, N, 3]` trajectory data (synthetic
  murmuration generator included for pipeline testing).
- `NeuralFlowFieldSO` (`Controller/Environment/FlowField/`) — `FlowFieldSO`
  subclass evaluating a tiny MLP instead of hand-designed flow equations;
  drop-in anywhere a `FlowFieldSO` is consumed.

## Relationship to the fundamentals

Learned automata are a *mechanism for authoring* Flora & Fauna behavior, not
a new fundamental. The curation checklist (CLAUDE.md § Design Philosophy)
comes out clean:

- **Flora & Fauna** — a Particle NCA creature is fauna: a population whose
  form *is* its behavior. It self-organizes, holds homeostasis, and heals —
  all emergent from one local rule, which is exactly the design philosophy's
  preferred shape (no keyframes, no bespoke state machines per creature).
- **Mass / Prisms** — automaton particles are **not prisms and not mass**.
  They are rendered as billboards, never enter the prism pipeline,
  `PrismSpatialIndex`, or the food web. If a future design wants NCA life to
  produce or consume mass, that exchange must go through the existing active
  forces (fauna consumption, abilities) — never as a side channel.
- **Domain** — the renderer's base/accent colors are externally settable;
  domain-theming an automaton creature is a material tint, same as vessels.
- **Elementals** — if automaton creatures ever buff/debuff vessels, that must
  be expressed through the elemental system like everything else.
- **Universality** — damage-response is in-model (trained), so the same
  creature behaves identically in any scene, menu or game. No context
  exemptions needed or allowed.

## Roadmap

1. ~~2D Growing NCA GPU port~~ ✓
2. ~~Neural boid pipeline~~ ✓
3. ~~Particle NCA: learned continuous automaton converging to specified
   animated 3D targets, trained end-to-end, with parity-verified Unity
   runtime~~ ✓ (this doc)
4. Author targets from real content: sample animated meshes (fauna concepts,
   vessel silhouettes) into `.npz` frame banks and train creature rules from
   them.
5. In-game integration pass: a Cell-resident NCA creature wired to
   `DamageSphere` from projectile impacts; evaluate perf on mobile targets.
6. Interaction conditioning: extend the perception vector with a small number
   of game inputs (nearest-vessel direction, cell phase) so creatures react
   to play, not just phase. Requires retraining; the PNCA header versioning
   covers the format change.
7. Multi-creature ecologies: two rules sharing one particle space (predation
   /avoidance between learned species).

## Files

| Role | Path |
|---|---|
| Particle NCA trainer (centerpiece) | `Tools/NCA_Training/train_particle_nca.py` |
| Export/shader parity verifier | `Tools/NCA_Training/verify_export_parity.py` |
| 2D NCA trainer | `Tools/NCA_Training/train_nca.py` |
| Neural boid trainer | `Tools/NCA_Training/train_neural_boids.py` |
| Legacy 2-layer exporter / random test weights | `Tools/NCA_Training/export_weights.py` |
| Particle NCA runtime | `Assets/_Scripts/Controller/Automata/ParticleNCA/` |
| 2D NCA runtime | `Assets/_Scripts/Controller/Automata/CellularNCA/` |
| Neural boids runtime | `Assets/_Scripts/Controller/Automata/NeuralBoids/` |
| Neural flow field | `Assets/_Scripts/Controller/Environment/FlowField/NeuralFlowFieldSO.cs` |
| Trained weight assets | `Assets/_SO_Assets/Automata/` |
