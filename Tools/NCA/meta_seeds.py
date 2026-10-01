"""Robustness for the meta direction: the shared yardstick (swarm_nca.rollout + tests_passed) on
several rollout seeds, not just the default 7, for G2, the designed homeostat on frozen G2, and a
meta rule. Also reports, per seed, whether each switch test's wanted plan is even the REAL majority
after the yardstick's cull (when it is not, a majority-following swarm cannot pass that test).

    NCA_THREADS=1 python Tools/NCA/meta_seeds.py --rule runs/meta/m3/rule_00500.pt --seeds 7 8 9 10 11
"""
import argparse, json, os, sys
import torch
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn
import meta_swarm as ms
from meta_oracle import OracleMeta


def models(rule_path):
    g2p = os.path.join(HERE, "results", "swarm_coevo_g2", "rule.pt")
    g2 = sn.load_rule(g2p)
    st = torch.load(g2p, weights_only=False)
    orc = OracleMeta(ms.MetaWorld(learned_lay=1, p_meta=1.0), hidden=st["hidden"]); orc.load_g2(st["rule"]); orc.targets = sn.load_targets()
    out = {"G2": g2, "oracle_on_G2": orc}
    if rule_path:
        out[os.path.basename(os.path.dirname(rule_path)) + "@" + os.path.basename(rule_path)] = ms.load_meta(rule_path)
    for m in out.values():
        m.eval(); m.events = None
    return out


def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--rule", default=""); ap.add_argument("--seeds", type=int, nargs="+", default=[7, 8, 9, 10, 11])
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    res = {}
    for name, m in models(a.rule).items():
        rows = []
        for sd in a.seeds:
            _, s = sn.rollout(m, 240, seed=sd)
            p, c = sn.tests_passed(s)
            own = sum(1 for k in sn.KINDS if min(s["cross"][k], key=s["cross"][k].get) == k)
            sw_ok = {k: min(v["cross"], key=v["cross"].get) == v["to"] for k, v in s["switch"].items()}
            rows.append(dict(seed=sd, passed=p, close=round(c, 1), own=own, switch=sw_ok,
                             majority_after=[v["majority"] for v in s["switch"].values()]))
            print(name, rows[-1], flush=True)
        res[name] = dict(rows=rows, mean_passed=sum(r["passed"] for r in rows) / len(rows))
        print(f"== {name}: mean tests passed {res[name]['mean_passed']:.2f} over seeds {a.seeds}", flush=True)
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)


if __name__ == "__main__":
    main()
