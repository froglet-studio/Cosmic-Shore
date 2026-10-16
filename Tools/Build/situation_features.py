#!/usr/bin/env python3
"""situation_features.py - what a pilot was facing, doing and trying to do, moment by moment (Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md S3).

Yash, 2026-10-15: "record the situations: what target the player was aiming at, where the ring or the target is, where
the obstacles are, how we skim, how quickly we change direction, how we ignore some skims when we must keep speed... This
is how we can actually train the AI, not just by input."

It turns a flight Unity recorded while someone played in the editor (HumanFlightRecorder, flight/2 or flight/3) into
situations (target, line-up, obstacles, skim, manoeuvre, race), labels (what the pilot was going for, the phase, skims
taken and skipped, mistakes), legs, and what to learn. When the flight recorded the camera it also redraws the pilot's
SCREEN (Caleb's review, plan §7): what was on screen, what a rail hid, time to contact, an 8 x 5 view grid (V2); labels
count only what the pilot could see (V3); and it measures how late the pilot answers the ring (V4). The definitions are in Docs/AI_SYSTEM/situation_features.json.
The ring and rail geometry comes from Tools/Build/regatta_course_harness/course.js, generated from RegattaCourse.
(It was written as the twin of a browser page's situation.js; the page was deleted on 2026-10-16 and this is now the
only implementation. --self-test checks it against the golden output both agreed on, with a negative control.)

    python3 Tools/Build/situation_features.py                     # every Regatta flight on every origin branch (stored with Store AI record)
    python3 Tools/Build/situation_features.py --dir <folder>      # ...or a local folder of flight_*.json
    python3 Tools/Build/situation_features.py --json <file>       # one flight: the full analysis as JSON on stdout
    python3 Tools/Build/situation_features.py --write             # ...and write what was learned (learned_situations.json + LEARNED_SITUATIONS.md)
    python3 Tools/Build/situation_features.py --self-test         # the fixture flight against its golden output

Training data comes from playing in the Unity EDITOR only (Yash, 2026-10-16: "let's not use artifact to train, we will
play in editor only for perfect training data"): the flights read here are the ones HumanFlightRecorder writes and Store AI
record stores.
"""
import argparse
import json
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
COURSE_JS = os.path.join(HERE, "regatta_course_harness", "course.js")
FIXTURE = os.path.join(HERE, "situation_fixtures", "flight_fixture_squirrel_I1.json")   # the self-test only, never training data
GOLDEN = os.path.join(HERE, "situation_fixtures", "flight_fixture_squirrel_I1.golden.json")
LEARNED_JSON = os.path.join(REPO, "Docs", "AI_SYSTEM", "learned_situations.json")
LEARNED_DOC = os.path.join(REPO, "Docs", "AI_SYSTEM", "LEARNED_SITUATIONS.md")
sys.path.insert(0, HERE)

VERSION = 1
DEG = 180 / math.pi
PRISM_HALF = 3
SLOWED_BIT = 8                          # VesselStatus state bit the recorder writes: Slowed (a prism hit)
LEAD_CAP, LEAD_SHARE, THROUGH = 480, 0.4, 320
MIN_FLIGHTS = 3   # editor flights of one hull before anything is learned from them
PHASES = ["cruise", "approach", "line-up", "thread", "exit", "skim run", "boost straight", "recovery"]

# The pilot's VIEW (Caleb's review, SITUATION_RECORDING_PLAN.md §7, V2-V4): what was on screen, what a rail hid, and how
# late the pilot answered it. Only for flights that recorded the camera (HumanFlightRecorder since 2026-10-16); a flight
# without a camera is read as ALL-SEEING, exactly as before (its labels are not gated, and the golden test holds).
VIEW_NEAR, VIEW_RANGE = 0.5, 1500      # the camera's near plane and how far the view features look (u)
GRID_W, GRID_H = 8, 5                  # the "compressed screenshot": 8 x 5 screen cells
KIND_EMPTY, KIND_OWN_RAIL, KIND_RIVAL_RAIL, KIND_RING, KIND_RIVAL = 0, 1, 2, 3, 4
RIVAL_RADIUS = 4.0                     # a hull, for its angular size and occlusion
SAME_RAIL_SPAN = 8                     # a rail prism is not hidden by its own rail's next 8 prisms each way (64 u: the
                                       # stretch the pilot is looking at); other rails, and the same rail further on, hide it
SUPER_SHIELD_HALF_SCALE = 1.5          # SkimRaceShell.SuperShieldHalfScale: the contact shell is 1.5 x the prism
MEMORY_S = 1.0                         # a target still counts this long after it left the screen
SKIM_SEEN_S = 0.3                      # a skim counts as SKIPPED only if it was in plain view this long before the choice
LAG_MAX_S, LAG_MIN_R = 1.0, 0.2        # the reaction-lag search window, and the weakest fit worth reporting
DEFAULT_ASPECT = 16 / 9


# ---------------------------------------------------------------- JavaScript's rounding and vectors

def jsround(x):
    return math.floor(x + 0.5)


def r2(x):
    return jsround(x * 100) / 100


def sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def vlen(a): return math.sqrt(dot(a, a))
def dist(a, b): return vlen(sub(a, b))
def clamp(x, lo, hi): return lo if x < lo else hi if x > hi else x


def norm(a):
    n = vlen(a)
    return mul(a, 1 / n) if n > 1e-9 else [0, 0, 0]


def angle(a, b):
    la, lb = vlen(a), vlen(b)
    if la < 1e-9 or lb < 1e-9:
        return 0
    return math.acos(clamp(dot(a, b) / (la * lb), -1, 1)) * DEG


def pct(arr, q):
    if not arr:
        return None
    s = sorted(arr)
    return s[min(len(s) - 1, max(0, jsround(q * (len(s) - 1))))]


# ---------------------------------------------------------------- the course (course.js is generated from RegattaCourse)

def load_course_js(path=COURSE_JS):
    text = open(path, encoding="utf-8").read()
    body = text[text.index("=") + 1:].strip().rstrip(";")
    body = re.sub(r"/\*.*?\*/", "", body, flags=re.S)
    body = re.sub(r"([{,]\s*)([A-Za-z_]\w*|\d+)\s*:", r'\1"\2":', body).replace("'", '"')
    body = re.sub(r",\s*([}\]])", r"\1", body)
    return json.loads(body)


def course_from(COURSE, intensity):
    src = COURSE.get(str(intensity))
    if not src:
        return None
    lanes = []
    for flat in src["lanes"]:
        lanes.append([[flat[k], flat[k + 1], flat[k + 2]] for k in range(0, len(flat) - 2, 3)])
    gates = [{"p": list(g["p"]), "a": norm(g["a"]), "r": g["r"]} for g in src["gates"]]
    return {"intensity": intensity, "gates": gates, "lanes": lanes, "laneDomains": list(COURSE.get("laneDomains", []))}


# ---------------------------------------------------------------- a flight -> columns

