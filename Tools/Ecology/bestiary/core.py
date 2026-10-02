"""Shared machinery for the bestiary (Direction B, Tools/Ecology/PROGRAM.md).

Every species here is a struct-of-arrays population stepped with vectorised numpy - no per-agent branch
selects a behaviour; each agent's velocity is a WEIGHTED SUM of steering terms whose weights are continuous
functions of its drives (hunger, alarm, density, ...). That is the substrate shape of Direction A
(agents + fields + quorum + context steering), kept small so each species reads on one screen.

What every species shares (`Herd`):
    pos, vel, size, alive, intent (the telegraph channel), body (volume held in the body), gut (volume eaten)
    kills, crystals, deaths                      - scorecard fields
    kill(i)       a pilot killed agent i: one elemental crystal drops (every lifeform drops ONE) and the body +
                  gut is laid back as a skeleton prism - mass conserved (CLAUDE.md, §26 skeleton)
    ledger()      body + gut + carried volume held by the species (the conservation audit)

Game channels a species may use on a pilot (`Arena.hit(pilot, kind, amount)`):
    "bite"   contact strike (a ram / snap / trample)          -> in game: a Strike-class combat hit + drain
    "burn"   touching a danger prism                          -> in game: hostile danger prism burns petals
    "drain"  an attached creature siphons an element          -> in game: elemental drain (a transfer)
    "steal"  a trail prism changes hands                       -> in game: Prism.Steal (the serpent wall verb)
    "eat"    a trail prism eaten                              -> in game: herbivore grazing the trail
Pilots never die; hits only feed the scorecard.
"""
from __future__ import annotations

import math

import numpy as np

TAU = 2 * math.pi


def unit(v: np.ndarray) -> np.ndarray:
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(n, 1e-9)


def clamp_len(v: np.ndarray, m) -> np.ndarray:
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    m = np.asarray(m, float)
    if m.ndim == 1:
        m = m[:, None]
    return v * np.minimum(1.0, m / np.maximum(n, 1e-9))


def steer(vel: np.ndarray, desired: np.ndarray, accel, dt: float) -> np.ndarray:
    """Move `vel` toward `desired` with a bounded acceleration (a body cannot snap its velocity)."""
    return vel + clamp_len(desired - vel, np.asarray(accel, float) * dt)


def contain(pos: np.ndarray, vel: np.ndarray, R: float, k: float = 0.92) -> np.ndarray:
    """Soft membrane: an inward push that grows past k*R."""
    r = np.linalg.norm(pos, axis=1)
    over = np.clip((r - k * R) / (0.06 * R), 0, None)
    return vel - pos / np.maximum(r[:, None], 1e-6) * (over[:, None] * 80.0)


def pairwise(P: np.ndarray):
    """All-pairs offsets and distances (fine for n <= ~400; the game uses the spatial hash)."""
    D = P[None, :, :] - P[:, None, :]        # D[i, j] = P[j] - P[i]
    d = np.linalg.norm(D, axis=2)
    np.fill_diagonal(d, np.inf)
    return D, d


def separation(D, d, radius, mask=None):
    """Sum of unit pushes away from neighbours inside `radius`, weighted (1 - d/r)."""
    w = np.clip(1.0 - d / radius, 0, None)
    if mask is not None:
        w = w * mask[None, :]
    return -(D / np.maximum(d[:, :, None], 1e-6) * w[:, :, None]).sum(axis=1)


