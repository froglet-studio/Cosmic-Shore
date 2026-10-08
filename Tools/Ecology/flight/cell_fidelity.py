"""The WHOLE-CELL gate: compare the page's FlightWorld 'cell' (cell_js.js) against its Python twin (cell_py.py).

    python Tools/Ecology/flight/cell_fidelity.py                     # full cell + every ablation found in results/
    python Tools/Ecology/flight/cell_fidelity.py --js results/cell_js_neg.json --label "negative: ..."

Two checks, per pilot policy (wander, hunter):
  1. LEVELS. Every ledger entry that is not small in both: hits on the pilot by species; mass flows (eat / destroy /
     steal / haul) by species x creator of the prism (env, the pilot's wake, or another species' laid mass such as a
     kill's skeleton); mean and final populations; crystals. Each row is classed `cross` (one species' mass taken by
     another), `shared` (env or wake: the common pools species compete for) or `own`.
  2. INTERACTION EFFECTS. For each ablation (the cell without one species): effect = full - ablated, per metric. This
     is the part only an ecosystem has: what removing the locusts does to the grazers' intake, and so on. JS and
     Python effects must agree in size, and in sign where either is clearly non-zero.
Tolerance per row: max(abs floor, rel x max(|js|, |py|), 2.5 x combined standard error) (as flight/fidelity.py).
Writes results/cell_fidelity.json and results/cell_fidelity.md.
"""
from __future__ import annotations

import argparse
import glob
import json
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "results")
KINDS = ("eat", "destroy", "steal", "move")
TOL = dict(hits=(1.0, 0.30), flow=(150.0, 0.35), pop=(3.0, 0.15), crystals=(0.5, 0.30))
POLICIES = ("wander", "hunter")


def flat(r):
    """One run -> {metric: value}; absent ledger entries are 0 (a flow that never happened)."""
    m = {"hits total": r["hits_per_min"]}
    for sp in r["species"]:
        m[f"hits {sp}"] = r["hits_by"].get(sp, 0.0)
        m[f"pop_mean {sp}"] = r["pop_mean"][sp]
        m[f"pop_end {sp}"] = r["pop_end"][sp]
        m[f"crystals {sp}"] = r["crystals_per_min"][sp]
    for k in KINDS:
        for a, w in r[k].items():
            for b, v in w.items():
                m[f"{k} {a}<-{b}"] = v
    return m


def mtype(name):
    if name.startswith("hits"): return "hits"
    if name.startswith("pop"): return "pop"
    if name.startswith("crystals"): return "crystals"
    return "flow"


def mclass(name):
    if mtype(name) != "flow":
        return ""
    a, b = name.split(" ", 1)[1].split("<-")
    return "shared" if b in ("env", "wake") else ("own" if a == b else "cross")


def stats(runs, pol):
    rr = [flat(r) for r in runs if r["policy"] == pol]
    keys = set().union(*[set(x) for x in rr]) if rr else set()
    out = {}
    for k in keys:
        xs = [x.get(k, 0.0) for x in rr]
        n = len(xs); mu = sum(xs) / n
        sd = math.sqrt(sum((x - mu) ** 2 for x in xs) / (n - 1)) if n > 1 else 0.0
        out[k] = (mu, sd / math.sqrt(n), n)
    return out


def tol(name, a, b, se):
    ab, rel = TOL[mtype(name)]
    return max(ab, rel * max(abs(a), abs(b)), 2.5 * se)


def compare_levels(js, py):
    rows = []
    for pol in POLICIES:
        J, P = stats(js, pol), stats(py, pol)
        for k in sorted(set(J) | set(P)):
            mj, sj, _ = J.get(k, (0.0, 0.0, 0)); mp, sp_, _ = P.get(k, (0.0, 0.0, 0))
            if max(abs(mj), abs(mp)) < TOL[mtype(k)][0]:
                continue                      # small in both: nothing to compare
            se = math.hypot(sj, sp_); t = tol(k, mj, mp, se)
            rows.append(dict(policy=pol, metric=k, cls=mclass(k), js=round(mj, 2), py=round(mp, 2),
                             diff=round(mj - mp, 2), tol=round(t, 2), ok=abs(mj - mp) <= t))
    return rows


