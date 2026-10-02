"""Build the reference set: every archetype family x variants x viewer policies -> feature rows.

    python dataset.py [--variants 12] [--viewers hover,cruise] [--out results/reference_set.json]
"""
from __future__ import annotations

import argparse
import json
import os
from multiprocessing import Pool

from archetypes import FAMILIES
from sealed import SEALED
from sealed2 import SEALED2
from sim import run, HERE


def _job(a):
    emo, fi, vi, viewer, seed = a
    F = SEALED[emo] if fi == 99 else SEALED2[emo] if fi == 98 else FAMILIES[emo][fi]
    feat, _ = run(F, seed, viewer)
    return dict(emotion=emo, family=F.__name__, family_idx=fi, variant=vi, viewer=viewer, seed=seed, f=feat)


def build(variants=12, viewers=("hover", "cruise"), base_seed=1000, sealed=False, sealed2=False):
    jobs = []
    for emo, fams in FAMILIES.items():
        for fi in ([99] if sealed else [98] if sealed2 else range(len(fams))):
            for vi in range(variants):
                for wi, w in enumerate(viewers):
                    jobs.append((emo, fi, vi, w, base_seed + vi * 31 + fi * 7 + wi))
    with Pool(4) as pool:
        return pool.map(_job, jobs, chunksize=4)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--variants", type=int, default=12)
    ap.add_argument("--viewers", default="hover,cruise")
    ap.add_argument("--seed", type=int, default=1000)
    ap.add_argument("--sealed", action="store_true")
    ap.add_argument("--sealed2", action="store_true")
    ap.add_argument("--out", default=os.path.join(HERE, "results", "reference_set.json"))
    a = ap.parse_args()
    rows = build(a.variants, tuple(a.viewers.split(",")), a.seed, a.sealed, a.sealed2)
    os.makedirs(os.path.dirname(a.out), exist_ok=True)
    json.dump(dict(rows=rows), open(a.out, "w"), indent=0)
    print(len(rows), "rows ->", a.out)