class Herd:
    """Base population. Subclasses implement `act(arena, dt)` and `colours()`."""
    name = "herd"
    emotion = ""
    counterplay = ""
    substrate = ""

    def __init__(self, arena, n, centre=None, spread=60.0, size=3.0, body=6.0):
        if centre is None:
            centre = arena._ball(1, 0.35 * arena.R, 0.7 * arena.R)[0]
        self.pos = centre + arena.rng.normal(0, spread, (n, 3))
        self.vel = arena.rng.normal(0, 5, (n, 3))
        self.size = np.full(n, float(size))
        self.alive = np.ones(n, bool)
        self.intent = np.zeros(n)
        self.body = np.full(n, float(body))
        self.gut = np.zeros(n)
        self.kills = 0
        self.crystals = 0
        self.deaths = 0
        self.carried = 0.0
        self.rng = arena.rng
        self.t = 0.0
        self.stat: dict = {}

    # -- scorecard surface (only LIVE agents are exposed; indices compacted - jerk uses stable shapes) --------
    @property
    def agent_pos(self):
        return self.pos[self.alive]

    @property
    def agent_vel(self):
        return self.vel[self.alive]

    @property
    def agent_size(self):
        return self.size[self.alive]

    @property
    def intent_live(self):
        return self.intent[self.alive]

    def ledger(self) -> float:
        return float(self.body[self.alive].sum() + self.gut[self.alive].sum() + self.carried)

    # -- lifecycle ----------------------------------------------------------------------------------------
    def kill(self, i, arena, by_pilot=True):
        if not self.alive[i]:
            return
        self.alive[i] = False
        self.deaths += 1
        if by_pilot:
            self.kills += 1
        self.crystals += 1                       # every lifeform drops ONE elemental crystal
        v = self.body[i] + self.gut[i]           # skeleton: the body's mass stays in the world as prisms
        if v > 0:
            arena.lay_mass(self.pos[i].copy(), v, elem=int(i) % 4)
        self.body[i] = self.gut[i] = 0.0

    def eat_near(self, arena, i, r, trail_only=False, env_only=False, by=None):
        """Agent i eats every edible prism inside r. Returns (volume, list of owners eaten)."""
        idx = arena.mass_near(self.pos[i], r)
        if len(idx) == 0:
            return 0.0, []
        own = arena.mass_owner[idx]
        if trail_only:
            idx = idx[own >= 0]
        if env_only:
            idx = idx[own < 0]
        got, owners = 0.0, []
        for j in idx:
            v = arena.consume(int(j), by or self.name)
            if v > 0:
                got += v
                owners.append(int(arena.mass_owner[j]))
        self.gut[i] += got
        return got, owners

    def pilot_vectors(self, arena):
        """For every agent: offset to its NEAREST pilot, that distance, and the pilot index."""
        if not arena.pilots:
            n = len(self.pos)
            return np.zeros((n, 3)), np.full(n, np.inf), np.zeros(n, int)
        PP = np.array([p.pos for p in arena.pilots])
        D = PP[None, :, :] - self.pos[:, None, :]
        d = np.linalg.norm(D, axis=2)
        k = np.argmin(d, axis=1)
        a = np.arange(len(self.pos))
        return D[a, k], d[a, k], k

    def hunter_contacts(self, arena, extra=4.0, can_kill=None):
        """A HUNTER pilot (the engaging player) kills an agent it touches. `can_kill` masks who is vulnerable."""
        for p in arena.pilots:
            if p.policy != "hunter":
                continue
            d = np.linalg.norm(self.pos - p.pos, axis=1)
            hit = self.alive & (d < p.radius + self.size + extra)
            if can_kill is not None:
                hit &= can_kill
            for i in np.flatnonzero(hit):
                self.kill(i, arena)

    def publish(self, arena, threat_mask=None, target_mask=None):
        a = self.alive
        tm = a if target_mask is None else (a & target_mask)
        th = a if threat_mask is None else (a & threat_mask)
        arena.targets = list(self.pos[tm])
        arena.threats = list(self.pos[th])

    # -- the arena loop calls these ------------------------------------------------------------------------
    def step(self, arena, dt):
        self.t += dt
        self.act(arena, dt)
        self.pos[self.alive] += self.vel[self.alive] * dt

    def render(self, out):
        a = self.alive
        out[self.name] = dict(pos=self.pos[a], col=self.colours()[a], size=self.size[a])

    def colours(self):
        return np.tile([1.0, 1.0, 1.0], (len(self.pos), 1))


class ScoreView:
    """Adapter the shared Probe reads: exposes only live agents + their intent."""

    def __init__(self, sp: Herd):
        self.sp = sp

    @property
    def agent_pos(self):
        return self.sp.agent_pos

    @property
    def agent_vel(self):
        return self.sp.agent_vel

    @property
    def agent_size(self):
        return self.sp.agent_size

    @property
    def intent(self):
        return self.sp.intent_live

    @property
    def kills(self):
        return self.sp.kills

    @property
    def crystals(self):
        return self.sp.crystals
