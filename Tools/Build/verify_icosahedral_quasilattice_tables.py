#!/usr/bin/env python3
"""Re-prove the SHIPPED QuasicrystalLatticeData.cs from a fresh derivation.

    python3 Tools/Build/verify_icosahedral_quasilattice_tables.py    # READ-ONLY

READ-ONLY. Writes nothing, authors nothing.

WHY THIS EXISTS SEPARATELY FROM THE MEASURING SCRIPT
----------------------------------------------------
Tools/Build/measure_icosahedral_quasilattice.py derives, measures and EMITS the
table; this script PARSES the shipped C# and re-proves every claim against an
independent derivation from first principles. The transcription from a proven
measurement to the asset is the step neither the measurement nor code review
can see (Docs/ECOSYSTEM.md 34.4) - the gyroid's z-mirror table corruption
shipped exactly there and cost five playtests. The two scripts share no
imports on purpose.

WHAT IS PROVEN
--------------
 1. The parsed projection bases equal the two 3D irreps of the icosahedral
    group, and rows of [Par; Perp]/sqrt(2) are 6D-orthonormal.
 2. The parsed window faces are the triacontahedron's (re-derived normals and
    the single shared support match), and the parsed Gamma leaves the ORIGIN a
    heart with every measured lattice point clear of the boundary.
 3. Strut frames: unit, mutually orthogonal, and cross(tangent, normal)
    reproduces the edge axis - so LookRotation puts +x along the edge.
 4. The frontier delta census re-measured on a fresh patch EQUALS the shipped
    set (as a set, and closed under negation), and the heart graph over it is
    connected.
 5. Every constant re-measures: heart spacing constant, HeartDistanceMax
    covers the measured max, MinAcceptanceMargin matches, MinPatchPrisms sits
    under the smallest complete plant.
 6. The growth simulation - the exact algorithm the C# runs (tree territory,
    canonical-edge layers, frontier reproduction) - completes a colony with
    ZERO holes.
 7. NEGATIVE CONTROLS ("a gate nobody has watched fail is not a gate",
    Docs/ECOSYSTEM.md 34.2): bare twelve-coordination admits ADJACENT hearts
    (the local-max rule is load-bearing); Euclidean-Voronoi territory leaves
    HOLES (the tree rule is load-bearing); tau alone is NOT an integer map on
    Z^6 (tau^3 is - the crystallography the doc record cites).
"""
from __future__ import annotations

import math
import pathlib
import time
import random
import re
import sys
from collections import deque

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
CS_PATH = ROOT / "Assets/_Scripts/Controller/Assemblers/QuasicrystalLatticeData.cs"

TAU = (1 + math.sqrt(5)) / 2
TOLERANCE = 5e-13   # emitted doubles carry 15 decimals; rounding is < 5e-16 per entry

# Sized so the whole verify stays a few minutes of pure Python: every property
# proven here is scale-free (the measuring script proves the same ones larger).
PATCH_VERTS = 22000
SIM_PLANTS = 150

FAILURES: list[str] = []


T0 = time.time()


def check(ok: bool, label: str, detail: str = "") -> bool:
    tag = "[ok  ]" if ok else "[FAIL]"
    print(f"{tag} [{time.time() - T0:5.0f}s] {label}" + (f" - {detail}" if detail else ""))
    if not ok:
        FAILURES.append(label)
    return ok


# ---------------------------------------------------------------------------
# Parse the shipped C#
# ---------------------------------------------------------------------------

