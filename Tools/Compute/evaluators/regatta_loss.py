#!/usr/bin/env python3
"""Regatta's LOSS, written down as code: what makes one course good, and what makes a POOL of courses good
(Plan step: time-arena-course; Regatta flywheel round 1, worker W3).

Garrett (2026-11-06): one Regatta level shuffles a pool of courses; a course may sit near 1.25x on its own only if the
spreads CANCEL across the pool, so each hull's mean over the pool is within about 1.10x and no hull is fastest on every
course. This module is the one place those rules live; the search (Tools/Build/regatta_qd.py) and the pool pick call it,
and it reads only a judge RESULT (regatta_judge / regatta_judge_model, the shared contract), so it does not care whether
the races were the game's own code or a model.

COURSE LOSS (lower is better), from the ALONE races (each hull by itself, mean finish over the judge's seeds):

    loss = ln(slowest / fastest)                         the spread of the scored hulls
         + CHAOS_WEIGHT x chaos                          race-to-race noise: each hull's standing (its ln time minus the
                                                         field's mean ln time) per seed, the std over seeds, the mean
                                                         over hulls. A course whose order flips from race to race is
                                                         not balanced, it is a coin. The model backend has no noise: 0.
         + LENGTH_WEIGHT x how far the mean race leaves [LENGTH_BAND] x the reference race (in ln)
         + DNF_WEIGHT per scored hull that does not finish
         + GUARD_WEIGHT per guard failure (rail missing a mouth, a rail or ring through the nucleus or the membrane,
                                           a placement sealing the line: the genome's validity guards)
         + FIELD_WEIGHT x ln(slowest / fastest) of the FIELD race (every hull together) when the judge ran one
           (the engine does; the model prices a field as each hull alone and this term is then left out)

A course PASSES when spread <= PASS_SPREAD (1.25), chaos <= PASS_CHAOS (0.06), no guard fails, the race is in band and
every scored hull finishes.

POOL LOSS, from the pool's courses' per-hull mean finishes:

    pool_loss = ln(max over hulls of the pool mean / min)                 the cancellation; the bar is ln(1.10)
              + ONE_LEADER per hull that is fastest on EVERY course
              + mean of the courses' own losses x COURSE_SHARE           so cancelling never buys bad courses

    python3 Tools/Compute/evaluators/regatta_loss.py --self-test      # the rules, with negative controls
"""
import math
import sys

CHAOS_WEIGHT = 2.0
LENGTH_BAND = (0.8, 1.25)
LENGTH_WEIGHT = 2.0
DNF_WEIGHT = 0.5
GUARD_WEIGHT = 0.5
FIELD_WEIGHT = 0.5
PASS_SPREAD = 1.25
PASS_CHAOS = 0.06
POOL_CANCEL = 1.10
ONE_LEADER = 0.05
COURSE_SHARE = 0.1


def alone_times(result, scored=None):
    """{hull: [finish_s or None per seed]} from the races with a single seat, or (when every race is a field) every
    seat's time (the model backend writes the field flag but prices seats alone)."""
    out = {}
    races = [r for r in result["races"] if len(r["seats"]) == 1] or [r for r in result["races"] if not r.get("field")] \
        or result["races"]
    for r in races:
        for s in r["seats"]:
            if scored and s["hull"] not in scored:
                continue
            out.setdefault(s["hull"], []).append(s["finish_s"])
    return out


def field_spread(result, scored):
    """ln(slowest / fastest) in the engine's field races (mean over races), or None when no real field race ran."""
    if result.get("backend") == "model":
        return None
    vals = []
    for r in result["races"]:
        if not (r.get("field") or r.get("mode") == "field"):
            continue
        t = [s["finish_s"] for s in r["seats"] if s["hull"] in scored and s["finish_s"]]
        if len(t) >= 2:
            vals.append(math.log(max(t) / min(t)))
    return sum(vals) / len(vals) if vals else None


def chaos_of(per_seed, hulls):
    """Mean over hulls of the std over seeds of each hull's standing. 0 with one seed or no noise."""
    n = min(len(per_seed[h]) for h in hulls)
    if n < 2:
        return 0.0
    stands = []
    for k in range(n):
        logs = {h: math.log(per_seed[h][k]) for h in hulls}
        m = sum(logs.values()) / len(logs)
        stands.append({h: logs[h] - m for h in hulls})
    out = []
    for h in hulls:
        xs = [s[h] for s in stands]
        mu = sum(xs) / len(xs)
        out.append(math.sqrt(sum((x - mu) ** 2 for x in xs) / (len(xs) - 1)))
    return sum(out) / len(out)


