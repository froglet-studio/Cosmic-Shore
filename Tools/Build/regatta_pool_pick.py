#!/usr/bin/env python3
"""Pick Regatta's shuffled course POOL from the pool search's archive (Plan step: time-arena-course).

One Regatta level shuffles between several balanced courses (Garrett, 2026-11-06). His pass rule: a course may sit near
1.25x on its own only if the spreads CANCEL across the pool, so

  1. every course in the pool PASSES on its own: spread (slowest / fastest scored hull) <= --pass-spread (1.25), chaos
     across seeds <= --pass-chaos (0.06), every rail through every mouth, the race 0.8-1.25x today's, every hull home;
  2. the pool CANCELS: each scored hull's mean seconds over the pool within --cancel (1.10) x of every other's;
  3. no hull is the fastest on EVERY course of the pool;
  4. among the pools that do all that, the courses are as far apart as possible in descriptor space (corner sharpness,
     mean corner, ridable rail, race length, mouth, gaps, crystals; each scaled to the candidates' own range): the
     pool maximises its closest pair's distance, so the shuffle feels like different races.

Candidates are the cells of one or more MAP-Elites archives (`archive.json`, Tools/Compute/evaluators/regatta_pool.py's
metrics) and any `best.json` / local_run result files (the sep-CMA-ES restarts). Every number is recomputed from each
candidate's per-hull seconds for the hulls --scored names (default: the evaluator's scored hulls), so a different hull
set or pass line needs no new search. Every number is a MODEL's (the measured roster), never a real-game race.

    python3 Tools/Build/regatta_pool_pick.py <archive.json|best.json ...> --out pool.json [--min 4 --max 8]
    python3 Tools/Build/regatta_pool_pick.py ... --render <dir>      # and draw every picked course (regatta_pool_render.py)
    python3 Tools/Build/regatta_pool_pick.py --self-test

When no pool passes, it says which rule failed and writes the CLOSEST pool (lowest per-hull mean ratio among pools of
passing courses, else among all courses) under "closest", so the gap can be read; "pool" is then empty.
A READER tool: it writes the pool file and stills where it is told and nothing into the project.
"""
import argparse
import itertools
import json
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DESC_KEYS = ("sharpness", "mean_turn", "ridable", "race_length", "mouth", "gaps", "crystals")


# ── candidates ──────────────────────────────────────────────────────────────

def load(paths):
    """Every candidate course in the given archives / result files: [{params, metrics, descriptors, source}]."""
    out = []
    for p in paths:
        doc = json.load(open(p, encoding="utf-8"))
        if "cells" in doc:
            for cell, e in doc["cells"].items():
                out.append({"params": e.get("params"), "metrics": e.get("metrics") or {},
                            "descriptors": e.get("descriptors") or (e.get("metrics") or {}).get("descriptors") or {},
                            "source": f"{os.path.basename(os.path.dirname(p)) or p} cell {cell}"})
        elif "entries" in doc:
            out.extend(dict(e, source=e.get("source", p)) for e in doc["entries"])
        elif doc.get("params") and doc.get("metrics"):
            out.append({"params": doc["params"], "metrics": doc["metrics"],
                        "descriptors": doc.get("descriptors") or doc["metrics"].get("descriptors") or {}, "source": p})
    seen, uniq = set(), []
    for c in out:
        if not c.get("params") or not (c.get("metrics") or {}).get("seconds"):
            continue
        key = json.dumps({k: round(float(v), 3) for k, v in sorted(c["params"].items())})
        if key not in seen:
            seen.add(key)
            uniq.append(c)
    return uniq


def judge(c, scored, a):
    """Recompute one candidate's spread, leader and pass from its seconds for `scored`; None when a hull is missing."""
    m = c["metrics"]
    secs = m["seconds"]
    if any(h not in secs for h in scored):
        return None
    sc = {h: float(secs[h]) for h in scored}
    fastest, slowest = min(sc, key=sc.get), max(sc, key=sc.get)
    spread = sc[slowest] / sc[fastest]
    why = []
    if spread > a.pass_spread + 1e-9:
        why.append(f"spread {spread:.2f}x")
    chaos = m.get("chaos")
    if chaos is None or chaos > a.pass_chaos:
        why.append("chaos " + ("unmeasured" if chaos is None else f"{chaos:.3f}"))
    if m.get("rail_fails"):
        why.append(f"{m['rail_fails']} rail misses")
    rl = m.get("race_length", 1.0)
    if not (0.8 - 1e-9 <= rl <= 1.25 + 1e-9):
        why.append(f"race {rl:.2f}x")
    if any(h in scored for h in m.get("dnf") or ()):
        why.append("a hull does not finish")
    return dict(c, sc=sc, fastest=fastest, slowest=slowest, spread=spread, passes=not why, why=why)


