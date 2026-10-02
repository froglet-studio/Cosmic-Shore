"""Score every builder species (and named variants) against the shared pilots, in parallel.
python Tools/Ecology/builders/run_all.py [name ...]    -> builders/results/<name>.json + out/<name>.html"""
import json, os, sys
from multiprocessing import Pool
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.harness import evaluate, save_card
from builders.nest import NestWeavers
from builders.traps import TrapBuilders
from builders.wearers import Wearers
from builders.wasp import WaspComb

STD = ("wander", "evader", "hunter")
LANE = ("wander", "evader", "hunter", "circuit", "varied")

SPECIES = {
    "nest_v0_shell": (lambda a, s: NestWeavers(a, seed=s), STD, "Nest weavers v0 (template shell)"),
    "nest_v1_logistic": (lambda a, s: NestWeavers(a, seed=s, Rc=44, w=30, k_cement=0.4, nucleate=0.02, homing="core"),
                         STD, "Nest weavers v1 (logistic)"),
    "nest_v2_wasp": (lambda a, s: WaspComb(a, seed=s), STD, "Nest weavers v2 (wasp comb, lattice-swarm rules)"),
    "traps_v1": (lambda a, s: TrapBuilders(a, seed=s), LANE, "Trap builders v1 (lane webs)"),
    "traps_v2_fair": (lambda a, s: TrapBuilders(a, seed=s, n=30, max_nb=2, lane_min=3.0), LANE,
                      "Trap builders v2 (tuned: 30 workers, lane_min 3)"),
    "wearers_v1": (lambda a, s: Wearers(a, seed=s), STD, "Wearers v1 (body from stolen mass)"),
    "wearers_v2_contact": (lambda a, s: Wearers(a, seed=s, contact=0.5), STD, "Wearers v2 (contact attachment)"),
}


def lane_extra(runs):
    def m(pol):
        v = [r["hits_per_min"] for (p, s), r in runs.items() if p == pol]
        return sum(v) / len(v) if v else None
    c, v = m("circuit"), m("varied")
    return dict(hits_per_min_circuit=c, hits_per_min_varied=v,
                lane_counterplay=round(v / c, 3) if c else None)


def one(name):
    fac, pols, title = SPECIES[name]
    lane = "circuit" in pols
    card, recs = evaluate(fac, name, policies=pols, minutes=3.0, extra=lane_extra if lane else None,
                          replay_policy="circuit" if lane else "wander")
    save_card(name, card, recs, title)
    return name, {k: card[k] for k in card if k != "structure"}


if __name__ == "__main__":
    names = sys.argv[1:] or list(SPECIES)
    with Pool(min(4, len(names))) as pool:
        for name, card in pool.imap_unordered(one, names):
            print("=====", name, json.dumps(card, default=str)[:1500], flush=True)
