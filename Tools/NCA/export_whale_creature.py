#!/usr/bin/env python3
"""Export the prism-whale 3D NCA (nca3d.py --target whale) as a flight-cell creature module.

    python3 Tools/NCA/export_whale_creature.py                       # runs/whale3d_swim -> nca_whale.js
    python3 Tools/NCA/export_whale_creature.py --run Tools/NCA/runs/whale3d_swim --no-build

1. weights.json: written into the run dir from its CURRENT model.pt (works on a mid-run
   checkpoint too; the trainer only writes weights.json when it finishes), in the exact format
   build_nca_creature.py loads (w1/b1/w2/b2, D/H/W, period, frames; plus "axes": D = world up).
2. Unless --no-build: Tools/Ecology/flight/creatures/build_nca_creature.py --run <run> --out
   nca_whale.js --global NcaWhale, i.e. the NcaCreature runtime with the whale's weights embedded,
   defining window.NcaWhale (same API: new NcaWhale({seed, scale, position, element, tint}), step,
   hit, mesh/sync). The whale grid's D axis is the whale's UP, which is the axis the runtime already
   maps to world up, so it swims upright with no runtime change. Its colours ARE the prism palette
   (back = space blue, belly = charge gold, mouth/eye = mass coral), so pass tint: 0 to show them
   as trained, or keep the default tint to lean toward one element like the lizard.
Torch is only needed for step 1 (CPU is fine).
"""
import argparse
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
CREATURES = os.path.normpath(os.path.join(HERE, "..", "Ecology", "flight", "creatures"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--run", default=os.path.join(HERE, "runs", "whale3d_swim"))
    ap.add_argument("--out", default=os.path.join(CREATURES, "nca_whale.js"))
    ap.add_argument("--global", dest="glob", default="NcaWhale")
    ap.add_argument("--fmt", default="f16", choices=["f16", "i8", "f32"])
    ap.add_argument("--no-build", action="store_true", help="only write <run>/weights.json")
    a = ap.parse_args()

    sys.path.insert(0, HERE)
    import json
    import numpy as np
    import nca3d
    cfg, ca, frames = nca3d.load_run(a.run)
    path = os.path.join(a.run, "weights.json")
    nca3d.export(ca, cfg, frames.numpy(), path)
    st = os.path.join(a.run, "state.json")
    step = json.load(open(st))["step"] if os.path.isfile(st) else "?"
    loss = np.load(os.path.join(a.run, "loss.npy"))
    print(f"wrote {path} (checkpoint step {step}, grid {tuple(frames.shape[1:4])} D,H,W, "
          f"mean loss of last 50 {float(loss[-50:].mean()):.5f})")
    if not a.no_build:
        subprocess.run([sys.executable, os.path.join(CREATURES, "build_nca_creature.py"), "--run", a.run,
                        "--out", a.out, "--global", a.glob, "--fmt", a.fmt], check=True)


if __name__ == "__main__":
    main()