def course_loss(result, scored, ref_mean, guard_fails=0):
    """Score one course from its judge RESULT. `scored`: the hulls the balance rules hold for; `ref_mean`: the mean race
    (seconds) of the scored hulls on the reference course (today's), for the length band."""
    times = alone_times(result, scored)
    missing = [h for h in scored if h not in times]
    if missing:
        raise ValueError(f"the judge result has no race for {missing}")
    dnf = [h for h in scored if any(t is None for t in times[h])]
    done = [h for h in scored if h not in dnf]
    means = {h: sum(times[h]) / len(times[h]) for h in done}
    if len(done) < 2:
        return {"loss": 50.0 + DNF_WEIGHT * len(dnf), "passes": False, "spread_x": None, "means": means, "dnf": dnf,
                "why": ["fewer than two scored hulls finish"]}
    fastest, slowest = min(means, key=means.get), max(means, key=means.get)
    spread = means[slowest] / means[fastest]
    chaos = chaos_of({h: times[h] for h in done}, done)
    length = (sum(means.values()) / len(means)) / ref_mean
    lo, hi = LENGTH_BAND
    length_pen = max(0.0, math.log(lo) - math.log(length)) + max(0.0, math.log(length) - math.log(hi))
    fs = field_spread(result, scored)
    terms = {"spread": math.log(spread), "chaos": CHAOS_WEIGHT * chaos, "length": LENGTH_WEIGHT * length_pen,
             "dnf": DNF_WEIGHT * len(dnf), "guards": GUARD_WEIGHT * guard_fails,
             "field": 0.0 if fs is None else FIELD_WEIGHT * fs}
    why = []
    if spread > PASS_SPREAD + 1e-9:
        why.append(f"spread {spread:.2f}x")
    if chaos > PASS_CHAOS:
        why.append(f"chaos {chaos:.3f}")
    if guard_fails:
        why.append(f"{guard_fails} guard fails")
    if length_pen > 0:
        why.append(f"race {length:.2f}x")
    if dnf:
        why.append(f"{', '.join(dnf)} do not finish")
    return {"loss": sum(terms.values()), "terms": {k: round(v, 4) for k, v in terms.items()}, "passes": not why,
            "why": why, "spread_x": round(spread, 4), "chaos": round(chaos, 4), "race_length": round(length, 4),
            "fastest": fastest, "slowest": slowest, "means": {h: round(v, 3) for h, v in means.items()}, "dnf": dnf,
            "field_spread": None if fs is None else round(fs, 4), "guard_fails": guard_fails}


def pool_loss(courses, scored):
    """`courses`: [course_loss() dicts]. The pool's cancellation, its leaders and the per-hull means."""
    if not courses:
        return {"loss": float("inf"), "ratio": None}
    if any(any(h not in c["means"] for h in scored) for c in courses):
        return {"loss": float("inf"), "ratio": None, "why": "a hull does not finish some course"}
    means = {h: sum(c["means"][h] for c in courses) / len(courses) for h in scored}
    ratio = max(means.values()) / min(means.values())
    leaders = [c["fastest"] for c in courses]
    always = [h for h in set(leaders) if leaders.count(h) == len(courses)] if len(courses) > 1 else []
    loss = math.log(ratio) + ONE_LEADER * len(always) + COURSE_SHARE * sum(c["loss"] for c in courses) / len(courses)
    return {"loss": loss, "ratio": round(ratio, 4), "passes": ratio <= POOL_CANCEL + 1e-9 and not always
            and all(c["passes"] for c in courses), "means": {h: round(v, 2) for h, v in means.items()},
            "leaders": leaders, "always_fastest": always}


def _result(times_by_seed, backend="model", field=None):
    races = [{"seed": k, "field": False, "seats": [{"hull": h, "finish_s": t} for h, t in row.items()]}
             for k, row in enumerate(times_by_seed)]
    if field:
        races.append({"seed": 99, "field": True, "seats": [{"hull": h, "finish_s": t} for h, t in field.items()]})
    return {"races": races, "backend": backend}


def self_test():
    bad = []
    hulls = ["A", "B", "C"]
    even = course_loss(_result([{"A": 60, "B": 62, "C": 64}]), hulls, 62.0)
    if not (even["passes"] and abs(even["loss"] - math.log(64 / 60)) < 1e-9):
        bad.append(f"regatta_loss: an even course does not pass at its spread ({even})")
    # Negative controls, each must cost more than the even course and fail the pass:
    lopsided = course_loss(_result([{"A": 40, "B": 62, "C": 64}]), hulls, 62.0)
    coin = course_loss(_result([{"A": 55, "B": 62, "C": 70}, {"A": 70, "B": 62, "C": 55}]), hulls, 62.0)
    long_ = course_loss(_result([{"A": 120, "B": 124, "C": 128}]), hulls, 62.0)
    dnf = course_loss(_result([{"A": 60, "B": None, "C": 64}]), hulls, 62.0)
    guard = course_loss(_result([{"A": 60, "B": 62, "C": 64}]), hulls, 62.0, guard_fails=2)
    for name, r in (("a 1.6x spread", lopsided), ("a course whose order flips", coin), ("a race twice as long", long_),
                    ("a hull that does not finish", dnf), ("two guard failures", guard)):
        if r["passes"] or not r["loss"] > even["loss"] + 0.05:
            bad.append(f"regatta_loss: {name} is not penalised ({r['loss']:.3f} vs {even['loss']:.3f})")
    fld = course_loss(_result([{"A": 60, "B": 62, "C": 64}], backend="engine", field={"A": 50, "B": 62, "C": 80}),
                      hulls, 62.0)
    if not fld["loss"] > even["loss"] + 0.1:
        bad.append("regatta_loss: a lopsided field race on the engine backend is not penalised")
    # Pools: two opposite 1.2x courses cancel; two courses with the same leader do not.
    c1 = course_loss(_result([{"A": 60, "B": 66, "C": 72}]), hulls, 66.0)
    c2 = course_loss(_result([{"A": 72, "B": 66, "C": 60}]), hulls, 66.0)
    p = pool_loss([c1, c2], hulls)
    q = pool_loss([c1, c1], hulls)
    if not (p["passes"] and p["ratio"] < 1.01):
        bad.append(f"regatta_loss: two opposite courses do not cancel ({p})")
    if q["passes"] or not q["loss"] > p["loss"] + 0.1 or q["always_fastest"] != ["A"]:
        bad.append(f"regatta_loss: a pool one hull leads everywhere is not penalised ({q})")
    return bad


if __name__ == "__main__":
    problems = self_test()
    print("\n".join(problems) or "regatta_loss --self-test: ok")
    sys.exit(1 if problems else 0)