def normalize(f):
    cols = f.get("columns") or []
    idx = {c: i for i, c in enumerate(cols)}
    rows = f.get("samples") or []
    n = len(rows)

    def col(name, dflt):
        k = idx.get(name)
        return [dflt if k is None or k >= len(r) or r[k] is None else float(r[k]) for r in rows]

    hull = f.get("hull") or ""
    N = {
        "schema": f.get("schema"), "source": f.get("source", "unity"), "mode": f.get("mode"), "hull": hull,
        "hullKey": (f.get("hullKey") or hull).lower(), "verb": f.get("verb") or ("skim" if hull.lower() == "squirrel" else ""),
        "domain": (f.get("domain") or "").lower(), "intensity": int(f.get("intensity") or 1), "hz": float(f.get("sampleHz") or 10),
        "turn": f.get("turn"), "skimReach": float(f["skimReach"]) if f.get("skimReach") is not None else (7.5 if hull.lower() == "squirrel" else 0),
        "raceTarget": int(f.get("raceTarget") or 0), "finished": bool(f.get("finished")), "overrides": f.get("overrides") or [],
        "n": n, "t": col("t", 0), "spd": col("speed", 0), "boost": col("boost", 1), "thr": col("throttle", 1), "lt": col("lt", 0),
        "rt": col("rt", 0), "rings": [int(x) for x in col("rings", 0)], "held": col("held", 0),
        "skims": col("skims", 0) if "skims" in idx else None, "rams": col("rams", 0) if "rams" in idx else None,
        # The recorder's VesselStatus bits (flight/3): Slowed (8) is set while a prism hit has the hull slowed.
        "slowed": [int(x) & SLOWED_BIT != 0 for x in col("state", 0)] if "state" in idx else None,
        "p": [], "f": [], "u": [], "rv": f.get("rivals"), "rivalHulls": f.get("rivalHulls") or [],
    }
    for r in rows:
        N["p"].append([float(r[idx["x"]]), float(r[idx["y"]]), float(r[idx["z"]])])
        N["f"].append(norm([float(r[idx["fx"]]), float(r[idx["fy"]]), float(r[idx["fz"]])]))
        N["u"].append(norm([float(r[idx["ux"]]), float(r[idx["uy"]]), float(r[idx["uz"]])]))
    N["yaw"], N["pitch"] = col("xSum", 0), col("ySum", 0)   # the sticks an AI writes too: XSum yaws, YSum pitches
    N["rvVel"] = f.get("rivalVel")
    shell = f.get("shellHalf") or []
    N["occR"] = min(shell[0], shell[1]) / SUPER_SHIELD_HALF_SCALE if len(shell) >= 2 and min(shell[0], shell[1]) > 0 else PRISM_HALF
    N["cam"] = None
    if all(c in idx for c in ("camx", "camy", "camz", "camfx", "camfy", "camfz", "fov")):
        cam = {"p": [], "f": [], "u": [], "fov": col("fov", 0), "aspect": col("camAspect", DEFAULT_ASPECT), "rear": col("camRear", 0), "ok": []}
        has_up = all(c in idx for c in ("camux", "camuy", "camuz"))
        for i, r in enumerate(rows):
            cf = norm([float(r[idx["camfx"]]), float(r[idx["camfy"]]), float(r[idx["camfz"]])])
            # Flights recorded before the camera's up was (2026-10-16 first cut): the hull's up, square to the camera.
            cu = [float(r[idx["camux"]]), float(r[idx["camuy"]]), float(r[idx["camuz"]])] if has_up else N["u"][i]
            cu = norm(sub(cu, mul(cf, dot(cu, cf))))
            cam["p"].append([float(r[idx["camx"]]), float(r[idx["camy"]]), float(r[idx["camz"]])])
            cam["f"].append(cf)
            cam["u"].append(cu)
            cam["ok"].append(cam["fov"][i] > 1 and vlen(cf) > 0.5 and vlen(cu) > 0.5)
            if cam["aspect"][i] <= 0.1:
                cam["aspect"][i] = DEFAULT_ASPECT
        if any(cam["ok"]):
            N["cam"] = cam
    return N


def omega_fn(N, turn_rates):
    t = N.get("turn")
    if t and t.get("p") is not None:
        return lambda v: min(t["p"], t["y"]) + v * (t.get("rts") or 0)
    hi = pct([abs(x) for x in turn_rates], 0.95) or 60
    hi = max(30, hi)
    return lambda v: hi


# ---------------------------------------------------------------- features

def features(N, C):
    G, ng, n, dt = C["gates"], len(C["gates"]), N["n"], 1 / N["hz"]
    own = C["laneDomains"].index(N["domain"]) if N["domain"] in C["laneDomains"] else -1
    turn_rates = []
    for i in range(n):
        a, b = N["f"][max(0, i - 1)], N["f"][min(n - 1, i + 1)]
        span = (min(n - 1, i + 1) - max(0, i - 1)) * dt
        turn_rates.append(angle(a, b) / span if span > 0 else 0)
    omega = omega_fn(N, turn_rates)
    out = []
    for i in range(n):
        p, f, u = N["p"][i], N["f"][i], N["u"][i]
        right, v = norm(cross(f, u)), max(1, N["spd"][i])
        ring = N["rings"][i] % ng
        g, g2, g0 = G[ring], G[(ring + 1) % ng], G[(ring - 1 + ng) % ng]
        rel = sub(g["p"], p)
        d, fz = vlen(rel), dot(rel, f)
        side = 1 if dot(sub(p, g["p"]), g["a"]) >= 0 else -1
        in_dir = mul(g["a"], -side)
        fa, mouth_offset, threads = dot(f, g["a"]), -1, False
        if abs(fa) > 1e-4:
            t_hit = -dot(sub(p, g["p"]), g["a"]) / fa
            if t_hit > 0:
                off = sub(add(p, mul(f, t_hit)), g["p"])
                mouth_offset = vlen(sub(off, mul(g["a"], dot(off, g["a"]))))
                threads = mouth_offset <= g["r"]
        leg_len = dist(g0["p"], g["p"])
        lead = min(LEAD_CAP, LEAD_SHARE * leg_len)
        heading_err = angle(f, rel)
        w = omega(N["spd"][i]) / DEG
        r_min = v / max(1e-3, w)
        r_need = d / (2 * math.sin(min(math.pi / 2, heading_err / DEG))) if heading_err > 1 else 1e6
        gap, gap_own, ahead_n, ahead_gap, ahead_bearing = 1e6, False, 0, 1e6, 0
        skim_detour, skim_t, skim_pt, skim_lane, skim_idx, ahead_pt, ahead_key = 1e6, 1e6, None, -1, -1, None, None
        reach, horizon = N["skimReach"], max(60, v * 2)
        for l, pts in enumerate(C["lanes"]):
            for k, q in enumerate(pts):
                dx, dy, dz = q[0] - p[0], q[1] - p[1], q[2] - p[2]
                if abs(dx) > horizon + 10 or abs(dy) > horizon + 10 or abs(dz) > horizon + 10:
                    continue
                dq = [dx, dy, dz]
                dd = vlen(dq)
                gq = dd - PRISM_HALF
                if gq < gap:
                    gap, gap_own = gq, l == own
                fwdq = dot(dq, f)
                if fwdq <= 0:
                    continue
                ang = angle(f, dq)
                if ang < 30 and dd < 160:
                    ahead_n += 1
                    if gq < ahead_gap:
                        ahead_gap, ahead_bearing = gq, math.atan2(dot(dq, right), fwdq) * DEG
                        ahead_pt, ahead_key = q, f"prism:{l}:{k}"
                if reach > 0 and ang < 35 and 15 < dd < horizon:
                    det = dd + dist(q, g["p"]) - d
                    if det < skim_detour:
                        skim_detour, skim_t, skim_pt, skim_lane, skim_idx = det, dd / v, q, l, k
        place, rv = 1, (N["rv"][i] if N["rv"] and i < len(N["rv"]) else None)
        if rv:
            for j in range(0, len(rv) - 3, 4):
                if rv[j + 3] > N["rings"][i]:
                    place += 1
        out.append({
            "t": N["t"][i], "ring": ring, "dist": d, "bearing": math.atan2(dot(rel, right), fz) * DEG,
            "elevation": math.atan2(dot(rel, u), math.sqrt(fz * fz + dot(rel, right) ** 2)) * DEG,
            "headingErr": heading_err, "axisAngle": angle(f, in_dir), "mouthOffset": mouth_offset, "threadsIfStraight": threads,
            "mouthRadius": g["r"], "timeToMouth": d / v, "legLen": leg_len, "legTurn": angle(sub(g["p"], g0["p"]), sub(g2["p"], g["p"])),
            "nextLeg": dist(g["p"], g2["p"]), "approachLead": lead, "rMin": r_min, "rNeed": r_need, "canTurn": r_need >= r_min,
            "turnRate": turn_rates[i], "turnJerk": 0, "speed": N["spd"][i], "throttle": N["thr"][i], "boost": N["boost"][i], "lt": N["lt"][i],
            "gap": gap, "gapOwnRail": gap_own, "aheadPrisms": ahead_n, "aheadGap": ahead_gap, "aheadBearing": ahead_bearing,
            "skimInReach": reach > 0 and gap < reach, "skimDetour": skim_detour, "skimTime": skim_t, "skimPoint": skim_pt, "skimLane": skim_lane,
            "lap": N["rings"][i] // ng + 1, "ringsLeft": N["raceTarget"] - N["rings"][i] if N["raceTarget"] else None,
            "place": place if rv else None, "side": side, "held": N["held"][i] > 0.5,
            "skimKey": f"prism:{skim_lane}:{skim_idx}" if skim_pt is not None else None, "aheadPoint": ahead_pt, "aheadKey": ahead_key,
        })
    for i in range(1, n):
        out[i]["turnJerk"] = (out[i]["turnRate"] - out[i - 1]["turnRate"]) * N["hz"]
    return out