def vectors(cands):
    """Descriptor vectors scaled to [0, 1] by the candidates' own range (a constant axis drops out)."""
    if not cands:
        return cands
    rng = {}
    for k in DESC_KEYS:
        vals = [float(c["descriptors"].get(k, 0.0)) for c in cands]
        lo, hi = min(vals), max(vals)
        rng[k] = (lo, hi - lo) if hi - lo > 1e-9 else None
    for c in cands:
        c["vec"] = [(float(c["descriptors"].get(k, 0.0)) - r[0]) / r[1] for k, r in rng.items() if r]
    return cands


def dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a["vec"], b["vec"])))


# ── a pool ──────────────────────────────────────────────────────────────────

def pool_stats(pool, scored):
    means = {h: sum(c["sc"][h] for c in pool) / len(pool) for h in scored}
    ratio = max(means.values()) / min(means.values())
    leaders = [c["fastest"] for c in pool]
    one_leader = len(set(leaders)) == 1
    closest = min((dist(a, b) for a, b in itertools.combinations(pool, 2)), default=0.0)
    return {"means": means, "ratio": ratio, "leaders": leaders, "one_leader": one_leader, "closest": closest}


def score(pool, scored, a):
    """Feasible pools rank above every infeasible one; feasible by their closest pair, infeasible by how far off."""
    s = pool_stats(pool, scored)
    excess = max(0.0, math.log(s["ratio"]) - math.log(a.cancel)) + (0.05 if s["one_leader"] else 0.0)
    return (1.0 + s["closest"]) if excess == 0 else -excess, s


def search(cands, k, scored, a, rng):
    """The best pool of k courses: greedy starts (farthest-first, by cancellation) refined by swaps."""
    if len(cands) < k:
        return None
    best = None
    for start in range(a.restarts):
        pool = [rng.choice(cands)]
        while len(pool) < k:
            rest = [c for c in cands if c not in pool]
            # even starts add the course that best repairs the cancellation (tie: farthest), odd ones farthest-first
            def gain(c):
                sc, _ = score(pool + [c], scored, a)
                return (sc, min(dist(c, p) for p in pool))
            pool.append(max(rest, key=gain) if start % 2 == 0 else
                        max(rest, key=lambda c: min(dist(c, p) for p in pool)))
        cur, _ = score(pool, scored, a)
        improved = True
        while improved:
            improved = False
            for i in range(k):
                for c in cands:
                    if c in pool:
                        continue
                    trial = pool[:i] + [c] + pool[i + 1:]
                    sc, _ = score(trial, scored, a)
                    if sc > cur + 1e-12:
                        pool, cur, improved = trial, sc, True
        if best is None or cur > best[0]:
            best = (cur, pool)
    return best


def entry(c, i):
    m = c["metrics"]
    return {"name": f"Course {i + 1}", "source": c.get("source"), "params": c["params"],
            "settings": m.get("settings"), "generator_seed": m.get("generator_seed"),
            "seed_step": m.get("seed_step", c["params"].get("seed")), "neutral_share": m.get("neutral_share"),
            "spread_x": round(c["spread"], 3), "fastest": c["fastest"], "slowest": c["slowest"],
            "chaos": m.get("chaos"), "descriptors": c["descriptors"], "passes": c["passes"], "why_not": c["why"],
            "metrics": dict(m, spread_x=round(c["spread"], 4), fastest=c["fastest"], slowest=c["slowest"],
                            scored=list(c["sc"]))}


