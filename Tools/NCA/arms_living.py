"""Record the LIVING pond for the viewer: the recommended pair (a3 g1460) in the open economy with the designed
satiety gate (births funded by mass eaten, starvation, conserved mass), 120 s. Adds it to results/arms/arms_viewer.html
next to the fixed-roster encounters, and writes a GIF.

    python Tools/NCA/arms_living.py
"""
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "Ecology"))

import numpy as np

import arms_eval as E
import arms_sim as A

OUT = os.path.join(HERE, "results", "arms")


def main(gen=1460, seed=22, secs=120.0):
    z = np.load(os.path.join(OUT, "snaps_a3", f"g{gen:05d}.npz"))
    cfg = A.Cfg(eco=True, cap_prey=480, cap_pred=48, sated=1.25)
    rec = E.record(cfg, z["thq"], z["thp"], seed, secs)
    nq = rec["qa"].sum(1); npr = rec["pa"].sum(1)
    k = len(nq) - 1
    note = (f"Open economy + satiety gate, pred g{gen} vs prey g{gen}: prey {nq[0]} -> {nq[k]}, predators {npr[0]} -> "
            f"{npr[k]} over {secs:.0f} s; births and starvation on, mass conserved. Predators heavier than 1.25x "
            f"their birth mass cannot burst (the designed gate).")
    path = E.viewer_json(rec, cfg, os.path.join(OUT, f"living_g{gen}.json"), f"living pond g{gen} (eco + gate)", note, every=3)
    E.gif(rec, cfg, os.path.join(OUT, f"living_g{gen}.gif"), 0, 30, every=3, title=f"living g{gen}")
    hud = [f"prey {a} pred {b}" for a, b in zip(nq[::3], npr[::3])]
    d = json.load(open(path)); d["meta"]["hud"] = hud; json.dump(d, open(path, "w"), separators=(",", ":"))
    from common.viewer import extract, build
    runs = [os.path.join(OUT, f) for f in ("encounter_g1460_g1460.json", "vessel_g1460_g1460.json")] + [path]
    print(note)
    print(build(os.path.join(OUT, "arms_viewer.html"), runs, "Arms race: predator vs prey"), "bytes")


if __name__ == "__main__":
    main()
