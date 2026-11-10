"""Tests for agenome.selection (survival, parents, operators, the CMA emitter) and the search's use of them.

    python3 Tools/Genome/tests/test_selection.py      (or: python3 -m pytest Tools/Genome/tests)

No renderer needed: the emitter is driven by a stand-in objective on a real genome's parameter bytes.
"""
import itertools
import os
import random
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.environ.setdefault("AGENOME_EXT", "0")  # the core operator set is enough here

from agenome.selection import (CMAEmitter, Lexicase, Matching, dominates, fronts, hv_contributions, hv_order,  # noqa
                               hypervolume, mad_eps, roi_bound, roi_order, structure_key)


def brute_hv(points, ref, n=400):
    """Dominated area on a grid (for checking the exact sweep)."""
    xs = np.linspace(min([p[0] for p in points] + [ref[0]]), ref[0], n, endpoint=False)
    ys = np.linspace(min([p[1] for p in points] + [ref[1]]), ref[1], n, endpoint=False)
    dx = (ref[0] - xs[0]) / n
    dy = (ref[1] - ys[0]) / n
    X, Y = np.meshgrid(xs + dx / 2, ys + dy / 2)
    dom = np.zeros_like(X, bool)
    for p in points:
        dom |= (X >= p[0]) & (Y >= p[1])
    return dom.sum() * dx * dy


def test_fronts_and_dominance():
    pts = [(1, 5), (2, 3), (3, 1), (2, 4), (4, 4), (1, 5), (5, 5)]
    F = fronts(pts)
    assert F[0] == [0, 5, 1, 2], F  # the duplicate (1, 5) is non-dominated too
    assert set(F[1]) == {3}
    assert set(F[2]) == {4}
    assert F[3] == [6]
    assert dominates((1, 1), (1, 2)) and not dominates((1, 2), (1, 2)) and not dominates((1, 3), (2, 1))


def test_hypervolume_matches_grid():
    rng = np.random.default_rng(3)
    for _ in range(5):
        pts = [tuple(x) for x in rng.uniform(0, 10, (12, 2))]
        ref = (11.0, 12.0)
        assert abs(hypervolume(pts, ref) - brute_hv(pts, ref)) < 0.6, (hypervolume(pts, ref), brute_hv(pts, ref))
    assert hypervolume([(1, 1)], (3, 4)) == 6.0
    assert hypervolume([(5, 5)], (3, 4)) == 0.0


def test_contributions_are_exclusive_areas():
    pts = [(1.0, 5.0), (2.0, 3.0), (4.0, 1.0)]
    ref = (6.0, 7.0)
    c = hv_contributions(pts, ref)
    total = hypervolume(pts, ref)
    for k, ck in enumerate(c):
        rest = pts[:k] + pts[k + 1:]
        assert abs(ck - (total - hypervolume(rest, ref))) < 1e-9, (k, ck)


def test_hv_order_keeps_the_spread():
    # a dense cluster in the middle and two extremes: the extremes and one of the cluster are worth most
    pts = [(1.0, 9.0), (4.9, 5.1), (5.0, 5.0), (5.1, 4.9), (9.0, 1.0)]
    order = hv_order(pts, (10.0, 10.0))
    assert set(order[:3]) >= {0, 4}, order
    assert len(set(order[:3]) & {1, 2, 3}) == 1, order


def test_roi_order():
    # dist, bytes: best is 2.0 (500 B); the band at span 0.05 is dist <= 2.1
    pts = [(2.0, 500), (2.05, 300), (2.08, 200), (2.09, 260), (2.5, 50), (3.0, 40), (2.02, 2000)]
    order = roi_order(pts, span=0.05, size_ref=1500)
    assert order[0] == 0
    band = {0, 1, 2}  # front 1 inside the band; (2.09, 260) is dominated by (2.08, 200)
    assert set(order[:3]) == band, order
    assert order[3] == 3, order  # the dominated band member comes before everything outside
    assert order[4:] == [6, 4, 5], order  # outside the band (incl. over budget) by dist
    # keep_best wins even when the best is over budget
    o2 = roi_order([(1.0, 5000), (1.01, 100), (1.02, 90)], span=0.05, size_ref=1500)
    assert o2[0] == 0 and o2[1:] in ([1, 2], [2, 1])
    # negative losses: the band is above the best, not below it
    assert abs(roi_bound(-2.0, 0.05) - (-1.9)) < 1e-12
    assert roi_bound(0.0, 0.05) == 0.05
    o3 = roi_order([(-2.0, 100), (-1.95, 50), (-1.5, 10)], span=0.05)
    assert o3[:2] == [0, 1] and o3[2] == 2


def test_lexicase():
    rng = random.Random(0)
    # one candidate best everywhere by more than epsilon is always chosen
    E = np.array([[1, 1, 1], [5, 5, 5], [9, 9, 9], [9, 9, 9]], float)
    assert all(Lexicase(E).select(rng) == 0 for _ in range(50))
    # ... but within epsilon (MAD = 1 here) it is a tie: 1 and 2 are both kept, 3 never
    E = np.array([[1, 1, 1], [2, 2, 2], [3, 3, 3]], float)
    assert {Lexicase(E).select(rng) for _ in range(100)} == {0, 1}
    # specialists: each best on one case, the generalist on none; everyone but the generalist gets picked
    E = np.array([[0, 9, 9], [9, 0, 9], [9, 9, 0], [5, 5, 5]], float)
    picks = [Lexicase(E).select(rng) for _ in range(600)]
    counts = np.bincount(picks, minlength=4)
    assert counts[3] == 0 and all(counts[:3] > 120), counts
    # epsilon (MAD) keeps near-ties: on one case with a wide spread, values within MAD of the best both survive
    E = np.array([[1.0], [1.2], [5.0], [9.0]])
    assert abs(mad_eps(E)[0] - 2.0) < 1e-9
    picks = {Lexicase(E).select(rng) for _ in range(200)}
    assert picks == {0, 1}, picks
    # missing cases (nan) are never best
    E = np.array([[np.nan, 1.0], [0.5, 2.0]])
    picks = [Lexicase(E).select(rng) for _ in range(200)]
    assert set(picks) == {0, 1}