def parse_cs():
    if not CS_PATH.exists():
        raise SystemExit(f"missing {CS_PATH}")
    text = CS_PATH.read_text()
    if "MEASURED TABLE BEGIN" not in text or "MEASURED TABLE END" not in text:
        raise SystemExit("markers missing")
    block = text[text.index("MEASURED TABLE BEGIN"): text.index("MEASURED TABLE END")]

    def const(name):
        m = re.search(rf"const (?:double|int) {name} = ([-\d.e]+);", block)
        if not m:
            raise SystemExit(f"missing const {name}")
        return float(m.group(1))

    def rows(name, width):
        m = re.search(rf"{name} =\s*\{{(.*?)\n        \}};", block, re.S)
        if not m:
            raise SystemExit(f"missing table {name}")
        out = []
        for line in m.group(1).splitlines():
            nums = re.findall(r"-?\d+\.\d+|-?\d+", line)
            if len(nums) == width and "{" in line:
                out.append([float(x) for x in nums])
        return np.array(out)

    def vec3_rows(name):
        m = re.search(rf"{name} =\s*\{{(.*?)\n        \}};", block, re.S)
        if not m:
            raise SystemExit(f"missing table {name}")
        out = re.findall(r"new Vector3\((-?[\d.]+)f, (-?[\d.]+)f, (-?[\d.]+)f\)", m.group(1))
        return np.array([[float(a), float(b), float(c)] for a, b, c in out])

    gamma_m = re.search(r"Gamma =\s*\{ ([-\d.]+), ([-\d.]+), ([-\d.]+) \};", block)
    if not gamma_m:
        raise SystemExit("missing Gamma")

    return {
        "support": const("WindowSupport"),
        "min_margin": const("MinAcceptanceMargin"),
        "d_max": int(const("HeartDistanceMax")),
        "min_patch": int(const("MinPatchPrisms")),
        "par": rows(r"double\[,\] Par", 3),
        "perp": rows(r"double\[,\] Perp", 3),
        "faces": rows(r"double\[,\] WindowFaceNormals", 3),
        "gamma": np.array([float(gamma_m.group(i)) for i in (1, 2, 3)]),
        "strut_normal": vec3_rows(r"Vector3\[\] StrutNormal"),
        "strut_tangent": vec3_rows(r"Vector3\[\] StrutTangent"),
        "deltas": rows(r"int\[,\] FrontierDeltas", 6).astype(int),
    }


# ---------------------------------------------------------------------------
# Independent derivation
# ---------------------------------------------------------------------------

def derive_bases():
    s5, c5 = 2 / math.sqrt(5), 1 / math.sqrt(5)
    par = [np.array([0.0, 0.0, 1.0])]
    perp = [np.array([0.0, 0.0, 1.0])]
    for k in range(5):
        th = 2 * math.pi * k / 5
        par.append(np.array([s5 * math.cos(th), s5 * math.sin(th), c5]))
        perp.append(np.array([s5 * math.cos(2 * th), s5 * math.sin(2 * th), -c5]))
    return np.array(par), np.array(perp)


def derive_faces(perp):
    faces = []
    for i in range(6):
        for j in range(i + 1, 6):
            n = np.cross(perp[i], perp[j])
            n /= np.linalg.norm(n)
            for c in n:
                if abs(c) > 1e-9:
                    if c < 0:
                        n = -n
                    break
            h = 0.5 * sum(abs(float(n @ perp[k])) for k in range(6))
            faces.append((n, h))
    return faces


class Lattice:
    """Window arithmetic over the SHIPPED constants - what the C# actually runs."""

    def __init__(self, t):
        self.par = t["par"]
        self.perp = t["perp"]
        self.face_n = t["faces"]
        self.h = float(t["support"])
        self.gamma = t["gamma"]
        # Hot-path copies as plain python floats: per-call numpy overhead on
        # 3-vectors is ~10x the arithmetic itself, and margin() runs millions
        # of times across the census and simulations.
        self._perp_rows = [tuple(float(x) for x in row) for row in t["perp"]]
        self._par_rows = [tuple(float(x) for x in row) for row in t["par"]]
        self._faces = [tuple(float(x) for x in row) for row in t["faces"]]
        self._g = tuple(float(x) for x in t["gamma"])
        self._margin = {}
        self._heart = {}
        self._dist = {}
        self._owner = {}
        self.d_max = t["d_max"]

    def margin(self, v):
        m = self._margin.get(v)
        if m is None:
            px, py, pz = -self._g[0], -self._g[1], -self._g[2]
            for c, row in zip(v, self._perp_rows):
                if c:
                    px += c * row[0]; py += c * row[1]; pz += c * row[2]
            h = self.h
            m = 1e30
            for fx, fy, fz in self._faces:
                d = px * fx + py * fy + pz * fz
                mm = h - (d if d >= 0 else -d)
                if mm < m: m = mm
            self._margin[v] = m
        return m

    def accepted(self, v):
        return self.margin(v) > 0.0

    def par_of(self, v):
        x = y = z = 0.0
        for c, row in zip(v, self._par_rows):
            if c:
                x += c * row[0]; y += c * row[1]; z += c * row[2]
        return np.array([x, y, z])

    @staticmethod
    def neighbours(v):
        for i in range(6):
            e = [0] * 6
            e[i] = 1
            yield tuple(a + b for a, b in zip(v, e))
            e[i] = -1
            yield tuple(a + b for a, b in zip(v, e))

    def deg12(self, v):
        return self.accepted(v) and all(self.accepted(u) for u in self.neighbours(v))

    def is_heart(self, v):
        h = self._heart.get(v)
        if h is None:
            h = self.deg12(v)
            if h:
                mv = self.margin(v)
                for u in self.neighbours(v):
                    if not self.deg12(u):
                        continue
                    mu = self.margin(u)
                    if mu > mv or (mu == mv and u < v):
                        h = False
                        break
            self._heart[v] = h
        return h

    def heart_dist(self, v):
        d = self._dist.get(v)
        if d is None:
            if self.is_heart(v):
                d = 0
            else:
                ring = {v}
                seen = {v}
                d = None
                for depth in range(1, self.d_max + 1):
                    nxt = set()
                    for w in ring:
                        for u in self.neighbours(w):
                            if u not in seen and self.accepted(u):
                                seen.add(u)
                                nxt.add(u)
                    if any(self.is_heart(u) for u in nxt):
                        d = depth
                        break
                    ring = nxt
                assert d is not None, f"no heart within {self.d_max} of {v}"
            self._dist[v] = d
        return d

    def owner(self, v):
        h = self._owner.get(v)
        if h is None:
            if self.is_heart(v):
                h = v
            else:
                dv = self.heart_dist(v)
                parent = min(u for u in self.neighbours(v)
                             if self.accepted(u) and self.heart_dist(u) == dv - 1)
                h = self.owner(parent)
            self._owner[v] = h
        return h

    def bfs_patch(self, start, max_verts):
        seen = {start}
        order = [start]
        q = deque([start])
        while q and len(order) < max_verts:
            v = q.popleft()
            for u in self.neighbours(v):
                if u not in seen and self.accepted(u):
                    seen.add(u)
                    order.append(u)
                    q.append(u)
        return order


