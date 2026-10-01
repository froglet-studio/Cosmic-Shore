import torch, numpy as np, swarm_nca as sn, evo_model as em, dataclasses as dc
torch.set_num_threads(4)
T=sn.load_targets()
base=sn.LossCfg()
variants={"full":base,"pos+elem+dom":dc.replace(base,w_h=0,w_tier=0,w_face=0,w_sp=0),
          "pos+elem":dc.replace(base,w_h=0,w_tier=0,w_face=0,w_sp=0,w_dom=0),"pos only":dc.replace(base,w_h=0,w_tier=0,w_face=0,w_sp=0,w_dom=0,w_elem=0)}
import field_swarm as fs, json
cfg=json.load(open("results/field/params.json"))["cfg"]; cfg["vmax"]=tuple(cfg["vmax"])
for name,model in [("field",fs.FieldSwarm(fs.FieldCfg(**cfg))),("g2",sn.load_rule("results/swarm_coevo_g2/rule.pt")),("evo",em.EvoRule(np.load("results/evo/genome.npy")))]:
    for k in sn.KINDS:
        gen=sn.make_gen(7); sw=sn.seed_swarm([T[k]],model.world,gen)
        for _ in range(240): sw=model(sw,gen)
        x=sn.decode(sw,0)
        print(f"{name:5s} {k:7s} "+"  ".join(f"{v}: {sn.swarm_loss(x,T[k],L)[1]['sink']:6.2f}" for v,L in variants.items()),flush=True)
