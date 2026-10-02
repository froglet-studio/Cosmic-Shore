"""Species F1 - SNAP TRAP (Dionaea-like). A colony of traps, each a crystal heart -> a stalk -> two lobes whose
rims carry DANGER teeth.

Behaviour is a small per-trap state machine driven by local sensing only (Burst-shaped: SoA over traps):
    OPEN     lobes at the resting gape; the trap senses any pilot inside `sense`
    PRIMING  a pilot is near: the lobes open WIDE (+dgape) and the trap glows, over `t_prime` s (the TELEGRAPH)
    ARMED    fully open and glowing; if a pilot enters the MOUTH volume the trap fires
    CLOSING  the lobes sweep shut over `t_close` s; a pilot still between them when they meet is SNAPPED, and a
             lobe sweeping past a pilot smacks it (both are danger-prism contacts in game: a burn)
    SHUT     digesting/resetting for `t_reset` s (longer after a catch), then the lobes ease back open
A primed trap whose pilot leaves eases back to OPEN (the glow fades: an un-fired telegraph is not a lie).

Counterplay is real and readable: the gape + glow says "armed"; crossing the mouth faster than t_close
(boost) escapes, and the closing sweep only covers the mouth, so passing outside the lobes is safe.

Replayability comes from the traps turning to FACE TRAFFIC: each trap keeps an EMA of directions toward pilots
it sensed and slerps its mouth axis toward it at `turn_deg` deg/s (heliotropism toward vessels). A colony
grown along your usual lane slowly aims its mouths down that lane - it learns where you fly.

Mass: the colony is planted with `plant_vol` and grows NEW traps (budding, animated growth crystal -> stalk ->
lobes) only from food prisms its roots absorb. Collecting a heart (the cutter) drops the trap's crystal and
leaves its body as an inert skeleton. Nothing decays on a clock.
"""
from __future__ import annotations

import math

import numpy as np

from harness import FloraSpecies, PlantBody, VIEW, GARDEN, seg_point_dist

OPEN, PRIMING, ARMED, CLOSING, SHUT = 0, 1, 2, 3, 4

DEFAULTS = dict(ram=True, heart_in_jaws=True, armour=0.0, n_traps=40, clumps=8, clump_r=70.0, sense=170.0, t_prime=0.7, t_close=0.3, t_reset=3.0,
                t_digest=9.0, gape=35.0, dgape=18.0, mouth_len=55.0, mouth_w=24.0, turn_deg=4.0, bud_bias=0.0,
                root=90.0, absorb_every=4.0, prism_vol=12.0, tooth_vol=8.0, fire_on="mouth")


def _layout():
    """Per-trap prism slots: (kind, r, s, danger). kind 0 = stalk (from the heart), 1/-1 = lobe (+n / -n)."""
    L = [(0, r, 0.0, False) for r in (5.0, 12.0, 19.0)]
    for side in (1, -1):
        L += [(side, r, s, False) for r in (10.0, 22.0, 34.0, 46.0) for s in (-12.0, 12.0)]
        L += [(side, 53.0, s, True) for s in (-18.0, -6.0, 6.0, 18.0)]
    return L


LAYOUT = _layout()
K = len(LAYOUT)
_kind = np.array([l[0] for l in LAYOUT]); _r = np.array([l[1] for l in LAYOUT])
_s = np.array([l[2] for l in LAYOUT]); _dg = np.array([l[3] for l in LAYOUT])


def _frame(a):
    """Orthonormal (n, b) perpendicular to axis a (n,3)."""
    up = np.where(np.abs(a[:, 1:2]) < 0.9, np.array([[0, 1, 0]]), np.array([[1, 0, 0]]))
    n = np.cross(a, up); n /= np.linalg.norm(n, axis=1, keepdims=True)
    b = np.cross(a, n)
    return n, b