def pick(cands, a):
    scored = a.scored or sorted(set.intersection(*[set(c["metrics"].get("scored") or c["metrics"]["seconds"])
                                                   for c in cands])) if cands else []
    judged = [j for j in (judge(c, scored, a) for c in cands) if j]
    passing = vectors([j for j in judged if j["passes"]]) if judged else []
    rng = random.Random(a.seed)
    report = {"scored": scored, "candidates": len(judged), "passing": len(passing),
              "rules": {"pass_spread": a.pass_spread, "pass_chaos": a.pass_chaos, "cancel": a.cancel,
                        "pool_size": [a.min, a.max]},
              "model": "measured roster (regatta_measured_roster.py); every number a MODEL, not a real-game race",
              "pool": [], "closest": None, "verdict": ""}
    fails = {}
    for j in judged:
        for w in j["why"]:
            key = "rails" if w[0].isdigit() else ("finish" if w.startswith("a hull") else w.split(" ")[0])
            fails[key] = fails.get(key, 0) + 1
    report["why_courses_fail"] = fails
    per_k = {}
    for k in range(min(a.max, len(passing)), a.min - 1, -1):
        r = search(passing, k, scored, a, rng)
        if r:
            per_k[k] = r
    feasible = {k: r for k, r in per_k.items() if r[0] > 0}
    if feasible:
        k = max(feasible)
        cur, pool = feasible[k]
        s = pool_stats(pool, scored)
        report["pool"] = [entry(c, i) for i, c in enumerate(pool)]
        report["pool_stats"] = _stats_out(s)
        report["verdict"] = (f"a pool of {k} passing courses cancels: per-hull means within {s['ratio']:.3f}x, "
                             f"{len(set(s['leaders']))} different leaders, closest pair {s['closest']:.2f} apart")
        report["other_sizes"] = {str(kk): {"closest": round(pool_stats(r[1], scored)["closest"], 3),
                                           "ratio": round(pool_stats(r[1], scored)["ratio"], 3)}
                                 for kk, r in feasible.items() if kk != k}
        return report
    # nothing cancels: the closest pool among passing courses, else among every course (so the gap is visible)
    base = passing if len(passing) >= a.min else vectors(list(judged))
    if len(base) >= a.min:
        r = search(base, a.min, scored, a, rng)
        s = pool_stats(r[1], scored)
        report["closest"] = {"from": "passing courses" if base is passing else "every course (too few pass)",
                             "pool": [entry(c, i) for i, c in enumerate(r[1])], "pool_stats": _stats_out(s)}
        worst = max(s["means"], key=s["means"].get), min(s["means"], key=s["means"].get)
        report["verdict"] = (f"NO pool passes: {len(passing)} of {len(judged)} courses pass on their own"
                             + (f" (most fail on: {', '.join(f'{k} {v}' for k, v in sorted(fails.items(), key=lambda kv: -kv[1]))})" if fails else "")
                             + f"; the closest pool of {a.min} has per-hull means {s['ratio']:.3f}x apart "
                             f"({worst[0]} slowest, {worst[1]} fastest)" + (", one hull leads every course" if s["one_leader"] else ""))
    else:
        report["verdict"] = f"NO pool: only {len(judged)} candidate courses"
    return report


# ── lifeforms: a pairing for variety, never a score ─────────────────────────

def pair_lifeforms(entries, seed=1, extra=48):
    """Give every pool entry a lifeform setting (brief 11, Tools/Build/regatta_lifeforms.py) so the shuffle's courses
    differ in what grows along them too: no lifeforms, the four presets and `extra` decoded points, chosen greedily so
    the pool's settings are as far apart as possible in regatta_lifeforms.descriptors. The race model cannot see any
    lifeform (race_models.py has no such term), so this pairing changes no number above: it is variety Garrett judges
    by flying. Named settings (none and the presets) come first, searched points only past five entries. Returns a note; does nothing (and says so) when regatta_lifeforms is not on this branch yet."""
    try:
        import regatta_lifeforms as RL
    except ImportError:
        return "lifeforms: regatta_lifeforms.py is not on this branch (draft PR #1783); entries carry none"
    rng = random.Random(seed)
    options = [("none", RL.NONE, None)] + [(k, v, None) for k, v in RL.PRESETS.items()]
    for _ in range(extra if len(entries) > len(options) else 0):   # named settings first; searched ones past five
        x = [rng.random() for _ in RL.SPACE]
        options.append(("searched", RL.decode(x), [round(u, 4) for u in x]))
    keys = ("plants_per_leg", "forest_prisms", "mean_offset", "all_round", "species", "creatures", "creatures_cross_line")
    vecs = []
    for _, lf, _ in options:
        d = RL.descriptors(lf)
        vecs.append([float(d[k]) for k in keys])
    span = [max(v[i] for v in vecs) - min(v[i] for v in vecs) or 1.0 for i in range(len(keys))]
    norm = [[x / span[i] for i, x in enumerate(v)] for v in vecs]
    d2 = lambda a, b: math.sqrt(sum((x - y) ** 2 for x, y in zip(norm[a], norm[b])))
    chosen = [0] if len(entries) > len(RL.PRESETS) else [1]     # a big pool keeps one bare course
    while len(chosen) < len(entries):
        rest = [i for i in range(len(options)) if i not in chosen]
        chosen.append(max(rest, key=lambda i: (min((d2(i, j) for j in chosen), default=0.0), i <= len(RL.PRESETS))))
    for e, i in zip(entries, chosen):
        name, lf, x = options[i]
        e["lifeforms"] = {"preset": name, "unit_point": x, "descriptors": RL.descriptors(lf),
                          "note": "variety only: the race model cannot see lifeforms"}
    return f"lifeforms: paired {len(entries)} entries ({', '.join(options[i][0] for i in chosen)})"


def _stats_out(s):
    return {"per_hull_mean_seconds": {h: round(v, 2) for h, v in sorted(s["means"].items(), key=lambda kv: kv[1])},
            "mean_ratio": round(s["ratio"], 4), "leaders": s["leaders"], "one_leader": s["one_leader"],
            "closest_pair": round(s["closest"], 3)}


