"""Promote the 7-class v3 probe to results/probe.json (the file the shared scorecard and every caller load)
and give it held-out permutation importances per emotion. The 6-class v1 is kept as results/probe_v1.json.
NOTE: re-running evaluate.py overwrites probe.json with a 6-class v1 - run this afterwards."""
from __future__ import annotations

import json
import os
import shutil

import numpy as np

import evaluate as ev
from model import permutation_importance
from v3 import E7, Ens7, mats, R


def main():
    p1, p3, p = (os.path.join(R, n) for n in ("probe_v1.json", "probe_v3.json", "probe.json"))
    if not os.path.exists(p1):
        shutil.copy(p, p1)
    ld = lambda n: json.load(open(os.path.join(R, n)))["rows"]
    nrows = ld("neutral_rows.json")
    ref = ld("reference_set.json") + [dict(r, family_idx=4) for r in ld("sealed_set.json")]
    ref += [r for r in nrows if r["viewer"] != "evade" and r["family_idx"] != 99]
    XL, XS, y = mats(ref); fam = np.array([r["family_idx"] for r in ref])
    imp_l = np.zeros((7, len(ev.K_L))); imp_s = np.zeros((7, len(ev.K_S)))
    import model
    old = model.EMOTIONS
    model.EMOTIONS = E7                                   # permutation_importance sizes its table from this
    try:
        for k in range(5):
            tr, te = fam != k, fam == k
            m = Ens7().fit(XL[tr], XS[tr], y[tr])
            imp_l += permutation_importance(m.l, XL[te], y[te], ev.K_L, reps=3)
            imp_s += permutation_importance(m.s, XS[te], y[te], ev.K_S, reps=3)
    finally:
        model.EMOTIONS = old
    imp_l /= 5; imp_s /= 5
    d = json.load(open(p3)); v3 = json.load(open(os.path.join(R, "v3.json")))
    top = lambda row, keys: [(keys[i], round(float(row[i]), 3)) for i in np.argsort(-row)[:6] if row[i] > 0.002]
    d["importance"] = {e: dict(permutation_logistic=top(imp_l[k], ev.K_L), permutation_subproto=top(imp_s[k], ev.K_S),
                               logistic_weights=d["importance_v3"][e]) for k, e in enumerate(E7)}
    d.pop("importance_v3", None); d.pop("mechanisms", None); d.pop("sealed", None)
    v1 = json.load(open(p1))
    d["version"] = "v3 (7 classes: six emotions + neutral; trained on 5 emotion families + 5 neutral families)"
    d["metrics"] = dict(v3=dict(lofo5=v3["lofo5"], sealed2_plus_neutral=v3["sealed2_plus_neutral"]),
                        v1=dict(lofo4=v1["lofo"], within=v1["within"], viewer_evade=v1["viewer_evade"], sealed1=v1.get("sealed")))
    d["protocol"] = ("LOFO = hold out one generator FAMILY of every class (a mechanism the model never saw); "
                     "sealed = families written before the model, scored once. Chance = 1/7 (v3), 1/6 (v1).")
    json.dump(d, open(p, "w"), indent=1)
    for e in E7:
        print(f"{e:11s}", d["importance"][e]["permutation_logistic"][:4])


if __name__ == "__main__":
    main()
