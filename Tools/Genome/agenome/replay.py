"""A recorded Amoebius session (targets/sessions/<name>) as a replay source for Renderer.render(replay=...).

    s = Session(path); s.frame(fi) -> {"t", "eye", "r", "u", "fwd", "fov", "inst"}
The camera's Unity quaternion (x, y, z, w) gives right/up/forward in Unity world axes (left-handed, +z forward); the
renderer only needs the three vectors. inst is filled by a builder (inst_fn(session, fi) -> instance lists), or empty.
"""
import gzip
import json
import math
import os
import subprocess

import numpy as np


def quat_axes(q):
    x, y, z, w = q
    R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    return R[:, 0], R[:, 1], R[:, 2]


def read_jsonl(sdir, name):
    """Lines of <name>.jsonl or <name>.jsonl.gz (the recorder repacks key files gzipped; targets/tools/load.py)."""
    p = os.path.join(sdir, name + ".jsonl")
    with (open(p) if os.path.exists(p) else gzip.open(p + ".gz", "rt")) as f:
        for l in f:
            if l.strip():
                yield json.loads(l)


def has_jsonl(sdir, name):
    p = os.path.join(sdir, name + ".jsonl")
    return os.path.exists(p) or os.path.exists(p + ".gz")


def read_frames(sdir, variant="_nohud"):
    """uint8 [T,144,256,3]; frames_144<variant>.npy, or the lossless .mkv the recorder repacks it to."""
    base = os.path.join(sdir, f"frames_144{variant}")
    if os.path.exists(base + ".npy"):
        return np.load(base + ".npy", mmap_mode="r")
    raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-i", base + ".mkv", "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(-1, 144, 256, 3)


class Session:
    def __init__(self, path, inst_fn=None, frames=None):
        self.path = path
        self.meta = json.load(open(os.path.join(path, "meta.json")))
        self.state = list(read_jsonl(path, "state"))
        self.inst_fn = inst_fn
        self.pick = list(frames) if frames is not None else list(range(len(self.state)))

    def real(self, variant=""):
        """uint8 [N,144,256,3] of the picked frames; variant "", "_nohud" or "_sky"."""
        return np.asarray(read_frames(self.path, variant)[self.pick])

    def frame(self, fi):
        st = self.state[self.pick[fi]]
        c = st["camera"]
        r, u, f = quat_axes(c["rot"])
        inst = self.inst_fn(self, self.pick[fi]) if self.inst_fn else {}
        return {"t": st["t"], "eye": np.array(c["pos"], np.float32), "r": r, "u": u, "fwd": f, "fov": c["fov"], "inst": inst}


def quat_mats(q):
    """[N,4] Unity quaternions (x, y, z, w) -> [N,3,3] rotation matrices (columns = local axes in world)."""
    q = np.asarray(q, np.float64)
    x, y, z, w = q[:, 0], q[:, 1], q[:, 2], q[:, 3]
    R = np.empty((len(q), 3, 3))
    R[:, 0, 0] = 1 - 2 * (y * y + z * z); R[:, 0, 1] = 2 * (x * y - z * w); R[:, 0, 2] = 2 * (x * z + y * w)
    R[:, 1, 0] = 2 * (x * y + z * w); R[:, 1, 1] = 1 - 2 * (x * x + z * z); R[:, 1, 2] = 2 * (y * z - x * w)
    R[:, 2, 0] = 2 * (x * z - y * w); R[:, 2, 1] = 2 * (y * z + x * w); R[:, 2, 2] = 1 - 2 * (x * x + y * y)
    return R


# linear HDR (rim, face) by domain 1 Jade, 2 Ruby, 3 Blue, 4 Gold; the starting guess is the Photic table of
# docs/MECHANICS_SPEC.md section 7, fitted per build by the paired look fit
PALETTE0 = {1: ((0.0, 0.588, 1.135), (0.0, 0.158, 0.342)), 2: ((0.549, 0.0, 1.498), (0.120, 0.007, 0.309)),
            3: ((0.467, 0.537, 1.217), (0.023, 0.023, 0.347)), 4: ((0.996, 0.405, 0.019), (0.238, 0.125, 0.0))}


