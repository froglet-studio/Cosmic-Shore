"""The living cell: round 1's port picks in ONE 1200-u arena, eating each other on one conserved ledger.

    from living_cell.cell import Cell, DEFAULT
    c = Cell(seed=1, cfg=dict(DEFAULT, pack_n=30), pilots=("explore", "wander"))
    for _ in range(600): c.step(0.1)

Trophic links (each is a rule in a species, not a script here):
    flora   <- N                       plants grow out of the soil nutrient
    grazer, locust  <- flora, trail, skeleton        (nucleus-cell diet: any domain outside the nucleus)
    thief   <- flora (nectar), its own HOARD (larder);  steals warm trail -> hoard
    pack    <- grazer, locust, thief    (Holling II in macro, a chase in micro)
    lurker  <- grazer, locust           (ambush)
    snaptrap<- grazer, locust, thief that enter an open mouth; roots take trail + skeleton
    fortress<- trail, skeleton, thieves' hoard (steals loose mass, walls its core, eats the surplus)
    physarum<- flora, trail, skeleton it covers (digests into its reserve)
    N       <- every metabolism, every upkeep, every death's stomach
    skeleton<- every starvation and every pilot kill (body left as a prism)

`cfg` is the whole dial set; DEFAULT is the recommended cell (see run.py `iterate` for how it was found).
"""
from __future__ import annotations

import time

import numpy as np

from .world import World, Pilot, K_HERB, FLORA, TRAIL, SKEL, WALL, TUBE, TRAP
from .fauna import Grazer, Locust, Pack, Thief, Lurker
from .structures import Flora, SnapTraps, Fortress, Physarum

DEFAULT = dict(
    N0=60000.0,
    n_plants=150, plant_cap=40, flora_r=0.03, N_half=20000.0, shield_frac=0.12,
    flora_seed_frac=0.6, flora_recruit=0.0, flora_N_ref=60000.0,
    grazer_n=900, locust_n=250, pack_n=24, thief_n=60, lurker_n=20,
    grazer_cap=2600, locust_cap=1200, pack_cap=40, thief_cap=220, lurker_cap=40,
    pack_metab=0.04, pack_attack=6.0e-4,
    traps=True, n_clumps=5, traps_per=6, trap_cap=10,
    fortress=True, fortress_n=40,
    physarum=True, phys_agents=6000,
    expand_r=520.0, ahead_r=760.0, absorb_r=680.0,
    trail_spacing=30.0, trail_vol=3.0,
    species=("grazer", "locust", "pack", "thief", "lurker"),
)

SPECIES = dict(grazer=Grazer, locust=Locust, pack=Pack, thief=Thief, lurker=Lurker)
THREAT = ("locust", "pack", "thief", "lurker", "snaptrap", "fortress", "physarum")
ALL = ("grazer",) + THREAT


