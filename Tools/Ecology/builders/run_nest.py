"""Score the nest weavers. python Tools/Ecology/builders/run_nest.py [variant]"""
import json, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from builders.harness import evaluate, save_card
from builders.nest import NestWeavers

VARIANTS = {
    "nest_v0_shell": dict(),                                                   # template-dominated (negative)
    "nest_v1_logistic": dict(Rc=44, w=30, k_cement=0.4, nucleate=0.02, homing="core"),
}

if __name__ == "__main__":
    for v in (sys.argv[1:] or VARIANTS):
        cfg = VARIANTS[v]
        card, recs = evaluate(lambda a, s: NestWeavers(a, seed=s, **cfg), v)
        save_card(v, card, recs, f"Nest weavers - {v}")
        print(json.dumps({k: card[k] for k in card if k not in ("structure",)}, indent=1, default=str))
