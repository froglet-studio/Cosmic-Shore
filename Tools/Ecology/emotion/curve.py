"""Learning curves for the stopping decision: held-out (LOFO) agreement vs (a) variants per family and
(b) number of training families (mechanisms) per emotion. -> results/curve.json"""
import itertools, json, os
import numpy as np
import evaluate as ev
from model import load
from sim import HERE

XL, y, fam, rows = load(os.path.join(HERE, "results", "reference_set.json"), ev.K_L)
XS, _, _, _ = load(os.path.join(HERE, "results", "reference_set.json"), ev.K_S)
var = np.array([r["variant"] for r in rows])


def acc(tr, te):
    m = ev.Ens().fit(XL[tr], XS[tr], y[tr]); return float(np.mean(m.proba(XL[te], XS[te]).argmax(1) == y[te]))


out = {"variants": {}, "families": {}}
for nv in (2, 4, 8, 12):
    out["variants"][nv] = round(float(np.mean([acc((fam != k) & (var < nv), fam == k) for k in range(4)])), 3)
for nf in (1, 2, 3):
    a = []
    for k in range(4):
        others = [j for j in range(4) if j != k]
        for combo in itertools.combinations(others, nf):
            a.append(acc(np.isin(fam, combo), fam == k))
    out["families"][nf] = round(float(np.mean(a)), 3)
print(out); json.dump(out, open(os.path.join(HERE, "results", "curve.json"), "w"), indent=1)
