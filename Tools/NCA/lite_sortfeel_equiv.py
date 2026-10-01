"""lite_sortfeel equivalence: LiteSortFeel with given flags vs SortFeel, same seed, N steps; max position gap."""
import sys, numpy as np, torch
import swarm_eval as se, swarm_nca as sn
torch.set_num_threads(1)
q = sys.argv[1] if len(sys.argv) > 1 else "vec_look=1"
a = se.load_model("sortfeel:results/sortfeel/params.json")
b = se.load_model("lite_sortfeel:results/sortfeel/params.json?" + q)
for k in sn.KINDS:
    ga, gb = sn.make_gen(7), sn.make_gen(7)
    sa = sn.seed_swarm([sn.load_targets()[k]], a.world, ga); sb = sn.seed_swarm([sn.load_targets()[k]], b.world, gb)
    for t in range(150):
        sa = a(sa, ga); sb = b(sb, gb)
    print(k, float((sa.pos - sb.pos).abs().max()), float((sa.s - sb.s).abs().max()), bool((sa.elem == sb.elem).all()))
