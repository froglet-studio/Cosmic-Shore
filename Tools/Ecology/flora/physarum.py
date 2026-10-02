"""Species F3 - PHYSARUM NETWORK (Jones 2010, "Characteristics of pattern formation and evolution in approximations
of Physarum transport networks", Artificial Life 16(2)), lifted to 3D and given a body of prisms.

The model is two layers, both trivially GPU-parallel (one thread per agent, one thread per voxel):
    agents   N particles, each a position + heading. Every step it samples the TRAIL field at `so` u ahead along
             its heading and at four sensors tilted `sa` degrees off it (a cone; Jones' left/forward/right in 3D,
             the ring's phase is per agent so the grid imposes no axis). It turns `ra` degrees toward the best
             sensor if that beats forward, moves `ss` u/s, and deposits `dep` into its voxel.
    trail    a scalar grid over the grove: 3x3x3 diffusion (`diffuse`) and evaporation (`evap`) - a CHEMICAL
             signal, not mass. Food prisms deposit `food_dep` every step, so the agents' trails condense into
             transport TUBES between food sources (the Tero et al. Tokyo-rail result, in a cell).

Tubes are the plant's body. A voxel whose slow EMA of trail passes `on` becomes a tube PRISM, laid from the
organism's reserve; one that falls under `off` is RESORBED back into it (transport, not decay - the reserve is
conserved). So the body is bounded by the mass it holds: planted `plant_vol` plus food it DIGESTS (a tube voxel
touching a food prism absorbs it into the reserve at `digest` per s), which deletes that food's attractant and
makes the network MIGRATE to the next food - a slime mould foraging across the grove.

The danger is PERISTALSIS: the tube network is an excitable medium (Greenberg-Hastings on tube voxels).
`hearts` - the organism's crystals, each a pacemaker - fire every `period` s; the excitation travels along the
tubes at ~`wave_speed` u/s, a voxel stays excited (DANGER, bright) for `ex_ticks` ticks then refractory.
A pilot touching an excited tube is burned; a resting tube is a wall (a BUMP, counted but not a burn). The wave
is the telegraph: you watch the pulse run toward you along the tube.

Cut a tube (the cutter) and its prisms leave as cut mass and the trail there is wiped; the agents re-route around
the hole and the network RE-GROWS the link or finds another (measured by `lane_cut`). Collecting a heart drops its
crystal and removes it as a pacemaker and agent source; with no hearts left the body is an inert skeleton.
"""
from __future__ import annotations

import math

import numpy as np

from harness import FloraSpecies, PlantBody, VIEW, GROVE_C, GROVE_R, seg_point_dist

DEFAULTS = dict(G=56, n_agents=16000, n_hearts=5, so=28.0, sa=30.0, ra=35.0, ss=40.0, dep=1.0,
                diffuse=0.5, evap=0.08, food_dep=1.5, on=6.0, off=3.0, ema=0.05, prism_vol=10.0,
                plant_vol=12000.0, digest=0.3, period=3.0, wave_speed=50.0, ex_ticks=2, refr=4,
                wake_dep=0.0, warmup=250, jitter=0.15, spread_init=1, heart_speed=6.0, ram=True, armour=0.0, heart_guard=0.0, beat_on=0.6, beat_glow=0.8)

_NB6 = ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))


def _blur(T):
    """Separable 3-tap box blur (the 3x3x3 mean, edges clamped)."""
    for ax in range(3):
        a = np.swapaxes(T, 0, ax)
        b = a.copy(); b[1:] += a[:-1]; b[:-1] += a[1:]
        b[0] += a[0]; b[-1] += a[-1]
        T = np.swapaxes(b / 3.0, 0, ax)
    return T


