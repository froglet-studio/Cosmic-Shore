# NCA Training — Unity ↔ ML pipeline

Training harnesses for the Continuous Automata initiative. Full architecture
and design rationale: `Docs/ContinuousAutomata/ARCHITECTURE.md`.

Requirements: `pip install torch numpy matplotlib pillow` (CPU is fine; the
models are tiny — full Particle NCA training is ~30 min on a 4-core CPU).

## train_particle_nca.py — the centerpiece

Trains a **learned continuous automaton**: a particle population whose
shared local rule is trained so the population grows from a seed into a
specified **animated 3D shape**, loops the animation, and self-heals.

```bash
# Built-in animated targets: pulse_sphere | sphere_torus | jelly | helix
python train_particle_nca.py --target sphere_torus \
    --output ../../Assets/_SO_Assets/Automata/sphere_torus.bytes \
    --steps 4000 --gif convergence.gif

# Custom target: any .npz with 'frames' [F, M, 3] — e.g. a sampled mesh animation
python train_particle_nca.py --target my_creature.npz --output creature.bytes

# Re-render the grow→animate→damage→heal GIF from a checkpoint
python train_particle_nca.py --target sphere_torus --render-only \
    --checkpoint creature.bytes.ckpt.pt --gif out.gif
```

Output is a self-describing `.bytes` (PNCA header + weights) consumed by
`ParticleAutomataWeightAsset` in Unity. The header bakes the trained
simulation constants (perception radius, step scale, fire rate, steps per
loop) so Unity cannot run weights with mismatched parameters.

## verify_export_parity.py

Transliterates the Unity compute shader's algorithm into NumPy and diffs one
deterministic step against the PyTorch model on the same state. Run after
any change to the export format, perception math, or compute shader:

```bash
python verify_export_parity.py --weights creature.bytes
# expect: PARITY OK, errors ~1e-10
```

## train_nca.py

2D Growing NCA (the Distill emoji result). Target: a PNG, or a directory of
PNG frames for an animated target. Consumed by `NCAWeightAsset` /
`NCASimulator`.

## train_neural_boids.py

Learns a boid steering rule by regression from trajectory data
(`.npz` with `positions`/`velocities` `[T, N, 3]`), or `--synthetic` for a
built-in murmuration generator. Consumed by `NeuralBoidWeightAsset`.

## export_weights.py

Legacy 2-layer exporter (headerless flat float32: w1, b1, w2, b2) for the
2D NCA / boid / flow-field formats, plus `--format random` to generate
pipeline-test weights. The Particle NCA does **not** use this — its trainer
exports the headered PNCA format directly.
