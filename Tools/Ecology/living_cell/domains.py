"""Part 2: multi-domain swarms. What happens when a swarm's members are NOT all the cell's colour?

The rule under study (the lead's proposal, being implemented as (a) on cece/swarm-fauna-game):
    each member's domain is inherited from the domain of the mass whose eaten volume funded its egg;
    seed members take the cell's controlling domain; the diet is evaluated per member's domain.
Compared, each on the same cell and the same seeds:
    (a) food     offspring domain ~ the domains of the volume it was funded by (sampled in proportion)
    (a1) food-majority   ... the plurality domain of that volume (a deterministic variant of (a))
    (b) parent   offspring domain = parent's domain (seeds in the controlling colour, as the rule says)
    (bmix) parent, but the seeds drawn in proportion to the domains' mass - the only way (b) can ever be
                 mixed, and the setting for the neutral-drift / founder question
    (c) control  offspring domain = the cell's controlling (most-volume) domain at birth (today's one-colour law)
    (d) last     a member's domain flips to the domain of whatever it last ate (offspring take the parent's)
    blind        reference: members have a domain but eat ANY domain (today's nucleus-cell exterior diet)
    none         reference: no swarm at all

The cell: radius 700, nucleus-less, three domains each holding a territory (an azimuth third of the sphere)
of plants that regrow out of a shared soil nutrient. The LEADER (domain 1) holds twice the plants. Three
pilots, one per domain, wander the whole cell laying their own domain's trail. Diet = mass whose domain
differs from the member's (the nucleus-less legacy diet, per member). Skeletons are Blue (edible to all).

Questions and the measurement for each:
    diversity / fixation   member domain shares over time; Simpson diversity; time until one domain > 95%
    territorial balance    COMEBACK INDEX = (share of the swarm's eating that hit the leader) / (leader's share
                           of edible mass), time-averaged. > 1: the swarm leans on the leader (damps, pro-
                           comeback); < 1: it shelters the leader (amplifies, anti-comeback). And the leader's
                           mass share at the end against the no-swarm cell.
    legibility             a DomainSlots body sorts members into one slot per domain. Per cluster of >= 20
                           members, every minute: slivers (a present domain under 10% of the body) and
                           turnover (fraction of members whose domain changed since the last minute: births,
                           deaths and flips). LEGIBLE minute = no sliver AND turnover < 20%.
"""
from __future__ import annotations

import json
import math
import os
import sys
import time
from multiprocessing import Pool

import numpy as np
from numba import njit

from .world import World, FLORA, TRAIL, SKEL, flock_terms

HERE = os.path.dirname(os.path.abspath(__file__))
RULES = ("a", "a1", "b", "bmix", "c", "d", "blind", "none")


@njit(cache=True)
def _nearest_dom(Q, qdom, r, pos, alive, kind, dom, excl, R, h, G, start, order, blind):
    """Nearest live, unclaimed prism within r whose domain differs from the querier's (Blue 0 is edible to
    all). blind = 1: any domain."""
    out = np.full(len(Q), -1, np.int64)
    for q in range(len(Q)):
        best = r * r
        lo = np.empty(3, np.int64); hi = np.empty(3, np.int64)
        for a in range(3):
            lo[a] = min(G - 1, max(0, int((Q[q, a] - r + R) / h)))
            hi[a] = min(G - 1, max(0, int((Q[q, a] + r + R) / h)))
        for x in range(lo[0], hi[0] + 1):
            for y in range(lo[1], hi[1] + 1):
                for z in range(lo[2], hi[2] + 1):
                    k = (x * G + y) * G + z
                    for jj in range(start[k], start[k + 1]):
                        j = order[jj]
                        if not alive[j] or excl[j] or kind[j] > SKEL_K:
                            continue
                        if blind == 0 and dom[j] == qdom[q] and dom[j] != 0:
                            continue
                        dx = pos[j, 0] - Q[q, 0]; dy = pos[j, 1] - Q[q, 1]; dz = pos[j, 2] - Q[q, 2]
                        d2 = dx * dx + dy * dy + dz * dz
                        if d2 < best:
                            best = d2; out[q] = j
    return out