class SnapTrap(FloraSpecies):
    name = "snaptrap"

    def __init__(self, arena, params=None):
        self.p = dict(DEFAULTS); self.p.update(params or {}); self.rng_a = np.random.default_rng(99)
        p = self.p; rng = arena.rng
        self.body = PlantBody(4096)
        self.cap = 512
        self.h = np.zeros((self.cap, 3)); self.a = np.zeros((self.cap, 3)); self.state = np.zeros(self.cap, int)
        self.timer = np.zeros(self.cap); self.theta = np.zeros(self.cap); self.itn = np.zeros(self.cap)
        self.grow = np.zeros(self.cap); self.alive = np.zeros(self.cap, bool); self.slots = np.full((self.cap, K), -1)
        self.ema = np.zeros((self.cap, 3)); self.absorb_t = np.zeros(self.cap); self.caught = np.zeros(self.cap, bool)
        self.hot_since = {}            # (trap, pilot) -> t the trap first showed intent>=0.5 within VIEW of pilot
        self.seen_since = {}           # (trap, pilot) -> t the pilot first came within VIEW of the trap
        self.n = 0; self.fired = np.zeros(self.cap)
        self.cost = 3 * p["prism_vol"] + 16 * p["prism_vol"] + 8 * p["tooth_vol"]
        self.reserve = p["n_traps"] * self.cost          # planted with exactly the mass of its initial traps
        self.cut_volume = 0.0; self.crystals = 0; self.deaths = 0; self.leads = []; self.snaps = 0
        centres = arena.grove(p["clumps"], 60.0)
        for i in range(p["n_traps"]):
            c = centres[i % p["clumps"]]
            pos = c + rng.normal(0, p["clump_r"], 3)
            self._sprout(pos, rng, grown=True)
        self._pose()

    # ---- life -------------------------------------------------------------------------------------------
    def _sprout(self, pos, rng, grown=False):
        if self.n >= self.cap or self.reserve < self.cost: return -1
        i = self.n; self.n += 1
        d = rng.normal(size=3); self.a[i] = d / np.linalg.norm(d); self.h[i] = pos
        self.state[i] = OPEN; self.theta[i] = math.radians(self.p["gape"]); self.alive[i] = True
        self.grow[i] = 1.0 if grown else 0.0; self.absorb_t[i] = rng.uniform(0, self.p["absorb_every"])
        self._lay_upto(i, 1.0 if grown else 0.0)
        return i

    def _lay_upto(self, i, g):
        """Growth law: the heart exists first; prisms are laid in slot order (stalk, then lobes from the hinge
        out, teeth last) as growth passes their threshold. Each lay debits the reserve."""
        for k in range(K):
            if self.slots[i, k] >= 0 or k / K > g: continue
            vol = self.p["tooth_vol"] if _dg[k] else self.p["prism_vol"]
            if self.reserve < vol: return
            self.reserve -= vol
            self.slots[i, k] = self.body.lay(self.h[i], 4.0 if not _dg[k] else 3.5, vol, owner=i, danger=bool(_dg[k]),
                                             shield=bool(not _dg[k] and self.rng_a.random() < self.p["armour"]))

    def _pose(self):
        idx = np.flatnonzero(self.slots[:self.n].max(axis=1) >= 0)
        if not len(idx): return
        a = self.a[idx]; n, b = _frame(a)
        m = self.h[idx] + a * 22.0
        th = self.theta[idx][:, None]
        S = self.slots[idx]
        for k in range(K):
            ok = S[:, k] >= 0
            if not ok.any(): continue
            if _kind[k] == 0:
                P = self.h[idx] + a * _r[k]
            else:
                u = np.cos(th) * a + _kind[k] * np.sin(th) * n
                P = m + u * _r[k] + b * _s[k]
            self.body.pos[S[ok, k]] = P[ok]

    def plant_count(self):
        return int(self.alive[:self.n].sum())

    # ---- step -------------------------------------------------------------------------------------------
    def step(self, arena, dt):
        p = self.p; rng = arena.rng; N = self.n
        live = np.flatnonzero(self.alive[:N])
        pil = arena.pilots
        # roots: absorb food, bud new traps
        for i in live:
            if self.grow[i] < 1.0:
                self.grow[i] = min(1.0, self.grow[i] + dt / 6.0); self._lay_upto(i, self.grow[i]); continue
            self.absorb_t[i] -= dt
            if self.absorb_t[i] <= 0:
                self.absorb_t[i] = p["absorb_every"] * rng.uniform(0.8, 1.2)
                f = arena.mass_near(self.h[i], p["root"])
                if len(f):
                    j = f[np.argmin(np.linalg.norm(arena.mass_pos[f] - self.h[i], axis=1))]
                    self.reserve += arena.consume(j, "snaptrap")
                self._lay_upto(i, 1.0)                      # repair rammed/cut prisms from the reserve
        if self.reserve >= self.cost:
            par = live[rng.integers(len(live))] if len(live) else None
            if par is not None:
                d = rng.normal(size=3); d /= np.linalg.norm(d)
                d = d + p["bud_bias"] * 3.0 * self.ema[par]; d /= max(np.linalg.norm(d), 1e-6)
                self._sprout(self.h[par] + d * rng.uniform(45, 80), rng)
        # sensing + state machine (vectorised over traps; pilots are few)
        grown = live[self.grow[live] >= 1.0]
        a = self.a[grown]; n, b = _frame(a) if len(grown) else (a, a)
        m = self.h[grown] + a * 22.0
        near = np.zeros(len(grown), bool); in_mouth = np.zeros(len(grown), bool)
        for pi in pil:
            d = pi.pos - m; dist = np.linalg.norm(d, axis=1)
            sens = dist < p["sense"]; near |= sens
            u = d / np.maximum(dist[:, None], 1e-6)
            self.ema[grown[sens]] = 0.97 * self.ema[grown[sens]] + 0.03 * u[sens]
            z = np.sum(d * a, 1); y = np.sum(d * n, 1); w = np.sum(d * b, 1)
            th = self.theta[grown]
            in_mouth |= (z > 0) & (z < p["mouth_len"]) & (np.abs(w) < p["mouth_w"]) & (np.abs(y) < z * np.tan(th) + 6)
        st = self.state[grown]; tm = self.timer[grown] + dt
        gape = math.radians(p["gape"]); wide = math.radians(p["gape"] + p["dgape"]); shut = math.radians(3.0)
        th = self.theta[grown].copy(); it = self.itn[grown].copy()
        # OPEN -> PRIMING
        go = (st == OPEN) & near; st[go] = PRIMING; tm[go] = 0
        pr = st == PRIMING
        it[pr] = np.minimum(1.0, it[pr] + dt / p["t_prime"])
        th[pr] = gape + (wide - gape) * it[pr]
        back = pr & ~near; it[back] = np.maximum(0, it[back] - 2 * dt / p["t_prime"])
        st[back & (it <= 0)] = OPEN
        st[pr & (it >= 1.0) & near] = ARMED
        ar_ = st == ARMED
        relax = ar_ & ~near; st[relax] = PRIMING
        intact = (self.slots[grown][:, 3:] >= 0).mean(axis=1) >= 0.5   # a jaw with half its lobes gone cannot snap
        fire = (ar_ & in_mouth if p["fire_on"] == "mouth" else ar_ & near) & intact
        st[fire] = CLOSING; tm[fire] = 0.0
        self.fired[grown[fire]] += 1
        cl = st == CLOSING
        th_prev = th.copy()
        th[cl] = np.maximum(shut, wide - (wide - shut) * tm[cl] / p["t_close"])
        it[cl] = 1.0
        done = cl & (tm >= p["t_close"])
        # the closing sweep: a lobe that passed the pilot this step, or a pilot still inside when shut -> snap
        for pi in pil:
            d = pi.pos - m
            z = np.sum(d * a, 1); y = np.abs(np.sum(d * n, 1)); w = np.abs(np.sum(d * b, 1))
            inslab = (z > 0) & (z < p["mouth_len"] + 6) & (w < p["mouth_w"] + 6)
            swept = cl & inslab & (y < z * np.tan(th_prev) + 6) & (y > z * np.tan(th) - 6)
            caught = done & inslab & (y < z * np.tan(th) + 8)
            for j in np.flatnonzero(swept | caught):
                i = grown[j]
                if self.caught[i]: continue
                self.caught[i] = True; self.snaps += 1
                self._credit(arena, i, pi)
                arena.hit(pi, "snap")
        st[done] = SHUT; tm[done] = 0.0
        sh = st == SHUT
        reset = np.where(self.caught[grown], p["t_digest"], p["t_reset"])
        it[sh] = np.maximum(0, it[sh] - dt / 0.5)
        reopen = sh & (tm > reset)
        th[sh] = np.where(reopen[sh], np.minimum(gape, th[sh] + dt * (gape - shut) / 1.5), shut)
        opened = reopen & (th >= gape - 1e-6)
        st[opened] = OPEN; self.caught[grown[opened]] = False
        self.state[grown] = st; self.timer[grown] = tm; self.theta[grown] = th; self.itn[grown] = it
        # heliotropism toward traffic
        for i in grown:
            e = self.ema[i]; ne = np.linalg.norm(e)
            if ne < 0.05 or self.state[i] in (CLOSING,): continue
            tgt = e / ne; cur = self.a[i]
            ang = math.acos(float(np.clip(cur @ tgt, -1, 1)))
            k = min(1.0, math.radians(p["turn_deg"]) * dt / max(ang, 1e-6))
            v = cur + (tgt - cur) * k; self.a[i] = v / np.linalg.norm(v)
        self._pose()
        # glow mirrors intent on the body; teeth always glow (danger material)
        bi = self.body.live()
        ow = self.body.owner[bi]
        self.body.hot[bi] = np.where(self.body.danger[bi], 0.6, 0.0)
        okb = ow >= 0
        self.body.hot[bi[okb]] = np.maximum(self.body.hot[bi[okb]], self.itn[ow[okb]] * self.alive[ow[okb]])
        # rim teeth burn on contact (a pilot that grazes the lobes outside a snap)
        for pi in pil:
            c = self.body.contacts(pi.prev, pi.pos, 6.0)
            for j in c:
                if not self.body.danger[j]: continue
                key = (int(j), pi.name)
                if self.body.burn_cd.get(key, -1e9) > arena.t: continue
                self.body.burn_cd[key] = arena.t + 1.0
                o = self.body.owner[j]
                if o >= 0 and self.caught[o]: continue   # the snap that just landed already counted
                self._credit(arena, o, pi); arena.hit(pi, "burn")
        self._ram(arena)
        # telegraph bookkeeping (per trap, per pilot)
        for pi in pil:
            dist = np.linalg.norm(self.h[:N] - pi.pos, axis=1)
            for i in np.flatnonzero((dist < VIEW) & self.alive[:N]):
                self.seen_since.setdefault((i, pi.name), arena.t)
                if self.itn[i] >= 0.5: self.hot_since.setdefault((i, pi.name), arena.t)
            for i in np.flatnonzero(dist >= VIEW):
                self.seen_since.pop((i, pi.name), None)
            for i in np.flatnonzero(self.itn[:N] < 0.2):
                self.hot_since.pop((i, pi.name), None)
        # common Probe channels

    @property
    def agent_pos(self):
        return self.h[:self.n][self.alive[:self.n]]

    @property
    def agent_vel(self):
        return np.zeros((int(self.alive[:self.n].sum()), 3))

    @property
    def agent_size(self):
        return np.full(int(self.alive[:self.n].sum()), 30.0)

    @property
    def intent(self):
        return self.itn[:self.n][self.alive[:self.n]]

    def _credit(self, arena, i, pi):
        if i < 0: return
        t0 = self.hot_since.get((i, pi.name), self.seen_since.get((i, pi.name), arena.t))
        self.leads.append(arena.t - t0)

    # ---- the reading pilot's view, threats, cutting -----------------------------------------------------
    def hazards(self):
        N = self.n
        live = np.flatnonzero(self.alive[:N] & (self.grow[:N] >= 1))
        hot = live[self.itn[live] > 0.25]
        # the glow must COVER the strike volume: at full gape the lip spans +-(L tan(gape+dgape)) - round 3's r=0.7L
        # sphere did not, and readers were snapped 9 times in 2 min by mouths that looked smaller than they were
        L = self.p["mouth_len"]; lip = L * math.tan(math.radians(self.p["gape"] + self.p["dgape"]))
        mouths = self.h[hot] + self.a[hot] * (22.0 + L * 0.6)
        bi = self.body.live(); teeth = bi[self.body.danger[bi]]
        H = np.concatenate([mouths, self.body.pos[teeth]])
        R = np.concatenate([np.full(len(hot), max(L * 0.7, lip)), np.full(len(teeth), 8.0)])
        W = np.concatenate([self.itn[hot], np.full(len(teeth), 0.5)])
        return H, R, W, None

    def threat_elements(self):
        N = self.n
        live = np.flatnonzero(self.alive[:N] & (self.grow[:N] >= 1))
        armed = live[np.isin(self.state[live], (OPEN, PRIMING, ARMED, CLOSING))]   # latent: any trap that can fire
        mouths = self.h[armed] + self.a[armed] * (22.0 + self.p["mouth_len"] * 0.5)
        bi = self.body.live(); teeth = bi[self.body.danger[bi]]
        return (np.concatenate([mouths, self.body.pos[teeth]]),
                np.concatenate([np.full(len(armed), self.p["mouth_len"] * 0.6), np.full(len(teeth), 3.5)]))

    def heart_pos(self, idx):
        """The crystal sits at the HINGE, between the lobes - the bait is in the jaws (heart_in_jaws), so taking it
        means diving into the mouth fast enough to beat t_close. Off: the heart sits at the stalk's root."""
        if not self.p["heart_in_jaws"]: return self.h[idx]
        return self.h[idx] + self.a[idx] * 30.0

    def cut_targets(self):
        live = np.flatnonzero(self.alive[:self.n] & (self.grow[:self.n] >= 1))
        return self.heart_pos(live)

    def cut(self, arena, pilot, a, b):
        c = self.body.contacts(a, b, 14.0)
        for j in c:
            if self.body.danger[j]: continue             # teeth burn, they do not cut
            o = self.body.owner[j]
            v = self.body.take(j)
            if not v: continue
            self.cut_volume += v
            if o >= 0: self.slots[o][self.slots[o] == j] = -1
        live = np.flatnonzero(self.alive[:self.n])
        if len(live):
            d = seg_point_dist(a, b, self.heart_pos(live))
            for i in live[d < 12.0]:
                self._die(i)

    def _die(self, i):
        """Heart collected: the crystal drops; the body stays as an inert skeleton (plain mass)."""
        self.alive[i] = False; self.crystals += 1; self.deaths += 1; self.itn[i] = 0
        for j in self.slots[i]:
            if j >= 0: self.body.danger[j] = False; self.body.owner[j] = -1

    def _ram(self, arena):
        """A pilot flying through PLAIN plant prisms breaks them (an active force; mass leaves as cut mass)."""
        if not self.p["ram"]: return
        for pi in arena.pilots:
            for j in self.body.contacts(pi.prev, pi.pos, pi.radius):
                if self.body.danger[j]: continue
                v = self.body.take(j)
                if not v: continue
                self.cut_volume += v
                o = self.body.owner[j]
                if o >= 0: self.slots[o][self.slots[o] == j] = -1

    def remove_ball(self, arena, c, r):
        bi = self.body.live(); m = bi[np.linalg.norm(self.body.pos[bi] - c, axis=1) < r]
        for j in m:
            v = self.body.take(j)
            if not v: continue
            self.cut_volume += v; o = self.body.owner[j]
            if o >= 0: self.slots[o][self.slots[o] == j] = -1

    def mass_total(self):
        return self.reserve + self.body.total_volume()

    def lane_cut(self, arena, ev):
        return None

    # ---- viewer -----------------------------------------------------------------------------------------
    def render(self, out):
        bi = self.body.live()
        hot = self.body.hot[bi]; dg = self.body.danger[bi]
        base = np.where(dg[:, None], np.array([[1.0, 0.25, 0.15]]), np.array([[0.35, 0.8, 0.35]]))
        col = base * (0.55 + 0.45 * hot[:, None]) + np.array([[0.6, 0.6, 0.1]]) * hot[:, None] * (~dg[:, None])
        out["snaptrap"] = dict(pos=self.body.pos[bi], col=np.clip(col, 0, 1), size=np.where(dg, 5.0, 7.0))
        live = np.flatnonzero(self.alive[:self.n])
        out["hearts"] = dict(pos=self.h[live], col=np.tile([[0.4, 0.7, 1.0]], (len(live), 1)), size=np.full(len(live), 9.0))


def _sig(self):
    """Per-trap fire count (who fired is what twin runs should disagree on)."""
    return self.fired[:self.n] if hasattr(self, "fired") else np.zeros(0)


SnapTrap.signature = _sig
