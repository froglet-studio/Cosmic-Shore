"""Write results/sortfeel/ for a sortfeel params file: summary.json + rollout.json (swarm_nca.rollout /
pack, exactly as sort_publish / swarm_gpu's publisher), probe.json (swarm_probe.probe), feel.json.
    python Tools/NCA/sortfeel_publish.py results/sortfeel/params.json
"""
import json, os, sys, time
import torch
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import swarm_nca as sn, swarm_probe, swarm_feel as sf, sortfeel_model as fm

if __name__ == "__main__":
    torch.set_num_threads(4)
    p = sys.argv[1]; out = os.path.dirname(p)
    m = fm.load(p); t0 = time.time()
    data, summary = sn.rollout(m, 240)
    passed, close = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_switch(summary)
    print(f"tests_passed {passed}/8 [{time.time()-t0:.0f}s]", flush=True)
    summary["meta"] = dict(tag="sortfeel", params="params.json", tests_passed=passed,
                           note="sort + flat-bottomed fate wells (no compression -> no sheets) + per-tadpole OU wander.")
    json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
    json.dump(sn.pack(data, 240), open(os.path.join(out, "rollout.json"), "w"))
    pr = swarm_probe.probe(fm.load(p)); print("probe", json.dumps(pr), flush=True)
    json.dump(pr, open(os.path.join(out, "probe.json"), "w"), indent=1)
    f = sf.feel(fm.load(p)); ok, ch = sf.in_band(f)
    f["organic"] = dict(ok=ok, checks=ch, planar_excess=round(sf.planar_excess(f), 3))
    json.dump(f, open(os.path.join(out, "feel.json"), "w"), indent=1)
    print("feel", json.dumps(f["organic"]))