class Cell:
    def __init__(self, seed=1, cfg=None, pilots=("explore", "wander"), pilot_domains=(1, 2, 3, 1), bug=""):
        cfg = dict(DEFAULT, **(cfg or {}))
        self.cfg = cfg
        w = self.w = World(seed=seed, N0=cfg["N0"])
        w.bug = bug
        self.flora = Flora(w, n_plants=cfg["n_plants"], cap=cfg["plant_cap"], r=cfg["flora_r"], N_half=cfg["N_half"],
                           shield_frac=cfg["shield_frac"], seed_frac=cfg["flora_seed_frac"],
                           recruit=cfg["flora_recruit"], N_ref=cfg["flora_N_ref"])
        self.guilds = {}
        for name in cfg["species"]:
            g = SPECIES[name](w, dict(cap=cfg[f"{name}_cap"], capacity=max(SPECIES[name].capacity, cfg[f"{name}_cap"] + 20)))
            self.guilds[name] = g
        if "pack" in self.guilds:
            self.guilds["pack"].metab = cfg["pack_metab"]; self.guilds["pack"].a_attack = cfg["pack_attack"]
        for k, v in cfg.items():
            if "." in k:                         # per-species overrides: "grazer.metab": 0.05 (win over the above)
                sp, attr = k.split(".", 1)
                if sp in self.guilds:
                    setattr(self.guilds[sp], attr, v)
        if bug == "leak_birth":
            for g in self.guilds.values():
                g.body_paid = False
        # seeding (all in macro; the volume is booked as initial)
        meadows = self.flora.pos
        for name, g in self.guilds.items():
            n = cfg[f"{name}_n"]
            if name == "thief":
                nests = meadows[w.rng.choice(len(meadows), 3, replace=False)]
                g.set_nests(nests); g.seed(n, centres=nests, spread=40.0)
            elif name == "lurker":
                g.seed(n, centres=meadows, spread=40.0)
            elif name in ("grazer", "locust"):
                g.seed(n, centres=meadows, spread=120.0)
            else:
                g.seed(n)
        self.traps = SnapTraps(w, n_clumps=cfg["n_clumps"], per=cfg["traps_per"], cap=cfg["trap_cap"],
                               centres=meadows[w.rng.choice(len(meadows), cfg["n_clumps"], replace=False)] + 60.0) if cfg["traps"] else None
        core = w.ball(1, 0.5 * w.R, 0.65 * w.R)[0]
        self.fortress = Fortress(w, core, n=cfg["fortress_n"]) if cfg["fortress"] else None
        if cfg["physarum"]:
            g = w.ball(8, 0.5 * w.R, 0.6 * w.R)
            gc = g[np.argmax(np.linalg.norm(g - core, axis=1))]           # far from the fortress
            self.phys = Physarum(w, gc, n_agents=cfg["phys_agents"])
        else:
            self.phys = None
        for k, v in cfg.items():                 # structure overrides: "physarum.upkeep": 0.002
            if "." in k:
                sp, attr = k.split(".", 1)
                obj = dict(snaptrap=self.traps, fortress=self.fortress, physarum=self.phys).get(sp)
                if obj is not None:
                    setattr(obj, attr, v)
        for i, pol in enumerate(pilots):
            w.add_pilot(Pilot(pol, w.rng, w.R, domain=pilot_domains[i % len(pilot_domains)], name=f"{pol}{i}",
                              trail_spacing=cfg["trail_spacing"], trail_vol=cfg["trail_vol"]))
        w.guild_list = list(self.guilds.values())
        w.predator_pos = []
        self.k = 0; self.cost = []; self.t_macro = 0.0
        self.contact_cd = {}
        self.rams = 0; self.pilot_kills = 0
        self.audit0 = w.audit()

    # ---- the LOD decision (Direction E's sim.py, cut down) -------------------------------------------------
    def hot_regions(self):
        w = self.w
        hot = np.zeros(w.nreg, bool)
        if self.cfg.get("force_hot"):
            return w.rin.copy()                  # consistency check: every region simulated as individuals
        if not w.pilots:
            return hot
        C = w.rcen_all
        for p in w.pilots:
            d = C - p.pos; dd = np.linalg.norm(d, axis=1)
            fwd = (d @ (p.vel / max(np.linalg.norm(p.vel), 1e-6))) / np.maximum(dd, 1e-6)
            hot |= (dd < self.cfg["expand_r"]) | ((dd < self.cfg["ahead_r"]) & (fwd > 0.5))
        return hot & w.rin

    def step(self, dt=0.1):
        t0 = time.perf_counter()
        w = self.w
        w.t += dt; self.k += 1
        for p in w.pilots:
            p.step(w, dt)
        w.rebuild()
        preds = []
        for nm in ("pack",):
            if nm in self.guilds:
                preds.append(self.guilds[nm].agents()[0])
        w.predator_pos = preds
        if self.k % 5 == 0 or self.k == 1:
            hot = self.hot_regions()
            for g in self.guilds.values():
                g.update_lod(hot)
                g.absorb(self.cfg["absorb_r"])
        for g in self.guilds.values():
            g.step(dt)
        prey = [self.guilds[n] for n in ("grazer", "locust", "thief") if n in self.guilds]
        if self.traps:
            self.traps.step(dt, prey)
        if self.fortress:
            self.fortress.step(dt)
        if self.phys:
            if self.k % 2 == 0:
                self.phys.step(2 * dt)                 # the slime mould at 5 Hz (F: 10-20 Hz is invisible)
            self.phys.contacts()
        self._contacts(dt)
        self.t_macro += dt
        if self.t_macro >= 1.0 - 1e-9:
            self.t_macro -= 1.0
            self._macro(1.0)
        self.cost.append(time.perf_counter() - t0)

    def _macro(self, dt):
        w = self.w
        n = w.n
        live = np.flatnonzero(w.alive[:n] & ~w.shield[:n] & (w.excl[:n] == 0))
        live = live[np.linalg.norm(w.pos[live], axis=1) > w.nucleus]
        reg = w.region_of(w.pos[live])
        kinds = w.kind[live].astype(np.int64)
        lists, food = {}, {}
        # macro edible lists: the legacy pair, plus one per guild that declares its own macro_mask (R8)
        specs = [("herb", K_HERB), ("nectar", 1 << FLORA)]
        for g in self.guilds.values():
            mm = getattr(g, "macro_mask", None)
            if mm is not None and ("m%d" % mm) not in dict(specs):
                specs.append(("m%d" % mm, int(mm)))
        for nm, km in specs:
            m = ((km >> kinds) & 1).astype(bool)
            idx = live[m]; r = reg[m]
            perm = w.rng.permutation(len(idx)); idx = idx[perm]; r = r[perm]
            o = np.argsort(r, kind="stable"); idx = idx[o]; r = r[o]
            food[nm] = np.bincount(r, weights=w.vol[idx], minlength=w.nreg)
            cut = np.flatnonzero(np.diff(r)) + 1
            groups = np.split(idx, cut); keys = r[np.concatenate([[0], cut])] if len(r) else []
            lists[nm] = {int(k): list(gp) for k, gp in zip(keys, groups)}
        for name, g in self.guilds.items():
            mm = getattr(g, "macro_mask", None)
            key = ("m%d" % mm) if mm is not None else ("nectar" if name == "thief" else "herb")
            g.macro_step(dt, food[key], lists[key], self.guilds)
        self.flora.grow(dt)

    def _contacts(self, dt):
        """Pilots ram plain structure prisms (an ability = the one sink), get burned by danger prisms, and a
        HUNTER kills the fauna it touches (crystal + skeleton)."""
        w = self.w
        for k, p in enumerate(w.pilots):
            near = w.within(p.pos, 9.0, (1 << WALL) | (1 << TRAP) | (1 << TUBE))
            for j in near:
                if w.danger[j]:
                    sp = "snaptrap" if w.kind[j] == TRAP else "physarum" if w.kind[j] == TUBE else "fortress"
                    if w.t - self.contact_cd.get((k, sp), -1e9) > 1.0:
                        self.contact_cd[(k, sp)] = w.t; w.hit(k, sp, "burn")
                elif not w.shield[j]:
                    w.destroy(int(j)); self.rams += 1
            if p.policy == "hunter":
                for g in list(self.guilds.values()) + ([self.fortress] if self.fortress else []):
                    a = np.flatnonzero(g.alive)
                    if not len(a):
                        continue
                    d = np.linalg.norm(g.pos[a] - p.pos, axis=1)
                    for i in a[d < 10.0]:
                        if isinstance(g, Thief):
                            g.recapture(i, w)
                        g.kill_agent(i, by="pilot"); self.pilot_kills += 1
        tg = [g.agents()[0] for g in self.guilds.values()]
        w.targets = np.concatenate(tg) if tg else np.zeros((0, 3))
        th = [self.guilds[n].agents()[0] for n in ("pack", "locust", "lurker") if n in self.guilds]
        w.threats = np.concatenate(th) if th else np.zeros((0, 3))

    # ---- what the metrics read ---------------------------------------------------------------------------
    def census(self):
        w = self.w
        out = {n: g.count() for n, g in self.guilds.items()}
        out["fortress"] = self.fortress.count() if self.fortress else 0
        out["snaptrap"] = len(self.traps.heart) if self.traps else 0
        out["physarum"] = self.phys.n_tubes() if self.phys else 0
        if self.phys:
            out["physarum_alive"] = int(self.phys.reserve + self.phys.n_tubes() * self.phys.pv > 1.0)
        return out

    def biomass(self):
        w = self.w
        b = {n: g.held() for n, g in self.guilds.items()}
        n = w.n; al = w.alive[:n]; kd = w.kind[:n]; v = w.vol[:n]
        b["flora"] = float(v[al & (kd == FLORA)].sum())
        if self.traps:
            b["snaptrap"] = float(self.traps.reserve.sum() + v[al & (kd == TRAP)].sum())
        if self.fortress:
            b["fortress"] = float(self.fortress.held() + v[al & (kd == WALL)].sum())
        if self.phys:
            b["physarum"] = float(self.phys.reserve + v[al & (kd == TUBE)].sum())
        return b

    def threat_sets(self):
        """species -> (points (n,3), active (n,) bool) that a pilot can meet."""
        out = {}
        for n, g in self.guilds.items():
            P, V, S, act = g.threat_agents()
            out[n] = (P, act)
        if self.traps:
            out["snaptrap"] = self.traps.threat_points()
        if self.fortress:
            out["fortress"] = self.fortress.threat_points()
        if self.phys:
            out["physarum"] = self.phys.threat_points()
        return out

    def affect_agents(self):
        """Every moving body a pilot can see, for the emotion probe: (key array, P, V, size, aspect)."""
        keys, P, V, S, A = [], [], [], [], []
        for n, g in self.guilds.items():
            a = np.flatnonzero(g.alive)
            if not len(a):
                continue
            _, _, sz, _ = g.threat_agents()
            keys += [(n, int(i)) for i in a]; P.append(g.pos[a]); V.append(g.vel[a]); S.append(sz)
            A.append(np.full(len(a), g.aspect))
        if self.fortress:
            a = np.flatnonzero(self.fortress.alive)
            keys += [("fortress", int(i)) for i in a]; P.append(self.fortress.pos[a]); V.append(self.fortress.vel[a])
            S.append(np.full(len(a), 3.0)); A.append(np.full(len(a), 1.2))
        if self.traps and self.traps.heart:
            h = self.traps.heart_pos()
            keys += [("snaptrap", i) for i in range(len(h))]; P.append(h); V.append(np.zeros_like(h))
            S.append(np.array([30.0 if s in (2, 3) else 22.0 for s in self.traps.state])); A.append(np.full(len(h), 1.4))
        if not P:
            return [], np.zeros((0, 3)), np.zeros((0, 3)), np.zeros(0), np.zeros(0)
        return keys, np.concatenate(P), np.concatenate(V), np.concatenate(S), np.concatenate(A)

    def render(self, out):
        for g in self.guilds.values():
            g.render(out)
        if self.traps:
            self.traps.render(out)
        if self.fortress:
            self.fortress.render(out)
        if self.phys:
            self.phys.render(out)
