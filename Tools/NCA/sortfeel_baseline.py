"""Accuracy of the published sort at seeds 7/23/41 (same evaluate call as the sortfeel scorecard)."""
import json, sys
from multiprocessing import Pool
def f(s):
    import torch; torch.set_num_threads(1)
    import swarm_eval as se
    r = se.evaluate(se.load_model("sort:results/sort/params.json"), seed=s, samples=3, full=True, log=lambda *a: None)
    return s, dict(passed=r["passed"], feasible=r["feasible"], own={k: r["own"][k]["cross"][k] for k in r["own"]},
                   fails={k: v["cross"][k.split("->")[1]] for k, v in r["switch"].items() if not v["ok"]})
if __name__ == "__main__":
    with Pool(3) as p: R = p.map(f, [7, 23, 41])
    json.dump({str(k): v for k, v in R}, open("results/sortfeel/sort_baseline_accuracy.json", "w"), indent=1)
    print(json.dumps({str(k): v for k, v in R}))
