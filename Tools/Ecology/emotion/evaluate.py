"""Fit the final probe, measure it, and write results/probe.json.

    python evaluate.py            # LOFO / within / viewer metrics + importances -> probe.json
    python evaluate.py --sealed   # ALSO score the sealed 5th families (do this once; record it)
"""
from __future__ import annotations

import argparse
import json
import os

import numpy as np

from iterate import GROUPS
from model import load, Logistic, SubPrototype, permutation_importance
from probe import EMOTIONS, A, affect_of
from sim import HERE

K_L = sum([g for _, g in GROUPS[:-1]], [])          # logistic: G0..G5 (G6 hurt it under LOFO)
K_S = sum([g for _, g in GROUPS], [])               # sub-prototype: G0..G6
LAM, PER = 0.1, 3
RES = os.path.join(HERE, "results")


class Ens:
    def fit(self, XL, XS, y):
        self.l = Logistic(LAM).fit(XL, y); self.s = SubPrototype(PER).fit(XS, y); return self

    def proba(self, XL, XS):
        return 0.5 * (self.l.proba(XL) + self.s.proba(XS))


def graded(P, y):
    """Affect-space error of the expected coordinate, valence-sign agreement, threat rank correlation."""
    E = P @ A; T = A[y]
    err = float(np.mean(np.linalg.norm(E - T, axis=1)))
    sign = float(np.mean(np.sign(E[:, 0]) == np.sign(T[:, 0])))
    from scipy.stats import spearmanr
    rho = float(spearmanr(E[:, 2], T[:, 2]).correlation)
    return dict(affect_err=round(err, 3), valence_sign=round(sign, 3), threat_spearman=round(rho, 3))


def main(sealed=False):
    ref = os.path.join(RES, "reference_set.json")
    XL, y, fam, rows = load(ref, K_L); XS, _, _, _ = load(ref, K_S)
    XLe, ye, _, _ = load(os.path.join(RES, "reference_evade.json"), K_L)
    XSe, _, _, _ = load(os.path.join(RES, "reference_evade.json"), K_S)
    out = dict(emotions=EMOTIONS, protocol="LOFO = hold out one generator family of EVERY emotion; chance 1/6")
    # ---- LOFO
    conf = np.zeros((6, 6), int); accs = []; Pall = np.zeros((len(y), 6)); imp = np.zeros((6, len(K_L)))
    imp_s = np.zeros((6, len(K_S))); judges = []
    for k in sorted(set(fam)):
        tr, te = fam != k, fam == k
        m = Ens().fit(XL[tr], XS[tr], y[tr])
        P = m.proba(XL[te], XS[te]); Pall[te] = P
        p = P.argmax(1); accs.append(float(np.mean(p == y[te])))
        judges.append(float(np.mean(m.l.proba(XL[te]).argmax(1) == m.s.proba(XS[te]).argmax(1))))
        for a, b in zip(y[te], p):
            conf[a, b] += 1
        imp += permutation_importance(m.l, XL[te], y[te], K_L, reps=3)
        imp_s += permutation_importance(m.s, XS[te], y[te], K_S, reps=3)
    imp /= len(set(fam)); imp_s /= len(set(fam))
    out["lofo"] = dict(accuracy=round(float(np.mean(accs)), 3), per_fold=[round(a, 3) for a in accs],
                       judges_agree=round(float(np.mean(judges)), 3), confusion=conf.tolist(), **graded(Pall, y))
    # ---- within (5-fold over variants)
    rng = np.random.default_rng(0); idx = rng.permutation(len(y)); parts = np.array_split(idx, 5); acc = []
    for i in range(5):
        te = parts[i]; tr = np.concatenate([parts[j] for j in range(5) if j != i])
        acc.append(np.mean(Ens().fit(XL[tr], XS[tr], y[tr]).proba(XL[te], XS[te]).argmax(1) == y[te]))
    out["within"] = round(float(np.mean(acc)), 3)
    # ---- viewer transfer
    m = Ens().fit(XL, XS, y)
    Pe = m.proba(XLe, XSe)
    out["viewer_evade"] = dict(accuracy=round(float(np.mean(Pe.argmax(1) == ye)), 3), **graded(Pe, ye))
    # ---- final model on everything (hover+cruise+evade)
    XLa = np.vstack([XL, XLe]); XSa = np.vstack([XS, XSe]); ya = np.concatenate([y, ye])
    m = Ens().fit(XLa, XSa, ya)
    # ---- importances
    def top(row, keys, n=6):
        o = np.argsort(-row)[:n]; return [(keys[i], round(float(row[i]), 3)) for i in o if row[i] > 0.002]
    W = m.l.W
    out["importance"] = {e: dict(
        permutation_logistic=top(imp[k], K_L), permutation_subproto=top(imp_s[k], K_S),
        logistic_weights=[(K_L[i], round(float(W[k, i]), 2)) for i in np.argsort(-np.abs(W[k]))[:8]])
        for k, e in enumerate(EMOTIONS)}
    # describe sub-prototype centres in feature units
    cent = m.s.Cs * m.s.std.sd + m.s.std.mu
    out["mechanisms"] = [dict(emotion=EMOTIONS[int(l)], centre={K_S[i]: round(float(cent[j, i]), 3) for i in range(len(K_S))})
                         for j, l in enumerate(m.s.lab)]
    out["logistic"] = dict(features=K_L, lam=LAM, mu=m.l.std.mu.tolist(), sd=m.l.std.sd.tolist(),
                           W=m.l.W.tolist(), b=m.l.b.tolist())
    out["subproto"] = dict(features=K_S, per=PER, mu=m.s.std.mu.tolist(), sd=m.s.std.sd.tolist(),
                           centres=m.s.Cs.tolist(), labels=m.s.lab.tolist(), w=m.s.w.tolist(), T=float(m.s.T))
    # ---- sealed
    prev = os.path.join(RES, "probe.json")
    if os.path.exists(prev):
        old = json.load(open(prev))
        if "sealed" in old:
            out["sealed"] = old["sealed"]           # keep the one-shot record
    if sealed:
        sp = os.path.join(RES, "sealed_set.json")
        XLs, ys, _, srows = load(sp, K_L); XSs, _, _, _ = load(sp, K_S)
        m_ref = Ens().fit(XL, XS, y)                 # the reference-family model, as LOFO used
        Ps = m_ref.proba(XLs, XSs)
        per = {}
        for e in range(6):
            s = ys == e; per[srows[np.flatnonzero(s)[0]]["family"]] = dict(
                acc=round(float(np.mean(Ps[s].argmax(1) == e)), 2),
                read_as=EMOTIONS[int(np.bincount(Ps[s].argmax(1), minlength=6).argmax())])
        rec = dict(accuracy=round(float(np.mean(Ps.argmax(1) == ys)), 3), per_family=per, **graded(Ps, ys))
        out.setdefault("sealed", {})
        out["sealed"][f"run{len(out['sealed']) + 1}"] = rec
        print("SEALED:", json.dumps(rec))
    json.dump(out, open(prev, "w"), indent=1)
    print(json.dumps({k: out[k] for k in ("lofo", "within", "viewer_evade")}, indent=1))
    for e in EMOTIONS:
        print(e, out["importance"][e]["permutation_logistic"][:4], "|", out["importance"][e]["logistic_weights"][:4])


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("--sealed", action="store_true")
    main(ap.parse_args().sealed)
