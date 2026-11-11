"""Tests for the steward loop's pieces: the parent -> child edit record, the evidence pack, operator-aligned CMA blocks.

    python3 Tools/Genome/tests/test_steward.py      (or: python3 -m pytest Tools/Genome/tests)
"""
import json
import os
import random
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.environ.setdefault("AGENOME_EXT", "0")

from agenome.genome import mutate  # noqa: E402
from agenome.handmade import amoebius_like, amoebius_v2  # noqa: E402
from agenome.selection import CMAEmitter  # noqa: E402
from agenome.steward import describe_edit, evidence, genome_diff, render  # noqa: E402


def test_genome_diff_params_and_structure():
    a = amoebius_v2()
    b = a.copy()
    b.nodes[3].params[0] = (b.nodes[3].params[0] + 17) % 256
    d = genome_diff(a, b)
    assert d["kind"] == "params" and d["n"] == 1 and d["changes"][0][0] == 3, d
    assert "->" in describe_edit(d)
    c = mutate(a, random.Random(4), structural=1.0)
    while len(c.nodes) == len(a.nodes) and all(x.op == y.op for x, y in zip(a.nodes, c.nodes)):
        c = mutate(c, random.Random(len(c.nodes)), structural=1.0)
    d2 = genome_diff(a, c)
    assert d2["kind"] == "structure", d2
    assert describe_edit(d2)


def test_operator_aligned_blocks():
    for g in (amoebius_v2(), amoebius_like()):
        for seed in range(4):
            em = CMAEmitter(block=24, blocks="op", seed=seed)
            kids = em.ask({"genome": g, "dist": 2.0}, 6)
            assert kids and 1 <= len(em.slots) <= 24 + max(len(n.params) for n in g.nodes)
            by = {}
            for i, j in em.slots:
                by.setdefault(i, set()).add(j)
            # every chosen operator contributes all of its parameters
            for i, js in by.items():
                assert js == set(range(len(g.nodes[i].params))), (i, js)
            if len(by) > 1:
                assert len(em.slots) <= 24


def test_evidence_pack_from_a_run_folder():
    g = amoebius_v2()
    with tempfile.TemporaryDirectory() as d:
        groups = {k: 1.0 for k in ("color", "luma", "spectrum", "grad", "layout", "motion", "texture", "blobs")}
        log = []
        for gen in range(12):
            log.append({"gen": gen, "t": 6.0 * gen, "evals": 24 * (gen + 1), "best_dist": 3.0 - 0.1 * min(gen, 6),
                        "best_zbytes": 260, "simplest_ok_zbytes": 250, "archive": 9, "in_band": 5,
                        "best_op": "cma" if gen % 2 else "grow", "best_groups": dict(groups, color=3.0 - 0.1 * gen),
                        "ops": {"grow": [18, 5, 0.01], "cma": [6, 1, 0.02]}})
        open(os.path.join(d, "log.jsonl"), "w").write("".join(json.dumps(r) + "\n" for r in log))
        b = g.copy()
        b.nodes[2].params[0] = (b.nodes[2].params[0] + 9) % 256
        imp = [{"gen": 3, "op": "cma", "parent_dist": 2.8, "dist": 2.7, "gain": 0.036, "leader": True,
                "edit": genome_diff(g, b), "groups_delta": {"color": -0.2, "texture": 0.01}}]
        open(os.path.join(d, "improvements.jsonl"), "w").write("".join(json.dumps(r) + "\n" for r in imp))
        rows = [{"genome": g.to_json(values=True), "dist": 2.4 + 0.01 * k, "zbytes": 260, "op": "grow"}
                for k in range(10)]
        json.dump(rows, open(os.path.join(d, "population.json"), "w"))
        json.dump({"survival": "pareto", "tuner": "cma"}, open(os.path.join(d, "args.json"), "w"))
        E = evidence(d)
        assert E["progress"]["last_gain_gen"] == 6 and E["progress"]["stall_gens"] == 5, E["progress"]
        assert E["operators"]["grow"]["children"] == 12 * 18 and E["operators"]["cma"]["leader_gens"] >= 1
        assert E["improvements"]["count"] == 1 and E["improvements"]["by_op"]["cma"]["leader"] == 1
        assert E["bottleneck"][0]["group"] == "color"
        assert any(f.startswith("bottleneck: color") for f in E["flags"]), E["flags"]
        assert E["diversity"]["top_share"] == 1.0 and any(f.startswith("monoculture") for f in E["flags"])
        md = render([E])
        assert "Steward evidence pack" in md and "Improvements" in md and "Leader" in md


if __name__ == "__main__":
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_") and callable(v)]
    for t in tests:
        t()
        print("ok", t.__name__)
    print(f"{len(tests)} passed")