class Physarum(FloraSpecies):
    name = "physarum"

    def __init__(self, arena, params=None):
        self.p = dict(DEFAULTS); self.p.update(params or {}); p = self.p
        self.rng = np.random.default_rng(int(arena.rng.integers(1 << 30)))
        rng = self.rng
        G = self.G = int(p["G"]); self.h = 2 * GROVE_R / G; self.o = GROVE_C - GROVE_R
        self.T = np.zeros((G, G, G), np.float32); self.S = np.zeros_like(self.T)
        c = (np.arange(G) + 0.5) * self.h - GROVE_R
        X, Y, Z = np.meshgrid(c, c, c, indexing="ij")
        self.inside = (X ** 2 + Y ** 2 + Z ** 2) < GROVE_R ** 2
        self.centres = np.stack([X, Y, Z], -1) + GROVE_C
        self.vox_prism = np.full(G ** 3, -1, np.int64); self.vox_shield = np.zeros(G ** 3, bool)
        self.E = np.zeros(G ** 3, np.int8); self.W = np.zeros(G ** 3, np.int64)
        self.body = PlantBody(8192)
        self.reserve = p["plant_vol"]
        self.cut_volume = 0.0; self.crystals = 0; self.deaths = 0; self.leads = []; self.bumps = 0
        self.hearts = arena.grove(p["n_hearts"], 80.0); self.heart_alive = np.ones(p["n_hearts"], bool)
        self.next_beat = rng.uniform(0, p["period"], p["n_hearts"]); self.wave_id = 0
        self.wave_seen = {}; self.burn_cd = {}; self.tick_acc = 0.0
        n = p["n_agents"]
        self.owner = rng.integers(0, p["n_hearts"], n)
        d = rng.normal(size=(n, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        if p.get("spread_init", 1):        # Jones: the plasmodium starts as a sheet over the whole domain
            self.apos = arena.grove(n, self.h)
        else:
            self.apos = self.hearts[self.owner] + d * rng.uniform(0, 60, (n, 1))
        self.adir = rng.normal(size=(n, 3)); self.adir /= np.linalg.norm(self.adir, axis=1, keepdims=True)
        self.aphase = rng.uniform(0, 2 * math.pi, n)
        self.fired_vox = np.zeros(G ** 3, np.int32); self.t_now = 0.0
        self.t_local = 0.0
        for _ in range(int(p["warmup"])):           # the grove before you arrive
            self._sim(arena, 0.1, warm=True)

    # ---- grid helpers -----------------------------------------------------------------------------------
    def vox(self, P):
        g = np.clip(((P - self.o) / self.h).astype(np.int64), 0, self.G - 1)
        return (g[..., 0] * self.G + g[..., 1]) * self.G + g[..., 2]

    def _sample(self, P):
        return self.T.ravel()[self.vox(P)]

    # ---- simulation -------------------------------------------------------------------------------------
    def _sim(self, arena, dt, warm=False):
        p = self.p; rng = self.rng; G = self.G
        live = self.heart_alive[self.owner]
        A = self.apos[live]; D = self.adir[live]; ph = self.aphase[live]
        if len(A):
            up = np.where(np.abs(D[:, 1:2]) < 0.9, np.array([[0, 1, 0]]), np.array([[1, 0, 0]]))
            u = np.cross(D, up); u /= np.linalg.norm(u, axis=1, keepdims=True); v = np.cross(D, u)
            sa = math.radians(p["sa"])
            F = self._sample(A + D * p["so"])
            best = F.copy(); bdir = D.copy()
            for k in range(4):
                th = ph + k * math.pi / 2
                sd = math.cos(sa) * D + math.sin(sa) * (np.cos(th)[:, None] * u + np.sin(th)[:, None] * v)
                s = self._sample(A + sd * p["so"])
                m = s > best; best[m] = s[m]; bdir[m] = sd[m]
            ra = math.radians(p["ra"]) / math.radians(p["sa"])
            turn = best > F
            D = np.where(turn[:, None], D + (bdir - D) * min(1.0, ra), D)
            D += rng.normal(0, p["jitter"], D.shape)
            D /= np.linalg.norm(D, axis=1, keepdims=True)
            A = A + D * p["ss"] * dt
            out = np.linalg.norm(A - GROVE_C, axis=1) > GROVE_R - self.h
            if out.any():
                D[out] = -D[out] + rng.normal(0, 0.3, (out.sum(), 3))
                D[out] /= np.linalg.norm(D[out], axis=1, keepdims=True)
                A[out] = GROVE_C + (A[out] - GROVE_C) * ((GROVE_R - 1.5 * self.h) / np.linalg.norm(A[out] - GROVE_C, axis=1))[:, None]
            self.apos[live] = A; self.adir[live] = D
            np.add.at(self.T.ravel(), self.vox(A), p["dep"])
        # food attractant (live food only) + optional vessel wake
        fa = np.flatnonzero(arena.mass_alive)
        if len(fa):
            np.add.at(self.T.ravel(), self.vox(arena.mass_pos[fa]), p["food_dep"])
        if p["wake_dep"] and not warm:
            for pi in arena.pilots:
                self.T.ravel()[self.vox(pi.pos)] += p["wake_dep"]
        # diffuse + evaporate (a chemical, not mass)
        self.T = (1 - p["diffuse"]) * self.T + p["diffuse"] * _blur(self.T)
        self.T *= (1 - p["evap"]); self.T[~self.inside] = 0
        np.maximum(self.T, 0, out=self.T)
        self.S += (self.T - self.S) * p["ema"]
        self._tubes(arena)
        self._digest(arena, dt)
        self._hearts(dt)

    def _hearts(self, dt):
        """A heart (sclerotium) climbs the slow trail gradient toward the thickest tube - so the crystal ends up
        inside the pulsing network instead of out in the open."""
        if self.p["heart_speed"] <= 0: return
        S = self.S.ravel()
        for k in np.flatnonzero(self.heart_alive):
            here = S[self.vox(self.hearts[k])]; best, bd = here, None
            for d in _NB6:
                q = self.hearts[k] + np.array(d) * self.h
                if np.linalg.norm(q - GROVE_C) > GROVE_R - self.h: continue
                s = S[self.vox(q)]
                if s > best: best, bd = s, np.array(d, float)
            if bd is not None:
                self.hearts[k] += bd * self.p["heart_speed"] * dt

    def _tubes(self, arena):
        p = self.p; S = self.S.ravel()
        has = self.vox_prism >= 0
        if self.heart_alive.any():
            # resorb first (it funds growth), then lay where the trail is strong, strongest first
            gone = np.flatnonzero(has & (S < p["off"]))
            for v in gone:
                self.reserve += self.body.resorb(self.vox_prism[v]); self.vox_prism[v] = -1; self.E[v] = 0
            cand = np.flatnonzero((self.vox_prism < 0) & (S > p["on"]) & self.inside.ravel())
            if len(cand):
                cand = cand[np.argsort(-S[cand])]
                k = int(min(len(cand), self.reserve // p["prism_vol"]))
                cen = self.centres.reshape(-1, 3)
                for v in cand[:k]:
                    self.reserve -= p["prism_vol"]
                    self.vox_prism[v] = self.body.lay(cen[v], self.h * 0.5, p["prism_vol"])
                    self.vox_shield[v] = self.rng.random() < p["armour"]

    def _digest(self, arena, dt):
        """Tube voxels that hold food absorb it into the reserve (a fraction of a prism's worth per tick is
        not representable, so a food prism is absorbed whole with probability digest*dt/vol)."""
        fa = np.flatnonzero(arena.mass_alive)
        if not len(fa) or not self.heart_alive.any(): return
        tube = self.vox_prism[self.vox(arena.mass_pos[fa])] >= 0
        cand = fa[tube]
        if not len(cand): return
        pr = self.p["digest"] * dt / np.maximum(arena.mass_vol[cand], 1e-6)
        for j in cand[self.rng.random(len(cand)) < pr]:
            self.reserve += arena.consume(j, "physarum")

    def _waves(self, arena, dt):
        """Greenberg-Hastings on tube voxels, advanced at wave_speed."""
        p = self.p; G = self.G
        self.tick_acc += dt
        tick = self.h / p["wave_speed"]
        while self.tick_acc >= tick:
            self.tick_acc -= tick
            E = self.E.reshape(G, G, G); W = self.W.reshape(G, G, G)
            tube = (self.vox_prism >= 0).reshape(G, G, G)
            ex = E == 1
            nb = np.zeros_like(ex); wid = np.zeros_like(W)
            for dx, dy, dz in _NB6:
                s = np.roll(ex, (dx, dy, dz), (0, 1, 2)); nb |= s
                wid = np.maximum(wid, np.roll(np.where(ex, W, 0), (dx, dy, dz), (0, 1, 2)))
            newE = E.copy()
            # 1..ex_ticks = excited (danger); ex_ticks+1 .. ex_ticks+refr = refractory
            act = (E >= 1); newE[act] = E[act] + 1
            newE[newE > p["ex_ticks"] + p["refr"]] = 0
            # excitation is "E in [1, ex_ticks]"; spreading uses voxels that are excited this tick
            start = tube & (E == 0) & nb
            newE[start] = 1; W[start] = wid[start]
            # pacemakers: each living heart fires its nearest tube voxel
            for k in np.flatnonzero(self.heart_alive):
                if arena.t >= self.next_beat[k]:
                    self.next_beat[k] = arena.t + p["period"]
                    tv = np.flatnonzero(tube.ravel())
                    if len(tv):
                        cen = self.centres.reshape(-1, 3)[tv]
                        v = tv[np.argmin(np.linalg.norm(cen - self.hearts[k], axis=1))]
                        if newE.ravel()[v] == 0:
                            self.wave_id += 1
                            newE.ravel()[v] = 1; W.ravel()[v] = self.wave_id
            newE[~tube] = 0
            self.E = newE.ravel(); self.W = W.ravel()
            self.fired_vox += (self.E == 1)

    def danger_mask(self):
        return (self.E >= 1) & (self.E <= self.p["ex_ticks"]) & (self.vox_prism >= 0)

    def beating(self, t):
        """SCLEROTIUM BEAT (round 5): each living heart's pacemaker beat stings within heart_guard for beat_on s, and
        glows beat_glow s before it - the crystal is taken by diving in BETWEEN beats. (A guard made of nearby tube
        voxels did nothing: hearts climb the trail gradient but rarely sit inside a tube - 7 crystals/min, 0 burns.)"""
        last = self.next_beat - self.p["period"]
        on = self.heart_alive & ((t - last) < self.p["beat_on"]) & (last >= 0)
        glow = self.heart_alive & ((self.next_beat - t) < self.p["beat_glow"])
        return on, glow

    # ---- step -------------------------------------------------------------------------------------------
    def step(self, arena, dt):
        self.t_now = arena.t
        self._sim(arena, dt)
        self._waves(arena, dt)
        dm = self.danger_mask()
        cen = self.centres.reshape(-1, 3)
        # telegraph bookkeeping: first time each wave was visible to each pilot
        exv = np.flatnonzero(dm)
        for pi in arena.pilots:
            if len(exv):
                vis = exv[np.linalg.norm(cen[exv] - pi.pos, axis=1) < VIEW]
                for w in np.unique(self.W[vis]):
                    self.wave_seen.setdefault((int(w), pi.name), arena.t)
        # contacts
        for pi in arena.pilots:
            seg = pi.pos - pi.prev; L = np.linalg.norm(seg)
            n = max(2, int(L / 4.0) + 1)
            P = pi.prev + np.linspace(0, 1, n)[:, None] * seg
            vs = np.unique(self.vox(P))
            vs = vs[self.vox_prism[vs] >= 0]
            if not len(vs): continue
            hot = vs[dm[vs]]
            if len(hot) and self.burn_cd.get(pi.name, -1e9) <= arena.t:
                self.burn_cd[pi.name] = arena.t + 1.0
                w = int(self.W[hot[0]])
                self.leads.append(arena.t - self.wave_seen.get((w, pi.name), arena.t))
                arena.hit(pi, "burn")
            cold = vs[~dm[vs]]
            if len(cold):
                self.bumps += len(cold)
                if self.p["ram"]: self._remove(cold)          # flying through a resting tube breaks it
        if self.p["heart_guard"] > 0:
            on, glow = self.beating(arena.t)
            for pi in arena.pilots:
                d = seg_point_dist(pi.prev, pi.pos, self.hearts)
                hit = np.flatnonzero(on & (d < self.p["heart_guard"]))
                if len(hit) and self.burn_cd.get(pi.name, -1e9) <= arena.t:
                    self.burn_cd[pi.name] = arena.t + 1.0
                    k = int(hit[0]); self.leads.append(self.p["beat_glow"] + (arena.t - (self.next_beat[k] - self.p["period"])))
                    arena.hit(pi, "burn")
        bi = self.body.live(); self.body.hot[bi] = 0.0
        hv = np.flatnonzero(dm)
        self.body.hot[self.vox_prism[hv]] = 1.0
        self.body.danger[:self.body.n] = False; self.body.danger[self.vox_prism[hv]] = True

    # ---- reader, threats, cutting -----------------------------------------------------------------------
    def hazards(self):
        cen = self.centres.reshape(-1, 3)
        dm = self.danger_mask()
        # the pulse and the voxels it is about to enter (tube neighbours of the front) are what a reader sees
        tube = np.flatnonzero(self.vox_prism >= 0)
        hot = np.flatnonzero(dm)
        H = np.concatenate([cen[hot], cen[tube]])
        R = np.concatenate([np.full(len(hot), self.h * 1.5), np.full(len(tube), self.h * 0.6)])
        W = np.concatenate([np.ones(len(hot)), np.full(len(tube), 0.12)])
        if self.p["heart_guard"] > 0:
            on, glow = self.beating(self.t_now)
            b = np.flatnonzero(on | glow)
            H = np.concatenate([H, self.hearts[b]]); R = np.concatenate([R, np.full(len(b), self.p["heart_guard"])])
            W = np.concatenate([W, np.ones(len(b))])
        return H, R, W, None

    def threat_elements(self):
        tube = np.flatnonzero(self.vox_prism >= 0)            # latent: every tube carries the pulse
        return self.centres.reshape(-1, 3)[tube], np.full(len(tube), self.h * 0.5)

    def cut_targets(self):
        return self.hearts[self.heart_alive]

    def cut(self, arena, pilot, a, b):
        seg = b - a; L = np.linalg.norm(seg); n = max(2, int(L / 4.0) + 1)
        P = a + np.linspace(0, 1, n)[:, None] * seg
        G = self.G
        g = np.clip(((P - self.o) / self.h).astype(np.int64), 0, G - 1)
        vs = set()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    q = np.clip(g + np.array([dx, dy, dz]), 0, G - 1)
                    vs.update(((q[:, 0] * G + q[:, 1]) * G + q[:, 2]).tolist())
        vs = np.array(sorted(vs))
        cen = self.centres.reshape(-1, 3)
        vs = vs[seg_point_dist(a, b, cen[vs]) < 14.0 + self.h * 0.5]
        self._remove(vs)
        d = seg_point_dist(a, b, self.hearts)
        for k in np.flatnonzero(self.heart_alive & (d < 12.0)):
            self.heart_alive[k] = False; self.crystals += 1; self.deaths += 1

    def _remove(self, vs):
        vs = np.asarray(vs, np.int64)
        sh = vs[self.vox_shield[vs] & (self.vox_prism[vs] >= 0)]
        self.vox_shield[sh] = False                      # CHARGE armour: the first hit sheds the shield
        vs = np.setdiff1d(vs, sh)
        for v in vs:
            j = self.vox_prism[v]
            if j >= 0:
                self.body.alive[j] = False; self.cut_volume += float(self.body.vol[j]); self.vox_prism[v] = -1
        self.T.ravel()[vs] = 0; self.S.ravel()[vs] = 0; self.E[vs] = 0

    def lane_cut(self, arena, ev):
        """The re-route experiment: wipe every tube in a ball of radius ev['r'] around the densest tube region
        and return a closure that reports recovery when the run ends."""
        cen = self.centres.reshape(-1, 3)
        tube = np.flatnonzero(self.vox_prism >= 0)
        if not len(tube): return dict(pre=0)
        c = cen[tube[np.argmax([np.sum(np.linalg.norm(cen[tube] - cen[t], axis=1) < ev["r"]) for t in tube[::7]]) * 7]]
        ball = np.flatnonzero(np.linalg.norm(cen - c, axis=1) < ev["r"])
        pre = int((self.vox_prism[ball] >= 0).sum())
        self._remove(ball)
        self.cut_ball = (ball, pre, arena.t, [])
        return dict(pre=pre, centre=c.round(1).tolist())

    def remove_ball(self, arena, c, r):
        cen = self.centres.reshape(-1, 3)
        self._remove(np.flatnonzero(np.linalg.norm(cen - c, axis=1) < r))

    def mass_total(self):
        return self.reserve + self.body.total_volume()

    def signature(self):
        return self.fired_vox.astype(float)

    @property
    def agent_pos(self):
        return self.hearts[self.heart_alive]

    @property
    def agent_vel(self):
        return np.zeros((int(self.heart_alive.sum()), 3))

    @property
    def intent(self):
        return np.zeros(int(self.heart_alive.sum()))

    def render(self, out):
        bi = self.body.live(); hot = self.body.hot[bi]
        col = np.array([[0.95, 0.8, 0.25]]) * (1 - hot[:, None]) * 0.6 + np.array([[1.0, 0.3, 0.1]]) * hot[:, None]
        out["tubes"] = dict(pos=self.body.pos[bi], col=col, size=np.full(len(bi), self.h * 0.7))
        k = np.arange(0, len(self.apos), 8)
        k = k[self.heart_alive[self.owner[k]]]
        out["plasmodium"] = dict(pos=self.apos[k], col=np.tile([[0.9, 0.85, 0.5]], (len(k), 1)) * 0.5, size=np.full(len(k), 1.5))
        out["hearts"] = dict(pos=self.hearts[self.heart_alive], col=np.tile([[0.4, 0.7, 1.0]], (int(self.heart_alive.sum()), 1)),
                             size=np.full(int(self.heart_alive.sum()), 10.0))
