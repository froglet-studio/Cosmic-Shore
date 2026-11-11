"""The game's own mechanics as agent operators (docs/MECHANICS_SPEC.md, read from the Cosmic Shore source).

A game population is one ag.course followed by a chain of ops that each add one rule:
  ag.course  closed spline with gate frames          ag.rail    static super-shielded prisms along it
  ag.gate    a ring per gate                         ag.crystal crystals with ownership, collect and respawn
  ag.vessel  the hull integrator (commanded frame, nose lag, speed lerp, boost)
  ag.pilot   stick input: lead-compensated line follow or bang-bang pursuit
  ag.skim    boost from grazing prisms               ag.lay     distance-based two-rail trail, persistent, grows in
  ag.shards  the static needle field aimed at the crystal
World units are game units (cell radius 1200, Squirrel 60..300 u/s). Rotations are 3x3 with columns right, up, forward.
"""
import colorsys
import math

import numpy as np
from scipy.spatial import cKDTree
from scipy.spatial.transform import Rotation

GAME_OPS = ("ag.rail", "ag.gate", "ag.crystal", "ag.vessel", "ag.pilot", "ag.skim", "ag.lay", "ag.shards", "ag.impact", "ag.jet")
RIM = -204.0  # instance edge value: the game's prism graph (face to rim by fresnel power 4, distance spread)
FACE = 0.072  # a domain's face colour is its rim times this, more saturated (Photic: Jade rim/face, the ribbon's)


def _u(v):
    return v / (np.linalg.norm(v, axis=-1, keepdims=True) + 1e-9)


def _hsv(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), 1.0)
    return np.array([r * v, g * v, b * v], np.float32)


def _inst(pos, R, scale, rgb, emit, edge):
    """pos [n,3], R [n,3,3] (columns right, up, fwd), scale [n,3] or [3] -> [n,17] instance rows."""
    n = len(pos)
    scale = np.broadcast_to(np.asarray(scale, np.float32), (n, 3))
    rgb = np.broadcast_to(np.asarray(rgb, np.float32), (n, 3))
    dark = FACE * rgb * rgb / np.maximum(rgb.max(1, keepdims=True), 1e-6) if edge <= -200.0 else np.zeros((n, 3))
    return np.concatenate([pos, R[:, :, 0] * scale[:, 0:1], R[:, :, 1] * scale[:, 1:2], R[:, :, 2] * scale[:, 2:3], rgb,
                           np.full((n, 1), emit, np.float32), np.full((n, 1), edge, np.float32), dark], 1).astype(np.float32)


