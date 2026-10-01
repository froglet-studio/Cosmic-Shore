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


# ------------------------------------------------------------------ homeo 5: the SCALED quota ---
# posinfo_rule.homeo_quota sizes the quota at the plan's native headcount. Nothing may die and a domain
# breeds true, so after a big -> small switch (whale -> dragonfly) a domain can hold MORE tadpoles than
# the plan's slot for it: its surplus has no legal element to molt into and clings on as debris (the
# game port's finding 17). The loss is a divergence between DISTRIBUTIONS (headcount is not a goal), so
# the lossless answer is to scale the plan's (element, domain) table up until every living domain fits
# inside its slot, molt the surplus into that larger quota, and let parents lay the rest.

@torch.no_grad()
def scaled_quota(sw, b, capacity=280, orphans=False):
    m = sw.active[b]
    h = sw.active[b] & sw.hatched[b]
    if int(h.sum()) == 0:
        return None, None
    k = sn.PLAN_OF[int(torch.bincount(sw.elem[b][h], minlength=4).argmax())]
    tab, slots = pr.plan_tables()[k]
    have = torch.zeros(4, 3, dtype=torch.long)
    have.index_put_((sw.elem[b][m], sw.dom[b][m]), torch.ones(int(m.sum()), dtype=torch.long), accumulate=True)
    D = have.sum(0).float()                                   # per-domain headcount (fixed but for laying)
    n_plan = int(tab.sum())
    smax = capacity / n_plan
    best = None
    for perm in sn.PERMS[slots]:
        P = torch.zeros(3)
        for sl, d in enumerate(perm):
            P[d] = float(tab[:, sl].sum())
        if bool(((D > 0) & (P == 0)).any()):                 # a living domain with no slot: last resort
            lost = float(D[(P == 0)].sum())
        else:
            lost = 0.0
        absent = float(P[D == 0].sum())                       # plan units a missing domain can never fill
        fit = D / P.clamp(min=1e-6) * (P > 0).float()
        s = min(smax, max(1.0, float(fit.max())))
        key = (lost, absent, s)
        if best is None or key < best[0]:
            best = (key, perm, s)
    _, perm, s = best
    q = torch.zeros(4, 3, dtype=torch.long)
    for sl, d in enumerate(perm):
        q[:, d] = torch.round(tab[:, sl].float() * s).long()
    if orphans:                                               # homeo 6: a living domain the plan has no slot for (a
        mix = tab.sum(1).float() / max(1, n_plan)             # 3-domain dragonfly -> a 2-slot jellyfish) molts toward
        for d in range(3):                                    # the plan's overall element MIX instead of keeping its
            if int(q[:, d].sum()) == 0 and D[d] > 0:          # old element as debris
                qd = torch.floor(mix * D[d]).long()
                rem = int(D[d]) - int(qd.sum())
                for e in torch.argsort(mix * D[d] - qd.float(), descending=True)[:rem].tolist():
                    qd[e] += 1
                q[:, d] = qd
    for d in range(3):                                        # rounding: a domain's quota is never below its headcount
        short = int(D[d]) - int(q[:, d].sum())
        if short > 0 and int(q[:, d].sum()) > 0:
            q[int(q[:, d].argmax()), d] += short
    return q, have


def homeo_lay5(self, sw, gi, gj, gen=None):
    """posinfo_rule._homeo_lay at level 4 (molting + reserved deficits + proportional fill + cross-lay),
    with the SCALED quota."""
    old = pr.homeo_quota
    orph = self.homeo >= 6
    pr.homeo_quota = lambda sw_, b_: scaled_quota(sw_, b_, self.world.capacity, orphans=orph)
    try:
        h = self.homeo
        self.homeo = 4
        pr._homeo_lay(self, sw, gi, gj, gen)
    finally:
        self.homeo = h
        pr.homeo_quota = old


_base_homeo_lay = PosInfo2Rule.homeo_lay


def _dispatch(self, sw, gi, gj, gen=None):
    return homeo_lay5(self, sw, gi, gj, gen) if self.homeo >= 5 else _base_homeo_lay(self, sw, gi, gj, gen)


PosInfo2Rule.homeo_lay = _dispatch
