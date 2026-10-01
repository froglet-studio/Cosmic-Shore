import torch, numpy as np, swarm_nca as sn, evo_model as em, sys
torch.set_num_threads(4)
T=sn.load_targets(); L=sn.LossCfg()
def loss(sw,b,k): return sn.swarm_loss(sn.decode(sw,b),T[k],L)[1]["sink"]
def subsample(sw,b,k,gen,how):
    m=sw.active[b]&sw.hatched[b]; e=sw.elem[b]; mix=T[k].mix
    keep=torch.zeros_like(m)
    for f in range(4):
        idx=(m&(e==f)).nonzero().squeeze(1); want=min(int(mix[f]),len(idx))
        if how=="rand": sel=idx[torch.randperm(len(idx),generator=gen)[:want]]
        else:   # keep those nearest the swarm centroid (compact core)
            c=sw.pos[b][m].mean(0); d=((sw.pos[b][idx]-c)**2).sum(-1); sel=idx[d.argsort()[:want]]
        keep[sel]=True
    kill=m&~keep; sw.active[b,kill]=False; sw.hatched[b,kill]=False
for name,model in [("g2",sn.load_rule("results/swarm_coevo_g2/rule.pt")),("evo",em.EvoRule(np.load("results/evo/genome.npy")))]:
    for k in sn.KINDS:
        gen=sn.make_gen(7); sw=sn.seed_swarm([T[k]],model.world,gen)
        for _ in range(240): sw=model(sw,gen)
        m=sw.active[0]&sw.hatched[0]; mix=torch.bincount(sw.elem[0][m],minlength=4).tolist()
        base=loss(sw,0,k)
        a=sn.Swarm.cat([sw.index(torch.tensor([0]))]); subsample(a,0,k,gen,"rand"); r=loss(a,0,k)
        c=sn.Swarm.cat([sw.index(torch.tensor([0]))]); subsample(c,0,k,gen,"core"); co=loss(c,0,k)
        print(f"{name:4s} {k:7s} n={int(m.sum())} mix={mix} plan={list(T[k].mix)}  loss {base:6.2f}  -> plan mix/count random {r:6.2f}  core {co:6.2f}",flush=True)
