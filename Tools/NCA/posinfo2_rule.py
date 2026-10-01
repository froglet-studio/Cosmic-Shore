"""posinfo2 - the posinfo learned rule carried to the full scorecard (round 4).

PosInfo2Rule = posinfo_rule.PosInfoRule (G2 + body-frame positional inputs, BPTT-trained) with:

  LOSSLESS   the learned death channel is masked (`no_die`): the rule's output on swarm_nca.DIE is
             forced to zero, so s[DIE] never leaves 0 and a tadpole can never decide to die. The only
             deaths left are the yardstick's cull (players eating members). Unhatched eggs that fail to
             hatch are not tadpoles and never were alive (the scorecard does not count them).
  MOLTING    the designed (element, domain) production homeostat at level 4 (posinfo_rule._homeo_lay):
             a SURPLUS tadpole of the current majority plan re-forms as a DEFICIT element of its OWN
             domain (domain breeds true; nothing dies), and parents lay only into deficit classes.
  DEFICIT    optional extra PERCEIVED inputs (`defin` > 0): the homeostat's (element, domain) deficit
             table, read back by each tadpole for its own class and its own domain - "my class is short /
             my team is short" - so the network knows what the controller is about to do. Zero-initialised
             columns, so a warm start equals posinfo exactly.

The composition controller is DESIGNED (who lays / molts into what); the SHAPE (where each tadpole
swims, i.e. the sorting) stays LEARNED. That pairing is the round-3 cross-family finding.
"""
from __future__ import annotations

import torch

import posinfo_rule as pr
import swarm_nca as sn


class PosInfo2Rule(pr.PosInfoRule):
    stateless = True

    def __init__(self, world, hidden=192, morph=0, morph_iters=4, homeo=4, no_die=1, p_molt=0.1):
        super().__init__(world, hidden=hidden, morph=morph, morph_iters=morph_iters, homeo=homeo)
        self.no_die = no_die
        self.p_molt = p_molt

    def mlp(self, f):
        out = super().mlp(f)
        if self.no_die:
            keep = torch.ones(out.shape[1], dtype=out.dtype)
            keep[sn.DIE] = 0.0
            out = out * keep
        return out


def load(path, homeo=None, no_die=None, p_molt=None):
    st = torch.load(path, weights_only=False, map_location=sn.DEVICE)
    w = dict(st["world"]); w["vmax"] = tuple(w["vmax"])
    rule = PosInfo2Rule(sn.World(**w), hidden=st["hidden"], morph=st.get("morph", 0), morph_iters=st.get("morph_iters", 4),
                        homeo=st.get("homeo", 4) if homeo is None else homeo,
                        no_die=st.get("no_die", 1) if no_die is None else no_die,
                        p_molt=st.get("p_molt", 0.1) if p_molt is None else p_molt)
    rule.load_state_dict(st["rule"])
    return rule


def save(rule, path, step=0, extra=None):
    torch.save(dict(rule={k: v.cpu() for k, v in rule.state_dict().items()}, world=sn.asdict(rule.world),
                    hidden=rule.hidden, morph=rule.morph, morph_iters=rule.morph_iters, homeo=rule.homeo,
                    no_die=rule.no_die, p_molt=rule.p_molt, step=step, **(extra or {})), path)
