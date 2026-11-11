"""The steward's evidence pack: what a search run learned, condensed so a reader (a Claude session on a schedule, or
a person) can explain it, turn the explanation into a testable change, and record the verdict in LEDGER.md.

    python3 -m agenome.steward RUN_DIR [RUN_DIR ...] [--out evidence.md] [--top 12]

The search is the fast loop (renders, around the clock); the steward is the slow loop (every few hours):
  observe   this pack: where the improvements came from (operator, which bytes or nodes, which feature groups moved),
            what the leader's distance is made of, what never works, how diverse the survivors are;
  explain   hypotheses that account for it, each with a prediction;
  condense  each hypothesis made usable: operator priors and byte ranges, motifs promoted to operators, genomes
            written by hand, fitness fixes when a measure is gamed;
  test      every change as an A/B arm against a control at equal renders (bench/ab_search.py, or an island A/B);
  record    LEDGER.md: claim, prediction, evidence, verdict; a claim that lost stays there so it is not retried.

The pack needs nothing but the run folder: log.jsonl, improvements.jsonl (agenome.search writes one line per child
that beat its parent and survived), population.json and pareto.json.
"""
import argparse
import collections
import json
import math
import os

import numpy as np

from .features import GROUPS, WEIGHTS


# ---------------------------------------------------------------------- what changed between parent and child
def genome_diff(a, b, max_changes=8):
    """A compact description of how child b differs from parent a: the parameter bytes that moved when the structure
    is the same, else the operators added and removed."""
    from .ops import OPS
    from .selection import structure_key
    if structure_key(a) == structure_key(b):
        ch = []
        for i, (na, nb) in enumerate(zip(a.nodes, b.nodes)):
            for j, (x, y) in enumerate(zip(na.params, nb.params)):
                if x != y:
                    ch.append([i, na.op, OPS[na.op].params[j].name, int(x), int(y)])
        out = {"kind": "params", "n": len(ch), "changes": ch[:max_changes]}
        if a.seed != b.seed:
            out["seed"] = [a.seed, b.seed]
        return out
    ca = collections.Counter(n.op for n in a.nodes)
    cb = collections.Counter(n.op for n in b.nodes)
    return {"kind": "structure", "added": sorted((cb - ca).elements()), "removed": sorted((ca - cb).elements()),
            "nodes": [len(a.nodes), len(b.nodes)]}


def describe_edit(e):
    if not e:
        return "?"
    if e.get("kind") == "params":
        s = ", ".join(f"{op}.{p} {x}->{y}" for _, op, p, x, y in e.get("changes", []))
        more = e.get("n", 0) - len(e.get("changes", []))
        return f"{e.get('n')} bytes: {s}" + (f" (+{more} more)" if more > 0 else "")
    add, rem = e.get("added", []), e.get("removed", [])
    if not add and not rem:
        return f"rewired ({e.get('nodes')[0]} -> {e.get('nodes')[1]} nodes)"
    return ("+" + " +".join(add) if add else "") + (" " if add and rem else "") + ("-" + " -".join(rem) if rem else "")


# ---------------------------------------------------------------------- reading a run
def _jsonl(p):
    if not os.path.exists(p):
        return []
    out = []
    for line in open(p):
        try:
            out.append(json.loads(line))
        except ValueError:
            pass
    return out


def _json(p, default=None):
    try:
        return json.load(open(p))
    except Exception:
        return default


def lineage(d):
    m = (d.get("genome") or {}).get("meta") or {}
    name = m.get("name") or os.path.splitext(os.path.basename(m.get("src", "") or ""))[0] or "?"
    return name.split("__")[0]


