"""Write results/creature/: the shared yardsticks on the creature model.

  summary.json      swarm_nca.rollout + tests_passed (the old 8-test yardstick, seed 7, unchanged)
  probe.json        swarm_probe.probe (the shared vessel-strike probe, seed 11, unchanged) plus the
                    8-seed version (creature_heal) with the residual-damage metric, beside evo's
  eval16.json       swarm_eval.evaluate --full (16 transitions, fair cull, 3 samples)
  interaction.json  creature_probe (fly-by / ram / circle / chase, shell on vs off)
  extra.json        creature_extra (switch latency + tell, escort)
  params.json       the shell's configuration (the body is results/evo/genome.npy over G2's rule.pt)

Steps that already ran (runs/creature/*.json) are copied rather than re-run when --reuse is given.

    python Tools/NCA/creature_publish.py [--reuse]
"""
import argparse
import json
import os
import shutil
import sys

import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import swarm_probe  # noqa: E402
import creature_model as cm  # noqa: E402

OUT = os.path.join(HERE, "results", "creature")
RUNS = os.path.join(HERE, "runs", "creature")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--reuse", action="store_true")
    a = ap.parse_args()
    torch.set_num_threads(4)
    os.makedirs(OUT, exist_ok=True)
    m = cm.CreatureRule()
    data, summary = sn.rollout(m, 240)
    passed, close = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_switch(summary)
    print(f"old yardstick tests_passed {passed}/8 (summed divergence {close:.1f})")
    summary["meta"] = dict(tag="creature", body="results/evo/genome.npy over results/swarm_coevo_g2/rule.pt",
                           tests_passed=passed, note="evo body + elastic vessel-reaction shell (creature_model.py)")
    json.dump(summary, open(os.path.join(OUT, "summary.json"), "w"), indent=1)
    pr = swarm_probe.probe(cm.CreatureRule())
    print("strike probe heal", {k: v["heal"] for k, v in pr.items()})
    probe = dict(seed11=pr)
    for name in ("heal_", "heal_evo", "heal_setwound_elem0", "heal_setwound_reach6"):
        p = os.path.join(RUNS, name + ".json")
        if os.path.exists(p):
            r = json.load(open(p))
            probe[{"heal_": "eight_seeds_wounds_on", "heal_evo": "eight_seeds_evo_and_shipped",
                   "heal_setwound_elem0": "eight_seeds_wounds_any_element",
                   "heal_setwound_reach6": "eight_seeds_wounds_reach6"}[name]] = {kk: r[kk] for kk in ("mean", "residual", "residual_overall")}
    json.dump(probe, open(os.path.join(OUT, "probe.json"), "w"), indent=1)
    for src, dst in (("eval16_v2.json", "eval16.json"), ("probe_v2.json", "interaction.json"), ("extra.json", "extra.json")):
        p = os.path.join(RUNS, src)
        if os.path.exists(p):
            shutil.copy(p, os.path.join(OUT, dst))
    json.dump(dict(shell=m.cfg, body=dict(genome="results/evo/genome.npy", rule="results/swarm_coevo_g2/rule.pt")),
              open(os.path.join(OUT, "params.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