E6 = [tuple(1 if k == i else 0 for k in range(6)) for i in range(6)]


def add6(u, v, s=1):
    return tuple(a + s * b for a, b in zip(u, v))


def edge_key(v, i, s):
    return (v, i) if s > 0 else (add6(v, E6[i], -1), i)


def run_sim(lat, deltas, n_plants, tree_ownership=True):
    """The colony algorithm the C# runs. tree_ownership=False swaps in the
    Euclidean-Voronoi owner - the NEGATIVE CONTROL that must leave holes."""
    random.seed(23)
    edge_owner = {}
    plants = {}

    voronoi_owner = {}
    hearts_np = None
    hearts_list = None

    def euclid_owner(v):
        h = voronoi_owner.get(v)
        if h is None:
            pv = lat.par_of(v)
            d2 = ((hearts_np - pv) ** 2).sum(1)
            h = hearts_list[int(d2.argmin())]
            voronoi_owner[v] = h
        return h

    def owner(v):
        return lat.owner(v) if tree_ownership else euclid_owner(v)

    def grow_one(heart):
        pl = plants[heart]
        seen = {heart}
        q = deque([heart])
        while q:
            v = q.popleft()
            for i in range(6):
                for s in (1, -1):
                    u = add6(v, E6[i], s)
                    if not lat.accepted(u):
                        continue
                    ek = edge_key(v, i, s)
                    if ek not in edge_owner and owner(ek[0]) == heart:
                        edge_owner[ek] = heart
                        pl["struts"] += 1
                        return True
                    if u not in seen and owner(u) == heart:
                        seen.add(u)
                        q.append(u)
        pl["done"] = True
        return False

    frontier, offered, contributed = [], set(), set()

    def contribute(heart):
        for d in deltas:
            u = add6(heart, tuple(d))
            if u in offered or u in plants:
                continue
            if lat.is_heart(u):
                offered.add(u)
                frontier.append(u)

    seed = (0,) * 6
    plants[seed] = {"struts": 0, "done": False}
    if not tree_ownership:
        # Euclidean control needs the heart set up front; harvest it lazily as
        # plants spawn is not enough (a vertex's nearest heart may be unborn).
        patch = lat.bfs_patch(seed, 14000)
        hearts_list = [v for v in patch if lat.is_heart(v)]
        hearts_np = np.array([lat.par_of(h) for h in hearts_list])

    for cycle in range(5000):
        for h in [h for h, p in plants.items() if not p["done"]]:
            grow_one(h)
        for h in [h for h, p in plants.items() if p["done"] and h not in contributed]:
            contributed.add(h)
            contribute(h)
        # Stop SPAWNING at the target, then run the survivors to completion -
        # without the gate a birth lands every few cycles, "all done" is never
        # true, and the loop always burns its whole cycle budget.
        if cycle % 4 == 0 and frontier and len(plants) < n_plants:
            k = random.randrange(len(frontier))
            h = frontier[k]
            frontier[k] = frontier[-1]
            frontier.pop()
            offered.discard(h)
            if h not in plants and lat.is_heart(h):
                plants[h] = {"struts": 0, "done": False}
        if len(plants) >= n_plants and all(p["done"] for p in plants.values()):
            break

    done = {h: p for h, p in plants.items() if p["done"]}
    complete = set(done.keys())
    holes = slots = 0
    checked = set()
    for (v, i) in list(edge_owner.keys()):
        for vv in (v, add6(v, E6[i], 1)):
            if vv in checked:
                continue
            checked.add(vv)
            if owner(vv) not in complete:
                continue
            for ii in range(6):
                for ss in (1, -1):
                    uu = add6(vv, E6[ii], ss)
                    if not lat.accepted(uu):
                        continue
                    ek = edge_key(vv, ii, ss)
                    if owner(ek[0]) in complete:
                        slots += 1
                        if ek not in edge_owner:
                            holes += 1
    sizes = [p["struts"] for p in done.values()]
    return len(done), sizes, holes, slots