# ---------------------------------------------------------------- the pilot's VIEW (V2): the screen, redrawn offline

def _rail_points(C):
    """Every rail prism as one array, with its lane and index (the occluders and the grid's rail cells)."""
    import numpy as np
    pts, lane, idx = [], [], []
    for l, lp in enumerate(C["lanes"]):
        for k, q in enumerate(lp):
            pts.append(q)
            lane.append(l)
            idx.append(k)
    return np.array(pts, dtype=float).reshape(-1, 3), np.array(lane, dtype=int), np.array(idx, dtype=int)


def _velocity(N, i):
    """The pilot's velocity from its positions either side (works for every flight, camera or not)."""
    n, a, b = N["n"], max(0, i - 1), min(N["n"] - 1, i + 1)
    span = N["t"][b] - N["t"][a] if n > 1 else 0
    return mul(sub(N["p"][b], N["p"][a]), 1 / span) if span > 1e-6 else [0, 0, 0]


def _rival_velocity(N, i, j):
    vel = N["rvVel"][i] if N["rvVel"] and i < len(N["rvVel"]) else None
    if vel and 3 * j + 2 < len(vel):
        return [vel[3 * j], vel[3 * j + 1], vel[3 * j + 2]]
    rv = N["rv"]
    a, b = max(0, i - 1), min(N["n"] - 1, i + 1)
    if rv and b < len(rv) and rv[a] and rv[b] and 4 * j + 2 < len(rv[a]) and 4 * j + 2 < len(rv[b]) and len(rv[a]) == len(rv[b]):
        span = N["t"][b] - N["t"][a]
        if span > 1e-6:
            return [(rv[b][4 * j + c] - rv[a][4 * j + c]) / span for c in range(3)]
    return [0, 0, 0]


class Viewer:
    """The pilot's camera at any sample, as a question you can ask: where does this point land on screen, how big is it,
    is a rail in front of it, how soon does it arrive. Built once per flight; each sample's rail prisms are cached."""

    def __init__(self, N, C):
        import numpy as np
        self.np, self.N, self.cam = np, N, N["cam"]
        self.P, self.lane_of, self.idx_of = _rail_points(C)
        self.occ_r = N["occR"]
        self._frames = {}

    def ok(self, i):
        return bool(self.cam) and 0 <= i < self.N["n"] and self.cam["ok"][i]

    def frame(self, i):
        fr = self._frames.get(i)
        if fr is None:
            np, cam = self.np, self.cam
            cp, cf, cu = np.array(cam["p"][i]), np.array(cam["f"][i]), np.array(cam["u"][i])
            cr = np.cross(cu, cf)               # Unity is left-handed: right = up x forward
            cr /= max(1e-9, np.linalg.norm(cr))
            tan_v = math.tan(cam["fov"][i] / 2 / DEG)
            rel = self.P - cp
            z = rel @ cf
            d2 = np.einsum("ij,ij->i", rel, rel)
            near = (z > VIEW_NEAR) & (d2 < VIEW_RANGE * VIEW_RANGE)
            fr = {"cp": cp, "cf": cf, "cu": cu, "cr": cr, "tan_v": tan_v, "tan_h": tan_v * cam["aspect"][i], "near": near,
                  "rel": rel[near], "z": z[near], "d": np.sqrt(d2[near]), "P": self.P[near], "lane": self.lane_of[near], "idx": self.idx_of[near]}
            self._frames[i] = fr
        return fr

    def project(self, i, q):
        fr = self.frame(i)
        r = self.np.asarray(q, dtype=float) - fr["cp"]
        zz = float(r @ fr["cf"])
        if zz <= VIEW_NEAR:
            return None, None, zz
        return float(r @ fr["cr"]) / (zz * fr["tan_h"]), float(r @ fr["cu"]) / (zz * fr["tan_v"]), zz

    def blocked(self, i, q, rad, key=None):
        np, fr, occ_r = self.np, self.frame(i), self.occ_r
        q = np.asarray(q, dtype=float)
        r = q - fr["cp"]
        dd = float(np.linalg.norm(r))
        if dd < 1e-6 or not len(fr["P"]):
            return False
        t = fr["rel"] @ (r / dd)
        perp2 = fr["d"] * fr["d"] - t * t
        mine = np.linalg.norm(fr["P"] - q, axis=1) <= rad + occ_r   # its own prism hides nothing
        if key and key.startswith("prism:"):
            _, l, k = key.split(":")
            mine |= (fr["lane"] == int(l)) & (np.abs(fr["idx"] - int(k)) <= SAME_RAIL_SPAN)
        return bool(np.any((t > 0) & (t < dd - rad - occ_r) & (perp2 < occ_r * occ_r) & ~mine))

    def look(self, i, q, rad, key=None, vo=(0, 0, 0), rim=None):
        N, fr = self.N, self.frame(i)
        sx, sy, _ = self.project(i, q)
        dcam = max(1e-6, dist(q, self.cam["p"][i]))
        ang = 2 * math.atan(rad / dcam) * DEG
        on = False
        if sx is not None:
            ex, ey = math.tan(ang / 2 / DEG) / fr["tan_h"], math.tan(ang / 2 / DEG) / fr["tan_v"]
            on = abs(sx) - ex <= 1 and abs(sy) - ey <= 1
        hidden = self.blocked(i, q, rad, key) if rim is None else all(self.blocked(i, r_, 0) for r_ in [q] + rim)
        to = sub(q, N["p"][i])
        dd = vlen(to)
        closing = dot(sub(_velocity(N, i), list(vo)), mul(to, 1 / dd)) if dd > 1e-6 else 0
        return {"onScreen": on, "sx": r2(sx) if sx is not None else None, "sy": r2(sy) if sy is not None else None,
                "angSize": r2(ang), "occluded": hidden, "tau": r2(dd / closing) if closing > 1 else None}

    def in_plain_view(self, i, q, rad, key=None):
        o = self.look(i, q, rad, key)
        return o["onScreen"] and not o["occluded"]

    def ring(self, i, g):
        a, cu, cr = g["a"], list(self.frame(i)["cu"]), list(self.frame(i)["cr"])
        e1 = norm(cross(a, cu))
        if vlen(e1) < 0.5:
            e1 = norm(cross(a, cr))
        e2 = norm(cross(a, e1))
        rim = [add(g["p"], mul(e, s_ * g["r"])) for e in (e1, e2) for s_ in (1, -1)]
        return self.look(i, g["p"], g["r"], rim=rim), rim