SKEL_K = SKEL


class DomainCell:
    def __init__(self, seed=1, rule="a", cap=600, e_birth=12.0, n0=150, R=700.0, plants=(40, 20, 20),
                 plant_cap=30, flora_r=0.03, N0=20000.0, N_half=6000.0, pilots=True, dt=0.2):
        self.rule = rule; self.cap = cap; self.e_birth = e_birth
        w = self.w = World(seed=seed, R=R, nucleus=0.0, cap=150_000, N0=N0)
        rng = w.rng
        self.body, self.e0, self.metab, self.imax, self.speed = 3.0, 2.0, 0.05, 0.6, 40.0
        # territories: domain d owns azimuth third d of the sphere
        self.ppos, self.pdom = [], []
        for d, n in zip((1, 2, 3), plants):
            for _ in range(n):
                while True:
                    p = w.ball(1, 0.25 * R, 0.85 * R)[0]
                    az = (math.atan2(p[1], p[0]) + math.pi) / (2 * math.pi)
                    if int(az * 3) == d - 1:
                        break
                self.ppos.append(p); self.pdom.append(d)
        self.ppos = np.array(self.ppos); self.pdom = np.array(self.pdom)
        self.plant_cap, self.flora_r, self.N_half = plant_cap, flora_r, N_half
        for i in range(len(self.ppos)):
            for _ in range(plant_cap // 2):
                self._lay(i); w.book_initial(6.0)
        # the swarm (members in the controlling colour)
        C = cap + 50
        self.pos = np.zeros((C, 3)); self.vel = np.zeros((C, 3)); self.st = np.zeros(C); self.dom = np.zeros(C, np.int64)
        self.alive = np.zeros(C, bool); self.claim = np.full(C, -1, np.int64); self.chew = np.zeros(C)
        self.fund = np.zeros((C, 4)); self.bcool = np.zeros(C)
        self.on = rule != "none"
        if self.on:
            ctrl = self.controlling()
            c0 = self.ppos[rng.integers(0, len(self.ppos), n0)] + rng.normal(0, 40, (n0, 3))
            self.pos[:n0] = c0; self.st[:n0] = 6.0; self.dom[:n0] = ctrl; self.alive[:n0] = True
            if rule == "bmix":
                dm = self.domain_mass()[1:]
                self.dom[:n0] = rng.choice(3, n0, p=dm / dm.sum()) + 1
            w.book_initial(n0 * (self.body + 6.0))
        w.held_fns.append(lambda: float(self.body * self.alive.sum() + self.st[self.alive].sum()))
        if pilots:
            from .world import Pilot
            for d in (1, 2, 3):
                w.add_pilot(Pilot("wander", rng, R, speed=110.0, domain=d, name=f"p{d}", trail_spacing=30.0, trail_vol=3.0))
        self.dt = dt
        self.eaten_dom = np.zeros(4); self.births = 0; self.deaths = 0; self.flips = 0
        self.rows = []; self.prev_dom = None; self.leg = []; self.t_fix = None
        self.ci_num = 0.0; self.ci_den = 0.0

    def _lay(self, i):
        w = self.w
        w.add(self.ppos[i] + w.rng.normal(0, 18.0, 3), 6.0, int(self.pdom[i]), FLORA, owner=i)

    def domain_mass(self):
        w = self.w; n = w.n
        m = w.alive[:n] & (w.kind[:n] <= SKEL)
        return np.bincount(w.dom[:n][m].astype(np.int64), weights=w.vol[:n][m], minlength=4)

    def controlling(self):
        return int(np.argmax(self.domain_mass()[1:]) + 1)

    def grow(self):
        w = self.w; n = w.n
        m = w.alive[:n] & (w.kind[:n] == FLORA)
        c = np.bincount(w.owner[:n][m], minlength=len(self.ppos))
        lam = self.flora_r * (c + 1) * np.clip(1 - c / self.plant_cap, 0, 1) * w.N / (w.N + self.N_half)
        for i in np.flatnonzero(w.rng.poisson(lam)):
            if w.N < 6.0:
                return
            w.N -= 6.0; self._lay(i)

    def step(self):
        w = self.w; dt = self.dt
        w.t += dt
        for p in w.pilots:
            p.step(w, dt)
        w.rebuild()
        a = np.flatnonzero(self.alive)
        if len(a):
            P = self.pos[a]; V = self.vel[a]
            # forage: claim the nearest edible prism (per-member diet), eat on contact (chew = vol / imax)
            c = self.claim[a]
            bad = (c < 0) | ~w.alive[np.maximum(c, 0)]
            for i in a[bad]:
                if self.claim[i] >= 0:
                    w.excl[self.claim[i]] = 0
                self.claim[i] = -1
            need = a[bad & (self.st[a] < 16.0)]
            if len(need):
                got = _nearest_dom(np.ascontiguousarray(self.pos[need]), self.dom[need], 250.0, w.pos, w.alive, w.kind,
                                   w.dom, w.excl, w.R, w.h, w.G, w.start, w.order, 1 if self.rule == "blind" else 0)
                for i, j in zip(need, got):
                    if j >= 0:
                        self.claim[i] = j; w.excl[j] = 1
            c = self.claim[a]; has = c >= 0
            food = np.zeros_like(P)
            if has.any():
                to = w.pos[c[has]] - P[has]; dd = np.linalg.norm(to, axis=1)
                food[has] = to / np.maximum(dd[:, None], 1e-9)
                self.chew[a] = np.maximum(0, self.chew[a] - dt)
                for k in np.flatnonzero(has)[(dd < 8) & (self.chew[a[has]] <= 0)]:
                    i = a[k]; j = self.claim[i]; w.excl[j] = 0; self.claim[i] = -1
                    d = int(w.dom[j]); v = w.eat(j, "swarm")
                    self.st[i] += v; self.fund[i, d] += v; self.chew[i] = v / self.imax; self.eaten_dom[d] += v
                    if self.rule == "d" and d != 0 and d != self.dom[i]:
                        self.dom[i] = d; self.flips += 1
            _, al, co, se = flock_terms(P, V, 30.0, 60.0, 12.0)
            des = (food * 1.0 + 0.4 * al / max(self.speed, 1) + 0.01 * co + 0.6 * se + 0.3 * w.rng.normal(size=P.shape))
            des = des / np.maximum(np.linalg.norm(des, axis=1, keepdims=True), 1e-9) * self.speed
            V = V + np.clip(des - V, -80 * dt, 80 * dt)
            r = np.linalg.norm(P, axis=1); over = np.clip((r - 0.92 * w.R) / (0.05 * w.R), 0, None)
            V -= P / np.maximum(r[:, None], 1e-6) * over[:, None] * 60
            self.vel[a] = V; self.pos[a] += V * dt
            # energetics
            burn = np.minimum(self.st[a], self.metab * dt); self.st[a] -= burn; w.N += float(burn.sum())
            for i in a[self.st[a] <= 1e-9]:
                self.alive[i] = False; self.deaths += 1
                if self.claim[i] >= 0:
                    w.excl[self.claim[i]] = 0; self.claim[i] = -1
                w.add(self.pos[i].copy(), self.body, 0, SKEL)            # the skeleton is Blue
            self.bcool = np.maximum(0, self.bcool - dt)
            a = np.flatnonzero(self.alive)
            par = a[(self.st[a] >= self.e_birth) & (self.bcool[a] <= 0)]
            free = np.flatnonzero(~self.alive)
            room = self.cap - len(a)
            for i, j in zip(par[:max(0, room)], free):
                self.st[i] -= self.body + self.e0
                f = self.fund[i, 1:]
                if self.rule == "a":
                    d = int(w.rng.choice(3, p=f / f.sum()) + 1) if f.sum() > 0 else int(self.dom[i])
                elif self.rule == "a1":
                    d = int(np.argmax(f) + 1) if f.sum() > 0 else int(self.dom[i])
                elif self.rule == "c":
                    d = self.controlling()
                else:
                    d = int(self.dom[i])         # b, d (last-eaten member's current domain), blind
                self.fund[i] = 0.0
                self.alive[j] = True; self.st[j] = self.e0; self.dom[j] = d
                self.pos[j] = self.pos[i] + w.rng.normal(0, 3, 3); self.vel[j] = self.vel[i]
                self.claim[j] = -1; self.chew[j] = 1.0; self.bcool[i] = self.bcool[j] = 5.0; self.fund[j] = 0.0
                self.births += 1
        if int(w.t / dt) % int(1.0 / dt) == 0:
            self.grow()

    # ---- measurement -----------------------------------------------------------------------------------
    def sample(self):
        w = self.w
        a = np.flatnonzero(self.alive)
        sh = np.bincount(self.dom[a], minlength=4)[1:] / max(len(a), 1)
        dm = self.domain_mass()[1:]
        ms = dm / max(dm.sum(), 1e-9)
        self.rows.append(dict(t=round(w.t, 1), n=len(a), member=sh.round(4).tolist(), mass=ms.round(4).tolist(),
                              eaten=(self.eaten_dom[1:]).round(1).tolist(), audit=float(w.audit())))
        if self.t_fix is None and len(a) and sh.max() > 0.95 and w.t > 60:
            self.t_fix = w.t

    def legibility(self):
        """Every 60 s: cluster members (100-u grid, 26-connected), per cluster >= 20: slivers + turnover."""
        a = np.flatnonzero(self.alive)
        cur = {int(i): int(self.dom[i]) for i in a}
        if self.prev_dom is not None:
            common = [i for i in cur if i in self.prev_dom]
            changed = sum(cur[i] != self.prev_dom[i] for i in common) + (len(cur) - len(common))
            turnover = changed / max(len(cur), 1)
        else:
            turnover = 0.0
        self.prev_dom = cur
        if len(a) < 20:
            return
        key = np.floor(self.pos[a] / 100.0).astype(np.int64)
        lab = -np.ones(len(a), np.int64); cells = {}
        for k, kk in enumerate(map(tuple, key)):
            cells.setdefault(kk, []).append(k)
        cid = 0
        for kk in cells:
            if lab[cells[kk][0]] >= 0:
                continue
            stack = [kk]; lab[cells[kk]] = cid
            while stack:
                x = stack.pop()
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        for dz in (-1, 0, 1):
                            y = (x[0] + dx, x[1] + dy, x[2] + dz)
                            if y in cells and lab[cells[y][0]] < 0:
                                lab[cells[y]] = cid; stack.append(y)
            cid += 1
        for c in range(cid):
            m = a[lab == c]
            if len(m) < 20:
                continue
            sh = np.bincount(self.dom[m], minlength=4)[1:] / len(m)
            present = sh[sh > 0]
            sliver = bool(np.any(present < 0.10))
            self.leg.append(dict(t=round(self.w.t, 1), n=len(m), share=sh.round(3).tolist(), sliver=sliver,
                                 turnover=round(turnover, 3), legible=(not sliver) and turnover < 0.20,
                                 n_domains=int((sh > 0).sum())))


def run_domain(seed=1, rule="a", minutes=20.0, cap=600, e_birth=12.0, n0=150, plants=(40, 20, 20)):
    t0 = time.time()
    c = DomainCell(seed=seed, rule=rule, cap=cap, e_birth=e_birth, n0=n0, plants=plants)
    n = int(minutes * 60 / c.dt)
    for i in range(n):
        c.step()
        if i % int(5.0 / c.dt) == 0:
            c.sample()
        if i % int(60.0 / c.dt) == 0 and i:
            c.legibility()
    R = c.rows
    burn = [r for r in R if r["t"] >= 120] or R
    M = np.array([r["member"] for r in burn]); S = np.array([r["mass"] for r in burn])
    E = np.diff(np.array([[0, 0, 0]] + [r["eaten"] for r in burn]), axis=0)
    simpson = 1 - (M ** 2).sum(1)
    tot = E.sum(1)
    ok = tot > 0
    lead = S[:, 0]
    ci = float((E[ok, 0] / tot[ok]).mean() / lead[ok].mean()) if ok.any() else float("nan")
    leg = c.leg
    out = dict(seed=seed, rule=rule, cap=cap, e_birth=e_birth, minutes=minutes, wall_s=round(time.time() - t0, 1),
               births=c.births, deaths=c.deaths, flips=c.flips, t_fix=c.t_fix,
               members_end=R[-1]["member"], n_end=R[-1]["n"], simpson_mean=round(float(simpson.mean()), 3) if len(simpson) else 0,
               simpson_end=round(float(simpson[-1]), 3) if len(simpson) else 0,
               leader_share_start=R[0]["mass"][0], leader_share_mean=round(float(lead.mean()), 4), leader_share_end=R[-1]["mass"][-3] if False else R[-1]["mass"][0],
               comeback_index=round(ci, 3), eaten_dom=c.eaten_dom.round(1).tolist(),
               legible_frac=round(float(np.mean([l["legible"] for l in leg])), 3) if leg else None,
               sliver_frac=round(float(np.mean([l["sliver"] for l in leg])), 3) if leg else None,
               turnover_mean=round(float(np.mean([l["turnover"] for l in leg])), 3) if leg else None,
               domains_per_body=round(float(np.mean([l["n_domains"] for l in leg])), 2) if leg else None,
               audit_max=max(abs(r["audit"]) for r in R), rows=R, leg=leg)
    return out


def _job(a):
    return run_domain(**a)


def study(seeds=(1, 2, 3), minutes=20.0):
    jobs = [dict(seed=s, rule=r, minutes=minutes) for r in RULES for s in seeds]
    # fixation vs size and birth rate, for the two heritable rules
    for r in ("a", "bmix", "c", "d"):
        for cap in (150, 600, 1500):
            for eb in (8.0, 16.0):
                if cap == 600 and eb == 12.0:
                    continue
                jobs += [dict(seed=s, rule=r, minutes=minutes, cap=cap, e_birth=eb, n0=min(150, cap // 2)) for s in seeds]
    with Pool(4) as p:
        res = p.map(_job, jobs)
    return res


def table(res):
    rows = {}
    for r in res:
        k = (r["rule"], r["cap"], r["e_birth"])
        rows.setdefault(k, []).append(r)
    out = []
    for (rule, cap, eb), rs in sorted(rows.items(), key=lambda x: (RULES.index(x[0][0]), x[0][1], x[0][2])):
        m = lambda k: round(float(np.nanmean([x[k] if x[k] is not None else np.nan for x in rs])), 3)
        fx = [x["t_fix"] for x in rs]
        out.append(dict(rule=rule, cap=cap, e_birth=eb, n=len(rs), n_end=m("n_end"), births=m("births"),
                        simpson_mean=m("simpson_mean"), simpson_end=m("simpson_end"),
                        fixed=sum(f is not None for f in fx), t_fix_mean=round(float(np.mean([f for f in fx if f])), 1) if any(fx) else None,
                        leader_share_mean=m("leader_share_mean"), leader_share_end=m("leader_share_end"),
                        comeback_index=m("comeback_index"), legible_frac=m("legible_frac"), sliver_frac=m("sliver_frac"),
                        turnover=m("turnover_mean"), domains_per_body=m("domains_per_body"), flips=m("flips"),
                        audit_max=max(x["audit_max"] for x in rs)))
    return out


if __name__ == "__main__":
    mins = float(sys.argv[1]) if len(sys.argv) > 1 else 20.0
    res = study(minutes=mins)
    T = table(res)
    for r in T:
        print(r)
    json.dump(dict(table=T, runs=[{k: v for k, v in r.items() if k not in ("rows",)} for r in res],
                   series={f"{r['rule']}_{r['cap']}_{r['e_birth']}_{r['seed']}": r["rows"] for r in res}),
              open(os.path.join(HERE, "results", "domains.json"), "w"), indent=0)
