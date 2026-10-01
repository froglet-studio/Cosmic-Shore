"""distill: full 16-test eval of a checkpoint with StudentCfg overrides.
    python Tools/NCA/distill_evalcfg.py runs/distill/g2dag/student_03.pt freeze_contested=1 [--out f.json]"""
import json, os, sys
import torch
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import swarm_eval as se, distill_train as dtr
torch.set_num_threads(int(os.environ.get("DISTILL_THREADS", "4")))
args = sys.argv[1:]
out = None
if "--out" in args:
    i = args.index("--out"); out = args[i + 1]; del args[i:i + 2]
st = dtr.load(args[0])
for kv in args[1:]:
    k, v = kv.split("=", 1)
    cur = getattr(st.cfg, k)
    setattr(st.cfg, k, type(cur)(v))
res = se.evaluate(st, full=True, samples=3, log=lambda *a: None)
print(args, "\n" + se.matrix(res)); print(f"PASSED {res['passed']}/{res['feasible']} own {res['own_passed']} std {res['std_passed']} rest {res['rest_passed']}")
if out:
    json.dump(res, open(out, "w"), indent=1)