class PrismWorld:
    """The session's key prism lists as renderer instances (the game's prism graph: ie = -(200 + fresnel power))."""

    def __init__(self, session, palette=None, power=4.0, shield_mix=0.5):
        self.keys = {}
        for d in read_jsonl(session.path, "prisms_key"):
            p = d["prisms"]
            self.keys[d["frame"]] = np.array(p, np.float64).reshape(-1, len(p[0]) if p else 13)
        self.kf = np.array(sorted(self.keys))
        self.palette = palette or PALETTE0
        self.power = power
        self.shield_mix = shield_mix
        self.shield_scale = 1.0

    def __call__(self, session, frame):
        k = self.kf[np.argmin(np.abs(self.kf - frame))]
        a = self.keys[int(k)]
        out = {}
        if not len(a):
            return out
        R = quat_mats(a[:, 4:8])
        S = a[:, 8:11]
        if a.shape[1] >= 28:  # drawn fraction per axis: a fresh prism grows in from a clock stamp in the vertex stage
            S = S * a[:, 25:28]
        dom = a[:, 11].astype(int)
        flags = a[:, 12].astype(int)
        rim = np.array([self.palette.get(d, self.palette[3])[0] for d in dom])
        face = np.array([self.palette.get(d, self.palette[3])[1] for d in dom])
        if a.shape[1] >= 25:  # the recorder's per-prism values: bright rgba, dark rgba, spread xyz, material
            rim, face = a[:, 13:16], a[:, 17:20]
        sh = ((flags & 3) != 0)[:, None] & (a.shape[1] < 25)  # shielded prisms read paler
        rim = np.where(sh, rim + (1.0 - rim / max(rim.max(), 1e-6)) * self.shield_mix * 0.5, rim)
        pos = a[:, 1:4]
        if a.shape[1] >= 31:  # a fired prism's transform is its landing point; the vertex stage walks it in from the muzzle
            pos = pos + a[:, 28:31]
        rows = np.concatenate([pos, R[:, :, 0] * S[:, 0:1], R[:, :, 1] * S[:, 1:2], R[:, :, 2] * S[:, 2:3], rim,
                               np.zeros((len(a), 1)), np.full((len(a), 1), -(200.0 + self.power)), face], 1)
        # the game swaps a shielded prism's box for an octahedron and a super-shielded one's for a stellated
        # octahedron, both with semi-axes 3x the box half-extents (OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE)
        sup = (flags & 2) != 0
        shd = ((flags & 1) != 0) & ~sup
        big = rows.copy()
        big[:, 3:12] *= self.shield_scale
        for name, m, src in (("box", ~sup & ~shd, rows), ("octa", shd, big), ("stella", sup, big)):
            if m.any():
                out[name] = src[m].astype(np.float32)
        return out


def _rows(pos, ax, ay, az, bright, dark, power):
    n = len(pos)
    return np.concatenate([pos, ax, ay, az, bright, np.zeros((n, 1)), np.full((n, 1), -(200.0 + power)), dark], 1).astype(np.float32)


