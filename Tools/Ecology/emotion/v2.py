"""Round 2 of the probe: the spent sealed-1 families become training family 5. Same features, same models.
LOFO over 5 families, then SEALED-2 scored ONCE, then the v1 'majestic dot' re-read.
-> results/probe_v2.json (same schema as probe.json, loadable by EmotionProbe.load(path))."""
from __future__ import annotations

import json
import os

import numpy as np

import evaluate as ev
from critter import factory
from model import load, Logistic, SubPrototype
from probe import EmotionProbe, EMOTIONS
from sim import run, HERE

R = os.path.join(HERE, "results")


def stack(paths, keys):
    Xs, ys, fs, rows = [], [], [], []
    for p, relabel in paths:
        X, y, f, r = load(os.path.join(R, p), keys)
        Xs.append(X); ys.append(y); fs.append(np.where(f == 99, 4, f) if relabel else f); rows += r
    return np.vstack(Xs), np.concatenate(ys), np.concatenate(fs), rows


def main():
    src = [("reference_set.json", False), ("sealed_set.json", True)]
    XL, y, fam, _ = stack(src, ev.K_L); XS, _, _, _ = stack(src, ev.K_S)
    accs, P = [], np.zeros((len(y), 6))
    for k in sorted(set(fam)):
        tr, te = fam != k, fam == k
        m = ev.Ens().fit(XL[tr], XS[tr], y[tr]); P[te] = m.proba(XL[te], XS[te]); accs.append(float(np.mean(P[te].argmax(1) == y[te])))
    out = dict(lofo5=dict(accuracy=round(float(np.mean(accs)), 3), per_fold=[round(a, 3) for a in accs], **ev.graded(P, y)))
    m = ev.Ens().fit(XL, XS, y)
    XL2, y2, _, rows2 = load(os.path.join(R, "sealed2_set.json"), ev.K_L); XS2, _, _, _ = load(os.path.join(R, "sealed2_set.json"), ev.K_S)
    P2 = m.proba(XL2, XS2)
    per = {}
    for e in range(6):
        s = y2 == e
        per[rows2[np.flatnonzero(s)[0]]["family"]] = dict(acc=round(float(np.mean(P2[s].argmax(1) == e)), 2),
                                                         read_as=EMOTIONS[int(np.bincount(P2[s].argmax(1), minlength=6).argmax())])
    out["sealed2"] = dict(accuracy=round(float(np.mean(P2.argmax(1) == y2)), 3), per_family=per, **ev.graded(P2, y2))
    # v1 on sealed-2, for the comparison (same data, older model)
    v1 = EmotionProbe.load()
    rows_f = json.load(open(os.path.join(R, "sealed2_set.json")))["rows"]
    out["sealed2_by_v1"] = round(float(np.mean([v1.score(r["f"])["top"] == r["emotion"] for r in rows_f])), 3)
    # save v2 in probe.json's schema
    base = json.load(open(os.path.join(R, "probe.json")))
    base.update(logistic=dict(features=ev.K_L, lam=ev.LAM, mu=m.l.std.mu.tolist(), sd=m.l.std.sd.tolist(), W=m.l.W.tolist(), b=m.l.b.tolist()),
                subproto=dict(features=ev.K_S, per=ev.PER, mu=m.s.std.mu.tolist(), sd=m.s.std.sd.tolist(), centres=m.s.Cs.tolist(),
                              labels=m.s.lab.tolist(), w=m.s.w.tolist(), T=float(m.s.T)), round2=out)
    p2 = os.path.join(R, "probe_v2.json"); json.dump(base, open(p2, "w"), indent=1)
    v2 = EmotionProbe.load(p2)
    srch = json.load(open(os.path.join(R, "search.json")))
    re = {}
    for t, r in srch.items():
        th = np.array(r["cma"]["theta"]); tops1, tops2 = [], []
        for s in (401, 402, 403):
            for v in ("hover", "cruise"):
                f, _ = run(factory(th), s, v, seconds=20.0)
                tops1.append(v1.score(f)["top"] == t); tops2.append(v2.score(f)["top"] == t)
        re[t] = dict(v1_hit=round(float(np.mean(tops1)), 2), v2_hit=round(float(np.mean(tops2)), 2))
    out["searched_under_v2"] = re
    base["round2"] = out; json.dump(base, open(p2, "w"), indent=1)
    print(json.dumps(out, indent=1))


if __name__ == "__main__":
    main()
