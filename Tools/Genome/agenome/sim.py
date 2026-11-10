"""Agent populations (A ops) and camera rigs (C ops), simulated in numpy at the game's fixed step.

A population is one ag.spawn (movers) or ag.scatter (static instances) followed by behaviours (boids, wander,
attract, bound) and looks (trail, body). ag.merge concatenates populations; a behaviour applied after a merge
applies to every population under it.
"""
import colorsys
import math

import numpy as np

from .ops import OPS

MESHES = ["box", "octa", "dart", "icosa", "ring", "pyramid"]


def _d(node):
    o = OPS[node.op]
    return {p.name: p.decode(b) for p, b in zip(o.params, node.params)}


def _rgb(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), 1.0)
    return np.array([r * v, g * v, b * v], np.float32)


class Population:
    def __init__(self, kind, cfg, seed):
        self.kind = kind  # "movers" | "static"
        self.cfg = cfg
        self.behaviours = []
        self.trail = None
        self.body = None
        self.rim = None  # ag.rim: fresnel rim shading for this population's instances
        self.rng = np.random.default_rng(seed)

    def copy_shallow(self):
        p = Population(self.kind, self.cfg, 0)
        p.behaviours = list(self.behaviours)
        p.trail, p.body, p.rng, p.rim = self.trail, self.body, self.rng, self.rim
        return p

    def edge(self, e):
        """Instance edge value: the op's box-edge highlight, or -power (rim mode in FS_INST) under ag.rim."""
        return -float(self.rim["power"]) if self.rim else float(e)

    def bright(self):
        return float(self.rim["bright"]) if self.rim else 1.0


def build_populations(g, idx, memo=None, gseed=0):
    """Return a list of Population for the A node idx (follows the A chain)."""
    if memo is None:
        memo = {}
    if idx is None:
        return []
    if idx in memo:
        return [p.copy_shallow() for p in memo[idx]]
    n = g.nodes[idx]
    d = _d(n)
    ins = n.inputs
    if n.op == "ag.spawn":
        out = [Population("movers", d, (gseed * 7919 + int(d["seed"]) * 131 + idx) & 0xFFFFFFFF)]
    elif n.op == "ag.scatter":
        out = [Population("static", d, (gseed * 7919 + int(d["seed"]) * 131 + idx) & 0xFFFFFFFF)]
    elif n.op == "ag.merge":
        out = build_populations(g, ins[0], memo, gseed) + build_populations(g, ins[1], memo, gseed)
    else:
        out = build_populations(g, ins[0] if ins else None, memo, gseed)
        for p in out:
            if n.op in ("ag.boids", "ag.wander", "ag.attract", "ag.bound"):
                p.behaviours.append((n.op, d))
            elif n.op == "ag.trail":
                p.trail = d
            elif n.op == "ag.body":
                p.body = d
            elif n.op == "ag.rim":
                p.rim = d
    memo[idx] = out
    return [p.copy_shallow() for p in out]


def _unit(v, eps=1e-6):
    return v / (np.linalg.norm(v, axis=-1, keepdims=True) + eps)


def _basis(fwd):
    """Orthonormal frames with z along fwd (N,3) -> x, y, z (N,3) each."""
    z = _unit(fwd)
    up = np.tile(np.array([0.0, 1.0, 0.0], np.float32), (len(z), 1))
    near = np.abs(z[:, 1]) > 0.95
    up[near] = np.array([1.0, 0.0, 0.0], np.float32)
    x = _unit(np.cross(up, z))
    y = np.cross(z, x)
    return x, y, z