# ── self-test ───────────────────────────────────────────────────────────────

def _fake(i, secs, desc, chaos=0.02):
    return {"params": {"seed": i}, "source": f"fake {i}",
            "metrics": {"seconds": secs, "scored": sorted(secs), "chaos": chaos, "rail_fails": 0, "race_length": 1.0,
                        "dnf": []},
            "descriptors": dict({"sharpness": 0, "mean_turn": 0, "ridable": 1, "race_length": 1, "mouth": 100}, **desc)}


def self_test():
    bad = []
    a = argparse.Namespace(pass_spread=1.25, pass_chaos=0.06, cancel=1.10, min=4, max=6, restarts=6, seed=1, scored=None)
    # Four courses each 1.2x, each won by a different hull: the spreads cancel, so they make a pool.
    hulls = ["A", "B", "C", "D"]
    cancel = []
    for i in range(4):
        secs = {h: 100.0 for h in hulls}
        secs[hulls[i]] = 100.0 / 1.2
        cancel.append(_fake(i, secs, {"sharpness": 30 * i, "mouth": 60 + 10 * i}))
    r = pick(cancel, a)
    if len(r["pool"]) != 4 or r["pool_stats"]["mean_ratio"] > 1.10:
        bad.append(f"regatta_pool_pick: four cancelling courses make no pool ({r['verdict']})")
    # Negative control 1: the same four courses all won by hull A never cancel and one hull leads every course.
    lead = []
    for i in range(4):
        secs = {h: 100.0 for h in hulls}
        secs["A"] = 100.0 / 1.2
        lead.append(_fake(10 + i, secs, {"sharpness": 30 * i}))
    r = pick(lead, a)
    if r["pool"] or not (r["closest"] and r["closest"]["pool_stats"]["one_leader"]):
        bad.append(f"regatta_pool_pick: a pool one hull leads every course was accepted ({r['verdict']})")
    # Negative control 2: a chaotic course and a 1.4x course never enter a pool, even when they would cancel.
    noisy = cancel[:3] + [_fake(20, dict(cancel[3]["metrics"]["seconds"]), {"sharpness": 90}, chaos=0.2)]
    if pick(noisy, a)["pool"]:
        bad.append("regatta_pool_pick: a chaotic course entered the pool")
    wide = cancel[:3] + [_fake(21, {"A": 100, "B": 100, "C": 100, "D": 100 / 1.4}, {"sharpness": 90})]
    if pick(wide, a)["pool"]:
        bad.append("regatta_pool_pick: a 1.4x course entered the pool")
    # Diversity: with a duplicate of course 0 available, the pool keeps the four different ones.
    dup = cancel + [_fake(30, dict(cancel[0]["metrics"]["seconds"]), dict(cancel[0]["descriptors"], sharpness=1))]
    r = pick(dup, dict_ns(a, max=4))
    srcs = sorted(e["source"] for e in r["pool"])
    if srcs != sorted(c["source"] for c in cancel) and not (r["pool"] and r["pool_stats"]["closest_pair"] > 0.3):
        bad.append(f"regatta_pool_pick: the pool kept a near-duplicate ({srcs})")
    return bad


def dict_ns(ns, **kw):
    return argparse.Namespace(**dict(vars(ns), **kw))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("files", nargs="*")
    ap.add_argument("--out", default="pool.json")
    ap.add_argument("--min", type=int, default=4)
    ap.add_argument("--max", type=int, default=8)
    ap.add_argument("--pass-spread", type=float, default=1.25)
    ap.add_argument("--pass-chaos", type=float, default=0.06)
    ap.add_argument("--cancel", type=float, default=1.10)
    ap.add_argument("--scored", nargs="*", help="the hulls the rules hold for (default: the evaluator's scored hulls)")
    ap.add_argument("--restarts", type=int, default=12)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--lifeforms", action="store_true", help="pair each picked course with a lifeform setting (variety)")
    ap.add_argument("--render", help="draw every picked (or closest) course into this folder")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        problems = self_test()
        print("\n".join(problems) or "regatta_pool_pick --self-test: ok")
        return 1 if problems else 0
    cands = load(a.files)
    report = pick(cands, a)
    if a.lifeforms:
        target = report["pool"] or (report["closest"] or {}).get("pool") or []
        report["lifeforms"] = pair_lifeforms(target, a.seed)
        print(report["lifeforms"])
    with open(a.out, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=1)
    print(report["verdict"])
    if a.render:
        sys.path.insert(0, HERE)
        import regatta_pool_render as R
        entries = report["pool"] or (report["closest"] or {}).get("pool") or []
        paths = [R.draw(e, os.path.join(a.render, f"course_{i + 1:02d}.png"), e["name"]) for i, e in enumerate(entries)]
        if paths:
            print(R.sheet(paths, os.path.join(a.render, "sheet.png")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
