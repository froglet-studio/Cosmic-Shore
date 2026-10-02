"""Species 4 - WEARERS (body-from-stolen-mass). A swarm of small hearts that steal prisms and WEAR them as their
body. Each theft makes a body bigger; bodies that meet FUSE into one creature; past a size threshold the thing that
was skulking behind your trail turns round and hunts you. Your own trail becomes the monster.

Body growth is a local attachment rule on the creature's own lattice (s = 6 u, heart at the origin): a new prism
attaches to a free site touching the body, chosen with weight nb^-alpha (nb = occupied 26-neighbours). alpha > 0
grows spiky DLA-like arms; alpha < 0 grows a compact blob; alpha ~ 0 is Eden growth. No body plan exists.

Phase (quorum on worn volume, not a script): below V_hunt a body is a THIEF - it shadows a pilot's trail at a
distance, steals the freshest prisms and flees if the pilot turns on it. Above V_hunt it is a HUNTER: it closes,
REARS for `windup` s (intent rises, the body contracts - the telegraph), then LUNGES. Speed falls with size,
reach grows with it.

Counterplay / payoff: a ramming pilot STRIPS every body prism it touches - the prism is stolen BACK to the pilot's
domain and falls loose (mass conserved, nothing destroyed). A heart with fewer than `exposed_at` body prisms near
it is exposed; touching it kills that member (one crystal). Killing a monster's leader splits it.
"""
from __future__ import annotations

import numpy as np

from builders.core import N26


