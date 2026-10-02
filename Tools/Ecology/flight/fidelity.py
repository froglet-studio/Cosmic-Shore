"""The flight FIDELITY GATE: does the JS the page flies behave like the Python that was scored?

    python Tools/Ecology/flight/fidelity.py                # compare results/fidelity_js.json vs fidelity_py.json
    python Tools/Ecology/flight/fidelity.py --negative     # also run the negative controls (deliberately broken
                                                           # parameters) through Node and require them to FAIL

Both halves run the species in their own direction's world against the same scripted pilots (fidelity_py.py,
fidelity_js.js). The JS cannot reproduce the Python bit for bit (different RNGs), so the comparison is
statistical: each metric is a mean over seeds (or a pooled median) with a bootstrap standard error, and
JS and Python AGREE on a metric when

    |js - py|  <=  max(abs_floor, rel * max(|js|, |py|), 2.5 * sqrt(se_js^2 + se_py^2))

i.e. the difference is inside the sampling noise of the two estimates, OR inside a stated practical band (a
species whose hits are rare cannot be resolved better than its noise, and a 10% wobble in a feel axis changes no
judgement). A species PASSES when every metric agrees. The negative controls prove the band is not so wide that
anything passes: a broken parameter must flip at least one metric to DISAGREE.

Writes results/fidelity.json and results/fidelity.md (the table quoted in DISCOVERIES.md).
"""
from __future__ import annotations

import json
import os
import subprocess
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "results")
WORLD = dict(pack="bestiary", thief="bestiary", locust="bestiary", lurker="bestiary", stampede="bestiary",
             leviathan="bestiary", mobber="bestiary", grazer="bestiary", fortress="builders", snaptrap="flora")
POL = dict(bestiary=("wander", "evader", "hunter"), builders=("wander", "evader", "hunter"), flora=("wander", "reader", "cutter"))
FEEL = ("speed_rel", "approach", "coherence", "burstiness", "jerk_rel", "size")
# metric -> (abs floor, relative band)
TOL = dict(hits=(1.0, 0.30), payoff=(1.0, 0.30), counterplay=(0.15, 0.35), tel_share=(0.15, 0.30), tel_first=(0.25, 0.30), tel=(0.25, 0.30),
           speed_rel=(0.05, 0.25), approach=(6.0, 0.30), coherence=(0.05, 0.20), burstiness=(0.5, 0.30),
           jerk_rel=(0.05, 0.30), size=(0.5, 0.15))
NEGATIVE = [("thief.WARM=1e9", "thief", "a thief that wants ANY trail (the 'cold' ablation as a parameter)"),
            ("lurker.GAPE=0.05", "lurker", "a lurker whose gape is 0.05 s (no telegraph)"),
            ("pack.SPRINT=60", "pack", "a pack that cannot sprint (60 u/s)")]
RNG = np.random.default_rng(1234)


def by(runs, sp, pol=None):
    return [r for r in runs if r["species"] == sp and (pol is None or r["policy"] == pol)]


def boot(runs_by_pol, fn, B=400):
    """Statistic fn(dict policy -> runs) and its bootstrap SE (resampling seeds within each policy)."""
    est = fn(runs_by_pol)
    if est is None:
        return None, None
    vals = []
    for _ in range(B):
        rs = {p: [rr[i] for i in RNG.integers(0, len(rr), len(rr))] if rr else rr for p, rr in runs_by_pol.items()}
        v = fn(rs)
        if v is not None:
            vals.append(v)
    return est, (float(np.std(vals)) if len(vals) > 5 else 0.0)


def metrics(runs, sp):
    pols = POL[WORLD[sp]]
    rb = {p: by(runs, sp, p) for p in pols}
    w, e, h = pols
    def m_mean(pol, f):
        return lambda R: float(np.mean([f(r) for r in R[pol]])) if R[pol] else None
    def cp(R):
        a = np.mean([r["hits_per_min"] for r in R[w]]); b = np.mean([r["hits_per_min"] for r in R[e]])
        return float(b / a) if a > 0 else None
    # first-strike leads are BIMODAL for some species (a strike nobody telegraphed - e.g. a hunter ramming a herd - has
    # lead 0, a telegraphed one 0.3-2 s), so their median flips between modes on a few strikes. Report the two modes
    # separately: the SHARE of first strikes that were telegraphed, and the median lead of those that were.
    def tel_share(R):
        x = [v for p in R for r in R[p] for v in r["first_leads"]]
        return float(np.mean([v >= 0.25 for v in x])) if len(x) >= 3 else None
    def tel_first(R):
        x = [v for p in R for r in R[p] for v in r["first_leads"] if v >= 0.25]
        return float(np.median(x)) if len(x) >= 3 else None
    def tel(R):
        x = [r["telegraph_s"] for p in R for r in R[p] if r["telegraph_s"] is not None]
        return float(np.median(x)) if len(x) >= 3 else None
    out = {}
    out[f"hits/min {w}"] = ("hits",) + boot(rb, m_mean(w, lambda r: r["hits_per_min"]))
    out[f"hits/min {e}"] = ("hits",) + boot(rb, m_mean(e, lambda r: r["hits_per_min"]))
    out[f"hits/min {h}"] = ("hits",) + boot(rb, m_mean(h, lambda r: r["hits_per_min"]))
    out[f"payoff/min ({h})"] = ("payoff",) + boot(rb, m_mean(h, lambda r: r["crystals_per_min"]))
    out[f"counterplay ({e}/{w})"] = ("counterplay",) + boot(rb, cp)
    out["telegraphed share of first strikes"] = ("tel_share",) + boot(rb, tel_share)
    out["telegraph first strike s (telegraphed)"] = ("tel_first",) + boot(rb, tel_first)
    out["telegraph (shared) s"] = ("tel",) + boot(rb, tel)
    for f in FEEL:
        def fm(R, f=f):
            x = [r["feel"][f] for p in R for r in R[p] if r["feel"].get(f) is not None]
            return float(np.mean(x)) if x else None
        out[f"feel {f}"] = (f,) + boot(rb, fm)
    return out


