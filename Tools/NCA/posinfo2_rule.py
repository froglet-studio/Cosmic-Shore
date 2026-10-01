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


# ------------------------------------------------------------------ homeo 7: the MAJORITY GUARD ---
# A fair cull can leave the new majority element ahead by ONE tadpole in a remnant of ~20 (jellyfish ->
# dragonfly: Time 7, Space 6, Charge 6, Mass 2). The homeostat re-reads the majority every step, so if its
# own molting or laying lets another element draw level, the plan flips back and the swarm regrows the OLD
# body (loss 63). The guard: no molt and no egg may bring a non-majority element level with the plan's
# majority element. It changes only the order things are made in, never the final quota.

def guarded_homeo_lay(self, sw, gi, gj, gen=None):
    W = self.world
    B, N, _ = sw.pos.shape
    pos = sw.pos.reshape(B * N, 3)
    with torch.no_grad():
        dxl = pos[gj] - pos[gi]
        close = ((dxl * dxl).sum(-1) < W.r_lay ** 2).float()
        cnt = torch.zeros(B * N).index_add(0, gi, close).view(B, N)
        elig = sw.hatched & sw.active & (cnt < W.k_bud) & (torch.rand(B, N, generator=gen) <= W.p_bud)
        q = torch.zeros(B, N)
        cross = {}
        for b in range(B):
            quota, have = scaled_quota(sw, b, W.capacity, orphans=True)
            if quota is None:
                continue
            maj = int(quota.sum(1).argmax())
            tot = have.sum(1).clone()                          # element totals (eggs included)
            hm = sw.active[b] & sw.hatched[b]                  # the plan is read off HATCHED tadpoles, so the
            lead = int((sw.elem[b][hm] == maj).sum())          # majority's lead is counted without its eggs

            def ok(e):                                         # may one more tadpole of element e appear?
                return e == maj or int(tot[e]) + 1 < lead
            # an EGG laid before the cull (the cull counts only hatched tadpoles) of an element that already
            # matches the lead re-forms as the majority element before it hatches - no death, just a molt
            for e in range(4):
                if e == maj or int(tot[e]) < lead:
                    continue
                eggs = (sw.active[b] & ~sw.hatched[b] & (sw.elem[b] == e)).nonzero().squeeze(1)
                for i in eggs.tolist():
                    if int(tot[e]) < lead:
                        break
                    d = int(sw.dom[b, i])
                    sw.elem[b, i] = maj; have[e, d] -= 1; have[maj, d] += 1; tot[e] -= 1; tot[maj] += 1
                    self.molts += 1
            surplus = (have - quota).clamp(min=0)
            lack = (quota - have).clamp(min=0)
            for d in range(3):                                 # molting (domain breeds true)
                for e_from in range(4):
                    if surplus[e_from, d] == 0 or lack[:, d].sum() == 0:
                        continue
                    mem = (sw.active[b] & sw.hatched[b] & (sw.elem[b] == e_from) & (sw.dom[b] == d)).nonzero().squeeze(1)
                    go = mem[torch.rand(len(mem), generator=gen) < self.p_molt][:int(surplus[e_from, d])]
                    for i in go.tolist():
                        if e_from == maj and lead - 1 <= max(int(tot[e]) for e in range(4) if e != maj) + 1:
                            break                              # never thin the majority while its lead is slim
                        w = lack[:, d].float() * torch.tensor([1.0 if ok(e) else 0.0 for e in range(4)])
                        if float(w.sum()) == 0:
                            break
                        e_to = int(torch.multinomial(w, 1, generator=gen))
                        sw.elem[b, i] = e_to; lack[e_to, d] -= 1; surplus[e_from, d] -= 1
                        have[e_from, d] -= 1; have[e_to, d] += 1; tot[e_from] -= 1; tot[e_to] += 1
                        if e_from == maj:
                            lead -= 1
                        if e_to == maj:
                            lead += 1
                        self.molts += 1
            deficit = (quota - have).clamp(min=0)
            surplus = (have - quota).clamp(min=0)
            for d in range(3):                                 # a domain's deficits are reserved for its molters
                res = int(surplus[:, d].sum())
                for e in sorted(range(4), key=lambda e_: -int(deficit[e_, d])):
                    take = min(res, int(deficit[e, d])); deficit[e, d] -= take; res -= take
            fill = float(have[maj].sum()) / max(1, int(quota[maj].sum()))   # grow in proportion
            cap = torch.ceil(quota.float() * min(1.0, fill + 0.15)).long().clamp(min=2)
            cap[maj] = quota[maj]
            deficit = torch.minimum(deficit, (cap - have).clamp(min=0))
            idx = elig[b].nonzero().squeeze(1)
            if len(idx) == 0:
                continue
            idx = idx[torch.randperm(len(idx), generator=gen)]
            left = deficit.reshape(-1).clone()
            spare = []
            for i in idx.tolist():
                e, d = int(sw.elem[b, i]), int(sw.dom[b, i])
                c = e * 3 + d
                if left[c] > 0 and ok(e):
                    left[c] -= 1; q[b, i] = 1.0 / W.p_bud; tot[e] += 1
                else:
                    spare.append(i)
            for i in spare:                                    # a class short of parents: same-domain cross-lay
                d = int(sw.dom[b, i])
                need = [e for e in range(4) if left[e * 3 + d] > 0 and ok(e)]
                if need:
                    e = need[int(torch.randint(len(need), (1,), generator=gen))]
                    left[e * 3 + d] -= 1; q[b, i] = 1.0 / W.p_bud; cross[(b, i)] = e; tot[e] += 1
        laid = self._lay(sw, gi, gj, gen, q.reshape(-1), None)
        for b, slots, parents, *_ in laid:
            for sl, pa in zip(slots.tolist(), parents.tolist()):
                if (b, pa) in cross:
                    sw.elem[b, sl] = cross[(b, pa)]


def _dispatch7(self, sw, gi, gj, gen=None):
    if self.homeo >= 7:
        return guarded_homeo_lay(self, sw, gi, gj, gen)
    return _dispatch(self, sw, gi, gj, gen)


PosInfo2Rule.homeo_lay = _dispatch7


@torch.no_grad()
def egg_guard(rule, sw):
    """homeo >= 7, run at the START of a step (eggs hatch inside the step, before laying is decided): an
    unhatched egg of a non-majority element whose hatched + egg count would draw level with the majority
    element's hatched count re-forms as the majority element. A molt, never a death."""
    for b in range(sw.B):
        h = sw.active[b] & sw.hatched[b]
        if int(h.sum()) == 0:
            continue
        hc = torch.bincount(sw.elem[b][h], minlength=4)
        maj = int(hc.argmax()); lead = int(hc[maj])
        eg = sw.active[b] & ~sw.hatched[b]
        ec = torch.bincount(sw.elem[b][eg], minlength=4)
        for e in range(4):
            over = int(hc[e]) + int(ec[e]) - (lead - 1)
            if e == maj or over <= 0:
                continue
            eggs = (eg & (sw.elem[b] == e)).nonzero().squeeze(1)[:over]
            sw.elem[b, eggs] = maj
            rule.molts += len(eggs)


_base_step = PosInfo2Rule._step


def _guarded_step(self, sw, gen=None, bud=True, fire=None):
    if self.homeo >= 7:
        egg_guard(self, sw)
    return _base_step(self, sw, gen, bud, fire)


PosInfo2Rule._step = _guarded_step