class Wearers:
    name = "wearers"
    note = ("Wearers: hearts (white) steal prisms - mostly your trail - and wear them as a body that grows and fuses. "
            "Small ones skulk behind your trail; past the threshold the monster rears (body contracts) and lunges. "
            "Ram it to strip your prisms back.")

    def __init__(self, arena, seed=0, n=40, alpha=1.0, V_hunt=600.0, speed=95.0, windup=1.0, lunge=2.2,
                 sense=180.0, exposed_at=3, s=6.0, fuse=True, dom=2, slow=0.15, rear_at=140.0, intercept=True,
                 contact=0.0, body_cap=0, keep=0.6, births=20):
        self.rng = np.random.default_rng(seed + 13)
        n0 = n; n = n + (births if body_cap else 0)       # spare slots for hearts born at a lair
        self.n, self.alpha, self.V_hunt, self.speed0, self.windup, self.lunge_k = n, alpha, V_hunt, speed, windup, lunge
        self.sense, self.exposed_at, self.s, self.fuse, self.dom = sense, exposed_at, s, fuse, dom
        self.slow, self.rear_at, self.intercept = slow, rear_at, intercept
        self.contact = contact          # 0 = draw from the whole frontier; >0 = stick near the touch point (temp.)
        c = arena._ball(1, 300, 800)[0]
        self.agent_pos = c + self.rng.normal(0, 150, (n, 3))
        self.agent_vel = np.zeros((n, 3)); self.agent_size = np.full(n, 2.0); self.intent = np.zeros(n)
        self.alive = np.zeros(n, bool); self.alive[:n0] = True
        self.unborn = ~self.alive.copy()
        self.body_cap, self.keep = body_cap, keep
        self.lair = {}                                    # mass index -> world position (static, shed by a moult)
        self.moults = 0; self.born = 0
        self.leader = np.arange(n)                         # leader[k] == k: an independent creature
        self.offset = np.zeros((n, 3))                     # a follower heart's offset in its leader's frame
        self.body = [dict() for _ in range(n)]            # leader: site -> mass index   (heart at (0,0,0) reserved)
        self.goal = np.full(n, -1, np.int64)
        self.phase = np.zeros(n, np.int8)                  # 0 thief, 1 approach, 2 rear, 3 lunge, 4 recover
        self.ptime = np.zeros(n)
        self.wander = self.rng.normal(size=(n, 3))
        self.kills = 0; self.crystals = 0; self.stripped = 0; self.stripped_vol = 0.0
        self.worn_steals = 0; self.worn_trail = 0; self.fusions = 0; self.splits = 0
        self.hits_by_phase = {}
        self.body_moves = 0; self.rebuckets = 0; self.container_writes = 0
        self.tick = 0
        self.extent = 60.0
        self.max_vol = 0.0
        self.claimed = set()
        self.vol_log = []

    # ---------------------------------------------------------------- body
    def volume(self, arena, k):
        b = self.body[k]
        return float(arena.mass_vol[list(b.values())].sum()) if b else 0.0

    def radius(self, k):
        return self.s * (1.0 + len(self.body[k]) ** (1 / 3))

    def attach(self, arena, k, i):
        b = self.body[k]
        occ = set(b.keys()) | {(0, 0, 0)}
        cand = {}
        for q in occ:
            for o in N26:
                r = (q[0] + o[0], q[1] + o[1], q[2] + o[2])
                if r not in occ and r not in cand:
                    nb = sum((r[0] + p[0], r[1] + p[1], r[2] + p[2]) in occ for p in N26)
                    cand[r] = nb
        sites = list(cand); w = np.array([cand[r] for r in sites], float) ** (-self.alpha)
        if self.contact:
            # the prism sticks where it TOUCHED: frontier sites are weighted by how close they lie to the prism's
            # current offset from the heart (DLA's arrival point) - bodies grow arms toward what they feed on
            rel = (arena.mass_pos[i] - self.agent_pos[k]) / self.s
            dd = np.array([np.sum((np.asarray(r, float) - rel) ** 2) for r in sites])
            w = w * np.exp(-(dd - dd.min()) / self.contact)
        site = sites[self.rng.choice(len(sites), p=w / w.sum())]
        b[site] = int(i)

    def wear_pos(self, k, site):
        return self.agent_pos[k] + np.asarray(site, float) * self.s * self.squash[k]

    # ---------------------------------------------------------------- step
    def step(self, arena, dt):
        self.tick += 1
        if not hasattr(self, "squash"):
            self.squash = np.ones(self.n)
        P = self.agent_pos
        pil = arena.pilots
        for k in range(self.n):
            if not self.alive[k] or self.leader[k] != k:
                continue
            V = self.volume(arena, k)
            sp = self.speed0 * (1 + V / self.V_hunt) ** -self.slow
            tgt = min(pil, key=lambda p: np.linalg.norm(p.pos - P[k])) if pil else None
            dist = np.linalg.norm(tgt.pos - P[k]) if tgt is not None else 1e9
            R = self.radius(k)
            self.ptime[k] += dt
            hunting = V >= self.V_hunt
            if hunting and self.phase[k] == 0:
                self.phase[k] = 1; self.ptime[k] = 0
            if not hunting and self.phase[k] in (1, 4):
                self.phase[k] = 0
            ph = self.phase[k]
            if ph == 0:                                          # THIEF: steal the trail, keep your distance
                v = self.thieve(arena, k, tgt, dist, sp)
            elif ph == 1:                                        # APPROACH (intercept: aim where the pilot WILL be)
                lead = min(3.0, dist / max(sp, 1e-6)) if self.intercept else 0.3
                v = (tgt.pos + tgt.vel * lead - P[k]); v = v / max(np.linalg.norm(v), 1e-6) * sp
                self.intent[k] = float(np.clip(1 - (dist - R) / 250, 0, 0.45))
                if dist < R + self.rear_at:
                    self.phase[k] = 2; self.ptime[k] = 0
            elif ph == 2:                                        # REAR: stop, contract, aim (the telegraph)
                v = self.agent_vel[k] * 0.3
                self.intent[k] = 0.5 + 0.5 * min(1.0, self.ptime[k] / self.windup)
                self.squash[k] = 1.0 - 0.3 * min(1.0, self.ptime[k] / self.windup)
                if self.ptime[k] >= self.windup:
                    self.phase[k] = 3; self.ptime[k] = 0
                    self.lunge_dir = getattr(self, "lunge_dir", {})
                    d = tgt.pos + tgt.vel * 0.4 - P[k]; self.lunge_dir[k] = d / max(np.linalg.norm(d), 1e-6)
            elif ph == 3:                                        # LUNGE: committed, straight, fast, overextends
                v = self.lunge_dir[k] * sp * self.lunge_k * 1.8
                self.squash[k] = 1.25; self.intent[k] = 1.0
                if dist < R * 2.5 + tgt.radius and self.touches(k, tgt):
                    arena.hit(tgt, "crush"); self.hits_by_phase["lunge"] = self.hits_by_phase.get("lunge", 0) + 1
                    self.phase[k] = 4; self.ptime[k] = 0
                elif self.ptime[k] > 0.9:
                    self.phase[k] = 4; self.ptime[k] = 0
            else:                                                # RECOVER: slow, vulnerable
                v = self.agent_vel[k] * 0.8; self.intent[k] = 0.0
                self.squash[k] = 1.0 + 0.25 * max(0.0, 1 - self.ptime[k] / 0.6)
                if self.ptime[k] > 2.5:
                    self.phase[k] = 1; self.ptime[k] = 0
            if ph in (0, 1, 4):
                self.squash[k] += (1.0 - self.squash[k]) * 0.2
            self.agent_vel[k] = 0.75 * self.agent_vel[k] + 0.25 * v
        # integrate leaders, then followers ride their leader
        for k in range(self.n):
            if self.alive[k] and self.leader[k] == k:
                P[k] = P[k] + self.agent_vel[k] * dt
                r = np.linalg.norm(P[k])
                if r > arena.R * 0.95:
                    P[k] *= arena.R * 0.95 / r
        for k in range(self.n):
            if self.alive[k] and self.leader[k] != k:
                L = self.leader[k]
                P[k] = P[L] + self.offset[k] * self.squash[L]; self.agent_vel[k] = self.agent_vel[L]
                self.intent[k] = self.intent[L]
        self.do_fuse(arena)
        if self.body_cap:
            for k in range(self.n):
                if self.alive[k] and self.leader[k] == k and len(self.body[k]) > self.body_cap and self.phase[k] in (0, 1):
                    self.moult(arena, k)
        self.fight(arena)
        self.sync_body(arena)
        arena.targets = [P[k] for k in np.flatnonzero(self.alive)]
        arena.threats = [P[k] for k in np.flatnonzero(self.alive & (self.leader == np.arange(self.n)))
                         if self.phase[k] > 0] or []
        big = max((self.volume(arena, k) for k in range(self.n) if self.alive[k] and self.leader[k] == k), default=0)
        self.max_vol = max(self.max_vol, big)
        self.vol_log.append((round(arena.t, 1), round(big, 1)))
        for k in range(self.n):
            self.agent_size[k] = self.radius(k) if (self.alive[k] and self.leader[k] == k) else 2.0

    def thieve(self, arena, k, tgt, dist, sp):
        P = self.agent_pos
        if tgt is not None and dist < 70:                       # turned on: flee
            d = P[k] - tgt.pos; self.intent[k] = 0.0
            return d / max(np.linalg.norm(d), 1e-6) * sp * 1.2
        g = self.goal[k]
        if g >= 0 and (not arena.mass_alive[g] or arena.mass_dom[g] == self.dom or arena.mass_shielded[g]):
            self.claimed.discard(int(g)); self.goal[k] = g = -1
        if g < 0 and (k + self.tick) % 4 == 0:
            c = arena.mass_near(P[k], self.sense)
            best, bd = -1, 1e18
            for i in c:
                if arena.mass_shielded[i] or arena.mass_dom[i] == self.dom or int(i) in self.claimed:
                    continue
                if tgt is not None and np.linalg.norm(arena.mass_pos[i] - tgt.pos) < 90:
                    continue                                     # never closer than ~90 u to the pilot
                d = float(np.sum((arena.mass_pos[i] - P[k]) ** 2)) * (0.15 if arena.mass_trail[i] else 1.0)
                if d < bd:
                    best, bd = int(i), d
            if best >= 0:
                self.goal[k] = best; self.claimed.add(best)
        g = self.goal[k]
        if g >= 0:
            d = arena.mass_pos[g] - P[k]
            if np.linalg.norm(d) < self.radius(k) + 3:
                if arena.steal(g, self.dom, by=self.name) > 0 or arena.mass_dom[g] == self.dom:
                    self.attach(arena, k, g); self.worn_steals += 1; self.worn_trail += int(arena.mass_trail[g])
                    if not hasattr(arena, "worn"):
                        arena.worn = set()
                    arena.worn.add(int(g))
                self.claimed.discard(int(g)); self.goal[k] = -1
            return d / max(np.linalg.norm(d), 1e-6) * sp
        # nothing in reach: drift toward the pilot's wake at a skulking distance
        if tgt is not None and dist > 160:
            d = tgt.pos - tgt.vel * 0.8 - P[k]
            return d / max(np.linalg.norm(d), 1e-6) * sp * 0.7
        w = self.wander[k] + self.rng.normal(0, 0.3, 3); self.wander[k] = w / np.linalg.norm(w)
        return self.wander[k] * sp * 0.5

    def do_fuse(self, arena):
        if not self.fuse:
            return
        L = [k for k in range(self.n) if self.alive[k] and self.leader[k] == k and self.body[k]]
        for a in range(len(L)):
            for b in range(a + 1, len(L)):
                i, j = L[a], L[b]
                if self.leader[i] != i or self.leader[j] != j:
                    continue
                if np.linalg.norm(self.agent_pos[i] - self.agent_pos[j]) < self.radius(i) + self.radius(j):
                    big, small = (i, j) if len(self.body[i]) >= len(self.body[j]) else (j, i)
                    for site, m in list(self.body[small].items()):
                        self.attach(arena, big, m)
                    self.body[small] = {}
                    # the small heart becomes a member riding inside the big body (it keeps its heart)
                    for k in range(self.n):
                        if self.alive[k] and (k == small or self.leader[k] == small):
                            self.leader[k] = big
                            self.offset[k] = self.rng.normal(0, 0.4, 3) * self.radius(big)
                    self.fusions += 1

    def fight(self, arena):
        for p in arena.pilots:
            if p.policy not in ("hunter", "cutter") and not getattr(p, "ram", False):
                continue
            for k in range(self.n):
                if not self.alive[k] or self.leader[k] != k or not self.body[k]:
                    continue
                if np.linalg.norm(self.agent_pos[k] - p.pos) > self.radius(k) * 2 + 20:
                    continue
                for site, m in list(self.body[k].items()):
                    if np.linalg.norm(self.wear_pos(k, site) - p.pos) < p.radius + 5:
                        del self.body[k][site]
                        arena.steal(m, p.domain, by=p.name)          # stripped BACK to the pilot
                        arena.worn.discard(m)
                        self.stripped += 1; self.stripped_vol += float(arena.mass_vol[m])
            for k in range(self.n):
                if not self.alive[k] or np.linalg.norm(self.agent_pos[k] - p.pos) > p.radius + 3:
                    continue
                L = self.leader[k]
                near = sum(1 for site in self.body[L] if np.linalg.norm(self.wear_pos(L, site) - self.agent_pos[k]) < 2.2 * self.s)
                if near < self.exposed_at:
                    self.kill(arena, k)

    def touches(self, k, p):
        """Contact is with the BODY ITSELF (a long tail can sweep), or the heart."""
        if np.linalg.norm(self.agent_pos[k] - p.pos) < self.s + p.radius:
            return True
        if not self.body[k]:
            return False
        S = np.array(list(self.body[k].keys()), float) * self.s * self.squash[k] + self.agent_pos[k]
        return bool((np.sum((S - p.pos) ** 2, axis=1) < (p.radius + 0.6 * self.s) ** 2).any())

    def moult(self, arena, k):
        """SATIATION MOULT: a body over its cap sheds its OUTERMOST prisms where they hang - they stop moving and
        become a static LAIR (cheap: a built structure never moves). Feeding pays out as population: each moult
        a new heart is born at the lair (production gating, not a cull - nothing is removed)."""
        b = self.body[k]
        keep = int(self.keep * self.body_cap)
        sites = sorted(b, key=lambda s: -(s[0] ** 2 + s[1] ** 2 + s[2] ** 2))
        for site in sites[:len(b) - keep]:
            m = b.pop(site)
            self.lair[m] = arena.mass_pos[m].copy()
            if not hasattr(arena, "struct_owner"):
                arena.struct_owner = {}
            arena.struct_owner[m] = self.name
            getattr(arena, "worn", set()).discard(m)
        self.moults += 1
        free = np.flatnonzero(self.unborn)
        if len(free):
            j = int(free[0]); self.unborn[j] = False; self.alive[j] = True; self.leader[j] = j
            self.agent_pos[j] = self.agent_pos[k] + self.rng.normal(0, 8, 3); self.agent_vel[j] = 0
            self.phase[j] = 0; self.born += 1

    def on_rammed(self, arena, k, pilot):
        pass                       # Wearers.fight owns contact (strip the body first, then the exposed heart)

    def kill(self, arena, k):
        self.alive[k] = False; self.kills += 1; self.crystals += 1
        if self.leader[k] == k:
            # its body falls loose where it is (still the thieves' domain - mass conserved), members split off
            for site, m in self.body[k].items():
                arena.worn.discard(m)
            self.body[k] = {}
            for j in range(self.n):
                if self.alive[j] and self.leader[j] == k:
                    self.leader[j] = j; self.splits += 1
                    self.agent_vel[j] = self.rng.normal(0, 1, 3) * 40

    def sync_body(self, arena):
        """The game cost model: a body rides ONE container transform per creature (1 write when it moves); each
        body prism still owes the spatial index a position, but PrismSpatialIndex only RE-BUCKETS when a prism
        crosses an 8 u bucket boundary - counted here as `rebuckets`."""
        for k in range(self.n):
            if not self.alive[k] or self.leader[k] != k:
                continue
            moved = False
            for site, m in self.body[k].items():
                tgt = self.wear_pos(k, site)
                old = arena.mass_pos[m]
                if np.sum((old - tgt) ** 2) > 0.25:
                    if (np.floor(old / 8.0) != np.floor(tgt / 8.0)).any():
                        self.rebuckets += 1
                    arena.move_mass(m, tgt); self.body_moves += 1; moved = True
            self.container_writes += int(moved and len(self.body[k]) > 0)

    def metrics(self, arena, minutes):
        worn = [len(self.body[k]) for k in range(self.n) if self.alive[k] and self.leader[k] == k]
        allw = [m for k in range(self.n) for m in self.body[k].values()]
        return dict(built=int(sum(worn)), worn_max=int(max(worn) if worn else 0), creatures=int(sum(1 for w in worn if w)),
                    max_vol=round(self.max_vol, 1), worn_steals=self.worn_steals,
                    trail_frac=round(float(arena.mass_trail[allw].mean()), 3) if allw else 0.0,
                    stripped=self.stripped, stripped_vol=round(self.stripped_vol, 1), fusions=self.fusions,
                    splits=self.splits, kills=self.kills, body_moves_per_s=round(self.body_moves / (minutes * 60), 1),
                    container_writes_per_s=round(self.container_writes / (minutes * 60), 1),
                    rebuckets_per_s=round(self.rebuckets / (minutes * 60), 1),
                    hits_lunge=self.hits_by_phase.get("lunge", 0),
                    lair=len([m for m in self.lair if arena.mass_alive[m]]), moults=self.moults, born=self.born,
                    hunt_time_s=None)

    def hud(self, arena):
        big = max(((len(self.body[k]), k) for k in range(self.n) if self.alive[k] and self.leader[k] == k), default=(0, -1))
        ph = ["thief", "approach", "REAR", "LUNGE", "recover"][self.phase[big[1]]] if big[1] >= 0 else ""
        return f"largest body {big[0]} prisms ({ph})  stripped {self.stripped}  killed {self.kills}"

    def render(self, out):
        a = self.alive
        col = np.tile([1.0, 1.0, 1.0], (a.sum(), 1)) * 1.0
        ph = self.phase[self.leader[a]]
        col[ph == 2] = (1.0, 0.85, 0.2); col[ph == 3] = (1.0, 0.2, 0.1)
        out[self.name] = dict(pos=self.agent_pos[a], col=col, size=np.full(a.sum(), 5.0))

    # the generic replayability check reads `lat`; expose the largest body as a point set
    @property
    def lat(self):
        class _L:
            pass
        L = _L()
        bigs = [(len(self.body[k]), k) for k in range(self.n) if self.alive[k] and self.leader[k] == k]
        k = max(bigs)[1] if bigs else 0
        L.anchor = np.zeros(3)
        L.sites = {m: s for s, m in self.body[k].items()}
        L.occupancy_points = lambda: np.array([np.asarray(s, float) * self.s for s in self.body[k]]) if self.body[k] else np.zeros((0, 3))
        return L