class World:
    """Runs every population; yields per-frame instance buffers and agent states."""

    def __init__(self, pops, max_prisms=24000):
        self.pops = pops
        self.max_prisms = max_prisms
        self.t = 0.0
        self.movers = []
        self.static = []
        for p in pops:
            if p.kind == "movers":
                self._init_movers(p)
                self.movers.append(p)
            else:
                self._init_static(p)
                self.static.append(p)
        # prism ring buffer
        self.pr_pos = np.zeros((max_prisms, 3), np.float32)
        self.pr_x = np.zeros((max_prisms, 3), np.float32)
        self.pr_y = np.zeros((max_prisms, 3), np.float32)
        self.pr_z = np.zeros((max_prisms, 3), np.float32)
        self.pr_col = np.zeros((max_prisms, 4), np.float32)
        self.pr_edge = np.zeros((max_prisms,), np.float32)
        self.pr_birth = np.full((max_prisms,), -1e9, np.float32)
        self.pr_mesh = np.zeros((max_prisms,), np.int8)
        self.pr_life = np.zeros((max_prisms,), np.float32)
        self.pr_next = 0

    # ------------------------------------------------------------------ init
    def _positions(self, rng, n, shape, radius):
        if shape == 0:  # ball
            v = rng.normal(size=(n, 3))
            v = _unit(v) * (rng.random((n, 1)) ** (1 / 3)) * radius
        elif shape == 1:  # shell
            v = _unit(rng.normal(size=(n, 3))) * radius
        elif shape == 2:  # ring / disk
            a = rng.random(n) * 2 * math.pi
            r = radius * (np.sqrt(rng.random(n)) if True else 1.0)
            v = np.stack([np.cos(a) * r, rng.normal(size=n) * radius * 0.03, np.sin(a) * r], 1)
        else:  # line
            v = np.stack([(rng.random(n) - 0.5) * 2 * radius, rng.normal(size=n) * radius * 0.02,
                          rng.normal(size=n) * radius * 0.02], 1)
        return v.astype(np.float32)

    def _init_movers(self, p):
        c = p.cfg
        n = max(1, int(round(c["n"])))
        rng = p.rng
        p.pos = self._positions(rng, n, int(c["shape"]), c["radius"])
        p.vel = _unit(rng.normal(size=(n, 3)).astype(np.float32)) * c["speed"]
        p.speed = float(c["speed"])
        teams = max(1, int(c["teams"]))
        p.team = (np.arange(n) % teams).astype(np.int32)
        p.team_rgb = np.stack([_rgb(c["hue0"] + k * c["huestep"], c["sat"], c["val"]) for k in range(teams)])
        p.phase = rng.random((n, 6)).astype(np.float32) * 6.2831853
        p.trail_t = rng.random(n).astype(np.float32)  # phase within the prism interval

    def _init_static(self, p):
        c = p.cfg
        n = max(1, int(round(c["n"])))
        rng = p.rng
        p.pos = self._positions(rng, n, int(c["shape"]), c["radius"])
        p.size = (c["size"] * (1.0 + c["sizevar"] * (rng.random(n) * 2 - 1))).astype(np.float32)
        p.axis = _unit(rng.normal(size=(n, 3)).astype(np.float32))
        p.phase0 = rng.random(n).astype(np.float32) * 6.2831853
        hue = c["h"] + c["huevar"] * (rng.random(n) - 0.5)
        p.rgb = np.stack([_rgb(h, c["s"], c["v"]) for h in hue]) if n <= 4096 else None

    # ------------------------------------------------------------------ step
    def step(self, dt):
        for p in self.movers:
            self._step_movers(p, dt)
        self.t += dt

    def _step_movers(self, p, dt):
        pos, vel = p.pos, p.vel
        n = len(pos)
        acc = np.zeros_like(pos)
        turn_cap = None
        for op, d in p.behaviours:
            if op == "ag.boids":
                if n > 1:
                    m = min(n, 96)
                    if n > m:
                        idx = p.rng.choice(n, m, replace=False)
                    else:
                        idx = np.arange(n)
                    diff = pos[idx][None, :, :] - pos[:, None, :]  # n,m,3
                    dist = np.linalg.norm(diff, axis=2) + 1e-6
                    R = d["radius"]
                    w = (dist < R).astype(np.float32)
                    cnt = w.sum(1, keepdims=True) + 1e-6
                    coh = (diff * w[..., None]).sum(1) / cnt
                    ali = (vel[idx][None, :, :] * w[..., None]).sum(1) / cnt - vel
                    rep = -(diff / (dist[..., None] ** 2) * w[..., None]).sum(1) * R
                    acc += d["coh"] * coh + d["ali"] * ali + d["sep"] * rep
                turn_cap = d["turn"]
            elif op == "ag.wander":
                fq = d["freq"] * 2 * math.pi
                ph = p.phase
                t = self.t
                wv = np.stack([np.sin(fq * t + ph[:, 0]) + 0.5 * np.sin(2.3 * fq * t + ph[:, 3]),
                               np.sin(fq * t * 0.8 + ph[:, 1]) + 0.5 * np.sin(1.7 * fq * t + ph[:, 4]),
                               np.sin(fq * t * 1.1 + ph[:, 2]) + 0.5 * np.sin(2.9 * fq * t + ph[:, 5])], 1)
                acc += d["amp"] * wv.astype(np.float32) * max(p.speed, 1e-3) * 0.3
            elif op == "ag.attract":
                c = np.array([d["cx"], d["cy"], d["cz"]], np.float32)
                rel = pos - c
                r = np.linalg.norm(rel, axis=1, keepdims=True) + 1e-6
                u = rel / r
                acc += -d["strength"] * (r - d["radius"]) * u
                if abs(d["orbit"]) > 1e-4:
                    tang = np.cross(np.array([0.0, 1.0, 0.0], np.float32), u)
                    acc += d["orbit"] * tang * p.speed * 2.0
            elif op == "ag.bound":
                r = np.linalg.norm(pos, axis=1, keepdims=True) + 1e-6
                over = np.maximum(r - d["radius"], 0.0)
                acc += -d["stiff"] * over * pos / r
        # integrate: steer, keep cruise speed (vessels fly at speed)
        newv = vel + acc * dt
        if turn_cap is not None:
            # limit turning rate
            u0, u1 = _unit(vel), _unit(newv)
            cosang = np.clip((u0 * u1).sum(1), -1, 1)
            ang = np.arccos(cosang)
            lim = turn_cap * dt
            over = ang > lim
            if over.any():
                k = (lim / (ang[over] + 1e-9))[:, None]
                newv[over] = _unit(u0[over] * (1 - k) + u1[over] * k) * np.linalg.norm(newv[over], axis=1, keepdims=True)
        sp = np.linalg.norm(newv, axis=1, keepdims=True) + 1e-6
        target = p.speed
        sp2 = sp + (target - sp) * min(1.0, 2.0 * dt)
        p.vel = (newv / sp * sp2).astype(np.float32)
        p.pos = (pos + p.vel * dt).astype(np.float32)
        # trails
        if p.trail is not None:
            tr = p.trail
            p.trail_t += dt / max(tr["interval"], 1e-3)
            drop = p.trail_t >= 1.0
            if drop.any():
                p.trail_t[drop] -= np.floor(p.trail_t[drop])
                self._drop_prisms(p, np.nonzero(drop)[0], tr)

    def _drop_prisms(self, p, ids, tr):
        k = len(ids)
        if k == 0:
            return
        x, y, z = _basis(p.vel[ids])
        cm = int(tr["colormode"])
        if cm == 0:
            col = p.team_rgb[p.team[ids]] * 0.6 + _rgb(tr["h"], tr["s"], tr["v"]) * 0.4
        else:
            col = np.tile(_rgb(tr["h"], tr["s"], tr["v"]), (k, 1))
        sl = (self.pr_next + np.arange(k)) % self.max_prisms
        self.pr_pos[sl] = p.pos[ids] - _unit(p.vel[ids]) * tr["sz"] * 0.6
        self.pr_x[sl] = x * tr["sx"]
        self.pr_y[sl] = y * tr["sy"]
        self.pr_z[sl] = z * tr["sz"]
        self.pr_col[sl, :3] = col * p.bright()
        self.pr_col[sl, 3] = tr["emit"]
        self.pr_edge[sl] = p.edge(tr["edge"])
        self.pr_birth[sl] = self.t
        self.pr_mesh[sl] = int(tr.get("mesh", 0))
        self.pr_life[sl] = tr["life"]
        self.pr_next = int((self.pr_next + k) % self.max_prisms)

    # ------------------------------------------------------------------ draw lists
    def instances(self):
        """Return dict mesh_name -> instance array [N, 17] (pos3, x3, y3, z3, rgb3, emit1, edge1, local-scale...)."""
        lists = {m: [] for m in MESHES}
        # prisms (boxes)
        age = self.t - self.pr_birth
        alive = (age >= 0) & (age < self.pr_life)
        if alive.any():
            fade = np.clip(1.0 - age[alive] / np.maximum(self.pr_life[alive], 1e-3), 0, 1)
            grow = np.clip(age[alive] * 6.0, 0, 1)[:, None]
            col = self.pr_col[alive].copy()
            col[:, 3] *= np.sqrt(fade)
            arr = np.concatenate([self.pr_pos[alive], self.pr_x[alive] * grow, self.pr_y[alive] * grow,
                                  self.pr_z[alive], col, self.pr_edge[alive][:, None]], 1)
            mesh_id = self.pr_mesh[alive]
            for mi in np.unique(mesh_id):
                lists[MESHES[int(mi)]].append(arr[mesh_id == mi])
        for p in self.movers:
            if p.body is None:
                continue
            b = p.body
            x, y, z = _basis(p.vel)
            n = len(p.pos)
            if int(b["colormode"]) == 0:
                col = p.team_rgb[p.team] * 0.5 + _rgb(b["h"], b["s"], b["v"]) * 0.5
            else:
                col = np.tile(_rgb(b["h"], b["s"], b["v"]), (n, 1))
            arr = np.concatenate([p.pos, x * b["sx"], y * b["sy"], z * b["sz"], col * p.bright(),
                                  np.full((n, 1), b["emit"], np.float32), np.full((n, 1), p.edge(b["edge"]), np.float32)], 1)
            lists[MESHES[int(b["shape"])]].append(arr)
        for p in self.static:
            c = p.cfg
            n = len(p.pos)
            ang = p.phase0 + c["spin"] * self.t
            # rotate a base frame around the per-instance axis (Rodrigues on x and y)
            ax = p.axis
            base_x = _unit(np.cross(ax, np.array([0.3, 1.0, 0.1], np.float32)))
            base_y = np.cross(ax, base_x)
            ca, sa = np.cos(ang)[:, None], np.sin(ang)[:, None]
            x = base_x * ca + base_y * sa
            y = -base_x * sa + base_y * ca
            s = p.size[:, None]
            rgb = p.rgb if p.rgb is not None else np.tile(_rgb(c["h"], c["s"], c["v"]), (n, 1))
            arr = np.concatenate([p.pos, x * s, y * s, ax * s, rgb * p.bright(), np.full((n, 1), c["emit"], np.float32),
                                  np.full((n, 1), p.edge(c["edge"]), np.float32)], 1)
            lists[MESHES[int(c["mesh"])]].append(arr)
        return {m: (np.concatenate(v, 0).astype(np.float32) if v else None) for m, v in lists.items()}

    def mover(self, k):
        if not self.movers:
            return None
        tot = sum(len(p.pos) for p in self.movers)
        k = k % tot
        for p in self.movers:
            if k < len(p.pos):
                return p.pos[k].copy(), p.vel[k].copy()
            k -= len(p.pos)
        return None


