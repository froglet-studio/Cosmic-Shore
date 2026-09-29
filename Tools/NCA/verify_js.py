"""Hold nca_core.js equal to the PyTorch model: same weights, same seed, fire rate 1
(deterministic), N steps, compare the whole 16-channel state.

    python3 Tools/NCA/verify_js.py --run Tools/NCA/runs/lizard_regenerating

Negative control: the same comparison against a JS run with the Sobel kernel's sign
flipped must FAIL, so a pass means the two agree rather than that the check is blind.
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
import torch

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from growing_nca import CAModel, make_seed  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))

JS = r"""
const fs = require('fs');
let src = fs.readFileSync(process.argv[2], 'utf8');
if (process.argv[5] === 'flip') src = src.replace('(tr + 2 * r + br) - (tl + 2 * l + bl)', '(tl + 2 * l + bl) - (tr + 2 * r + br)');
const m = {}; new Function('module', src)(m);
const w = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const n = +process.argv[4], H = w.grid, W = w.grid;
const ca = m.exports.makeNCA(w, H, W);
ca.seed();
for (let i = 0; i < n; i++) ca.step({ fireRate: 1.0 });
process.stdout.write(JSON.stringify(Array.from(ca.state)));
"""


def run_js(run_dir, n, mode=""):
    path = os.path.join(HERE, "_verify.js")
    with open(path, "w") as f:
        f.write(JS)
    try:
        out = subprocess.check_output(["node", path, os.path.join(HERE, "nca_core.js"),
                                       os.path.join(run_dir, "weights.json"), str(n), mode])
    finally:
        os.remove(path)
    return np.array(json.loads(out), dtype=np.float32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True)
    ap.add_argument("--steps", type=int, default=60)
    a = ap.parse_args()
    # Built from weights.json alone (rounded to 1e-6, as the JS sees them), so any run
    # directory works: static or animated.
    w = json.load(open(os.path.join(a.run, "weights.json")))
    ca = CAModel(w["channel_n"], w["hidden"], w["fire_rate"])
    with torch.no_grad():
        ca.w1.copy_(torch.tensor(w["w1"])); ca.b1.copy_(torch.tensor(w["b1"]))
        ca.w2.copy_(torch.tensor(w["w2"])); ca.b2.copy_(torch.tensor(w["b2"]))
        g = w["grid"]
        x = make_seed(1, g, g, w["channel_n"])
        for _ in range(a.steps):
            x = ca(x, fire_rate=1.0)
    ref = x[0].numpy().reshape(-1)
    js = run_js(a.run, a.steps)
    err = float(np.abs(js - ref).max()) / max(1e-6, float(np.abs(ref).max()))
    bad = run_js(a.run, a.steps, "flip")
    err_bad = float(np.abs(bad - ref).max()) / max(1e-6, float(np.abs(ref).max()))
    print(f"JS vs torch after {a.steps} steps: rel max err {err:.2e}  (negative control {err_bad:.2e})")
    assert err < 1e-3, "JS runner disagrees with the PyTorch model"
    assert err_bad > 1e-2, "negative control did not fire; the comparison is blind"
    print("OK")


if __name__ == "__main__":
    main()