class VesselWorld:
    """Vessels, their jets and tails, and the crystals of a session line as renderer instances.

    A vessel is a dart the size of the hull's mesh with two engine pods; a trail is rebuilt from the vessel's own
    recorded path (emitters in hull space from world.json: four jets, two tails 12 u behind); a crystal is an
    icosahedron of the crystal's world size.
    """
    trail_classes = None  # e.g. ("Squirrel",) when ExtrasWorld draws the other hulls' trails
    JETS = ((-1.08, -0.11, -1.61), (1.12, -0.11, -1.61), (-1.81, 0.15, 0.72), (1.82, 0.15, 0.72))
    TAILS = ((4.0, 0.0, -12.0), (-4.0, 0.0, -12.0))
    JET_COL = {"Jade": (0.1, 0.75, 1.0), "Ruby": (0.7, 0.12, 1.0), "Blue": (0.3, 0.4, 1.0), "Gold": (1.0, 0.5, 0.08)}
    jet_vmax = 1e9  # no gate: the flare texture and the near plane hide a fast hull's jets (render.FLARE_A)

    def __init__(self, session, hull=(4.5, 1.13, 6.7), hull_col=(0.1, 0.13, 0.24), pod_col=(0.7, 0.2, 0.25),
                 jet_secs=0.5, jet_width=0.15, jet_col=(0.1, 0.75, 1.0),
                 tail_secs=4.0, tail_width=0.5, tail_col=(0.0, 0.6, 0.62), tail_gain=1.0,
                 crystal=3.8, crystal_bright=(0.055, 0.51, 0.749), crystal_dark=(0.0, 0.02, 0.05)):
        self.__dict__.update(locals())
        self.fps = session.meta.get("fps", 15.0)

    def _trail(self, session, frame, v, emit, secs, width, eye, grad, soft=False):
        """Ribbons as SceneRenderer.CollectRibbon builds them: u is the distance fraction from the emitter to the
        oldest point, the strip faces the camera, colour = grad(u) (rgba, linear)."""
        n = max(int(math.ceil(secs * self.fps)), 1)
        hist = []
        for k in range(n + 1):
            if frame - k < 0:
                break
            w = next((x for x in session.state[frame - k]["vessels"] if x["id"] == v["id"]), None)
            if w is None or (hist and np.linalg.norm(np.array(w["pos"]) - hist[-1][0]) > 80.0):
                break  # a respawn jump is not a trail
            hist.append((np.array(w["pos"], np.float64), quat_mats([w["rot"]])[0]))
        if len(hist) < 2:
            return []
        # resample the path in time so a trail shorter than a recorded line still has its length
        tt = np.linspace(0.0, min(secs * self.fps, len(hist) - 1), 24)
        i0 = np.minimum(tt.astype(int), len(hist) - 2)
        f = (tt - i0)[:, None]
        out = []
        for e in emit:
            P = np.array([h[0] + h[1] @ np.array(e) for h in hist])
            p = P[i0] * (1 - f) + P[i0 + 1] * f
            seg = np.linalg.norm(np.diff(p, axis=0), axis=1)
            tot = seg.sum()
            if tot < 1e-4:
                continue
            u = np.concatenate([[0.0], np.cumsum(seg)]) / tot
            out.append((p, 0.5 * width(u), grad(u), soft))
        return out

    def __call__(self, session, frame):
        st = session.state[frame]
        out = {}
        V = st["vessels"]
        eye = np.array(st["camera"]["pos"])
        box = []
        if V:
            pos = np.array([v["pos"] for v in V], np.float64)
            R = quat_mats([v["rot"] for v in V])
            h = self.hull
            n = len(V)
            hc = np.tile(self.hull_col, (n, 1))
            out["dart"] = _rows(pos, R[:, :, 0] * h[0], R[:, :, 1] * h[1] * 2, R[:, :, 2] * h[2], hc, hc, 0.001)
            pc = np.tile(self.pod_col, (n, 1))
            for sx in (-1.0, 1.0):
                p = pos + R[:, :, 0] * sx * 1.8 + R[:, :, 2] * -0.6
                box.append(_rows(p, R[:, :, 0] * 0.5, R[:, :, 1] * 0.4, R[:, :, 2] * 1.6, pc, pc, 0.001))
            jc, tc = np.array(self.jet_col), np.array(self.tail_col)
            rib = []
            for v in V:
                if self.trail_classes is not None and v.get("class") not in self.trail_classes:
                    continue  # another hull's trails come from the recorded extras
                gate = float(np.clip((self.jet_vmax * 1.15 - v.get("speed", 0.0)) / (self.jet_vmax * 0.3), 0.0, 1.0))
                jc = np.array(self.JET_COL.get(v.get("domain"), self.jet_col))
                tc = jc * 0.75  # the tail takes the domain colour too
                # as the recorder reads them off the live renderers: jet 0.5 s, 0.3 wide, alpha 1 -> 0 under the flare
                # texture; tail 4 s, width 1 / 0.7 / 0 and alpha 0 / 0.42 / 0 at 0 / 50 / 100 %
                rib += self._trail(session, frame, v, self.JETS, self.jet_secs, lambda u: np.full_like(u, self.jet_width), eye,
                                   lambda u, jc=jc, gate=gate: np.concatenate([np.tile(jc, (len(u), 1)), (1 - u)[:, None] * gate], 1), soft=True)
                rib += self._trail(session, frame, v, self.TAILS, self.tail_secs,
                                   lambda u: self.tail_width * np.interp(u, [0, 0.5, 1], [1, 0.7, 0]), eye,
                                   lambda u, tc=tc: np.concatenate([np.tile(tc, (len(u), 1)), np.interp(u, [0, 0.5, 1], [0, 0.42, 0])[:, None] * self.tail_gain], 1))
            if rib:
                out["lines"] = rib
        C = [c for c in st.get("crystals", []) if not c.get("exploding")]
        if C:
            pos = np.array([c["pos"] for c in C], np.float64)
            s = np.array([c["scale"][0] for c in C])[:, None] / 1.7 * self.crystal
            I = np.eye(3)
            out["icosa"] = _rows(pos, I[0][None] * s, I[1][None] * s, I[2][None] * s,
                                 np.tile(self.crystal_bright, (len(C), 1)), np.tile(self.crystal_dark, (len(C), 1)), 2.0)
        box = [b for b in box if len(b)]
        if box:
            out["box"] = np.concatenate(box, 0)
        return out


