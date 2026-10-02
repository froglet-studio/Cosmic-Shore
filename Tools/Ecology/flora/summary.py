"""Markdown tables for DISCOVERIES from results/: per-species best scorecard (search), search trajectory, element
variants, re-route, cost. python summary.py > results/summary.md"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__)); RES = os.path.join(HERE, "results")
NAMES = ("snaptrap", "spores", "physarum", "coral", "walker")


def load(p):
    """Rows that predate R_hard are re-scored from their card, so every row ranks on obj = R + R_hard."""
    sys.path.insert(0, HERE)
    from harness import replay_score_hard
    rows = [json.loads(l) for l in open(p)] if os.path.exists(p) else []
    for r in rows:
        if True:                                     # re-score every row on the CURRENT stretch bar
            r["R_hard"] = replay_score_hard(dict(r["card"])); r["obj"] = r["R"] + r["R_hard"]
    return rows


def f(x, n=2):
    return "-" if x is None else (f"{x:.{n}f}" if isinstance(x, float) else str(x))


def main():
    print("| species | start R | best R | best R_hard | evals | hits/min (wander) | reader/wander | telegraph p10 s | crystals/burn | twin variety | access | lane cov | route-bias x | prisms end |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for n in NAMES:
        rows = load(os.path.join(RES, f"search_{n}.jsonl"))
        if not rows: continue
        b = max(rows, key=lambda r: r["obj"])
        c = b["card"]
        print(f"| {n} | {f(rows[0]['R'])} | {f(b['R'])} | {f(b.get('R_hard'))} | {len(rows)} | {f(c['hits_per_min_wander'])} | {f(c['counterplay'])} | "
              f"{f(c['telegraph_p10'])} | {f(c['crystals_per_burn'])} | {f(c['variety'])} | {f(c['avoid_cost'])} | {f(c['lane_coverage'])} | "
              f"{f(c['adapt'])} | {f(c['prisms_end'], 0)} |")
    el = json.load(open(os.path.join(RES, "elements.json"))) if os.path.exists(os.path.join(RES, "elements.json")) else {}
    if el:
        print("\n| species | element | R | R_hard | hits/min | telegraph p10 s | reader/wander | crystals/burn | prisms end |")
        print("|---|---|---|---|---|---|---|---|---|")
        for n in NAMES:
            for e, r in el.get(n, {}).items():
                c = r["card"]
                print(f"| {n} | {e} | {f(r['R'])} | {f(r['R_hard'])} | {f(c['hits_per_min_wander'])} | {f(c['telegraph_p10'])} | {f(c['counterplay'])} | {f(c['crystals_per_burn'])} | {f(c['prisms_end'], 0)} |")
    rr = json.load(open(os.path.join(RES, "reroute.json"))) if os.path.exists(os.path.join(RES, "reroute.json")) else {}
    if rr:
        print("\n| species | pre-cut threat in ball (3 seeds) | t50 s (median) | t80 s (median) | end fraction at +90 s |")
        print("|---|---|---|---|---|")
        for n in NAMES:
            if n in rr:
                r = rr[n]
                print(f"| {n} | {[x.get('pre') for x in r['runs']]} | {f(r['t50_med'], 1)} | {f(r['t80_med'], 1)} | {f(r['end_frac_med'])} |")


if __name__ == "__main__":
    main()
