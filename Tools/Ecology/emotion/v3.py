"""Round 3: SEVEN classes - the six emotions plus NEUTRAL (background life; see neutral.py). Training: the 5
emotion families of round 2 + 5 neutral families. LOFO over 5 folds; SEALED-2 + the sealed neutral forager
scored ONCE; the searched creatures and the game species re-read. -> results/probe_v3.json, results/v3.json
"""
from __future__ import annotations

import json
import os
from multiprocessing import Pool

import numpy as np

import evaluate as ev
from critter import factory
from model import Logistic, SubPrototype
from neutral import NEUTRAL, SEALED_NEUTRAL
from probe import EmotionProbe, EMOTIONS
from sim import run, HERE, vec
from species import GAME

E7 = EMOTIONS + ("neutral",)
R = os.path.join(HERE, "results")


def _job(a):
    fi, vi, viewer, seed = a
    F = SEALED_NEUTRAL if fi == 99 else NEUTRAL[fi]
    f, _ = run(F, seed, viewer)
    return dict(emotion="neutral", family=F.__name__, family_idx=fi, variant=vi, viewer=viewer, seed=seed, f=f)


def neutral_rows(pool):
    jobs = [(fi, vi, v, 7000 + vi * 31 + fi * 7 + wi) for fi in range(len(NEUTRAL)) for vi in range(12)
            for wi, v in enumerate(("hover", "cruise"))]
    jobs += [(fi, vi, "evade", 7600 + vi * 31 + fi * 7) for fi in range(len(NEUTRAL)) for vi in range(6)]
    jobs += [(99, vi, v, 7900 + vi * 31 + wi) for vi in range(12) for wi, v in enumerate(("hover", "cruise"))]
    return pool.map(_job, jobs, chunksize=4)


class Ens7:
    def fit(self, XL, XS, y):
        self.l = Logistic(ev.LAM).fit(XL, y, K=7); self.s = SubPrototype(ev.PER).fit(XS, y, K=7); return self

    def proba(self, XL, XS):
        return 0.5 * (self.l.proba(XL) + self.s.proba(XS))


def mats(rows):
    XL = np.array([vec(r["f"], ev.K_L) for r in rows]); XS = np.array([vec(r["f"], ev.K_S) for r in rows])
    y = np.array([E7.index(r["emotion"]) for r in rows]); return XL, XS, y


def main():
    path = os.path.join(R, "neutral_rows.json")
    if os.path.exists(path):
        nrows = json.load(open(path))["rows"]
    else:
        with Pool(4) as pool:
            nrows = neutral_rows(pool)
        json.dump(dict(rows=nrows), open(path, "w"))
    ld = lambda p: json.load(open(os.path.join(R, p)))["rows"]
    ref = ld("reference_set.json") + [dict(r, family_idx=4) for r in ld("sealed_set.json")]
    ref += [r for r in nrows if r["viewer"] != "evade" and r["family_idx"] != 99]
    evade = ld("reference_evade.json") + [r for r in nrows if r["viewer"] == "evade"]
    sealed = ld("sealed2_set.json") + [r for r in nrows if r["family_idx"] == 99]
    XL, XS, y = mats(ref); fam = np.array([r["family_idx"] for r in ref])
    P = np.zeros((len(y), 7)); accs = []; conf = np.zeros((7, 7), int)
    for k in range(5):
        tr, te = fam != k, fam == k
        m = Ens7().fit(XL[tr], XS[tr], y[tr]); P[te] = m.proba(XL[te], XS[te])
        accs.append(float(np.mean(P[te].argmax(1) == y[te])))
    for a, b in zip(y, P.argmax(1)):
        conf[a, b] += 1
    out = dict(classes=E7, lofo5=dict(accuracy=round(float(np.mean(accs)), 3), per_fold=[round(a, 3) for a in accs],
                                     emotions_only=round(float(np.mean(P.argmax(1)[y < 6] == y[y < 6])), 3),
                                     neutral_recall=round(float(np.mean(P.argmax(1)[y == 6] == 6)), 3), confusion=conf.tolist()))
    XLe, XSe, ye = mats(evade)
    XLa, XSa, ya = np.vstack([XL, XLe]), np.vstack([XS, XSe]), np.concatenate([y, ye])
    m_ref = Ens7().fit(XL, XS, y)
    XLs, XSs, ys = mats(sealed); Ps = m_ref.proba(XLs, XSs)
    per = {}
    for r, p in zip(sealed, Ps.argmax(1)):
        d = per.setdefault(r["family"], [0, 0, {}]); d[0] += int(p == E7.index(r["emotion"])); d[1] += 1
        d[2][E7[p]] = d[2].get(E7[p], 0) + 1
    out["sealed2_plus_neutral"] = dict(accuracy=round(float(np.mean(Ps.argmax(1) == ys)), 3),
                                       per_family={k: dict(acc=round(a / n, 2), read_as=max(c, key=c.get)) for k, (a, n, c) in per.items()})
    m = Ens7().fit(XLa, XSa, ya)
    base = json.load(open(os.path.join(R, "probe.json")))
    base.update(emotions=list(E7),
                logistic=dict(features=ev.K_L, lam=ev.LAM, mu=m.l.std.mu.tolist(), sd=m.l.std.sd.tolist(), W=m.l.W.tolist(), b=m.l.b.tolist()),
                subproto=dict(features=ev.K_S, per=ev.PER, mu=m.s.std.mu.tolist(), sd=m.s.std.sd.tolist(), centres=m.s.Cs.tolist(),
                              labels=m.s.lab.tolist(), w=m.s.w.tolist(), T=float(m.s.T)))
    W = m.l.W
    base["importance_v3"] = {e: [(ev.K_L[i], round(float(W[k, i]), 2)) for i in np.argsort(-np.abs(W[k]))[:8]] for k, e in enumerate(E7)}
    p3 = os.path.join(R, "probe_v3.json"); json.dump(base, open(p3, "w"), indent=1)
    v3 = EmotionProbe.load(p3)
    # searched creatures and game species under v3
    srch = json.load(open(os.path.join(R, "search.json"))); out["searched_under_v3"] = {}
    for t, r in srch.items():
        tops = [v3.score(run(factory(np.array(r["cma"]["theta"])), s, v, seconds=20.0)[0])["top"]
                for s in (401, 402, 403) for v in ("hover", "cruise")]
        out["searched_under_v3"][t] = {k: tops.count(k) for k in set(tops)}
    out["game_under_v3"] = {}
    for name, f in GAME.items():
        res = {}
        for v in ("hover", "cruise", "evade"):
            tops = [v3.score(run(f, s, v, seconds=40.0)[0]) for s in (201, 202, 203, 204)]
            pm = {e: round(float(np.mean([t["p"][e] for t in tops])), 3) for e in E7}
            res[v] = dict(top=max(pm, key=pm.get), p=pm)
        out["game_under_v3"][name] = res
        print(name, {v: (r["top"], r["p"][r["top"]]) for v, r in res.items()}, flush=True)
    json.dump(out, open(os.path.join(R, "v3.json"), "w"), indent=1)
    print(json.dumps({k: v for k, v in out.items() if k not in ("game_under_v3",)}, indent=1)[:4000])


if __name__ == "__main__":
    main()
