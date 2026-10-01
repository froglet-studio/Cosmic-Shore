"""Sweep hgrid2_model configs through the per-term diagnosis. Each arg is one config "k=v,k=v".
    python Tools/NCA/hgrid2_sweep.py "k_fine=1" "k_fine=2,sigma=2" ... [--seeds 7,108]"""
import sys, time, json, torch
import swarm_nca as sn, hgrid2_model as hm, hgrid2_diag as hd
torch.set_num_threads(int(__import__("os").environ.get("NT", "1")))
args = [a for a in sys.argv[1:] if not a.startswith("--")]
seeds = (7,)
kinds = sn.KINDS
for a in sys.argv[1:]:
    if a.startswith("--seeds="):
        seeds = tuple(int(x) for x in a.split("=")[1].split(","))
    if a.startswith("--kinds="):
        kinds = tuple(a.split("=")[1].split(","))
for a in args:
    kw = dict(kv.split("=") for kv in a.split(",") if kv)
    if kw.pop("best", None):
        import hgrid2_eval
        kw = dict(hgrid2_eval.BEST, **kw)
    m = hm.make(**kw)
    t0 = time.time()
    r = hd.diag(m, seeds=seeds, kinds=kinds, log=lambda k, r: None)
    worst = max(r[k]["full"] for k in kinds)
    print(f"{a:40s} " + " | ".join(f"{k[:2]} {r[k]['pos']:.1f}/{r[k]['+elem']:.1f}/{r[k]['+dom']:.1f}/{r[k]['full']:.1f} n{r[k]['n']:.0f}" for k in kinds)
          + f" | worst {worst:.1f} {time.time()-t0:.0f}s", flush=True)
