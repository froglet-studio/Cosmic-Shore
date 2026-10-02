"""Run scorecards for a list of (label, 'module:Class', params) and append them to results/rounds.jsonl.

    python round.py <round-name> '<json list of [label, spec, params]>' [minutes]
"""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(__file__) or ".")
from harness import scorecard

if __name__ == "__main__":
    name = sys.argv[1]; jobs = json.loads(sys.argv[2]); minutes = float(sys.argv[3]) if len(sys.argv) > 3 else 2.0
    out = os.path.join(os.path.dirname(__file__) or ".", "results", "rounds.jsonl")
    for label, spec, params in jobs:
        t = time.time()
        card, runs = scorecard(spec, params, minutes=minutes)
        rec = dict(round=name, label=label, spec=spec, params=params, minutes=minutes, wall_s=round(time.time() - t, 1), card=card)
        with open(out, "a") as fh: fh.write(json.dumps(rec) + "\n")
        print(label, "R", card["R"], json.dumps(card.get("R_terms")), "hits", card["hits_per_min_wander"],
              "cov", card["lane_coverage"], "adapt", card["adapt"], "cpm", card["payoff_per_min"], "var", card["variety"], flush=True)