def test_matching():
    rng = random.Random(1)
    m = Matching(["a", "b", "c", "d"], p0={"a": 0.4, "b": 0.25, "c": 0.12, "d": 0.23}, p_min=0.1, alpha=0.2)
    for a, p in (("a", 0.4), ("b", 0.25), ("c", 0.12), ("d", 0.23)):
        assert abs(m.p[a] - p) < 1e-9, m.p
    for _ in range(40):
        m.update({"a": [0.0] * 10, "b": [1.0] * 10, "c": [0.1] * 5})  # d gets no children: keeps its quality
        assert abs(sum(m.p.values()) - 1) < 1e-9
        assert min(m.p.values()) >= 0.1 - 1e-12
    # b paid every time, a never, c a little; d had no children and kept its prior quality (rescaled once to the
    # first rewards: their mean was (10 * 1 + 5 * 0.1) / 25 = 0.42 over 4 arms)
    assert m.p["b"] == max(m.p.values()) and m.p["b"] > 0.5 and abs(m.p["a"] - 0.1) < 0.01, m.p
    assert abs(m.Q["d"] - (0.23 - 0.1) / 0.6 * 4 * 0.42) < 1e-9 and abs(m.Q["c"] - 0.1) < 0.01, m.Q
    # tiny rewards move the mix as fast as large ones
    m2 = Matching(["a", "b"], p_min=0.1)
    for _ in range(10):
        m2.update({"a": [0.0, 0.001], "b": [0.01, 0.02]})
    assert m2.p["b"] > 0.75, m2.p
    counts = {a: 0 for a in m.arms}
    for _ in range(4000):
        counts[m.pick(rng)] += 1
    assert abs(counts["b"] / 4000 - m.p["b"]) < 0.03, counts


def test_cma_emitter_tunes_bytes():
    from agenome.handmade import amoebius_v2
    from agenome.tune import param_slots
    g = amoebius_v2()
    slots = param_slots(g)
    rs = np.random.default_rng(5)
    target = {s: int(rs.integers(256)) for s in slots}

    def dist(h):
        return float(np.mean([(h.nodes[i].params[j] - target[(i, j)]) ** 2 for i, j in slots])) / 1000.0

    em = CMAEmitter(block=24, gens_per_block=6, sigma=20.0, seed=0)
    leader = {"genome": g, "dist": dist(g)}
    d0 = leader["dist"]
    for _ in range(60):
        kids = em.ask(leader, 8)
        assert len(kids) == 8
        assert all(structure_key(k) == structure_key(g) for k in kids)  # parameters only
        assert len({k.to_bytes() for k in kids}) == 8  # rounding duplicates are nudged apart
        ds = [dist(k) for k in kids]
        em.tell(ds)
        k = int(np.argmin(ds))
        if ds[k] < leader["dist"]:
            leader = {"genome": kids[k], "dist": ds[k]}
    assert leader["dist"] < 0.6 * d0, (d0, leader["dist"])
    assert em.blocks >= 10  # 60 generations of 6-generation blocks
    # a new structure in the lead ends the block at once
    from agenome.handmade import amoebius_like
    other = amoebius_like()
    b0 = em.blocks
    em.ask({"genome": other, "dist": -1.0}, 8)
    assert em.blocks == b0 + 1 and structure_key(em.anchor) == structure_key(other)


def test_search_parts_wire_up():
    """vary() returns a child and its operator name in each mode; lexicase_matrix has one column per case."""
    from agenome import search
    from agenome.handmade import amoebius_like, amoebius_v2
    rng = random.Random(2)
    a, b = amoebius_like(), amoebius_v2()
    m = Matching(search.ARMS, p0=search.PRIOR_MIX)
    seen = set()
    for _ in range(80):
        child, tag = search.vary(a, rng, search.PARETO, [b], [a], m)
        assert tag in search.ARMS and child.nodes
        seen.add(tag)
    assert seen == set(search.ARMS)
    for regime in (search.COMPLEXIFY, search.COMPRESS, search.PARETO):
        child, tag = search.vary(a, rng, regime, [b], [a], None, 0.25)
        assert tag in ("grow", "shrink") and child.nodes
    cands = [{"groups": {"color": 1.0, "luma": 2.0}, "zbytes": 300, "judges": [1.1, 1.3]},
             {"groups": {"color": 0.5}, "zbytes": 200}]
    E = search.lexicase_matrix(cands)
    assert E.shape == (2, 8 + 2 + 1), E.shape
    assert np.isnan(E[1, 1]) and np.isnan(E[1, 8]) and abs(E[0, -1] - np.log(300)) < 1e-12


if __name__ == "__main__":
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_") and callable(v)]
    for t in tests:
        t()
        print("ok", t.__name__)
    print(f"{len(tests)} passed")