class Layers:
    """Several instance builders as one (their per-mesh lists concatenated)."""

    def __init__(self, *fns):
        self.fns = fns

    def __call__(self, session, frame):
        out = {}
        for f in self.fns:
            for k, v in f(session, frame).items():
                out[k] = (out[k] + v if k == "lines" else np.concatenate([out[k], v], 0)) if k in out else v
        return out


def _lin(c):
    c = np.asarray(c, np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


class ExtrasWorld:
    """What extras_key.jsonl holds, drawn as recorded: every Line and Trail renderer as a ribbon with its own points,
    width curve and gradient, and every other mesh renderer (creature spindles, ring wires) as a stand-in of its
    recorded pose and size in its material's colour. Use it in place of VesselWorld's rebuilt trails."""

    def __init__(self, session, gain=1.0, mesh_gain=1.0, soft=("DistortedFlare",), skip=()):
        # skip: the recorder lists a hull's jet and tail trails whether or not the game shows them (a Squirrel's
        # jets are gone above ~120 u/s though their points go on being recorded), so those stay with VesselWorld
        self.skip = skip
        self.bursts = False  # stand-in boxes for burst pieces: no gain measured on Skim Race, Regatta or Maelstrom
        self.keys = {}
        for d in read_jsonl(session.path, "extras_key"):
            self.keys[d["frame"]] = d
        self.kf = np.array(sorted(self.keys))
        self.gain, self.mesh_gain, self.soft = gain, mesh_gain, soft
        w = json.load(open(os.path.join(session.path, "world.json")))
        self.mat = {}
        self.clear = {k for k, m in w.get("materials", {}).items() if m.get("render_queue", 2000) >= 3000}
        for k, m in w.get("materials", {}).items():
            v = m.get("vectors", {})
            for name in ("_BrightColor", "_Color2", "_Straight_Color", "_Edge_Color", "_Color", "_BaseColor"):
                if name in v and max(v[name][:3]) > 0:
                    self.mat[k] = np.array(v[name][:3], np.float64)
                    break

    def __call__(self, session, frame):
        d = self.keys[int(self.kf[np.argmin(np.abs(self.kf - frame))])]
        out, lines = {}, []
        for x in d.get("lines", []):
            p = np.array(x["points"], np.float64).reshape(-1, 3)
            if len(p) < 2 or (x.get("path", "").startswith("Squirrel") and any(k in x["path"] for k in self.skip)):
                continue
            if x["type"] == "TrailRenderer":
                p = p[::-1]  # stored oldest first; the ribbon runs from the emitter
            seg = np.linalg.norm(np.diff(p, axis=0), axis=1)
            if seg.sum() < 1e-4:
                continue
            u = np.concatenate([[0.0], np.cumsum(seg)]) / seg.sum()
            wc = x.get("width_curve") or [1, 1, 1]
            g = np.array(x.get("gradient") or [[1, 1, 1, 1]] * 3, np.float64)
            hw = 0.5 * np.interp(u, [0, 0.5, 1], wc)
            rgba = np.stack([np.interp(u, [0, 0.5, 1], g[:, i]) for i in range(4)], 1)
            rgba[:, :3] = _lin(rgba[:, :3]) * self.gain
            lines.append((p, hw, rgba, any(s in x.get("material", "") for s in self.soft)))
        if lines:
            out["lines"] = lines
        # destroyed and consumed prisms (PrismCollisionBurst.hlsl): the prism's own box, cut into fragments that fly
        # apart or wind into a sink and shrink away, coverage linear in age. Stand-in: the box, shrinking with its
        # age, drifting to the sink when it is a suction. _SliceTiming = (start, life, -, kind)
        B = d.get("bursts", []) if self.bursts else []
        if B:
            m = np.array([b["m"] for b in B], np.float64).reshape(-1, 4, 4)  # column-major: rows here are columns
            tm = np.array([b["_SliceTiming"] for b in B], np.float64)
            p = np.clip((d["t"] - tm[:, 0]) / np.maximum(tm[:, 1], 1e-3), 0.0, 1.0)
            k = (1.0 - p)[:, None]
            bs = np.array([b.get("bounds", [1, 1, 1]) for b in B], np.float64)
            pos = m[:, 3, :3].copy()
            suck = tm[:, 3] == 7
            sink = np.array([b["_SliceCentre"][:3] for b in B], np.float64)
            pos[suck] += (sink[suck] - pos[suck]) * (p[suck, None] ** 2)
            pos[~suck] += m[~suck, 2, :3] * 0.0
            br = np.array([b["_BrightColor"][:3] for b in B], np.float64)
            dk = np.array([b["_DarkColor"][:3] for b in B], np.float64)
            out["box"] = _rows(pos, m[:, 0, :3] * bs[:, 0:1] * k, m[:, 1, :3] * bs[:, 1:2] * k, m[:, 2, :3] * bs[:, 2:3] * k, br, dk, 4.0)
        # translucent shells (explosion ripples, charge fields) are not solid stand-in material
        M = [m for m in d.get("meshes", []) if m["material"] not in self.clear and m["material"].split("#")[0] not in self.clear]
        if M:
            pos = np.array([m["world_center"] for m in M], np.float64)
            R = quat_mats([m["rot"] for m in M])
            sz = np.array([np.array(m["mesh_size"]) * np.abs(np.array(m["scale"])) for m in M])
            col = np.array([self.mat.get(m["material"], self.mat.get(m["material"].split("#")[0], (0.2, 0.5, 0.4))) for m in M]) * self.mesh_gain
            ring = np.array(["Ring" in m["mesh"] for m in M])
            big = sz.max(1) > 400  # the nucleus cage and other world-sized shells are not stand-in material
            rows = _rows(pos, R[:, :, 0] * sz[:, 0:1], R[:, :, 1] * sz[:, 1:2], R[:, :, 2] * sz[:, 2:3], col, col * 0.15, 2.0)
            for name, m in (("octa", ~ring & ~big), ("hoop", ring & ~big)):
                if m.any():
                    out[name] = rows[m]
        return out