# ---------------------------------------------------------------------------

def main() -> int:
    print("=" * 78)
    print("Verifying shipped QuasicrystalLatticeData.cs against a fresh derivation")
    print("=" * 78)

    t = parse_cs()

    # 1. bases
    par, perp = derive_bases()
    check(float(np.abs(t["par"] - par).max()) < TOLERANCE, "shipped Par is the icosahedral irrep",
          f"max dev {float(np.abs(t['par'] - par).max()):.2e}")
    check(float(np.abs(t["perp"] - perp).max()) < TOLERANCE, "shipped Perp is the conjugate irrep",
          f"max dev {float(np.abs(t['perp'] - perp).max()):.2e}")
    g = t["par"] @ t["par"].T + t["perp"] @ t["perp"].T
    check(float(np.abs(g - 2 * np.eye(6)).max()) < 1e-12,
          "rows of [Par; Perp]/sqrt(2) are 6D-orthonormal")

    # 2. window
    faces = derive_faces(perp)
    check(len(t["faces"]) == 15, "shipped window has 15 faces", f"{len(t['faces'])}")
    ref_n = np.array([f[0] for f in faces])
    check(float(np.abs(t["faces"] - ref_n).max()) < TOLERANCE,
          "shipped face normals match the derived triacontahedron")
    hs = [f[1] for f in faces]
    check(abs(t["support"] - hs[0]) < TOLERANCE and max(hs) - min(hs) < 1e-12,
          "shipped support equals the derived (single, shared) support",
          f"{t['support']:.12f} vs {hs[0]:.12f}")

    lat = Lattice(t)
    seed = (0,) * 6
    check(lat.accepted(seed), "origin accepted under shipped Gamma", f"margin {lat.margin(seed):.4f}")
    check(lat.is_heart(seed), "origin is a heart under shipped Gamma")

    # 3. strut frames
    for i in range(6):
        n = t["strut_normal"][i]
        tg = t["strut_tangent"][i]
        ok = (abs(np.linalg.norm(n) - 1) < 1e-6 and abs(np.linalg.norm(tg) - 1) < 1e-6 and
              abs(float(n @ tg)) < 1e-6 and
              float(np.abs(np.cross(tg, n) - par[i]).max()) < 1e-6)
        if not ok:
            check(False, f"strut frame {i} is orthonormal with cross(t,n) = axis")
            break
    else:
        check(True, "all 6 strut frames orthonormal with cross(tangent, normal) = edge axis",
              "LookRotation(normal, tangent) puts +x along the edge")

    # 4. patch census + margins
    patch = lat.bfs_patch(seed, PATCH_VERTS)
    ms = [lat.margin(v) for v in patch]
    check(min(ms) >= t["min_margin"] - 1e-12,
          "no measured margin falls below the shipped MinAcceptanceMargin",
          f"min {min(ms):.3e} vs shipped {t['min_margin']:.3e}")

    hearts = [v for v in patch if lat.is_heart(v)]
    hp = np.array([lat.par_of(h) for h in hearts])
    vp = np.array([lat.par_of(v) for v in patch])
    rmax = float(np.linalg.norm(vp, axis=1).max())
    interior = [k for k in range(len(hearts)) if np.linalg.norm(hp[k]) < 0.6 * rmax]

    nn = []
    shell = set()
    for k in interior:
        d = np.linalg.norm(hp - hp[k], axis=1)
        d[k] = 1e9
        srt = np.argsort(d)[:16]
        nn.append(float(d[srt[0]]))
        for m_ in srt:
            if d[m_] < 3.4:
                shell.add(tuple(int(a - b) for a, b in zip(hearts[m_], hearts[k])))
    nn = np.array(nn)
    check(float(nn.max() - nn.min()) < 1e-6, "nearest-heart spacing re-measures CONSTANT",
          f"{nn.mean():.4f} edges across {len(interior)} interior hearts")

    shipped_deltas = {tuple(int(c) for c in row) for row in t["deltas"]}
    check(all(tuple(-c for c in d) in shipped_deltas for d in shipped_deltas),
          "shipped frontier deltas closed under negation")
    check(shell == shipped_deltas, "re-measured shell census EQUALS the shipped FrontierDeltas",
          f"fresh {len(shell)} vs shipped {len(shipped_deltas)} "
          f"(missing {len(shell - shipped_deltas)}, extra {len(shipped_deltas - shell)})")

    hset = set(hearts)
    seen = {hearts[0]}
    q = deque([hearts[0]])
    while q:
        h = q.popleft()
        for d in shipped_deltas:
            u = add6(h, d)
            if u in hset and u not in seen:
                seen.add(u)
                q.append(u)
    inner = [h for k, h in enumerate(hearts) if np.linalg.norm(hp[k]) < 0.5 * rmax]
    unreached = sum(1 for h in inner if h not in seen)
    check(unreached == 0, "heart graph over shipped deltas is connected",
          f"{unreached}/{len(inner)} interior unreached")

    d_seen = 0
    for v in patch:
        if np.linalg.norm(lat.par_of(v)) > 0.6 * rmax:
            continue
        d_seen = max(d_seen, lat.heart_dist(v))
    check(d_seen <= t["d_max"], "shipped HeartDistanceMax covers the measured maximum",
          f"measured {d_seen} <= shipped {t['d_max']}")

    # 5-6. the growth simulation, exactly as shipped
    plants_done, sizes, holes, slots = run_sim(lat, t["deltas"], SIM_PLANTS, tree_ownership=True)
    check(plants_done >= SIM_PLANTS * 0.9, "growth simulation completes a colony",
          f"{plants_done} plants, sizes {min(sizes)}..{max(sizes)}")
    check(holes == 0, "tree territory leaves ZERO holes", f"{holes} of {slots} slots")
    check(min(sizes) > t["min_patch"], "shipped MinPatchPrisms sits under the smallest plant",
          f"{t['min_patch']} < {min(sizes)}")

    # 7. negative controls
    bare_adjacent = 0
    for v in patch[:15000]:
        if not lat.deg12(v):
            continue
        if any(lat.deg12(u) for u in lat.neighbours(v)):
            bare_adjacent += 1
            break
    check(bare_adjacent > 0,
          "NEGATIVE CONTROL: bare deg-12 admits adjacent hearts (local-max rule load-bearing)")

    ruled = sum(1 for h in hearts for u in lat.neighbours(h) if lat.is_heart(u))
    check(ruled == 0, "no two shipped-rule hearts are adjacent")

    _, _, holes_v, slots_v = run_sim(lat, t["deltas"], 200, tree_ownership=False)
    check(holes_v > 0,
          "NEGATIVE CONTROL: Euclidean-Voronoi territory leaves holes (tree rule load-bearing)",
          f"{holes_v} of {slots_v} slots unlaid")

    m6 = np.zeros((6, 6))
    m6[:3, :] = par.T
    m6[3:, :] = perp.T
    x1 = np.linalg.solve(m6, np.concatenate([TAU * par[0], (-1 / TAU) * perp[0]]))
    x3 = np.linalg.solve(m6, np.concatenate([TAU ** 3 * par[0], (-1 / TAU) ** 3 * perp[0]]))
    check(float(np.abs(x1 - np.rint(x1)).max()) > 0.4,
          "NEGATIVE CONTROL: tau alone is not an integer map on Z^6")
    check(float(np.abs(x3 - np.rint(x3)).max()) < 1e-9, "tau^3 is an integer map on Z^6")

    print("-" * 78)
    if FAILURES:
        print(f"FAILED {len(FAILURES)} check(s):")
        for label in FAILURES:
            print(f"  - {label}")
        return 1
    print("all checks passed - the shipped table is the quasilattice")
    return 0


if __name__ == "__main__":
    sys.exit(main())