class Course:
    def __init__(self, d, rng):
        n = max(4, int(d["gates"]))
        R, pert = float(d["radius"]), float(d["perturb"])
        kind = int(d["kind"])
        ang = np.arange(n) * 2 * math.pi / n
        rad = R + (rng.random(n) * 2 - 1) * pert
        y = (rng.random(n) * 2 - 1) * pert * (1.0 if kind != 0 else 0.0)
        if kind == 3:  # harmonic ring (the port's standalone rig)
            rad = R * (1 + 0.18 * np.sin(2 * ang + 1.0) + 0.08 * np.sin(3 * ang))
            y = R * 0.16 * np.sin(2 * ang) + R * 0.08 * np.cos(3 * ang)
        cp = np.stack([np.cos(ang) * rad, y, np.sin(ang) * rad], 1)
        self.cp = cp
        pts = []
        sub = 48
        for i in range(n):
            p0, p1, p2, p3 = cp[(i - 1) % n], cp[i], cp[(i + 1) % n], cp[(i + 2) % n]
            t = (np.arange(sub) / sub)[:, None]
            if kind == 0:  # straight legs
                pts.append(p1 + (p2 - p1) * t)
            else:  # Catmull-Rom
                pts.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                  + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
        P = np.concatenate(pts, 0)
        if kind == 1:  # sphere seam: a ring in the xy plane on the sphere of radius R, two lobes along z (Skim Race)
            th = np.arange(n * sub) * 2 * math.pi / (n * sub)
            q = np.stack([np.cos(th), np.sin(th), -(pert / 300.0) * np.sin(2 * th)], 1)
            P = R * q / np.linalg.norm(q, axis=1, keepdims=True)
            self.cp = P[::sub]
        seg = np.linalg.norm(np.roll(P, -1, 0) - P, axis=1)
        s = np.concatenate([[0.0], np.cumsum(seg)])
        self.length = float(s[-1])
        m = max(64, int(self.length / 2.0))  # resample every ~2 u
        self.ds = self.length / m
        ss = np.arange(m) * self.ds
        Pc = np.concatenate([P, P[:1]], 0)
        self.P = np.stack([np.interp(ss, s, Pc[:, k]) for k in range(3)], 1)
        self.T = _u(np.roll(self.P, -1, 0) - np.roll(self.P, 1, 0))
        up = np.array([0.0, 1.0, 0.0])
        B = _u(np.cross(self.T, up))  # outward-ish
        self.N = _u(np.cross(B, self.T))  # ribbon normal (up-ish)
        self.B = B
        self.m = m
        self.gate_s = np.array([i * sub for i in range(n)]) * (self.length / (n * sub))
        self.tree = cKDTree(self.P)
        self.mouth = float(d["mouth"])

    def idx(self, s):
        return (np.round(np.asarray(s) / self.ds).astype(int)) % self.m

    def frame(self, s):
        i = self.idx(s)
        return self.P[i], self.T[i], self.N[i], self.B[i]

    def nearest_s(self, pos):
        _, i = self.tree.query(pos)
        return i * self.ds