def view(N, C, F):
    """Per sample, what the pilot's screen showed (None for a flight with no camera, or a sample without one):
    for the target ring, the ring after it, the skim candidate, the nearest prism ahead and each rival - onScreen,
    screen x / y (-1..1, + right / up), angular size (deg), occluded (a ray from the camera hits a rail prism first;
    a ring is hidden only when its centre and all four rim points are), tau (time to contact = distance / closing
    speed, which is angular size / its growth rate; None when not closing) - plus an 8 x 5 grid of the nearest thing
    in each screen cell (kind, depth) and the things in plain view (visible)."""
    if not N["cam"]:
        return None
    import numpy as np
    vw = Viewer(N, C)
    N["viewer"] = vw
    G, ng = C["gates"], len(C["gates"])
    own = C["laneDomains"].index(N["domain"]) if N["domain"] in C["laneDomains"] else -1
    out = []
    for i in range(N["n"]):
        if not vw.ok(i):
            out.append(None)
            continue
        fr, x = vw.frame(i), F[i]
        objs, vis = {}, []
        ring_now, rim_now = vw.ring(i, G[x["ring"]])
        ring_next, rim_next = vw.ring(i, G[(x["ring"] + 1) % ng])
        objs["ring"], objs["nextRing"] = ring_now, ring_next
        for key, o in ((f"ring:{x['ring']}", ring_now), (f"ring:{(x['ring'] + 1) % ng}", ring_next)):
            if o["onScreen"] and not o["occluded"]:
                vis.append(key)
        for name, pt, key in (("skim", x["skimPoint"], x["skimKey"]), ("ahead", x["aheadPoint"], x["aheadKey"])):
            if pt is not None:
                objs[name] = vw.look(i, pt, vw.occ_r, key=key)
                if objs[name]["onScreen"] and not objs[name]["occluded"]:
                    vis.append(key)
        rv = N["rv"][i] if N["rv"] and i < len(N["rv"]) else None
        rivals = []
        if rv:
            for j in range(0, len(rv) - 3, 4):
                o = vw.look(i, [rv[j], rv[j + 1], rv[j + 2]], RIVAL_RADIUS, vo=_rival_velocity(N, i, j // 4))
                rivals.append(o)
                if o["onScreen"] and not o["occluded"]:
                    vis.append(f"rival:{j // 4}")
        objs["rivals"] = rivals

        # The view grid: the nearest thing in each of 8 x 5 screen cells (rails, ring centres and rims, rival hulls).
        relN, zN = fr["rel"], fr["z"]
        X = list((relN @ fr["cr"]) / (zN * fr["tan_h"]))
        Y = list((relN @ fr["cu"]) / (zN * fr["tan_v"]))
        D = list(zN)
        K = [KIND_OWN_RAIL if l == own else KIND_RIVAL_RAIL for l in fr["lane"]]
        extra = [(q, KIND_RING) for g, rim in ((G[x["ring"]], rim_now), (G[(x["ring"] + 1) % ng], rim_next)) for q in [g["p"]] + rim]
        if rv:
            extra += [([rv[j], rv[j + 1], rv[j + 2]], KIND_RIVAL) for j in range(0, len(rv) - 3, 4)]
        for q, k in extra:
            px, py, zz = vw.project(i, q)
            if px is not None:
                X.append(px)
                Y.append(py)
                D.append(zz)
                K.append(k)
        X, Y, D, K = np.array(X, dtype=float), np.array(Y, dtype=float), np.array(D, dtype=float), np.array(K, dtype=int)
        inside = (np.abs(X) < 1) & (np.abs(Y) < 1)
        grid_kind, grid_depth = [KIND_EMPTY] * (GRID_W * GRID_H), [0] * (GRID_W * GRID_H)
        if inside.any():
            col = np.clip(((X[inside] + 1) / 2 * GRID_W).astype(int), 0, GRID_W - 1)
            row = np.clip(((1 - Y[inside]) / 2 * GRID_H).astype(int), 0, GRID_H - 1)
            cell, dd, kk = row * GRID_W + col, D[inside], K[inside]
            order = np.lexsort((dd, cell))
            first = np.unique(cell[order], return_index=True)[1]
            for o_ in order[first]:
                grid_kind[int(cell[o_])], grid_depth[int(cell[o_])] = int(kk[o_]), jsround(float(dd[o_]))
        objs["gridKind"], objs["gridDepth"], objs["visible"] = grid_kind, grid_depth, vis
        out.append(objs)
    return out


def _window(N, i, seconds):
    return range(max(0, i - jsround(seconds * N["hz"])), i + 1)


def _point_of(key, C):
    if key and key.startswith("prism:"):
        _, l, k = key.split(":")
        return C["lanes"][int(l)][int(k)]
    return None


def was_seen(N, V, i, key, seconds, C=None):
    """In plain view (on screen, not behind a rail) at some sample in the last `seconds`. A rail prism is asked about
    ITSELF at each earlier sample, from the camera there (the skim candidate moves along the rail every sample); a ring
    or a rival by its id. A sample without a camera counts as seen: a flight we cannot see through stays all-seeing."""
    if key is None:
        return False
    q = _point_of(key, C) if C else None
    vw = N.get("viewer")
    for j in _window(N, i, seconds):
        if V[j] is None:
            return True
        if (q is not None and vw.in_plain_view(j, q, vw.occ_r, key)) or key in V[j]["visible"]:
            return True
    return False


def seen_throughout(N, V, i, key, seconds, C=None):
    if key is None:
        return False
    q = _point_of(key, C) if C else None
    vw = N.get("viewer")
    for j in _window(N, i, seconds):
        if V[j] is None:
            continue
        if not ((q is not None and vw.in_plain_view(j, q, vw.occ_r, key)) or key in V[j]["visible"]):
            return False
    return True


def view_summary(N, V, skims, misses):
    if V is None:
        return {"known": False, "note": "no camera in this flight: read as all-seeing (labels not gated)"}
    known = [v for v in V if v is not None]
    return {"known": True, "samples": len(known), "share": r2(len(known) / max(1, N["n"])),
            "ringOnScreen": r2(sum(1 for v in known if v["ring"]["onScreen"]) / max(1, len(known))),
            "ringHidden": r2(sum(1 for v in known if v["ring"]["onScreen"] and v["ring"]["occluded"]) / max(1, len(known))),
            "skimsSkippedSeen": len([s for s in skims if not s["taken"] and s.get("seen")]),
            "skimsUnseen": len([s for s in skims if not s["taken"] and s.get("seen") is False]),
            "controlMisses": len([m for m in misses if m.get("cause") == "control"]),
            "navigationMisses": len([m for m in misses if m.get("cause") == "navigation"])}


# ---------------------------------------------------------------- reaction lag (V4)

def lag_from_signals(t_ok, sig_x, stick_x, sig_y, stick_y, hz):
    """The shift (s) that best lines the target's position up with the sticks a moment later: for each shift k in
    0..LAG_MAX_S, the correlation of sig(t - k) with stick(t), summed over yaw and pitch (by magnitude: the sign of a
    stick is the hull's business). The peak is refined between samples with a parabola. None if nothing fits."""
    n, K = len(t_ok), jsround(LAG_MAX_S * hz)

    def corr(sig, stick, k):
        xs, ys = [], []
        for i in range(k, n):
            if t_ok[i] and t_ok[i - k] and sig[i - k] is not None:
                xs.append(sig[i - k])
                ys.append(stick[i])
        if len(xs) < 20:
            return 0, len(xs)
        mx, my = sum(xs) / len(xs), sum(ys) / len(ys)
        sxy = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
        sxx = sum((a - mx) ** 2 for a in xs)
        syy = sum((b - my) ** 2 for b in ys)
        return (sxy / math.sqrt(sxx * syy) if sxx > 1e-9 and syy > 1e-9 else 0), len(xs)

    score, pairs = [], 0
    for k in range(K + 1):
        rx, nx = corr(sig_x, stick_x, k)
        ry, ny = corr(sig_y, stick_y, k)
        score.append((abs(rx) + abs(ry)) / 2)
        pairs = max(pairs, nx, ny)
    best = max(range(len(score)), key=lambda k: score[k])
    if score[best] < LAG_MIN_R:
        return None
    shift = float(best)
    if 0 < best < K:
        a, b, c = score[best - 1], score[best], score[best + 1]
        den = a - 2 * b + c
        if abs(den) > 1e-9:
            shift += clamp(0.5 * (a - c) / den, -0.5, 0.5)
    return {"lag": r2(shift / hz), "r": r2(score[best]), "pairs": pairs}


def measure_lag(N, F, V):
    """V4: how late this pilot answers what they see. On a flight with a camera, the target ring's SCREEN position
    against the yaw and pitch sticks; without one, its bearing and elevation in the hull's frame (the same idea, minus
    the camera's own lag). Samples the AI flew are left out."""
    n = N["n"]
    if V is not None:
        ok = [V[i] is not None and V[i]["ring"]["sx"] is not None and not F[i]["held"] for i in range(n)]
        sx = [V[i]["ring"]["sx"] if ok[i] else None for i in range(n)]
        sy = [V[i]["ring"]["sy"] if ok[i] else None for i in range(n)]
        src = "screen"
    else:
        ok = [not F[i]["held"] for i in range(n)]
        sx = [math.sin(F[i]["bearing"] / DEG) for i in range(n)]
        sy = [math.sin(F[i]["elevation"] / DEG) for i in range(n)]
        src = "hull"
    m = lag_from_signals(ok, sx, N["yaw"], sy, N["pitch"], N["hz"])
    if m:
        m["source"] = src
    return m


# ---------------------------------------------------------------- events, intent, labels

def events(N, C, F):
    G, ng, n = C["gates"], len(C["gates"]), N["n"]
    ev = {"ring": [], "miss": [], "ram": [], "skim": [], "boostPress": []}
    skim_hull = N["skimReach"] > 0
    for i in range(1, n):
        if N["rings"][i] > N["rings"][i - 1]:
            ev["ring"].append(i)
        else:
            g = G[N["rings"][i - 1] % ng]
            s0, s1 = dot(sub(N["p"][i - 1], g["p"]), g["a"]), dot(sub(N["p"][i], g["p"]), g["a"])
            if s0 * s1 < 0:
                t = s0 / (s0 - s1)
                off = sub(add(N["p"][i - 1], mul(sub(N["p"][i], N["p"][i - 1]), t)), g["p"])
                lat = vlen(sub(off, mul(g["a"], dot(off, g["a"]))))
                if g["r"] < lat < g["r"] * 3:
                    ev["miss"].append(i)
        # A rail hit: the page counts rams; Unity's flight/3 sets the Slowed bit on the hit (one per slowed episode);
        # older Unity flights only have speed, where a drop below 65% in one sample is the best guess.
        if N["rams"]:
            hit = N["rams"][i] > N["rams"][i - 1]
        elif N["slowed"]:
            hit = N["slowed"][i] and not N["slowed"][i - 1]
        else:
            hit = N["spd"][i] < N["spd"][i - 1] * 0.65
        if hit:
            ev["ram"].append(i)
        if skim_hull and ((N["skims"][i] > N["skims"][i - 1]) if N["skims"] else (N["boost"][i] > N["boost"][i - 1] + 0.04)):
            ev["skim"].append(i)
        on = N["lt"][i] > 0.5 or (not skim_hull and N["boost"][i] > 1.05)
        was = N["lt"][i - 1] > 0.5 or (not skim_hull and N["boost"][i - 1] > 1.05)
        if on and not was:
            ev["boostPress"].append(i)
    return ev


def candidates(N, C, F, i):
    x, G = F[i], C["gates"]
    ng = len(G)
    g = G[x["ring"]]
    in_dir = mul(g["a"], -x["side"])
    ring_key = f"ring:{x['ring']}"
    lst = [
        {"kind": "mouth", "at": g["p"], "key": ring_key},
        {"kind": "approach", "at": add(g["p"], mul(g["a"], x["side"] * x["approachLead"])), "key": ring_key},
        {"kind": "through", "at": add(g["p"], mul(in_dir, THROUGH)), "key": ring_key},
        {"kind": "next ring", "at": G[(x["ring"] + 1) % ng]["p"], "key": f"ring:{(x['ring'] + 1) % ng}"},
    ]
    if x["skimPoint"] is not None and x["skimDetour"] < 40:
        lst.append({"kind": "skim", "at": x["skimPoint"], "key": x["skimKey"]})
    rv = N["rv"][i] if N["rv"] and i < len(N["rv"]) else None
    if rv:
        best, bd, bj = None, 150, -1
        for j in range(0, len(rv) - 3, 4):
            q = [rv[j], rv[j + 1], rv[j + 2]]
            d = dist(q, N["p"][i])
            if d < bd:
                bd, best, bj = d, q, j // 4
        if best:
            lst.append({"kind": "rival", "at": best, "key": f"rival:{bj}"})
    return lst


def intent(N, C, F, i, V=None):
    K = max(2, jsround(0.8 * N["hz"]))
    cands = candidates(N, C, F, i)
    if V is not None:
        # V3: a target counts only if the pilot could see it now or saw it within MEMORY_S (else flying blind: all count).
        seen = [c for c in cands if was_seen(N, V, i, c["key"], MEMORY_S, C)]
        cands = seen or cands
    scores = []
    for c in cands:
        s = m = 0
        k = 1
        while k <= K and i + k < N["n"]:
            s += angle(N["f"][i + k], sub(c["at"], N["p"][i + k]))
            m += 1
            k += 1
        scores.append(s / m if m else 180)
    order = sorted(range(len(cands)), key=lambda c: scores[c])
    best = order[0]
    second = scores[order[1]] if len(order) > 1 else 180
    return {"target": cands[best]["kind"], "at": cands[best]["at"], "err": scores[best], "conf": clamp((second - scores[best]) / max(1, second), 0, 1)}


def labels(N, C, F, ev, V=None):
    n, hz = N["n"], N["hz"]

    def near(lst, i, before, after):
        return any(-after * hz <= e - i <= before * hz for e in lst)

    out, recovering, last_ring = [], False, -1
    miss, ring_set = set(ev["miss"]), set(ev["ring"])
    for i in range(n):
        x = F[i]
        it = intent(N, C, F, i, V)
        if i in miss:
            recovering = True
        if i in ring_set:
            recovering, last_ring = False, i
        if recovering and x["headingErr"] < 25:
            recovering = False
        boosting = N["lt"][i] > 0.5 or (N["skimReach"] == 0 and N["boost"][i] > 1.05)
        if recovering:
            ph = "recovery"
        elif near(ev["ring"], i, 1.0, 0):
            ph = "thread"
        elif last_ring >= 0 and i - last_ring <= 0.8 * hz:
            ph = "exit"
        elif near(ev["skim"], i, 0.5, 0.5) or it["target"] == "skim":
            ph = "skim run"
        elif x["timeToMouth"] < 4 and x["turnRate"] > 25:
            ph = "line-up"
        elif boosting and x["turnRate"] < 12:
            ph = "boost straight"
        elif x["headingErr"] < 30 or it["target"] in ("approach", "mouth"):
            ph = "approach"
        else:
            ph = "cruise"
        out.append({"phase": ph, "target": it["target"], "targetAt": it["at"], "targetErr": it["err"], "conf": it["conf"], "corrected": False})
    for o in N["overrides"]:
        for i in range(n):
            if o["t0"] <= N["t"][i] <= o["t1"]:
                if o.get("phase"):
                    out[i]["phase"] = o["phase"]
                if o.get("target"):
                    out[i]["target"] = o["target"]
                out[i]["corrected"] = True
    return out


def segments(N, F, L, ev):
    out, s, n = [], 0, N["n"]
    for i in range(1, n + 1):
        if i < n and L[i]["phase"] == L[s]["phase"]:
            continue
        e = i - 1
        threaded = any(s <= r <= min(n - 1, e + 1) for r in ev["ring"])
        missed = any(s <= m <= e for m in ev["miss"])
        out.append({"phase": L[s]["phase"], "i0": s, "i1": e, "t0": r2(N["t"][s]), "t1": r2(N["t"][e]), "dur": r2(N["t"][e] - N["t"][s] + 1 / N["hz"]),
                    "speedIn": jsround(N["spd"][s]), "speedOut": jsround(N["spd"][e]), "threaded": threaded, "missed": missed})
        s = i
    return out


def legs(N, C, F, L, ev, lag_k=0):
    out, start = [], 0
    for j in ev["ring"]:
        c = j - 1
        while c > start and L[c]["target"] != "approach":
            c -= 1
        # V4: the commit was decided a reaction lag before the target switched, so read the distance there.
        commit = jsround(F[max(start, c + 1 - lag_k)]["dist"]) if start < c < j - 1 else None
        missed = any(start < m <= j for m in ev["miss"])
        rams = len([m for m in ev["ram"] if start < m <= j])
        out.append({"ring": N["rings"][j - 1] % len(C["gates"]), "i0": start, "i1": j, "t0": r2(N["t"][start]), "time": r2(N["t"][j] - N["t"][start]),
                    "speedIn": jsround(N["spd"][start]), "speedOut": jsround(N["spd"][j]), "legLen": jsround(F[j - 1]["legLen"]),
                    "legTurn": jsround(F[j - 1]["legTurn"]), "commitDist": commit,
                    "clean": not missed and rams == 0 and not any(x["held"] for x in F[start:j]), "missed": missed, "rams": rams})
        start = j
    return out


def skim_choices(N, F, ev, V=None, lag_k=0, C=None):
    if not N["skimReach"] > 0:
        return []
    out, hz, prev = [], N["hz"], -1e9
    for i in ev["skim"]:
        gap_t = N["t"][i] - prev
        prev = N["t"][i]
        if gap_t < 1.5:
            continue
        b = max(0, i - jsround(0.5 * hz))
        x = F[max(0, b - lag_k)]
        c = {"i": b, "t": r2(N["t"][b]), "taken": True, "detour": r2(min(x["skimDetour"], 99)), "timeToMouth": r2(x["timeToMouth"]),
             "headingErr": r2(x["headingErr"]), "speed": jsround(x["speed"])}
        if V is not None:
            c["seen"] = True   # taken: the pilot went for it
        out.append(c)
    last = -1e9
    for i in range(N["n"]):
        x = F[i]
        if x["held"] or x["skimDetour"] > 12 or x["skimTime"] > 2 or N["t"][i] - last < 1.5:
            continue
        if any(i < e <= i + 2 * hz for e in ev["skim"]):
            continue
        last = N["t"][i]
        j = F[max(0, i - lag_k)]
        c = {"i": i, "t": r2(N["t"][i]), "taken": False, "detour": r2(j["skimDetour"]), "timeToMouth": r2(j["timeToMouth"]),
             "headingErr": r2(j["headingErr"]), "speed": jsround(j["speed"])}
        if V is not None:
            # V3: a skim the pilot never saw is not a skim they chose to skip. It must have been on screen and not behind
            # a rail for SKIM_SEEN_S before the choice; otherwise it is "unseen" and the skim rule leaves it out.
            c["seen"] = seen_throughout(N, V, i, x["skimKey"], SKIM_SEEN_S, C)
        out.append(c)
    return sorted(out, key=lambda c: c["t"])


def analyze(flight, course, lag=None):
    """lag: the pilot's measured reaction lag in seconds (V4). When given, every decision is read off the situation that
    long before the action (the commit, the skim choice, the boost); None reads them at the action, as before."""
    N = normalize(flight) if "columns" in flight else flight
    C = course
    F = features(N, C)
    V = view(N, C, F)
    ev = events(N, C, F)
    L = labels(N, C, F, ev, V)
    lag_k = jsround(lag * N["hz"]) if lag else 0
    segs, lg, sk = segments(N, F, L, ev), legs(N, C, F, L, ev, lag_k), skim_choices(N, F, ev, V, lag_k, C)
    boosts = []
    for i in ev["boostPress"]:
        x = F[max(0, i - lag_k)]
        boosts.append({"i": i, "t": r2(N["t"][i]), "headingErr": r2(x["headingErr"]), "dist": jsround(x["dist"]), "timeToMouth": r2(x["timeToMouth"])})
    misses = []
    for i in ev["miss"]:
        m = {"i": i, "t": r2(N["t"][i]), "kind": "missed mouth"}
        if V is not None:
            # V3: the ring in view and missed is a CONTROL error; missed off screen is a NAVIGATION error.
            m["cause"] = "control" if was_seen(N, V, i, f"ring:{N['rings'][i - 1] % len(C['gates'])}", MEMORY_S) else "navigation"
        misses.append(m)
    mistakes = sorted(misses + [{"i": i, "t": r2(N["t"][i]), "kind": "rail hit"} for i in ev["ram"]], key=lambda m: m["t"])
    share = {p: 0 for p in PHASES}
    for l in L:
        share[l["phase"]] = share.get(l["phase"], 0) + 1
    share = {p: r2(v / max(1, N["n"])) for p, v in share.items()}
    return {"version": VERSION, "norm": N, "feats": F, "labels": L, "events": ev, "segments": segs, "legs": lg, "skims": sk, "boosts": boosts, "mistakes": mistakes,
            "viewFeats": V, "view": view_summary(N, V, sk, misses), "lag": measure_lag(N, F, V), "lagApplied": lag or 0,
            "summary": {"samples": N["n"], "seconds": r2(N["t"][-1] - N["t"][0]) if N["n"] else 0, "rings": len(ev["ring"]), "missed": len(ev["miss"]), "rams": len(ev["ram"]),
                        "skimsTaken": len([s for s in sk if s["taken"]]), "skimsSkipped": len([s for s in sk if not s["taken"]]), "boostPresses": len(boosts),
                        "meanTurnRate": r2(sum(x["turnRate"] for x in F) / max(1, N["n"])), "phaseShare": share}}


# ---------------------------------------------------------------- learning (step 1 curves, step 2 rules)

def learn(analyses, good_leg=None):
    pts, sk, boost_err = [], [], []
    for A in analyses:
        for g in A["legs"]:
            if g["clean"] and (good_leg is None or good_leg(g, A)) and g["legLen"] > 0 and g["commitDist"] is not None and g["commitDist"] > 0:
                pts.append([g["legLen"], g["commitDist"]])
        sk.extend(s for s in A["skims"] if s.get("seen", True))   # V3: an unseen skim was never a choice
        boost_err.extend(b["headingErr"] for b in A["boosts"])
        boost_err.extend(x["headingErr"] for x in A["feats"] if x["lt"] > 0.5 and not x["held"])
    commit = None
    if len(pts) >= 3:
        mx = sum(q[0] for q in pts) / len(pts)
        my = sum(q[1] for q in pts) / len(pts)
        sxy = sum((q[0] - mx) * (q[1] - my) for q in pts)
        sxx = sum((q[0] - mx) ** 2 for q in pts)
        syy = sum((q[1] - my) ** 2 for q in pts)
        b = clamp(sxy / sxx, -0.5, 1) if sxx > 1e-6 else 0
        a = clamp(my - b * mx, 60, 800)
        fit = sxy * sxy / (sxx * syy) if sxx > 0 and syy > 0 else 0
        med = pct([q[1] for q in pts], 0.5)
        if fit < 0.2:
            a, b = clamp(med, 60, 800), 0
        commit = {"a": jsround(a), "b": r2(b), "n": len(pts), "r2": r2(fit), "median": jsround(med)}
    skim = None
    if len(sk) >= 4 and any(s["taken"] for s in sk) and any(not s["taken"] for s in sk):
        best = None
        for D in range(1, 16):
            T = 0.0
            while T <= 3.001:
                ok = sum(1 for s in sk if (s["detour"] <= D and s["timeToMouth"] >= T) == s["taken"])
                if best is None or ok > best["ok"]:
                    best = {"ok": ok, "D": D, "T": r2(T)}
                T += 0.25
        skim = {"maxDetour": best["D"], "minTimeToMouth": best["T"], "agreement": r2(best["ok"] / len(sk)), "n": len(sk), "taken": len([s for s in sk if s["taken"]])}
    boost_within = {"deg": jsround(pct(boost_err, 0.9)), "n": len(boost_err)} if len(boost_err) >= 5 else None
    return {"commit": commit, "skim": skim, "boostWithin": boost_within}


# ---------------------------------------------------------------- the comparable part of an analysis (the golden test)

def comparable(A):
    F, L = A["feats"], A["labels"]
    keys = ["dist", "bearing", "elevation", "headingErr", "axisAngle", "mouthOffset", "timeToMouth", "legLen", "legTurn", "rMin", "turnRate", "gap", "aheadPrisms", "skimDetour"]
    return {
        "feats": [[r2(min(x[k], 1e5)) if not isinstance(x[k], bool) else int(x[k]) for k in keys] for x in F],
        "threads": [int(x["threadsIfStraight"]) for x in F],
        "labels": [[l["phase"], l["target"]] for l in L],
        "segments": A["segments"], "legs": A["legs"], "skims": A["skims"], "boosts": A["boosts"], "mistakes": A["mistakes"], "summary": A["summary"],
    }


def diff(py, js, path="", out=None, tol=0.02):   # py: this run, js: the golden
    out = [] if out is None else out
    if len(out) > 12:
        return out
    if isinstance(py, dict) and isinstance(js, dict):
        for k in sorted(set(py) | set(js)):
            if k not in py or k not in js:
                out.append(f"{path}.{k}: only in {'this run' if k in py else 'the golden'}")
            else:
                diff(py[k], js[k], f"{path}.{k}", out, tol)
    elif isinstance(py, list) and isinstance(js, list):
        if len(py) != len(js):
            out.append(f"{path}: length {len(py)} (this run) vs {len(js)} (golden)")
        for i, (a, b) in enumerate(zip(py, js)):
            diff(a, b, f"{path}[{i}]", out, tol)
    elif isinstance(py, (int, float)) and isinstance(js, (int, float)) and not isinstance(py, bool):
        if abs(py - js) > tol * max(1, abs(js)):
            out.append(f"{path}: {py} (this run) vs {js} (golden)")
    elif py != js:
        out.append(f"{path}: {py!r} (this run) vs {js!r} (golden)")
    return out


def self_test():
    if not os.path.exists(FIXTURE) or not os.path.exists(GOLDEN):
        print(f"self-test: fixture or golden missing ({FIXTURE}, {GOLDEN})")
        return 1
    fl = json.load(open(FIXTURE, encoding="utf-8"))
    C = course_from(load_course_js(), int(fl.get("intensity") or 1))
    got = json.loads(json.dumps(comparable(analyze(fl, C))))
    want = json.load(open(GOLDEN, encoding="utf-8"))
    problems = diff(got, want)
    # negative control: a changed label must be caught
    neg = json.loads(json.dumps(got))
    mid = len(neg["labels"]) // 2
    neg["labels"][mid][0] = "cruise" if neg["labels"][mid][0] != "cruise" else "approach"
    caught = bool(diff(neg, want))
    ok = not problems and caught and got["summary"]["rings"] > 0
    print(f"self-test: {'OK' if ok else 'FAILED'}  ({got['summary']['samples']} samples, {got['summary']['rings']} rings, "
          f"{got['summary']['skimsTaken']} skims taken, {got['summary']['skimsSkipped']} skipped; "
          f"{'matches' if not problems else 'DIFFERS FROM'} the golden output; negative control {'caught' if caught else 'NOT caught'})")
    for p in problems:
        print("   ", p)
    view_ok = view_self_test(fl, C)
    return 0 if ok and view_ok else 1


def _with_camera(fl, back=False, fov=60.0):
    """The fixture with a chase camera added (30 u behind, 8 u up, smoothed like the game's 0.2 s follow); back=True
    turns it round (it looks behind the hull), so nothing ahead is on screen."""
    cols = fl["columns"]
    ix = {c: i for i, c in enumerate(cols)}
    rows, cam = [], None
    for r in fl["samples"]:
        p = [r[ix["x"]], r[ix["y"]], r[ix["z"]]]
        f, u = norm([r[ix["fx"]], r[ix["fy"]], r[ix["fz"]]]), norm([r[ix["ux"]], r[ix["uy"]], r[ix["uz"]]])
        # Look-behind mirrors the follow offset: the camera sits ahead of the hull and looks back at it.
        want = add(add(p, mul(f, 30 if back else -30)), mul(u, 8))
        cam = want if cam is None else add(cam, mul(sub(want, cam), 0.5))
        cf = norm(sub(p, cam)) if back else norm(sub(add(p, mul(f, 40)), cam))
        rows.append(list(r) + cam + cf + [fov, 0] + u + [16 / 9, 1 if back else 0])
    out = dict(fl)
    out["columns"] = cols + ["camx", "camy", "camz", "camfx", "camfy", "camfz", "fov", "camMode", "camux", "camuy", "camuz", "camAspect", "camRear"]
    out["samples"] = rows
    return out


def view_self_test(fl, C):
    """V2-V4 (SITUATION_RECORDING_PLAN.md §7), each with a negative control."""
    fails = []

    def check(cond, what):
        if not cond:
            fails.append(what)

    # V2 projection and occlusion on a one-ring, one-prism course; the camera at the origin looking +z, up +y.
    tiny = {"gates": [{"p": [0, 0, 100], "a": [0, 0, 1], "r": 20}], "lanes": [[[0, 0, 50]]], "laneDomains": ["jade"]}
    fl0 = {"columns": ["t", "x", "y", "z", "fx", "fy", "fz", "ux", "uy", "uz", "speed", "camx", "camy", "camz", "camfx", "camfy", "camfz", "fov", "camux", "camuy", "camuz", "camAspect"],
           "samples": [[k / 10, 0, 0, 10 * k, 0, 0, 1, 0, 1, 0, 100, 0, 0, 0, 0, 0, 1, 60, 0, 1, 0, 16 / 9] for k in range(3)], "domain": "jade", "hull": "Squirrel"}
    N0 = normalize(fl0)
    vw = Viewer(N0, tiny)
    ahead, right, behind, wide = vw.look(0, [0, 0, 100], 1), vw.look(0, [50, 0, 100], 1), vw.look(0, [0, 0, -100], 1), vw.look(0, [200, 0, 100], 1)
    check(ahead["onScreen"] and abs(ahead["sx"]) < 0.01 and abs(ahead["sy"]) < 0.01, f"straight ahead is centre screen: {ahead}")
    check(right["onScreen"] and right["sx"] > 0.3, f"to the right is right of centre: {right}")
    check(not behind["onScreen"] and not wide["onScreen"], f"behind and far off-axis are off screen: {behind} {wide}")
    check(ahead["occluded"], "a rail prism on the line of sight hides what is behind it")
    check(not vw.look(0, [20, 0, 100], 1)["occluded"], "negative control: a prism off the line of sight hides nothing")
    check(not vw.look(0, [0, 0, 40], 1)["occluded"], "a prism behind the target hides nothing")
    check(ahead["tau"] is not None and abs(ahead["tau"] - 1.0) < 0.05, f"time to contact = 100 u at 100 u/s: {ahead['tau']}")

    # V2/V3 on the fixture: a forward camera sees the rings and the skims; the same flight with the camera looking
    # behind sees none of them, so every skipped skim becomes "unseen" and leaves the skim fit.
    fwd = analyze(_with_camera(fl), C)
    back = analyze(_with_camera(fl, back=True), C)
    blind = analyze(fl, C)
    vf, vb = fwd["view"], back["view"]
    check(vf["known"] and vf["ringOnScreen"] > 0.8, f"forward camera: the target ring is on screen most of the time ({vf})")
    check(vb["ringOnScreen"] < 0.25, f"camera looking behind: the ring is off screen ({vb})")
    skipped = blind["summary"]["skimsSkipped"]
    check(skipped > 0 and vf["skimsSkippedSeen"] == skipped, f"forward camera: every skipped skim was seen ({vf}, {skipped} skipped)")
    check(vb["skimsUnseen"] == len([s_ for s_ in back["skims"] if not s_["taken"]]) > 0, f"camera behind: every skipped skim is unseen ({vb})")
    n_fit = lambda A: len([s_ for s_ in A["skims"] if s_.get("seen", True)])
    check(n_fit(back) < n_fit(blind), "the skim fit leaves unseen skims out")
    check(n_fit(blind) == len(blind["skims"]), "negative control: a flight with no camera is all-seeing (nothing left out)")
    check(not blind["view"]["known"] and comparable(blind)["labels"] == comparable(analyze(fl, C))["labels"], "no camera: labels unchanged")
    g = fwd["viewFeats"][len(fwd["viewFeats"]) // 2]
    check(len(g["gridKind"]) == GRID_W * GRID_H and KIND_RING in g["gridKind"], f"the view grid shows the ring ({g['gridKind']})")

    # V4: a stick that answers the target 0.25 s late is measured at 0.25 s; a stick that ignores it is not measured.
    hz, n = 10.0, 600
    sig = [math.sin(0.9 * k / hz) + 0.6 * math.sin(2.3 * k / hz + 1) + 0.3 * math.sin(4.1 * k / hz + 2) for k in range(n)]
    late = lambda k, d: sig[k - d] if k >= d else 0
    stick = [0.8 * (0.5 * late(k, 2) + 0.5 * late(k, 3)) for k in range(n)]   # 0.25 s: halfway between samples 2 and 3
    m = lag_from_signals([True] * n, sig, stick, [0] * n, [0] * n, hz)
    check(m is not None and abs(m["lag"] - 0.25) <= 0.05, f"a 0.25 s lag is measured as {m}")
    noise = [math.sin(k * 12.9898) * 43758.5453 % 1 - 0.5 for k in range(n)]
    check(lag_from_signals([True] * n, sig, noise, [0] * n, [0] * n, hz) is None, "negative control: a stick that ignores the target has no lag")
    # The fixture was flown by the Medium AI, whose reaction delay is 0.25 s (SkimRaceDifficultySO.MediumReactionSeconds):
    # the measure must find it from the hull's frame (no camera) and from the screen (the chase camera).
    for A_, src in ((blind, "hull"), (fwd, "screen")):
        g_ = A_["lag"]
        check(g_ is not None and g_["source"] == src and 0.18 <= g_["lag"] <= 0.33, f"the Medium AI's 0.25 s reaction is measured from the {src}: {g_}")
    k3 = jsround(0.3 * hz)
    shifted = analyze(fl, C, lag=0.3)
    check(any(a_["commitDist"] != b_["commitDist"] for a_, b_ in zip(shifted["legs"], blind["legs"])) or not any(l_["commitDist"] for l_ in blind["legs"]),
          "a measured lag moves where the commit is read")
    check(all(b_["headingErr"] == r2(blind["feats"][max(0, b_["i"] - k3)]["headingErr"]) for b_ in shifted["boosts"]), "boosts are read a lag earlier")

    print(f"view self-test: {'OK' if not fails else 'FAILED'}  (V2 projection, occlusion, time to contact, view grid; V3 seen / unseen skims, "
          f"no-camera flights all-seeing; V4 lag {m['lag'] if m else None} s for a 0.25 s pilot, none for noise; forward camera: ring on screen "
          f"{round(vf['ringOnScreen'] * 100)}%, behind: {round(vb['ringOnScreen'] * 100)}%)")
    for f_ in fails:
        print("   ", f_)
    return not fails


# ---------------------------------------------------------------- report over recorded flights

def good_legs_filter(analyses):
    """A GOOD leg, judged from the pilot's own flights (no model): clean (no missed mouth, no rail hit, nobody else flying)
    and in the faster half of the pilot's own clean times for that ring of that intensity."""
    times = {}
    for A in analyses:
        for g in A["legs"]:
            if g["clean"]:
                times.setdefault((A["norm"]["intensity"], g["ring"]), []).append(g["time"])
    med = {k: pct(v, 0.5) for k, v in times.items()}
    return lambda g, A: g["time"] <= med.get((A["norm"]["intensity"], g["ring"]), 1e9)


def describe_view(A):
    v, g = A["view"], A["lag"]
    lag = f"reaction lag {g['lag']} s (fit {g['r']}, from the {g['source']})" if g else "reaction lag not measured"
    if not v["known"]:
        return f"view: no camera in this flight (read as all-seeing); {lag}"
    return (f"view: target ring on screen {round(v['ringOnScreen'] * 100)}% of the time, behind a rail {round(v['ringHidden'] * 100)}%; "
            f"skipped skims seen {v['skimsSkippedSeen']}, unseen {v['skimsUnseen']} (left out); misses: control {v['controlMisses']}, "
            f"navigation {v['navigationMisses']}; {lag}")


def hull_lag(analyses):
    lags = [A["lag"] for A in analyses if A["lag"]]
    if not lags:
        return None
    return {"lag": pct([g["lag"] for g in lags], 0.5), "flights": len(lags), "source": "screen" if any(g["source"] == "screen" for g in lags) else "hull"}


def write_learned(by_hull, n_flights):
    import datetime
    data = {"schema": "cosmic-shore.learned-situations/1", "source": "Unity editor flights (HumanFlightRecorder)", "date": datetime.date.today().isoformat(),
            "spec": "Docs/AI_SYSTEM/situation_features.json", "hulls": by_hull}
    with open(LEARNED_JSON, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)
        fh.write("\n")
    lines = ["# What the AI learned from editor flights", "",
             f"Written by `python3 Tools/Build/situation_features.py --write` on {data['date']} from {n_flights} Regatta flight(s) flown in the Unity editor",
             "(training data comes from the editor only, Yash 2026-10-16). Plan: `SITUATION_RECORDING_PLAN.md`. Nothing here changes the game until",
             "the two knobs are ported to Unity's steering and a person reviews the numbers (team task T-20261015-7fc3).", "",
             "| Hull | Flights | Good legs | Commit to the ring | When a skim is worth it | Boost within | Reaction lag | The AI today |", "|---|---|---|---|---|---|---|---|"]
    for hull, L in sorted(by_hull.items()):
        c, sk, b = L.get("commit"), L.get("skim"), L.get("boostWithin")
        lines.append(f"| {hull} | {L['flights']} | {L['goodLegs']} | "
                     + (f"{c['a']} u + {c['b']} x leg (median {c['median']} u, n {c['n']})" if c else "needs 3 good legs with an approach") + " | "
                     + (f"detour <= {sk['maxDetour']} u and >= {sk['minTimeToMouth']} s to the mouth ({round(sk['agreement'] * 100)}% of {sk['n']} choices)" if sk else "needs skims taken and skipped") + " | "
                     + (f"{b['deg']} deg (n {b['n']})" if b else "needs boost moments") + " | "
                     + (f"{L['reactionLag']['lag']} s ({L['reactionLag']['source']})" if L.get("reactionLag") else "not measured")
                     + " | commit 420 u on every leg; the Squirrel skims whenever it can |")
    with open(LEARNED_DOC, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    print(f"\nwrote {os.path.relpath(LEARNED_JSON, REPO)} and {os.path.relpath(LEARNED_DOC, REPO)}")


def report(sources, write=False):
    from learn_from_flights import parse  # the same reader (schema check) as the rail learner
    COURSE = load_course_js()
    analyses = []
    for name, text in sources:
        d, err = parse(name, text)
        if err:
            print("  skip", err)
            continue
        if d.get("mode") != "Regatta":
            continue
        C = course_from(COURSE, int(d.get("intensity") or 1))
        if not C:
            print(f"  skip {name}: intensity {d.get('intensity')} has no course")
            continue
        A = analyze(d, C)
        A["flight"], A["course"] = d, C
        analyses.append(A)
        m = A["summary"]
        print(f"{os.path.basename(name)}: {d.get('hull')} I{d.get('intensity')}  {m['seconds']} s, {m['rings']} rings, {m['missed']} missed, "
              f"{m['rams']} rail hits, skims {m['skimsTaken']} taken / {m['skimsSkipped']} skipped, {m['boostPresses']} boost presses")
        print("   time in each phase: " + ", ".join(f"{k} {round(v * 100)}%" for k, v in sorted(m["phaseShare"].items(), key=lambda kv: -kv[1]) if v > 0))
        print("   " + describe_view(A))
    if not analyses:
        print("No Regatta flight found. Fly a Regatta race by hand in Unity and press Store AI record, or pass --dir.")
        return 0
    by_hull = {}
    for hull in sorted({A["norm"]["hullKey"] for A in analyses}):
        mine = [A for A in analyses if A["norm"]["hullKey"] == hull]
        if len(mine) < MIN_FLIGHTS:
            print(f"\n{hull}: {len(mine)} editor flight(s); nothing is learned below {MIN_FLIGHTS} (one race says how you flew that race, not how you fly)")
            continue
        # V4: the hull's reaction lag is the median of its flights' measured lags; every decision is then re-read that
        # long before the action, and the rules are fitted on those re-read flights.
        lag = hull_lag(mine)
        if lag:
            mine = [dict(analyze(A["flight"], A["course"], lag=lag["lag"]), flight=A["flight"], course=A["course"]) for A in mine]
        good = good_legs_filter(mine)
        L = learn(mine, good)
        L["reactionLag"] = lag
        L["flights"] = len(mine)
        L["goodLegs"] = sum(1 for A in mine for g in A["legs"] if g["clean"] and good(g, A))
        by_hull[hull] = L
        print(f"\n{hull}: learned from {L['goodLegs']} good legs of {len(mine)} flight(s) (clean, and in the faster half of your own times for that ring):")
        print("  commit to the ring:", L["commit"] or "needs 3 good legs with an approach")
        print("  when a skim is worth it:", L["skim"] or "needs skims both taken and skipped")
        print("  boost within:", L["boostWithin"] or "needs boost moments")
        print("  reaction lag:", f"{lag['lag']} s (median of {lag['flights']} flight(s), from the {lag['source']}); proposed as a measured "
              "ReactionSeconds for the handicap owners (Garrett), changes no AI by itself" if lag else "not measured (no flight's sticks follow the ring)")
    if write and by_hull:
        write_learned(by_hull, len(analyses))
    elif write:
        print(f"\nnothing written: no hull has {MIN_FLIGHTS} editor flights yet")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dir")
    ap.add_argument("--json")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--write", action="store_true", help="write learned_situations.json and LEARNED_SITUATIONS.md")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.json:
        fl = json.load(open(a.json, encoding="utf-8"))
        A = analyze(fl, course_from(load_course_js(), int(fl.get("intensity") or 1)))
        A.pop("norm", None)
        json.dump(A, sys.stdout, default=lambda o: None)
        return 0
    from learn_from_flights import flights_in_dir, flights_on_branches
    return report(flights_in_dir(a.dir) if a.dir else flights_on_branches(), a.write)


if __name__ == "__main__":
    sys.exit(main())
