import torch, swarm_nca as sn
torch.set_num_threads(3)
T=sn.load_targets(); L=sn.LossCfg()
for p in sn.KINDS:
    r=sn.load_rule(f"results/solo_{p}/rule_latest.pt"); vals=[]
    for s in range(3):
        gen=sn.make_gen(7+101*s); sw=sn.seed_swarm([T[p]],r.world,gen)
        with torch.no_grad():
            for _ in range(240): sw=r(sw,gen)
        vals.append(sn.swarm_loss(sn.decode(sw,0),T[p],L)[1]["sink"])
    print(p, "own-plan loss (3 seeds):", [round(v,2) for v in vals], flush=True)