class GameSim:
    def __init__(self, cfg, rng, body=None, max_trail=12000):
        self.cfg = cfg
        self.rng = rng
        self.body = body
        self.t = 0.0
        c = self.course = Course(cfg["ag.course"], rng)
        self.static = {}  # mesh -> [arrays]
        self.sk_pos = []  # skimmable static prism centres
        self._build_rail()
        self._build_gates()
        self._init_vessels()
        self._init_crystals()
        self._init_shards()
        self.tr = np.zeros((max_trail, 20), np.float32)
        self.tr_birth = np.full(max_trail, -1e9, np.float32)
        self.tr_octa = np.zeros(max_trail, bool)  # shielded prisms (the crystal rings) draw as octahedra
        self.hist = []  # (pos, Rh) of the last few seconds, newest first, for the jet ribbons
        self.tr_n = 0
        self.tr_next = 0
        self.max_trail = max_trail
        self.sk_tree = cKDTree(np.concatenate(self.sk_pos, 0)) if self.sk_pos else None

    # ------------------------------------------------------------------ static world
    def domain_rgb(self, k):
        v = self.cfg.get("ag.vessel")
        if v is None:
            return np.array([0.3, 0.6, 1.1], np.float32)
        hs = (v["ha"], v["hb"], v["hc"])
        return _hsv(hs[int(k) % 3], v["sat"], v["val"])

    def _build_rail(self):
        r = self.cfg.get("ag.rail")
        if r is None:
            return
        c = self.course
        lanes = max(1, int(r["lanes"]))
        ss = np.arange(0.0, c.length - 0.5 * r["spacing"], r["spacing"])
        P, T, N, B = c.frame(ss)
        neutral = _hsv(r["h"], r["s"], r["v"])
        self.lane_off = []
        for ln in range(lanes):
            a = ln * 2 * math.pi / lanes + r["twist"] * 2 * math.pi * ss / c.length
            off = (np.cos(a)[:, None] * N + np.sin(a)[:, None] * B) * r["offset"]
            pos = P + off
            if r["offset"] > 0.5:  # a rail around the spine: its wide face looks at the spine
                up = _u(off)
            else:
                up = N
            right = _u(np.cross(up, T))
            R = np.stack([right, up, T], 2)
            scale = np.tile(np.array([r["sx"], r["sy"], r["sz"]], np.float32), (len(ss), 1))
            if lanes == 1:  # a marker block at each waypoint
                gi = np.unique(np.round(c.gate_s / r["spacing"]).astype(int) % len(ss))
                scale[gi] *= r["marker"]
            rgb = neutral if int(r["colour"]) == 0 else self.domain_rgb(ln)
            mesh = "stella" if int(r["state"]) == 2 else "box"
            self.static.setdefault(mesh, []).append(_inst(pos, R, scale, rgb, r["emit"], RIM))
            self.sk_pos.append(pos)
        self.rail = r

    def lane_point(self, s, lane, height):
        """A point on the racing line: the spine (or the vessel's lane rail) lifted along the ribbon normal."""
        c = self.course
        P, T, N, B = c.frame(s)
        r = self.cfg.get("ag.rail")
        if r is not None and r["offset"] > 0.5:
            lanes = max(1, int(r["lanes"]))
            a = (lane % lanes) * 2 * math.pi / lanes + r["twist"] * 2 * math.pi * np.asarray(s) / c.length
            off = np.cos(a)[..., None] * N + np.sin(a)[..., None] * B
            return P + off * (r["offset"] - r["sy"] * 0.5 - height), -off
        return P + N * height, N

    def _build_gates(self):
        g = self.cfg.get("ag.gate")
        self.gate = g
        if g is None:
            return
        c = self.course
        P, T, N, B = c.frame(c.gate_s)
        R = np.stack([B, T, N], 2)  # ring mesh lies in its local xz plane: the ring axis (y) along the tangent
        d = 2 * c.mouth / 0.84
        col = _hsv(g["h"], g["s"], g["v"])
        self.static.setdefault("hoop", []).append(_inst(P, R, [d, d, d], col, g["emit"], 0.0))

    def _init_shards(self):
        s = self.cfg.get("ag.shards")
        self.shards = s
        if s is None:
            return
        R = s["radius"]
        n = int(min(6000, (4 / 3) * math.pi * R ** 3 / max(s["spacing"], 40.0) ** 3))
        v = self.rng.normal(size=(n, 3))
        self.sh_pos = (_u(v) * (self.rng.random((n, 1)) ** (1 / 3)) * R).astype(np.float32)
        self.sh_rgb = _hsv(s["h"], s["s"], s["v"])

    # ------------------------------------------------------------------ vessels, crystals
    def _init_vessels(self):
        v = self.cfg.get("ag.vessel")
        self.v = v
        n = self.n = 0 if v is None else max(1, int(v["count"]))
        c = self.course
        if n == 0:
            return
        self.dom = np.arange(n) % max(1, int(v["domains"]))
        self.s = (np.arange(n) * 14.0) % c.length  # staggered start slots along the line
        pil = self.cfg.get("ag.pilot") or {"height": 5.0}
        self.pil = pil
        self.lane_h = pil["height"] + (np.arange(n) % 3) * 1.4
        P, T, N, B = c.frame(self.s)
        self.pos, nrm = self.lane_point(self.s, self.dom, self.lane_h)
        self.pos = self.pos.copy()
        up = _u(nrm)
        right = _u(np.cross(up, T))
        self.Rh = np.stack([right, np.cross(T, right), T], 2).copy()
        self.Rc = self.Rh.copy()
        self.speed = np.full(n, v["cruise"] * 0.8, np.float32)
        self.boost = np.ones(n, np.float32)
        self.dist = np.zeros(n, np.float32)  # distance since the last laid pair
        self.score = np.zeros(n, int)
        self.near = [set() for _ in range(n)]
        self.react = np.zeros(n, np.float32)
        self.slow = np.ones(n, np.float32)
        self.resets = np.zeros(n, int)
        self.tr_tree = None
        self.tr_tree_t = -1e9
        self.wob = np.zeros((n, 3), np.float32)
        self.wob_t = 0.0

    def _init_crystals(self):
        cr = self.cfg.get("ag.crystal")
        self.cr = cr
        if cr is None or self.n == 0:
            self.cr_pos = None
            return
        c = self.course
        self.anchors = c.gate_s + c.length / (2 * len(c.gate_s))  # mid-leg anchors
        self.cr_k = (np.arange(self.n) * 2 + 1) % len(self.anchors)
        self.cr_pos = np.zeros((self.n, 3), np.float32)
        self.cr_s = np.zeros(self.n, np.float32)
        for i in range(self.n):
            self._place(i)

    def _place(self, i):
        cr, c = self.cr, self.course
        s = self.anchors[self.cr_k[i]]
        P, T, N, B = c.frame(s)
        j = _u(self.rng.normal(size=3)) * self.rng.random() ** (1 / 3) * cr["jitter"]
        self.cr_pos[i] = P - B * cr["inset"] + N * cr["lift"] + j
        self.cr_s[i] = s

    # ------------------------------------------------------------------ step
    def step(self, dt):
        if self.n:
            self._pilot(dt)
            self._integrate(dt)
            self._events(dt)
            if self.cfg.get("ag.jet"):
                self.hist_dt = dt
                self.hist.insert(0, (self.pos.copy(), self.Rh.copy()))
                del self.hist[int(8.5 / max(dt, 1e-3)):]
        self.t += dt

    def _aim(self):
        """Aim point per vessel: look ahead on the racing line, bent through the vessel's own crystal."""
        c, p = self.course, self.pil
        look = np.clip(self.speed * p["look"], p["lookmin"], p["lookmax"])
        sa = self.s + look
        pt, _ = self.lane_point(sa, self.dom, self.lane_h)
        if self.cr_pos is not None:
            ds = (self.cr_s - sa + c.length / 2) % c.length - c.length / 2
            hw = p["bend"]
            w = np.where(np.abs(ds) < hw, 0.5 * (1 + np.cos(math.pi * ds / hw)), 0.0)[:, None]
            cp0, _ = self.lane_point(self.cr_s, self.dom, self.lane_h)
            pt = pt + (self.cr_pos - cp0) * w
            # close to the crystal: fly at it
            to = self.cr_pos - self.pos
            dist = np.linalg.norm(to, axis=1)
            ahead = (to * self.Rh[:, :, 2]).sum(1) > 0
            near = (dist < look * 1.2) & ahead
            pt[near] = self.cr_pos[near] + _u(to[near]) * 20.0
        return pt

    def _pilot(self, dt):
        p, v = self.pil, self.v
        aim = self._aim() - self.pos
        im = self.cfg.get("ag.impact")
        if im is not None and im["wobble"] > 0:  # the pilot's aim wanders: a new offset every period
            if self.t - self.wob_t > im["period"]:
                self.wob = self.rng.normal(0, 1, (self.n, 3)).astype(np.float32)
                self.wob_t = self.t
            aim = aim + self.wob * im["wobble"]
        aim = _u(aim)
        fwd, cf = self.Rh[:, :, 2], self.Rc[:, :, 2]
        err = np.degrees(np.arccos(np.clip((fwd * aim).sum(1), -1, 1)))
        if int(p.get("law", 1)) == 0:  # bang-bang pursuit
            want = aim
            stick = (err > 1.0).astype(np.float32)
        else:  # lead-compensated: command past the aim to cancel the nose lag
            lead = np.minimum(np.minimum(err * p["lead"], err + p["maxlead"]), 179.0)
            ax = _u(np.cross(fwd, aim))
            rot = Rotation.from_rotvec(ax * np.radians(lead)[:, None])
            want = rot.apply(fwd)
            gap = np.degrees(np.arccos(np.clip((cf * want).sum(1), -1, 1)))
            stick = np.clip(gap * p["gain"], 0, 1)
        axis = _u(np.cross(cf, want))
        ang = np.radians(stick * (v["turn"] + self.speed * v["rotthr"]) * dt)
        # roll the commanded frame toward the ribbon normal (the skimmer's alignment pull)
        _, nrm = self.lane_point(self.s, self.dom, self.lane_h)
        up = self.Rc[:, :, 1]
        tgt_up = _u(nrm - (nrm * cf).sum(1, keepdims=True) * cf)
        roll_err = np.arctan2((np.cross(up, tgt_up) * cf).sum(1), (up * tgt_up).sum(1))
        roll = np.clip(roll_err * p["bank"], -1, 1) * np.radians(v["roll"]) * dt
        rv = axis * ang[:, None] + cf * roll[:, None]
        self.Rc = Rotation.from_rotvec(rv).as_matrix() @ self.Rc
        # throttle: ease off when the aim sits inside the turning circle
        self.throttle = np.where(err > 60.0, p["thrlo"], p["thrhi"]).astype(np.float32)

    def _integrate(self, dt):
        v = self.v
        f = min(1.0, v["follow"] * dt)
        rel = Rotation.from_matrix(self.Rc @ np.transpose(self.Rh, (0, 2, 1))).as_rotvec()
        self.Rh = Rotation.from_rotvec(rel * f).as_matrix() @ self.Rh
        self.boost = np.maximum(1.0, self.boost - v["decay"] * dt)
        im = self.cfg.get("ag.impact")
        self.slow = np.minimum(1.0, self.slow + dt / (im["dur"] if im else 1.0))
        target = self.throttle * v["cruise"] * self.boost * self.slow + v["minspeed"]
        self.speed = self.speed + (target - self.speed) * min(1.0, v["accel"] * dt)
        step = self.Rh[:, :, 2] * (self.speed * dt)[:, None]
        self.pos = self.pos + step
        self.dist += self.speed * dt
        # arc parameter: nearest spine sample (vessels stay near the line)
        self.s = self.course.nearest_s(self.pos)

    def _events(self, dt):
        v = self.v
        sk = self.cfg.get("ag.skim")
        if sk is not None and self.sk_tree is not None:
            hits = self.sk_tree.query_ball_point(self.pos, sk["radius"])
            hull = self.sk_tree.query_ball_point(self.pos, 1.5)
            for i, h in enumerate(hits):
                hs = set(h)
                new = len(hs - self.near[i])
                self.near[i] = hs
                if new:
                    self.boost[i] = min(v["boostmax"], self.boost[i] + sk["add"] * new)
                if hull[i] and self.cfg.get("ag.impact") is None:  # the hull clipped a prism: lose the boost
                    self.boost[i] = 1.0
                    self.slow[i] = 0.6
        im = self.cfg.get("ag.impact")
        if im is not None:
            hit = np.zeros(self.n, bool)
            if self.sk_tree is not None:
                hit |= np.array([len(h) > 0 for h in self.sk_tree.query_ball_point(self.pos, im["hull"])])
            if self.t - self.tr_tree_t > 0.25 and self.tr_n:
                old = np.nonzero(self.tr_birth[:self.tr_n] < self.t - im["fresh"])[0]
                self.tr_tree = cKDTree(self.tr[old, :3]) if len(old) else None
                self.tr_tree_t = self.t
            if self.tr_tree is not None:
                hit |= np.array([len(h) > 0 for h in self.tr_tree.query_ball_point(self.pos, im["hull"])])
            fresh_hit = hit & (self.slow >= 1.0)
            self.resets += fresh_hit
            self.boost[fresh_hit] = 1.0
            self.slow[fresh_hit] = 1.0 - im["slow"]
        if self.cr_pos is not None:
            d = np.linalg.norm(self.cr_pos - self.pos, axis=1)
            for i in np.nonzero(d < self.cr["radius"])[0]:
                self.score[i] += 1
                if sk is not None:
                    self.boost[i] = min(v["boostmax"], self.boost[i] + sk["pickup"])
                self._aoe_ring(i)
                self.cr_k[i] = (self.cr_k[i] + 1) % len(self.anchors)
                self._place(i)
        lay = self.cfg.get("ag.lay")
        if lay is not None and self.t > lay["delay"]:
            drop = (self.dist >= lay["wave"]) & (self.speed > 3.0)
            ids = np.nonzero(drop)[0]
            if len(ids):
                self.dist[ids] = 0.0
                self._lay(ids, lay)

    def _push(self, rows, octa=False):
        k = len(rows)
        sl = (self.tr_next + np.arange(k)) % self.max_trail
        self.tr[sl] = rows
        self.tr_octa[sl] = octa
        self.tr_birth[sl] = self.t
        self.tr_next = int((self.tr_next + k) % self.max_trail)
        self.tr_n = min(self.max_trail, self.tr_n + k)

    def _lay(self, ids, lay):
        half = min(lay["gap"] * 0.5, 0.95 * lay["sx"] * 0.5)
        sx = max(lay["sx"] * 0.5 - half, 0.05)
        R = self.Rh[ids]
        base = self.pos[ids] - R[:, :, 2] * lay["offset"]
        rgb = np.stack([self.domain_rgb(k) for k in self.dom[ids]])
        signs = (1.0, -1.0) if half > 0.05 else (0.0,)
        for sg in signs:
            pos = base + R[:, :, 0] * sg * (sx * 0.5 + half)
            self._push(_inst(pos, R, [sx, lay["sy"], lay["sz"]], rgb, lay["emit"], RIM))

    def _aoe_ring(self, i):
        """The ring a collected crystal leaves: 8 shielded prisms around the flight line, long axis radial
        (recorded: 5.4 x 5.4 x 22.5 at radius 27)."""
        R = self.Rh[i]
        a = np.arange(8) * 2 * math.pi / 8
        rad = np.cos(a)[:, None] * R[:, 0] + np.sin(a)[:, None] * R[:, 1]
        tan = -np.sin(a)[:, None] * R[:, 0] + np.cos(a)[:, None] * R[:, 1]
        Rs = np.stack([tan, np.tile(R[:, 2], (8, 1)), -rad], 2)
        rgb = self.domain_rgb(self.dom[i]) * 0.9 + np.array([0.12, 0.06, 0.2], np.float32)
        k = self.cr["ring"] if "ring" in self.cr else 1.0
        self._push(_inst(self.pos[i] + rad * 9.0 * k, Rs, [1.8 * k, 1.8 * k, 7.5 * k], rgb, 1.0, RIM), octa=True)

    # ------------------------------------------------------------------ draw
    def add_instances(self, lists):
        for m, arrs in self.static.items():
            lists[m].extend(arrs)
        if self.tr_n:
            rows = self.tr[:self.tr_n].copy()
            lay = self.cfg.get("ag.lay")
            k = lay["grow"] if lay else 1.25
            g = 1.0 - (1.0 - 0.05) * np.exp(-k * np.maximum(self.t - self.tr_birth[:self.tr_n], 0.0))
            rows[:, 3:12] *= g[:, None]
            o = self.tr_octa[:self.tr_n]
            lists["box"].append(rows[~o])
            if o.any():
                lists["octa"].append(rows[o])
        jets = self.cfg.get("ag.jet")
        if jets and len(self.hist) > 1:
            dt = self.hist_dt
            for j in (jets if isinstance(jets, list) else [jets]):
                n = min(int(math.ceil(j["secs"] / dt)), len(self.hist) - 1)
                if n < 1:
                    continue
                tt = np.linspace(0.0, min(j["secs"] / dt, n), 16)
                i0 = np.minimum(tt.astype(int), n - 1)
                f = (tt - i0)[:, None]
                base = _hsv(j["h"], j["s"], j["v"])
                for k in range(self.n):
                    gate = float(np.clip((j["vmax"] * 1.15 - self.speed[k]) / (j["vmax"] * 0.3), 0.0, 1.0))
                    if gate <= 0.0:
                        continue
                    d = self.domain_rgb(self.dom[k])
                    col = base * (1 - j["domain"]) + j["domain"] * (d / max(float(d.max()), 1e-6) * 0.85 + 0.12) * j["v"]
                    for sg in (-1.0, 1.0):
                        e = np.array([sg * j["x"], 0.0, j["z"]], np.float32)
                        P = np.array([h[0][k] + h[1][k] @ e for h in self.hist[:n + 1]])
                        if np.abs(np.diff(P, axis=0)).max() > 60.0:
                            continue  # a respawn jump is not a trail
                        p = P[i0] * (1 - f) + P[i0 + 1] * f
                        seg = np.linalg.norm(np.diff(p, axis=0), axis=1)
                        if seg.sum() < 1e-3:
                            continue
                        u = np.concatenate([[0.0], np.cumsum(seg)]) / seg.sum()
                        if j["ramp"] > 0.004:
                            al = np.interp(u, [0.0, 0.4 * j["ramp"], j["ramp"], 1.0], [0.0, 0.0, 1.0, 0.0])
                            hw = 0.5 * j["width"] * np.where(u < 0.19, 1.0, (1 - u) / 0.81)
                        else:
                            al, hw = 1.0 - u, np.full_like(u, 0.5 * j["width"])
                        rgba = np.concatenate([np.tile(col, (len(u), 1)), (al * j["alpha"] * gate)[:, None]], 1)
                        lists.setdefault("lines", []).append((p, hw, rgba, int(j["soft"]) >= 1))
        if self.n:
            b = self.body
            if b is None:
                sc, rgbw, emit, mesh = [5.0, 1.6, 6.0], np.array([1.5, 1.5, 1.7], np.float32), 1.5, "dart"
            else:
                from .sim import MESHES
                sc, emit, mesh = [b["sx"], b["sy"], b["sz"]], b["emit"], MESHES[int(b["shape"])]
                rgbw = _hsv(b["h"], b["s"], b["v"])
            rgb = np.stack([self.domain_rgb(k) for k in self.dom]) * 0.35 + rgbw * 0.65
            lists[mesh].append(_inst(self.pos, self.Rh, sc, rgb, emit, 0.6))
        if self.cr_pos is not None:
            cr = self.cr
            rgb = np.stack([self.domain_rgb(k) for k in self.dom])
            to = _u(self.course.frame(self.cr_s)[1])
            up = np.tile(np.array([0.0, 1.0, 0.0]), (self.n, 1))
            x = _u(np.cross(up, to))
            R = np.stack([x, to, np.cross(x, to)], 2)  # ring axis along the course
            d = 2 * cr["radius"] / 0.84
            lists["hoop"].append(_inst(self.cr_pos, R, [d, d, d], rgb, cr["emit"], 0.0))
            spin = Rotation.from_rotvec(np.array([0.3, 1.0, 0.2]) * self.t * 0.8).as_matrix()
            lists["icosa"].append(_inst(self.cr_pos, np.tile(spin[None], (self.n, 1, 1)), cr["radius"] * 0.35, rgb,
                                        cr["emit"], RIM))
        if self.shards is not None:
            s = self.shards
            tgt = self.cr_pos[0] if self.cr_pos is not None else np.zeros(3, np.float32)
            to = tgt - self.sh_pos
            dn = np.linalg.norm(to, axis=1, keepdims=True)
            z = to / (dn + 1e-6)
            d = np.clip(dn / (2 * s["radius"]), 0, 1)
            x = _u(np.cross(z, np.array([0.31, 0.9, 0.3])))
            R = np.stack([x, np.cross(z, x), z], 2)
            thin = (0.333 * d + 0.025) * s["scale"]
            scale = np.concatenate([thin, thin, (1.333 * d + 0.05) * s["scale"]], 1)
            lists["box"].append(_inst(self.sh_pos, R, scale, self.sh_rgb, s["emit"], 0.0))

    def frame_of(self, k):
        k = k % max(self.n, 1)
        return self.pos[k].copy(), self.Rh[k].copy(), float(self.speed[k])
