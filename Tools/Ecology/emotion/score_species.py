"""Score species with the frozen probe: today's game species (species.GAME) and any sibling species
importable from Tools/Ecology/<dir>/ that exposes a `PROBE_SPECIES = {name: factory(rng, pilot)}` dict.

    python score_species.py   -> results/species_scores.json
"""
from __future__ import annotations

import glob
import importlib.util
import json
import os
import sys

import numpy as np

from probe import EmotionProbe, EMOTIONS
from sim import run, HERE
from species import GAME

SEEDS, VIEWERS = (201, 202, 203, 204), ("hover", "cruise", "evade")


def score(name, factory, pr, seconds=40.0):
    per = {}
    for v in VIEWERS:
        P, tops, agree, feats = [], [], [], []
        for s in SEEDS:
            f, _ = run(factory, s, v, seconds=seconds)
            sc = pr.score(f); P.append([sc["p"][e] for e in EMOTIONS]); tops.append(sc["top"]); agree.append(sc["agreement"]); feats.append(f)
        Pm = np.mean(P, 0)
        mf = {k: round(float(np.mean([q[k] for q in feats])), 3) for k in feats[0]}
        top = EMOTIONS[int(Pm.argmax())]
        per[v] = dict(p={e: round(float(x), 3) for e, x in zip(EMOTIONS, Pm)}, top=top,
                      votes={e: tops.count(e) for e in set(tops)}, judges_agree=round(float(np.mean(agree)), 2),
                      affect=pr.score(mf)["affect"], why=pr.explain(mf, top, 4), features=mf)
    return per


def sibling_species():
    out = {}
    root = os.path.join(HERE, "..")
    for path in glob.glob(os.path.join(root, "*", "*.py")):
        if "/emotion/" in path or "/common/" in path:
            continue
        try:
            src = open(path).read()
        except OSError:
            continue
        if "PROBE_SPECIES" not in src:
            continue
        spec = importlib.util.spec_from_file_location(os.path.basename(path)[:-3], path)
        mod = importlib.util.module_from_spec(spec); sys.path.insert(0, os.path.dirname(path))
        try:
            spec.loader.exec_module(mod)
            for k, f in getattr(mod, "PROBE_SPECIES", {}).items():
                out[f"{os.path.basename(os.path.dirname(path))}/{k}"] = f
        except Exception as e:  # a sibling's module failing is reported, not fatal
            print("skip", path, e)
    return out


if __name__ == "__main__":
    pr = EmotionProbe.load()
    res = {}
    for name, f in list(GAME.items()) + list(sibling_species().items()):
        res[name] = score(name, f, pr)
        print(f"{name:28s}", " | ".join(f"{v}: {r['top']} {max(r['p'].values()):.2f} agree {r['judges_agree']}" for v, r in res[name].items()), flush=True)
    json.dump(res, open(os.path.join(HERE, "results", "species_scores.json"), "w"), indent=1)