def evidence(run, top=12):
    """The evidence pack of one run folder, as a dict (render() turns it into markdown)."""
    L = _jsonl(os.path.join(run, "log.jsonl"))
    if not L:
        return {"run": run, "error": "no log.jsonl"}
    args = _json(os.path.join(run, "args.json"), {}) or {}
    imp = _jsonl(os.path.join(run, "improvements.jsonl"))
    pop = _json(os.path.join(run, "population.json"), []) or []
    front = _json(os.path.join(run, "pareto.json"), []) or []
    E = {"run": run, "gens": L[-1]["gen"] + 1, "evals": L[-1]["evals"], "minutes": round(L[-1]["t"] / 60, 1),
         "setup": {k: args.get(k) for k in ("survival", "parents", "operators", "tuner", "tune", "mu", "lam",
                                            "frames", "judge_w", "critic_w", "stat_w", "window_mode",
                                            "judge_windows", "span", "seed") if k in args}}
    # progress: best dist at quarters of the budget, the last improvement, the stall
    ev = np.array([r["evals"] for r in L])
    best = np.minimum.accumulate(np.array([r["best_dist"] for r in L]))
    E["progress"] = {f"{q}%": round(float(best[min(len(best) - 1, int(np.searchsorted(ev, q / 100 * ev[-1])))]), 4)
                     for q in (25, 50, 75, 100)}
    last = int(np.argmax(best <= best[-1] + 1e-9))
    E["progress"]["last_gain_gen"] = int(L[last]["gen"])
    E["progress"]["stall_gens"] = int(L[-1]["gen"] - L[last]["gen"])
    E["progress"]["anytime"] = round(float(best.mean()), 4)
    # operators over the run (log) and the improvements they made (improvements.jsonl)
    ops = {}
    for r in L:
        for k, v in (r.get("ops") or {}).items():
            a = ops.setdefault(k, {"children": 0, "survived": 0, "gain_sum": 0.0, "leader_gens": 0})
            a["children"] += v[0]
            a["survived"] += v[1]
            if len(v) > 2:
                a["gain_sum"] += v[2] * v[0]
    prev = None
    for r in L:
        if prev is not None and r["best_dist"] < prev - 1e-9 and r.get("best_op") in ops:
            ops[r["best_op"]]["leader_gens"] += 1
        prev = r["best_dist"] if prev is None else min(prev, r["best_dist"])
    for k, a in ops.items():
        a["survival"] = round(a["survived"] / max(1, a["children"]), 3)
        a["mean_gain"] = round(a.pop("gain_sum") / max(1, a["children"]), 5)
    E["operators"] = dict(sorted(ops.items(), key=lambda kv: -kv[1]["children"]))
    if imp:
        by = collections.defaultdict(lambda: {"n": 0, "gain": 0.0, "leader": 0})
        bytes_moved = collections.Counter()
        groups = collections.defaultdict(list)
        for e in imp:
            b = by[e.get("op", "?")]
            b["n"] += 1
            b["gain"] += e.get("gain", 0.0)
            b["leader"] += int(bool(e.get("leader")))
            ed = e.get("edit") or {}
            if ed.get("kind") == "params":
                for _, op, p, x, y in ed.get("changes", []):
                    bytes_moved[f"{op}.{p}"] += 1
            for g, d in (e.get("groups_delta") or {}).items():
                groups[g].append(d)
        E["improvements"] = {
            "count": len(imp),
            "by_op": {k: {"n": v["n"], "gain": round(v["gain"], 4), "leader": v["leader"]}
                      for k, v in sorted(by.items(), key=lambda kv: -kv[1]["gain"])},
            "top": [{"gen": e["gen"], "op": e.get("op"), "gain": round(e.get("gain", 0), 4),
                     "dist": round(e.get("dist", 0), 4), "leader": bool(e.get("leader")),
                     "edit": describe_edit(e.get("edit"))}
                    for e in sorted(imp, key=lambda e: -e.get("gain", 0))[:top]],
            "bytes_moved": bytes_moved.most_common(top),
            "groups_moved": {g: round(float(np.mean(v)), 4) for g, v in sorted(groups.items(),
                                                                             key=lambda kv: np.mean(kv[1]))},
        }
    # what the leader's distance is made of, and how each group moved over the run
    g0, g1 = L[0].get("best_groups") or {}, L[-1].get("best_groups") or {}
    wsum = sum(WEIGHTS.values())
    if g1:
        tot = sum(WEIGHTS[g] * g1.get(g, 0) for g in GROUPS) / wsum or 1.0
        E["bottleneck"] = [{"group": g, "dist": g1.get(g), "share": round(WEIGHTS[g] * g1.get(g, 0) / wsum / tot, 3),
                            "start": g0.get(g)} for g in sorted(GROUPS, key=lambda g: -WEIGHTS[g] * g1.get(g, 0))]
    # compactness and the front
    E["compact"] = {"best_zbytes": L[-1].get("best_zbytes"), "simplest_in_band": L[-1].get("simplest_ok_zbytes"),
                    "archive": L[-1].get("archive"), "in_band": L[-1].get("in_band")}
    # diversity and what the best genomes are made of
    if pop:
        lin = collections.Counter(lineage(d) for d in pop)
        E["diversity"] = {"lineages": len(lin), "top": lin.most_common(3),
                          "top_share": round(lin.most_common(1)[0][1] / len(pop), 3)}
    rows = [d for d in pop + front if isinstance(d, dict) and "genome" in d and d.get("ok", True)]
    if len(rows) >= 8:
        rows.sort(key=lambda d: d.get("dist", 99))
        k = max(3, len(rows) // 4)
        cnt = lambda rs: collections.Counter(n["op"] for d in rs for n in d["genome"]["nodes"])
        hi, lo = cnt(rows[:k]), cnt(rows[k:])
        nh, nl = sum(hi.values()) or 1, sum(lo.values()) or 1
        enr = {op: (hi[op] / nh + 1e-3) / (lo[op] / nl + 1e-3) for op in set(hi) | set(lo)}
        E["ops_in_best"] = {"over": [(op, round(r, 2)) for op, r in sorted(enr.items(), key=lambda kv: -kv[1])[:8]
                                     if hi[op] >= 2],
                            "under": [(op, round(r, 2)) for op, r in sorted(enr.items(), key=lambda kv: kv[1])[:6]
                                      if lo[op] >= 3]}
        lead = rows[0]["genome"]
        E["leader"] = {"dist": rows[0].get("dist"), "zbytes": rows[0].get("zbytes"), "lineage": lineage(rows[0]),
                       "op": rows[0].get("op"),
                       "nodes": [f"{i} {n['op']}({', '.join(f'{k}={v}' for k, v in list(n.get('v', n.get('p', {})).items())[:6])})"
                                 for i, n in enumerate(lead["nodes"])]}
    E["flags"] = flags(E)
    return E


def flags(E):
    """Plain prompts for the steward: the patterns that have needed a decision before."""
    out = []
    p = E.get("progress", {})
    if p.get("stall_gens", 0) >= max(8, E.get("gens", 0) // 4):
        out.append(f"stalled: no better genome for {p['stall_gens']} generations")
    for b in (E.get("bottleneck") or [])[:2]:
        if b["share"] >= 0.25:
            moved = "" if b.get("start") is None else f" (was {b['start']} at the start)"
            out.append(f"bottleneck: {b['group']} is {round(100 * b['share'])}% of the leader's distance{moved}")
    for op, a in (E.get("operators") or {}).items():
        if a["children"] >= 100 and a["survival"] < 0.05:
            out.append(f"operator {op}: {a['children']} children, {round(100 * a['survival'], 1)}% survive")
    d = E.get("diversity")
    if d and d["top_share"] >= 0.5:
        out.append(f"monoculture: {d['top'][0][0]} holds {round(100 * d['top_share'])}% of the survivors")
    imp = E.get("improvements")
    if imp and imp["count"]:
        lead_ops = [k for k, v in imp["by_op"].items() if v["leader"]]
        if lead_ops:
            out.append("leaders came from: " + ", ".join(f"{k} ({imp['by_op'][k]['leader']})" for k in lead_ops))
    return out


def render(packs):
    out = ["# Steward evidence pack", ""]
    for E in packs:
        out.append(f"## {E['run']}")
        if "error" in E:
            out += [E["error"], ""]
            continue
        s = E.get("setup", {})
        out.append(f"{E['gens']} generations, {E['evals']} renders, {E['minutes']} min. Setup: "
                   + ", ".join(f"{k} {v}" for k, v in s.items()))
        out.append("")
        if E["flags"]:
            out.append("**Flags**")
            out += [f"- {f}" for f in E["flags"]]
            out.append("")
        p = E["progress"]
        out.append(f"**Progress** best dist at 25/50/75/100% of the renders: {p['25%']} / {p['50%']} / {p['75%']} / "
                   f"{p['100%']}; anytime {p['anytime']}; last gain at generation {p['last_gain_gen']}")
        out.append("")
        out.append("**Operators** (children, survival, mean gain over the parent, generations it made a new best)")
        for op, a in E["operators"].items():
            out.append(f"- {op}: {a['children']}, {a['survival']}, {a['mean_gain']}, {a['leader_gens']}")
        out.append("")
        imp = E.get("improvements")
        if imp:
            out.append(f"**Improvements** ({imp['count']} children beat their parent and survived)")
            for k, v in imp["by_op"].items():
                out.append(f"- {k}: {v['n']} improvements, total gain {v['gain']}, {v['leader']} made the leader")
            out.append("")
            out.append("Largest:")
            for e in imp["top"]:
                out.append(f"- gen {e['gen']} {e['op']}: -{round(100 * e['gain'], 1)}% to {e['dist']}"
                           f"{' (leader)' if e['leader'] else ''}: {e['edit']}")
            if imp["bytes_moved"]:
                out.append("")
                out.append("Bytes that moved in improving parameter edits: "
                           + ", ".join(f"{k} x{n}" for k, n in imp["bytes_moved"]))
            if imp["groups_moved"]:
                out.append("Mean change per feature group in improvements: "
                           + ", ".join(f"{g} {d:+.3f}" for g, d in imp["groups_moved"].items()))
            out.append("")
        if E.get("bottleneck"):
            out.append("**The leader's distance** (group: distance, share of the total, at the start)")
            out += [f"- {b['group']}: {b['dist']}, {round(100 * b['share'])}%, {b['start']}" for b in E["bottleneck"]]
            out.append("")
        c = E["compact"]
        out.append(f"**Compactness** leader {c['best_zbytes']} B, smallest in the band {c['simplest_in_band']} B, "
                   f"archive {c['archive']}, survivors in the band {c['in_band']}")
        if E.get("diversity"):
            d = E["diversity"]
            out.append(f"**Diversity** {d['lineages']} lineages among the survivors; top: "
                       + ", ".join(f"{k} {n}" for k, n in d["top"]))
        if E.get("ops_in_best"):
            o = E["ops_in_best"]
            out.append("**Operators over-represented in the best quarter**: " + ", ".join(f"{k} x{r}" for k, r in o["over"]))
            if o["under"]:
                out.append("**Under-represented**: " + ", ".join(f"{k} x{r}" for k, r in o["under"]))
        if E.get("leader"):
            ld = E["leader"]
            out.append("")
            out.append(f"**Leader** dist {ld['dist']}, {ld['zbytes']} B, lineage {ld['lineage']}, made by {ld['op']}:")
            out += [f"    {n}" for n in ld["nodes"]]
        out.append("")
    return "\n".join(out)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--out", default=None, help="write the markdown here (default: print it)")
    ap.add_argument("--json", default=None, help="also write the packs as json")
    ap.add_argument("--top", type=int, default=12)
    a = ap.parse_args(argv)
    packs = [evidence(r, a.top) for r in a.runs]
    md = render(packs)
    if a.out:
        open(a.out, "w").write(md)
    else:
        print(md)
    if a.json:
        json.dump(packs, open(a.json, "w"), indent=1, default=str)


if __name__ == "__main__":
    main()
