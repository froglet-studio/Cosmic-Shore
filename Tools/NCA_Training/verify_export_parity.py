"""
verify_export_parity.py — Prove the Unity compute shader math matches training.

Re-implements the EXACT algorithm of ParticleAutomataStep.compute (brute-force
insertion-sort kNN, the shader's perception array layout, the shader's weight
indexing w1[input * hidden + output] / w2[hidden * outputDim + output], the
tanh-bounded position step) in NumPy, loads weights from an exported PNCA
.bytes file, and compares one deterministic automaton step (fire mask all-on)
against the PyTorch ParticleNCA model running the same weights on the same
state.

If this passes, any discrepancy seen in Unity is a binding/dispatch bug, not
a math or file-format bug.

Usage:
    python verify_export_parity.py --weights path/to/weights.bytes
"""

import argparse
import math
import struct

import numpy as np
import torch

from train_particle_nca import ParticleNCA, MAGIC, VERSION


def parse_pnca(path):
    with open(path, 'rb') as f:
        raw = f.read()

    magic, version = struct.unpack_from('<4si', raw, 0)
    assert magic == MAGIC, f"bad magic {magic!r}"
    assert version == VERSION, f"unsupported version {version}"

    (c, k, hidden, perception_dim, output_dim, steps_per_loop,
     radius, step_scale, fire_rate, _reserved) = struct.unpack_from('<iiiiiiffff', raw, 8)

    offset = 48
    def take(count):
        nonlocal offset
        arr = np.frombuffer(raw, dtype='<f4', count=count, offset=offset)
        offset += count * 4
        return arr.copy()

    w1 = take(perception_dim * hidden).reshape(perception_dim, hidden)
    b1 = take(hidden)
    w2 = take(hidden * output_dim).reshape(hidden, output_dim)
    b2 = take(output_dim)
    assert offset == len(raw), f"trailing bytes: {len(raw) - offset}"

    meta = dict(channels=c, k=k, hidden=hidden, perception_dim=perception_dim,
                output_dim=output_dim, steps_per_loop=steps_per_loop,
                radius=radius, step_scale=step_scale, fire_rate=fire_rate)
    return meta, w1, b1, w2, b2


def shader_reference_step(p, h, phase, meta, w1, b1, w2, b2):
    """NumPy transliteration of ParticleAutomataStep.compute's StepParticles
    kernel (fire mask all-on). p: [N,3], h: [N,C]."""
    n = p.shape[0]
    c = meta['channels']
    k = meta['k']
    radius = meta['radius']

    dp_out = np.zeros((n, 3), dtype=np.float64)
    dh_out = np.zeros((n, c), dtype=np.float64)

    s_phase = math.sin(2.0 * math.pi * phase)
    c_phase = math.cos(2.0 * math.pi * phase)

    for i in range(n):
        # Brute-force insertion-sort kNN, as in the shader.
        best_dist = np.full(k, 1e9)
        best_idx = np.full(k, i, dtype=int)
        for j in range(n):
            if j == i:
                continue
            d = float(np.linalg.norm(p[j] - p[i]))
            if d >= best_dist[k - 1]:
                continue
            slot = k - 1
            while slot > 0 and best_dist[slot - 1] > d:
                best_dist[slot] = best_dist[slot - 1]
                best_idx[slot] = best_idx[slot - 1]
                slot -= 1
            best_dist[slot] = d
            best_idx[slot] = j

        # Perception, in the shader's exact layout:
        # [h(C), lap(C), grad(C*3), mu(3), density(1), sin, cos]
        lap = np.zeros(c)
        grad = np.zeros((c, 3))
        mu = np.zeros(3)
        wsum = 0.0
        for nn in range(k):
            j = best_idx[nn]
            d = p[j] - p[i]
            r = max(best_dist[nn], 1e-6)
            direction = d / r
            w = min(max(1.0 - best_dist[nn] / radius, 0.0), 1.0)
            hd = h[j] - h[i]
            lap += hd * w
            grad += np.outer(hd, direction) * w
            mu += d * w
            wsum += w

        wnorm = max(wsum, 1e-6)
        perception = np.concatenate([
            h[i],
            lap / wnorm,
            (grad / wnorm).reshape(-1),
            mu / wnorm,
            [wsum / k, s_phase, c_phase],
        ])
        assert perception.shape[0] == meta['perception_dim']

        # MLP with the shader's flat indexing.
        hidden = np.maximum(0.0, perception @ w1 + b1)
        out = hidden @ w2 + b2

        dp_out[i] = np.tanh(out[:3]) * meta['step_scale']
        dh_out[i] = out[3:]

    return dp_out, dh_out


def main():
    ap = argparse.ArgumentParser(description='Verify PNCA export / shader parity')
    ap.add_argument('--weights', type=str, required=True, help='Exported .bytes file')
    ap.add_argument('--particles', type=int, default=64)
    ap.add_argument('--tolerance', type=float, default=1e-4)
    args = ap.parse_args()

    meta, w1, b1, w2, b2 = parse_pnca(args.weights)
    print(f"Parsed {args.weights}: {meta}")

    # PyTorch model with weights loaded back from the export.
    model = ParticleNCA(
        channel_count=meta['channels'], k_neighbors=meta['k'],
        hidden_size=meta['hidden'], perception_radius=meta['radius'],
        step_scale=meta['step_scale'], fire_rate=meta['fire_rate'],
        steps_per_loop=meta['steps_per_loop'])
    with torch.no_grad():
        model.fc1.weight.copy_(torch.from_numpy(w1.T.copy()))
        model.fc1.bias.copy_(torch.from_numpy(b1.copy()))
        model.fc2.weight.copy_(torch.from_numpy(w2.T.copy()))
        model.fc2.bias.copy_(torch.from_numpy(b2.copy()))

    rng = np.random.default_rng(7)
    n = args.particles
    p = rng.normal(0, 0.5, (n, 3)).astype(np.float32)
    h = rng.normal(0, 0.5, (n, meta['channels'])).astype(np.float32)
    phase = 0.37

    # Torch side: perception -> MLP -> dp/dh (deterministic part of step()).
    with torch.no_grad():
        tp = torch.from_numpy(p).unsqueeze(0)
        th = torch.from_numpy(h).unsqueeze(0)
        tphase = torch.tensor([phase])
        percep = model.perceive(tp, th, tphase)
        out = model.fc2(torch.relu(model.fc1(percep)))
        torch_dp = (torch.tanh(out[..., :3]) * meta['step_scale'])[0].numpy()
        torch_dh = out[..., 3:][0].numpy()

    ref_dp, ref_dh = shader_reference_step(
        p.astype(np.float64), h.astype(np.float64), phase, meta, w1, b1, w2, b2)

    dp_err = np.abs(ref_dp - torch_dp).max()
    dh_err = np.abs(ref_dh - torch_dh).max()
    print(f"max |dp| error: {dp_err:.2e}")
    print(f"max |dh| error: {dh_err:.2e}")

    if dp_err < args.tolerance and dh_err < args.tolerance:
        print("PARITY OK — shader algorithm matches the trained model.")
    else:
        raise SystemExit("PARITY FAILED — shader algorithm diverges from the trained model.")


if __name__ == '__main__':
    main()
