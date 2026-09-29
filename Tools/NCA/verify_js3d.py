"""Hold nca3d_core.js equal to nca3d.CA3D: same weights (as rounded in weights.json), same
seed, fire rate 1 (deterministic), N steps, compare the whole 16-channel volume.

    python3 Tools/NCA/verify_js3d.py --run Tools/NCA/results/lizard3d_swim

Negative control: the JS with the z-derivative's sign flipped must DISAGREE — the one
component the 2D runner never had, so a pass means the new axis is actually checked.
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from nca3d import CA3D, make_seed  # noqa: E402

JS = r"""
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
if (process.argv[5] === 'flip') src = src.replace('df[z] * sm[y] * sm[x] / 32', '-df[z] * sm[y] * sm[x] / 32');
const m = {}; new Function('module', src)(m);
const w = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const ca = m.exports.makeNCA3D(w);
ca.seed();
for (let i = 0; i < +process.argv[4]; i++) ca.step({ fireRate: 1.0 });
process.stdout.write(JSON.stringify(Array.from(ca.state)));
"""


def run_js(run, n, mode=""):
    p = os.path.join(HERE, "_verify3d.js")
    open(p, "w").write(JS)
    try:
        out = subprocess.check_output(["node", p, os.path.join(HERE, "nca3d_core.js"),
                                       os.path.join(run, "weights.json"), str(n), mode])
    finally:
        os.remove(p)
    return np.array(json.loads(out), np.float32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--steps", type=int, default=40)
    a = ap.parse_args()
    w = json.load(open(os.path.join(a.run, "weights.json")))
    ca = CA3D(w["channel_n"], w["hidden"], w["fire_rate"])
    with torch.no_grad():
        for k in ("w1", "b1", "w2", "b2"):
            getattr(ca, k).copy_(torch.tensor(w[k]))
        x = make_seed(1, w["D"], w["H"], w["W"], w["channel_n"])
        for _ in range(a.steps):
            x = ca(x, fire_rate=1.0)
    ref = x[0].numpy().reshape(-1)
    scale = max(1e-6, float(np.abs(ref).max()))
    err = float(np.abs(run_js(a.run, a.steps) - ref).max()) / scale
    bad = float(np.abs(run_js(a.run, a.steps, "flip") - ref).max()) / scale
    print(f"JS3D vs torch after {a.steps} steps: rel max err {err:.2e}  (z-flip negative control {bad:.2e})")
    assert err < 1e-3, "JS 3D runner disagrees with the PyTorch model"
    assert bad > 1e-2, "negative control did not fire"
    print("OK")


if __name__ == "__main__":
    main()