def compare_effects(js_full, py_full, js_ab, py_ab, dropped):
    rows = []
    for pol in POLICIES:
        Jf, Pf, Ja, Pa = stats(js_full, pol), stats(py_full, pol), stats(js_ab, pol), stats(py_ab, pol)
        for k in sorted(set(Jf) | set(Pf)):
            if any(k.endswith(" " + d) or f" {d}<-" in k or k.endswith("<-" + d) for d in dropped):
                continue                      # the dropped species' own rows: trivially "all of it"
            g = lambda S: S.get(k, (0.0, 0.0, 0))
            ej = g(Jf)[0] - g(Ja)[0]; ep = g(Pf)[0] - g(Pa)[0]
            se = math.sqrt(g(Jf)[1] ** 2 + g(Ja)[1] ** 2 + g(Pf)[1] ** 2 + g(Pa)[1] ** 2)
            floor = TOL[mtype(k)][0]
            if max(abs(ej), abs(ep)) < floor or max(abs(ej), abs(ep)) < 2.5 * se:
                continue                      # no clear effect either side
            t = tol(k, ej, ep, se)
            sign_ok = not (abs(ej) > t and abs(ep) > t and (ej > 0) != (ep > 0))
            rows.append(dict(policy=pol, ablation="-" + "-".join(dropped), metric=k, cls=mclass(k),
                             js=round(ej, 2), py=round(ep, 2), diff=round(ej - ep, 2), tol=round(t, 2),
                             ok=abs(ej - ep) <= t and sign_ok))
    return rows


def load(path):
    return json.load(open(path))["runs"]


def md(rows, title, effect=False):
    n_ok = sum(r["ok"] for r in rows)
    out = [f"### {title}: {n_ok}/{len(rows)} agree", "",
           "| pilot | " + ("ablation | " if effect else "") + "metric | class | JS | Python | diff | tol | |",
           "|---|" + ("---|" if effect else "") + "---|---|---:|---:|---:|---:|---|"]
    for r in rows:
        out.append(f"| {r['policy']} | " + (f"{r['ablation']} | " if effect else "") +
                   f"{r['metric']} | {r['cls']} | {r['js']} | {r['py']} | {r['diff']} | {r['tol']} | "
                   f"{'ok' if r['ok'] else '**FAIL**'} |")
    return "\n".join(out) + "\n"


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--js", default=os.path.join(RES, "cell_js.json"))
    ap.add_argument("--py", default=os.path.join(RES, "cell_py.json"))
    ap.add_argument("--label", default=None, help="a negative control: report only, write nothing")
    a = ap.parse_args()
    js, py = load(a.js), load(a.py)
    levels = compare_levels(js, py)
    effects = []
    for pj in sorted(glob.glob(os.path.join(RES, "cell_py_minus_*.json"))):
        jj = pj.replace("cell_py_", "cell_js_")
        if a.label or not os.path.exists(jj):
            continue
        dropped = json.load(open(pj))["drop"]
        effects += compare_effects(js, py, load(jj), load(pj), dropped)
    fails = [r for r in levels + effects if not r["ok"]]
    summ = dict(levels=f"{sum(r['ok'] for r in levels)}/{len(levels)}",
                effects=f"{sum(r['ok'] for r in effects)}/{len(effects)}",
                cross=f"{sum(r['ok'] for r in levels if r['cls'] == 'cross')}/{sum(1 for r in levels if r['cls'] == 'cross')}",
                fails=len(fails))
    print(("NEGATIVE " + a.label + ": " if a.label else "") + json.dumps(summ))
    for r in fails:
        print("  FAIL", r.get("ablation", ""), r["policy"], r["metric"], "js", r["js"], "py", r["py"], "tol", r["tol"])
    if not a.label:
        json.dump(dict(summary=summ, levels=levels, effects=effects), open(os.path.join(RES, "cell_fidelity.json"), "w"), indent=1)
        open(os.path.join(RES, "cell_fidelity.md"), "w").write(
            "## Whole-cell fidelity (cell_js.js vs cell_py.py)\n\n" + md(levels, "Levels") + "\n" +
            (md(effects, "Interaction effects (full cell - ablated cell)", True) if effects else ""))
        print("wrote results/cell_fidelity.json, results/cell_fidelity.md")
