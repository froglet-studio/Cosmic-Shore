"""Quick yardstick of a posinfo2 rule: swarm_eval full at one seed + lossless/cost/feel (scorecard pieces)."""
import json, sys, time
import torch
import swarm_eval as se, scorecard as scd
import posinfo2_rule as p2

if __name__ == "__main__":
    torch.set_num_threads(4)
    path = sys.argv[1]; seed = int(sys.argv[2]) if len(sys.argv) > 2 else 7
    kw = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
    rule = p2.load(path, **kw); rule.eval()
    t = time.time()
    res = se.evaluate(rule, seed=seed, samples=3, full=True)
    print(se.matrix(res)); print(f"PASSED {res['passed']}/{res['feasible']} na={res['na']} {time.time()-t:.0f}s")
    print({k: res['own'][k]['cross'][k] for k in res['own']})
    print({k: (v['cross'], v['rate']) for k, v in res['switch'].items()})
    if '--lossless' in sys.argv:
        ll = scd.lossless_and_cost(rule, seed=seed)
        print("lossless", ll['lossless'], ll['deaths'], ll['ms_per_step'], ll['grown_n'])