def compare(js_runs, py_runs, species):
    rows, verdict = {}, {}
    for sp in species:
        mj, mp = metrics(js_runs, sp), metrics(py_runs, sp)
        ok_all = True; rows[sp] = []
        w = POL[WORLD[sp]][0]
        low = min(mj[f"hits/min {w}"][1] or 0, mp[f"hits/min {w}"][1] or 0) < 1.0
        for k in mj:
            kind, j, sej = mj[k]; _, p, sep = mp[k]
            if kind == "counterplay" and low:   # a ratio of two near-zero counts is noise: the hit rates are compared above
                rows[sp].append(dict(metric=k, js=j, py=p, tol=None, agree=True, note="wanderer hit < 1/min on a side: not resolvable")); continue
            if j is None and p is None:
                rows[sp].append(dict(metric=k, js=None, py=None, tol=None, agree=True, note="undefined both")); continue
            if j is None or p is None:
                # undefined on one side only (e.g. too few strikes for a telegraph): a disagreement if the other side
                # is clearly defined; report it, do not hide it
                rows[sp].append(dict(metric=k, js=j, py=p, tol=None, agree=False, note="defined on one side only"))
                ok_all = False; continue
            fl, rel = TOL[kind]
            tol = max(fl, rel * max(abs(j), abs(p)), 2.5 * float(np.hypot(sej or 0, sep or 0)))
            ag = abs(j - p) <= tol
            ok_all &= ag
            rows[sp].append(dict(metric=k, js=round(j, 3), py=round(p, 3), se_js=round(sej or 0, 3), se_py=round(sep or 0, 3),
                                 tol=round(tol, 3), agree=bool(ag)))
        verdict[sp] = ok_all
    return rows, verdict


def load(name):
    return json.load(open(os.path.join(RES, name)))


def md_table(rows, verdict, title, js_dt):
    L = [f"### {title}", "",
         "| species | metric | JS | Python | tolerance | agree |", "|---|---|---|---|---|---|"]
    for sp, rr in rows.items():
        for r in rr:
            f = lambda x: "-" if x is None else (f"{x:.2f}" if isinstance(x, float) else str(x))
            L.append(f"| {sp} | {r['metric']} | {f(r['js'])} | {f(r['py'])} | {f(r['tol'])} | "
                     f"{'yes' if r['agree'] else '**NO**'}{(' (' + r['note'] + ')') if r.get('note') else ''} |")
    L.append("")
    L.append("Verdict: " + ", ".join(f"{sp} {'PASS' if v else '**FAIL**'}" for sp, v in verdict.items()))
    L.append("")
    return "\n".join(L)


if __name__ == "__main__":
    py = load("fidelity_py.json")["runs"]
    out = dict(tol=TOL, summary={})
    md = ["# Flight fidelity gate (generated by flight/fidelity.py)", ""]
    species = [s for s in WORLD if by(py, s)]
    for fname, label in (("fidelity_js.json", "JS at the Python's step (dt 0.1 s)"),
                         ("fidelity_js_dt30.json", "JS at the page's step (dt 1/30 s, probe sampled at 10 Hz)")):
        if not os.path.exists(os.path.join(RES, fname)):
            continue
        js = load(fname)
        rows, verdict = compare(js["runs"], py, [s for s in species if by(js["runs"], s)])
        out[fname] = dict(rows=rows, verdict=verdict, js_seeds=js["seeds"], py_seeds=load("fidelity_py.json")["seeds"])
        md.append(md_table(rows, verdict, label, js["dt"]))
        if fname == "fidelity_js_dt30.json" or "dt30" not in out["summary"]:
            out["summary"]["dt30" if "dt30" in fname else "dt01"] = {s: bool(v) for s, v in verdict.items()}
    if "--negative" in sys.argv:
        neg = {}
        for brk, sp, why in NEGATIVE:
            o = os.path.join(RES, f"neg_{sp}.json")
            subprocess.run(["node", os.path.join(HERE, "fidelity_js.js"), sp, "--break", brk, "--out", o], check=True,
                           stdout=subprocess.DEVNULL)
            rows, verdict = compare(load(f"neg_{sp}.json")["runs"], py, [sp])
            failed = [r["metric"] for r in rows[sp] if not r["agree"]]
            neg[brk] = dict(species=sp, why=why, gate_failed=not verdict[sp], failing_metrics=failed, rows=rows[sp])
            md.append(f"### Negative control `{brk}` ({why})\n\nGate {'FAILED as required' if not verdict[sp] else '**PASSED - the gate has no teeth here**'}"
                      f"; disagreeing metrics: {', '.join(failed) or 'none'}.\n")
        out["negative"] = neg
        out["summary"]["negative_controls_fail"] = all(v["gate_failed"] for v in neg.values())
    json.dump(out, open(os.path.join(RES, "fidelity.json"), "w"), indent=1)
    open(os.path.join(RES, "fidelity.md"), "w").write("\n".join(md))
    print("\n".join(md))