# ---------------------------------------------------------------------- cameras
MODIFIERS = ("cam.shake", "cam.lens")  # camera ops that sit over a rig (input C) instead of being one


class Camera:
    def __init__(self, g, idx, world, seed=0):
        self.chain = []
        j = idx
        while j is not None:
            n = g.nodes[j]
            self.chain.append((n.op, _d(n)))
            j = n.inputs[0] if (n.op in MODIFIERS and n.inputs) else None
            if n.op == "cam.chase":
                break
        self.chain.reverse()  # base rig first, then shakes and lenses
        if not self.chain or self.chain[0][0] in MODIFIERS:  # no rig under the modifiers: the default orbit
            self.chain.insert(0, ("cam.orbit", {"radius": 30.0, "height": 8.0, "speed": 0.1, "fov": 60.0, "phase": 0.0}))
        self.world = world
        self.eye = None
        self.tgt = None
        self.rng = np.random.default_rng(seed)
        self.ph = self.rng.random(12) * 6.2831853

    def fov(self):
        return float(self.chain[0][1].get("fov", 60.0))

    def panini(self):
        """(distance, crop to fit) of the first cam.lens over the rig; (0, 1) without one (rectilinear)."""
        for op, d in self.chain[1:]:
            if op == "cam.lens":
                return float(d["panini"]), float(d["crop"])
        return 0.0, 1.0

    def update(self, t, dt):
        op, d = self.chain[0]
        if op == "cam.chase":
            m = self.world.mover(int(d["agent"])) if self.world is not None else None
            if m is None:
                op, d = "cam.orbit", {"radius": d["dist"] * 3, "height": d["height"], "speed": 0.2, "fov": d["fov"],
                                     "phase": 0.0}
            else:
                pos, vel = m
                fwd = vel / (np.linalg.norm(vel) + 1e-6)
                want_eye = pos - fwd * d["dist"] + np.array([0, 1, 0], np.float32) * d["height"]
                want_tgt = pos + fwd * d["look"] * d["dist"]
                if self.eye is None:
                    self.eye, self.tgt = want_eye, want_tgt
                else:
                    a = 1.0 - d["lag"] ** (dt * 15.0)
                    self.eye = self.eye + (want_eye - self.eye) * a
                    self.tgt = self.tgt + (want_tgt - self.tgt) * min(1.0, a * 1.5)
        if op == "cam.orbit":
            w = d["speed"] * t + d["phase"]
            self.eye = np.array([math.cos(w) * d["radius"], d["height"], math.sin(w) * d["radius"]], np.float32)
            self.tgt = np.zeros(3, np.float32)
        elif op == "cam.fly":
            R, s, ph = d["radius"], d["speed"], self.ph

            def path(tt):
                return np.array([R * math.sin(s * tt + ph[0]) * 0.8 + R * 0.2 * math.sin(2.3 * s * tt + ph[3]),
                                 R * 0.3 * math.sin(0.7 * s * tt + ph[1]),
                                 R * math.cos(s * tt + ph[2]) * 0.8 + R * 0.2 * math.cos(1.9 * s * tt + ph[4])],
                                np.float32)
            self.eye = path(t)
            self.tgt = path(t + 0.35)
        eye, tgt = self.eye.copy(), self.tgt.copy()
        for op2, d2 in self.chain[1:]:
            if op2 == "cam.shake":
                fq = d2["freq"] * 2 * math.pi
                sc = d2["amp"] * (np.linalg.norm(tgt - eye) + 1.0)
                off = np.array([math.sin(fq * t + self.ph[5]), math.sin(1.3 * fq * t + self.ph[6]),
                                math.sin(0.7 * fq * t + self.ph[7])], np.float32) * sc
                eye = eye + off
                tgt = tgt + off * 0.5
        return eye, tgt
