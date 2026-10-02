"""(1+1) local search over a species' parameters, maximising the replayable score R (harness.replay_score).

    python search.py <species> <iters> [start-json]

Every candidate is appended to results/search_<species>.jsonl, kept or not (negatives are data). A candidate
replaces the incumbent only if it beats it by `margin` (3 seeds x 2 min is noisy; a tie keeps the incumbent).
Each proposal perturbs 1-3 parameters inside SPACE; ints stay ints; a log-scaled range perturbs multiplicatively.
"""
import json, os, sys, time
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from harness import scorecard

SPEC = dict(snaptrap="snaptrap:SnapTrap", spores="spores:SporeBurster", walker="walker:Walker",
            coral="coral:Coral", physarum="physarum:Physarum")

# name: (lo, hi, kind) kind in {"int", "lin", "log"}
SPACE = dict(
    snaptrap=dict(n_traps=(16, 60, "int"), clumps=(3, 14, "int"), clump_r=(30, 140, "lin"), sense=(90, 260, "lin"),
                  t_prime=(0.3, 1.4, "lin"), t_close=(0.15, 0.6, "lin"), dgape=(4, 30, "lin"), mouth_len=(30, 80, "lin"),
                  mouth_w=(12, 36, "lin"), turn_deg=(0, 12, "lin"), bud_bias=(0, 1, "lin"), t_reset=(1, 8, "lin")),
    spores=dict(n_pods=(30, 140, "int"), clumps=(4, 16, "int"), clump_r=(30, 120, "lin"), touch=(12, 40, "lin"),
                v_soft=(40, 110, "lin"), kick_hard=(0.5, 2.0, "lin"), kick_soft=(0.05, 0.5, "lin"), alarm_r=(40, 160, "lin"),
                alarm_speed=(25, 150, "lin"), alarm_gain=(0.2, 1.2, "lin"), relax=(0.1, 1.0, "log"), t_swell=(0.4, 1.6, "lin"),
                launch=(15, 80, "lin"), drag=(0.2, 2.0, "log"), wind=(3, 30, "lin"), spore_cd=(0.3, 1.5, "lin"),
                spore_r=(3, 12, "lin")),
    walker=dict(n_walkers=(6, 24, "int"), speed=(2, 14, "lin"), sense=(80, 260, "lin"), tell=(40, 200, "lin"),
                prime=(70, 220, "lin"), strike=(25, 90, "lin"), thorn=(25, 90, "lin"), t_erupt=(0.15, 0.9, "lin"),
                t_hold=(0.2, 1.5, "lin"), r_body=(18, 40, "lin"),
                guard=(0.0, 0.8, "lin")),
    coral=dict(n_hearts=(3, 12, "int"), Du=(0.1, 1.2, "lin"), Dv=(0.003, 0.08, "log"), rate=(5, 120, "log"),
               k=(0.002, 0.05, "log"), vth=(0.15, 0.8, "lin"), sting=(0.005, 0.2, "log"), eat_per_s=(0.1, 3.0, "log"),
               reach=(1, 4, "int")),
    physarum=dict(sa=(15, 60, "lin"), ra=(15, 60, "lin"), so=(12, 50, "lin"), ss=(20, 80, "lin"), food_dep=(0.3, 6, "log"),
                  on=(3, 14, "lin"), off=(1, 6, "lin"), period=(1.5, 6, "lin"), wave_speed=(20, 120, "lin"),
                  ex_ticks=(1, 4, "int"), wake_dep=(-4, 8, "lin"), heart_speed=(0, 15, "lin")),
)


def propose(rng, x, space):
    y = dict(x); keys = list(space)
    for k in rng.choice(keys, size=rng.integers(1, 4), replace=False):
        lo, hi, kind = space[k]; cur = y.get(k, (lo + hi) / 2)
        if kind == "log":
            v = float(np.exp(np.log(max(cur, 1e-9)) + rng.normal(0, 0.35)))
        else:
            v = cur + rng.normal(0, 0.18 * (hi - lo))
        v = float(np.clip(v, lo, hi))
        y[k] = int(round(v)) if kind == "int" else round(v, 4)
    return y


if __name__ == "__main__":
    name = sys.argv[1]; iters = int(sys.argv[2]); x = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
    # import the species module in THIS process so every forked scorecard worker inherits one fixed copy - a
    # worker that imported it from disk itself would pick up edits made to the file while the search runs
    import importlib; importlib.import_module(SPEC[name].split(":")[0])
    margin = 0.01
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "results", f"search_{name}.jsonl")
    rng = np.random.default_rng(hash(name) % (1 << 31))
    def score(p):
        t = time.time(); c, _ = scorecard(SPEC[name], p)
        obj = c["R"] + c.get("R_hard", 0.0)        # R, then the stretch bar once R saturates
        rec = dict(params=p, R=c["R"], R_hard=c.get("R_hard"), obj=obj, terms=c.get("R_terms"),
                   hard_terms=c.get("R_hard_terms"), card=c, wall=round(time.time() - t, 1))
        with open(out, "a") as fh: fh.write(json.dumps(rec) + "\n")
        return obj, c
    best_R, best_c = score(x)
    print("start", best_R, json.dumps(best_c.get("R_terms")), flush=True)
    for it in range(iters):
        y = propose(rng, x, SPACE[name])
        R, c = score(y)
        kept = R > best_R + margin
        if kept: x, best_R, best_c = y, R, c
        print(it, "obj", round(R, 4), "R", c["R"], "Rh", c.get("R_hard"), "best", round(best_R, 4), "KEPT" if kept else "", json.dumps({k: y[k] for k in y if y.get(k) != x.get(k) or kept}),
              json.dumps(c.get("R_terms")), flush=True)
    with open(out.replace(".jsonl", "_best.json"), "w") as fh:
        json.dump(dict(params=x, R=best_R, card=best_c), fh, indent=1)
    print("BEST", best_R, json.dumps(x))
