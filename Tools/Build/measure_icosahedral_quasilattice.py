#!/usr/bin/env python3
"""Measure the icosahedral quasilattice tables for QuasicrystalLatticeData.cs.

    python3 Tools/Build/measure_icosahedral_quasilattice.py            # verify + print the emit
    python3 Tools/Build/measure_icosahedral_quasilattice.py --check    # verify only (CI gate)
    python3 Tools/Build/measure_icosahedral_quasilattice.py --write    # rewrite the C# table

WHAT THE LATTICE IS
-------------------
The quasicrystal flora grows the vertex-and-edge graph of the icosahedral
Ammann-Kramer-Neri tiling - the three-dimensional analogue of the Penrose
tiling: perfect long-range order with icosahedral ("forbidden" five-fold)
symmetry that never repeats. It is built by CUT-AND-PROJECT from the
six-dimensional hypercubic lattice Z^6:

  * The 6D basis vectors project to PHYSICAL space as the six vertex axes of
    the icosahedron (PAR below) and to the internal/PERP space as the same
    construction with the pentagon angle doubled and z flipped (PERP below) -
    the two 3D irreducible representations of the icosahedral group. Rows of
    [PAR; PERP]/sqrt(2) are an orthonormal 6D basis (checked here to 1e-12).
  * A lattice point n in Z^6 is a VERTEX of the tiling iff its perp image
    PERP(n) - GAMMA lies inside the acceptance WINDOW: the rhombic
    triacontahedron that is the perp projection of the unit 6-cube (a zonotope
    of the six PERP axes - 15 face-normal pairs, all with the SAME support,
    which this script asserts rather than assumes).
  * An EDGE joins n and n +- e_i whenever both endpoints are accepted. Every
    edge therefore projects to one of six directions and has EXACTLY the same
    physical length (asserted) - one strut prism serves the whole species.

So a prism's address is six integers plus a direction: the aperiodic pattern
is the shadow of a periodic 6D one, and "sameness is an integer address"
(Docs/ECOSYSTEM.md 34) holds even though the structure never repeats. Unlike
Schwarz P there are no mirror tile transforms at all - one global frame per
colony, no reflection to compose a bond delta through - and bond deltas DO
add, because upstairs in Z^6 the lattice is honestly Euclidean.

THE HEART RULE
--------------
A plant is one HEART's territory. A heart is a vertex that (a) is
12-coordinated - all twelve neighbours accepted, a full icosahedral star -
and (b) is a LOCAL MAXIMUM of window margin among its 12-coordinated
neighbours (lexicographic address tie-break). (a) alone admits ADJACENT
hearts (the 12-coordination region of the window is wider than one lattice
step - measured, and asserted as this script's negative control), which
means crystal pairs one edge apart and two-vertex runt plants; (b) is the
exact, closed-form gate that removes them. Measured consequence: every
heart's nearest heart lands at EXACTLY the same distance (2.3840 edges).

TERRITORY IS A TREE, NOT A RADIUS
---------------------------------
A vertex belongs to the heart at the end of its lex-parent chain:
  dist(v)   = graph distance to the nearest heart (BFS over accepted vertices)
  parent(v) = lexicographically least neighbour u with dist(u) = dist(v) - 1
  owner(v)  = v if heart, else owner(parent(v))
parent() is a pure function of the vertex alone, so chains are suffix-closed
and every cell is a TREE rooted at its heart - connected by construction.
An edge is laid by the plant owning its canonical (minus-end) vertex, so
every edge has exactly one designated layer and holes are impossible: the
growth simulation below asserts ZERO unlaid interior edges (the Euclidean-
Voronoi variant it replaced measured 47 - graph-disconnected pockets).

WHY THIS IS MEASURED AND NOT AUTHORED BY EYE
--------------------------------------------
Everything below is either proven (orthogonality, equal supports, single
edge length, tau/tau^3 module facts) or measured over a large patch (heart
density and spacing, the frontier delta census, the depth bound, plant-size
distribution, acceptance margins). Every number in the emitted C# block is
pasted verbatim from this run; Tools/Build/verify_icosahedral_quasilattice_tables.py
re-proves the SHIPPED file from a fresh derivation, because the transcription
from a proven measurement to the asset is the step neither the measurement
nor code review can see (Docs/ECOSYSTEM.md 34.4).
"""
from __future__ import annotations

import argparse
import math
import pathlib
import random
import sys
from collections import defaultdict, deque

import numpy as np

ROOT = pathlib.Path(__file__).resolve().parents[2]
CS_PATH = ROOT / "Assets/_Scripts/Controller/Assemblers/QuasicrystalLatticeData.cs"

CS_BEGIN = "        // <<< MEASURED TABLE BEGIN"
CS_END = "        // <<< MEASURED TABLE END"

TAU = (1 + math.sqrt(5)) / 2

# Generic window offset in perp space. Any generic value works; this one is
# asserted to (a) leave the ORIGIN a heart, so a founder's seed address is
# always (0,0,0,0,0,0), and (b) keep every lattice point measured below clear
# of the window boundary by >= MIN_MARGIN_FLOOR - the "no singular cut"
# guarantee that makes the strict > 0 acceptance test deterministic.
GAMMA = (0.03400571, 0.01267392, 0.02209043)

# The window-margin floor the measured patch must respect. Cut-and-project
# margins genuinely shrink (log-slowly) as the patch grows - the measured
# minimum over 60k vertices is ~4e-5 - so this floor is NOT a claim about the
# lattice; it exists to catch a DEGENERATE gamma, where a singular cut puts a
# lattice point on the boundary at ~1e-15. The C# runtime evaluates the window
# test in doubles with rounding < 1e-12 at colony addresses (|n_i| < 1000), so
# the ordering that makes strict > 0 deterministic is
#   double error (1e-12)  <<  floor (1e-6)  <=  measured min (4e-5).
MIN_MARGIN_FLOOR = 1e-6

# Census sizes. PATCH_VERTS drives every statistic. SIM_PLANTS is the MINIMUM
# colony the growth simulation must complete - the sim deliberately runs its
# whole cycle budget (births land every few cycles, so "all plants done" only
# happens when the budget stops the frontier), which yields ~1,461 complete
# plants: the bigger census is the stronger measurement of the plant-size
# band, and MinPatchPrisms is derived from its minimum. The verify script's
# smaller re-run gates the same properties with a spawn-capped sim.
# NOTE: --check is an exact string compare of 15-decimal emits computed
# through libm cos/sin, so it is authoritative on the machine that emitted
# the table; across platforms a 1-ulp libm difference can fail it spuriously,
# and verify_icosahedral_quasilattice_tables.py (tolerance 5e-13) is the
# portable re-proof.
PATCH_VERTS = 60000
SIM_PLANTS = 400

# Frontier shell radius in edge lengths: generous enough to catch both
# measured heart-link shells (2.3840 and 2.7528) and nothing beyond.
SHELL_RADIUS = 3.4


class Failures(list):
    def check(self, ok: bool, label: str, detail: str = "") -> bool:
        tag = "[ok  ]" if ok else "[FAIL]"
        print(f"{tag} {label}" + (f" - {detail}" if detail else ""))
        if not ok:
            self.append(label)
        return ok


# ---------------------------------------------------------------------------
# Derivation
# ---------------------------------------------------------------------------

def build_bases():
    """The two 3D irreps of the icosahedral group over Z^6 (unit rows)."""
    s5, c5 = 2 / math.sqrt(5), 1 / math.sqrt(5)
    par = [np.array([0.0, 0.0, 1.0])]
    perp = [np.array([0.0, 0.0, 1.0])]
    for k in range(5):
        th = 2 * math.pi * k / 5
        par.append(np.array([s5 * math.cos(th), s5 * math.sin(th), c5]))
        perp.append(np.array([s5 * math.cos(2 * th), s5 * math.sin(2 * th), -c5]))
    return np.array(par), np.array(perp)


PAR, PERP = build_bases()
GAMMA_V = np.array(GAMMA)


def window_faces():
    """Zonotope faces: 15 unit normals (b_i x b_j) with support (1/2) sum|n.b_k|."""
    faces = []
    for i in range(6):
        for j in range(i + 1, 6):
            n = np.cross(PERP[i], PERP[j])
            n /= np.linalg.norm(n)
            # canonical sign so the emit is stable run to run
            for c in n:
                if abs(c) > 1e-9:
                    if c < 0:
                        n = -n
                    break
            h = 0.5 * sum(abs(float(n @ PERP[k])) for k in range(6))
            faces.append((n, h))
    return faces


FACES = window_faces()
FACE_N = np.array([f[0] for f in FACES])
FACE_H = np.array([f[1] for f in FACES])

E6 = [tuple(1 if k == i else 0 for k in range(6)) for i in range(6)]

_margin_memo: dict = {}

# Hot-path copies as plain python floats: per-call numpy overhead on 3-vectors
# is ~10x the arithmetic itself, and margin() runs millions of times across the
# census and the growth simulation.
_PERP_ROWS = [tuple(float(x) for x in row) for row in PERP]
_PAR_ROWS = [tuple(float(x) for x in row) for row in PAR]
_FACE_ROWS = [(float(n[0]), float(n[1]), float(n[2]), float(h)) for n, h in FACES]
_G = tuple(float(x) for x in GAMMA)


def margin(v) -> float:
    m = _margin_memo.get(v)
    if m is None:
        px, py, pz = -_G[0], -_G[1], -_G[2]
        for c, row in zip(v, _PERP_ROWS):
            if c:
                px += c * row[0]; py += c * row[1]; pz += c * row[2]
        m = 1e30
        for fx, fy, fz, h in _FACE_ROWS:
            d = px * fx + py * fy + pz * fz
            mm = h - (d if d >= 0 else -d)
            if mm < m: m = mm
        _margin_memo[v] = m
    return m


def accepted(v) -> bool:
    return margin(v) > 0.0


def add6(u, v, s=1):
    return tuple(a + s * b for a, b in zip(u, v))


def par_of(v):
    x = y = z = 0.0
    for c, row in zip(v, _PAR_ROWS):
        if c:
            x += c * row[0]; y += c * row[1]; z += c * row[2]
    return np.array([x, y, z])


def neighbours(v):
    for i in range(6):
        yield add6(v, E6[i], 1)
        yield add6(v, E6[i], -1)


def deg12(v) -> bool:
    return accepted(v) and all(accepted(u) for u in neighbours(v))


_heart_memo: dict = {}


def is_heart(v) -> bool:
    h = _heart_memo.get(v)
    if h is None:
        h = deg12(v)
        if h:
            mv = margin(v)
            for u in neighbours(v):
                if not deg12(u):
                    continue
                mu = margin(u)
                if mu > mv or (mu == mv and u < v):
                    h = False
                    break
        _heart_memo[v] = h
    return h


def bfs_patch(start, max_verts):
    seen = {start}
    order = [start]
    q = deque([start])
    while q and len(order) < max_verts:
        v = q.popleft()
        for u in neighbours(v):
            if u not in seen and accepted(u):
                seen.add(u)
                order.append(u)
                q.append(u)
    return order


# Tree territory --------------------------------------------------------------

_dist_memo: dict = {}
_owner_memo: dict = {}


def heart_dist(v, d_max):
    d = _dist_memo.get(v)
    if d is None:
        if is_heart(v):
            d = 0
        else:
            ring = {v}
            seen = {v}
            d = None
            for depth in range(1, d_max + 1):
                nxt = set()
                for w in ring:
                    for u in neighbours(w):
                        if u not in seen and accepted(u):
                            seen.add(u)
                            nxt.add(u)
                if any(is_heart(u) for u in nxt):
                    d = depth
                    break
                ring = nxt
            if d is None:
                raise AssertionError(f"no heart within {d_max} steps of {v}")
        _dist_memo[v] = d
    return d


def owning_heart(v, d_max):
    h = _owner_memo.get(v)
    if h is None:
        if is_heart(v):
            h = v
        else:
            dv = heart_dist(v, d_max)
            parent = min(u for u in neighbours(v)
                         if accepted(u) and heart_dist(u, d_max) == dv - 1)
            h = owning_heart(parent, d_max)
        _owner_memo[v] = h
    return h


def edge_key(v, i, s):
    """Canonical edge key: the minus-end vertex plus the axis index."""
    return (v, i) if s > 0 else (add6(v, E6[i], -1), i)


# ---------------------------------------------------------------------------
# Measurement
# ---------------------------------------------------------------------------

def measure(f: Failures) -> dict:
    out: dict = {}

    # -- projection ---------------------------------------------------------
    g = PAR @ PAR.T + PERP @ PERP.T
    worst = float(np.abs(g - 2 * np.eye(6)).max())
    f.check(worst < 1e-12, "6D orthogonality of [PAR; PERP]", f"worst residual {worst:.2e}")

    hs = FACE_H
    f.check(len(FACES) == 15, "window has 15 face-normal pairs", f"{len(FACES)}")
    f.check(float(hs.max() - hs.min()) < 1e-12, "all 15 supports equal (triacontahedron)",
            f"spread {float(hs.max() - hs.min()):.2e}")
    out["support"] = float(hs.mean())

    # tau is NOT integral on the primitive module; tau^3 is. Both are proven
    # here so the doc record's crystallography claims are measurements.
    m6 = np.zeros((6, 6))
    m6[:3, :] = PAR.T
    m6[3:, :] = PERP.T
    x1 = np.linalg.solve(m6, np.concatenate([TAU * PAR[0], (-1 / TAU) * PERP[0]]))
    x3 = np.linalg.solve(m6, np.concatenate([TAU ** 3 * PAR[0], (-1 / TAU) ** 3 * PERP[0]]))
    f.check(float(np.abs(x1 - np.rint(x1)).max()) > 0.4,
            "NEGATIVE CONTROL: tau is not an integer map on Z^6",
            f"lift of e_0 = {np.round(x1, 3)}")
    f.check(float(np.abs(x3 - np.rint(x3)).max()) < 1e-9,
            "tau^3 IS an integer map on Z^6 (P-type icosahedral inflation)",
            f"lift of e_0 = {np.rint(x3).astype(int)}")

    # -- patch census -------------------------------------------------------
    seed = (0,) * 6
    f.check(accepted(seed), "origin is accepted", f"margin {margin(seed):.4f}")
    f.check(is_heart(seed), "origin is a heart (founder seeds at the zero address)")

    patch = bfs_patch(seed, PATCH_VERTS)
    vset = set(patch)
    degrees = []
    edge_lengths = set()
    for v in patch:
        d = 0
        for i in range(6):
            for s in (1, -1):
                u = add6(v, E6[i], s)
                if accepted(u):
                    d += 1
                    if s == 1 and u in vset:
                        edge_lengths.add(round(float(np.linalg.norm(par_of(u) - par_of(v))), 9))
        degrees.append(d)
    mean_deg = sum(degrees) / len(degrees)
    hist = defaultdict(int)
    for d in degrees:
        hist[d] += 1
    f.check(len(edge_lengths) == 1 and abs(next(iter(edge_lengths)) - 1.0) < 1e-9,
            "every edge has the same physical length (unit)", f"{sorted(edge_lengths)}")
    f.check(abs(mean_deg - 6.0) < 0.05, "mean vertex degree ~ 6 (edges/vertex ~ 3)",
            f"measured {mean_deg:.4f}, histogram {dict(sorted(hist.items()))}")
    out["mean_degree"] = mean_deg

    ms = np.array([margin(v) for v in patch])
    rej = []
    for v in patch[:15000]:
        for u in neighbours(v):
            if u not in vset and not accepted(u):
                rej.append(-margin(u))
    min_margin = float(min(ms.min(), min(rej)))
    f.check(min_margin > MIN_MARGIN_FLOOR,
            "acceptance margins clear the determinism floor",
            f"min |margin| {min_margin:.2e} > floor {MIN_MARGIN_FLOOR:.0e} "
            f"(double error at |n|<1000 is < 1e-12)")
    out["min_margin"] = min_margin

    # -- hearts -------------------------------------------------------------
    hearts = [v for v in patch if is_heart(v)]
    density = len(hearts) / len(patch)
    out["heart_density"] = density
    f.check(0.03 < density < 0.08, "heart density in the expected band",
            f"{density:.4f} ({len(hearts)}/{len(patch)})")

    # NEGATIVE CONTROL for the local-max rule: bare 12-coordination admits
    # ADJACENT hearts. If this stops failing, the rule is dead weight; while
    # it fails, the rule is provably load-bearing (a gate nobody has watched
    # fail is not a gate - Docs/ECOSYSTEM.md 34.2).
    bare_adjacent = 0
    for v in patch[:20000]:
        if not deg12(v):
            continue
        for u in neighbours(v):
            if deg12(u):
                bare_adjacent += 1
                break
        if bare_adjacent:
            break
    f.check(bare_adjacent > 0,
            "NEGATIVE CONTROL: bare deg-12 admits adjacent hearts (local-max rule is load-bearing)")
    ruled_adjacent = sum(1 for h in hearts for u in neighbours(h) if is_heart(u))
    f.check(ruled_adjacent == 0, "no two hearts are adjacent under the local-max rule",
            f"{ruled_adjacent} adjacent pairs")

    hp = np.array([par_of(h) for h in hearts])
    vp = np.array([par_of(v) for v in patch])
    rmax = float(np.linalg.norm(vp, axis=1).max())
    interior = [k for k in range(len(hearts)) if np.linalg.norm(hp[k]) < 0.6 * rmax]

    shell = defaultdict(int)
    nn = []
    for k in interior:
        d = np.linalg.norm(hp - hp[k], axis=1)
        d[k] = 1e9
        srt = np.argsort(d)[:16]
        nn.append(float(d[srt[0]]))
        for m_ in srt:
            if d[m_] < SHELL_RADIUS:
                shell[tuple(a - b for a, b in zip(hearts[m_], hearts[k]))] += 1
    nn = np.array(nn)
    f.check(float(nn.max() - nn.min()) < 1e-6,
            "nearest-heart spacing is CONSTANT across the colony",
            f"{nn.mean():.4f} edges for every one of {len(interior)} interior hearts")
    out["heart_spacing"] = float(nn.mean())

    deltas = sorted(shell.keys())
    lens = sorted({round(float(np.linalg.norm(par_of(d))), 4) for d in deltas})
    out["frontier_deltas"] = deltas
    out["shell_lengths"] = lens
    f.check(len(deltas) >= 40, "frontier shell census is substantial",
            f"{len(deltas)} distinct deltas, physical lengths {lens}")
    closed = all(tuple(-c for c in d) in shell for d in deltas)
    f.check(closed, "frontier delta set is closed under negation")

    # heart-graph connectivity over the measured deltas
    hset = set(hearts)
    seen = {hearts[0]}
    q = deque([hearts[0]])
    while q:
        h = q.popleft()
        for d in deltas:
            u = add6(h, d)
            if u in hset and u not in seen:
                seen.add(u)
                q.append(u)
    inner = [h for k, h in enumerate(hearts) if np.linalg.norm(hp[k]) < 0.5 * rmax]
    unreached = sum(1 for h in inner if h not in seen)
    f.check(unreached == 0, "heart graph over the frontier deltas is connected",
            f"{unreached}/{len(inner)} interior hearts unreached")

    # -- depth bound --------------------------------------------------------
    d_max_seen = 0
    for v in patch:
        if np.linalg.norm(par_of(v)) > 0.6 * rmax:
            continue
        d_max_seen = max(d_max_seen, heart_dist(v, 12))
    d_max = d_max_seen + 2  # slack for boundary-adjacent probes at runtime
    f.check(d_max_seen <= 6, "every vertex within a few steps of a heart",
            f"max graph distance {d_max_seen} (shipping bound {d_max})")
    out["d_max"] = d_max

    # -- growth simulation: the algorithm the C# runs, to zero holes --------
    random.seed(11)
    edge_owner: dict = {}
    plants: dict = {}

    def grow_one(heart):
        pl = plants[heart]
        seen_ = {heart}
        q_ = deque([heart])
        while q_:
            v = q_.popleft()
            for i in range(6):
                for s in (1, -1):
                    u = add6(v, E6[i], s)
                    if not accepted(u):
                        continue
                    ek = edge_key(v, i, s)
                    if ek not in edge_owner and owning_heart(ek[0], d_max) == heart:
                        edge_owner[ek] = heart
                        pl["struts"] += 1
                        return True
                    if u not in seen_ and owning_heart(u, d_max) == heart:
                        seen_.add(u)
                        q_.append(u)
        pl["done"] = True
        return False

    frontier: list = []
    offered: set = set()

    def contribute(heart):
        for d in deltas:
            u = add6(heart, d)
            if u in offered or u in plants:
                continue
            if is_heart(u):
                offered.add(u)
                frontier.append(u)

    plants[seed] = {"struts": 0, "done": False}
    contributed: set = set()
    for _cycle in range(6000):
        for h in [h for h, p in plants.items() if not p["done"]]:
            grow_one(h)
        for h in [h for h, p in plants.items() if p["done"] and h not in contributed]:
            contributed.add(h)
            contribute(h)
        if _cycle % 4 == 0 and frontier:
            k = random.randrange(len(frontier))
            h = frontier[k]
            frontier[k] = frontier[-1]
            frontier.pop()
            offered.discard(h)
            if h not in plants and is_heart(h):
                plants[h] = {"struts": 0, "done": False}
        if len(plants) >= SIM_PLANTS and all(p["done"] for p in plants.values()):
            break

    done = {h: p for h, p in plants.items() if p["done"]}
    sizes = np.array([p["struts"] for p in done.values()])
    f.check(len(done) >= SIM_PLANTS * 0.9, "growth simulation completed a real colony",
            f"{len(done)} complete plants, {len(edge_owner)} struts")
    out["plant_min"] = int(sizes.min())
    out["plant_mean"] = float(sizes.mean())
    out["plant_max"] = int(sizes.max())
    print(f"       struts/plant over {len(done)} complete plants: "
          f"mean {sizes.mean():.1f} min {sizes.min()} max {sizes.max()}")

    complete = set(done.keys())
    holes = 0
    slots = 0
    checked: set = set()
    for (v, i) in list(edge_owner.keys()):
        for vv in (v, add6(v, E6[i], 1)):
            if vv in checked:
                continue
            checked.add(vv)
            if owning_heart(vv, d_max) not in complete:
                continue
            for ii in range(6):
                for ss in (1, -1):
                    uu = add6(vv, E6[ii], ss)
                    if not accepted(uu):
                        continue
                    ek = edge_key(vv, ii, ss)
                    if owning_heart(ek[0], d_max) in complete:
                        slots += 1
                        if ek not in edge_owner:
                            holes += 1
    f.check(holes == 0, "ZERO holes: tree territory makes the lattice complete",
            f"{holes} unlaid of {slots} edge slots owned by complete plants "
            f"(the Euclidean-Voronoi variant measured 47 - disconnected pockets)")

    # completeness floor for AssembledFlora's maturity gate: comfortably under
    # the measured minimum so no legitimate plant ever stalls on it.
    out["min_patch"] = max(8, int(sizes.min()) - 6)

    # -- strut orientation frames ------------------------------------------
    normals = []
    tangents = []
    for i in range(6):
        n = np.cross(PAR[i], PAR[(i + 1) % 6])
        n /= np.linalg.norm(n)
        t = np.cross(n, PAR[i])
        # exactness: t x n == a_i and both unit
        assert abs(np.linalg.norm(t) - 1) < 1e-12
        assert float(np.abs(np.cross(t, n) - PAR[i]).max()) < 1e-12
        normals.append(n)
        tangents.append(t)
    out["strut_normals"] = normals
    out["strut_tangents"] = tangents

    return out


# ---------------------------------------------------------------------------
# Emit
# ---------------------------------------------------------------------------

def emit(m: dict) -> str:
    def d(x: float) -> str:
        return f"{x:.15f}"

    def v3f(p) -> str:
        return f"new Vector3({p[0]:.7f}f, {p[1]:.7f}f, {p[2]:.7f}f)"

    lines: list[str] = []
    a = lines.append
    a("        // ---- MEASURED by Tools/Build/measure_icosahedral_quasilattice.py. Do not")
    a("        // hand-edit; regenerate with --write. Every row is pasted verbatim from the")
    a("        // emit and re-proven from a fresh derivation by")
    a("        // Tools/Build/verify_icosahedral_quasilattice_tables.py (Docs/ECOSYSTEM.md 36).")
    a(f"        // Census: mean degree {m['mean_degree']:.4f}, heart density {m['heart_density']:.4f},")
    a(f"        // heart spacing {m['heart_spacing']:.4f} edges (constant), plant struts")
    a(f"        // {m['plant_min']}..{m['plant_max']} (mean {m['plant_mean']:.1f}), shell lengths {m['shell_lengths']}.")
    a("")
    a("        /// <summary>Window face support - identical for all 15 faces (the rhombic")
    a("        /// triacontahedron is face-transitive), asserted by the measurement.</summary>")
    a(f"        public const double WindowSupport = {d(m['support'])};")
    a("")
    a("        /// <summary>Acceptance margins measured over a 60k-vertex patch never fall")
    a("        /// below this; double rounding at colony addresses is &lt; 1e-12, so the")
    a("        /// strict &gt; 0 window test is deterministic with 7 orders of headroom.")
    a("        /// (Margins shrink log-slowly with patch size; the population cap bounds")
    a("        /// a live colony to the measured scale.)</summary>")
    a(f"        public const double MinAcceptanceMargin = {d(m['min_margin'])};")
    a("")
    a("        /// <summary>Every accepted vertex sits within this many graph steps of a")
    a("        /// heart (measured max plus slack) - the BFS bound for HeartDistance.</summary>")
    a(f"        public const int HeartDistanceMax = {m['d_max']};")
    a("")
    a("        /// <summary>Complete plants measured " + f"{m['plant_min']}..{m['plant_max']} struts; the maturity")
    a("        /// floor sits under the minimum so no legitimate plant stalls on it.</summary>")
    a(f"        public const int MinPatchPrisms = {m['min_patch']};")
    a("")
    a("        /// <summary>Physical images of the six Z^6 basis vectors: the icosahedron's")
    a("        /// vertex axes. A vertex's position is the integer-weighted sum of these.</summary>")
    a("        static readonly double[,] Par =")
    a("        {")
    for p in PAR:
        a(f"            {{ {d(p[0])}, {d(p[1])}, {d(p[2])} }},")
    a("        };")
    a("")
    a("        /// <summary>Perp images (the second 3D irrep: pentagon angle doubled, z")
    a("        /// flipped). The window test lives entirely in this projection.</summary>")
    a("        static readonly double[,] Perp =")
    a("        {")
    for p in PERP:
        a(f"            {{ {d(p[0])}, {d(p[1])}, {d(p[2])} }},")
    a("        };")
    a("")
    a("        /// <summary>The 15 unit face normals of the acceptance triacontahedron.</summary>")
    a("        static readonly double[,] WindowFaceNormals =")
    a("        {")
    for n, _h in FACES:
        a(f"            {{ {d(n[0])}, {d(n[1])}, {d(n[2])} }},")
    a("        };")
    a("")
    a("        /// <summary>Generic window offset - keeps every lattice point clear of the")
    a("        /// boundary (no singular cut) and makes the ORIGIN a heart, so a founder")
    a("        /// always seeds at the zero address.</summary>")
    a("        static readonly double[] Gamma =")
    a(f"            {{ {d(GAMMA[0])}, {d(GAMMA[1])}, {d(GAMMA[2])} }};")
    a("")
    a("        /// <summary>Per-direction strut frames: LookRotation(forward: Normal, up:")
    a("        /// Tangent) puts local +x exactly along the edge axis Par[i]. Vectors, not")
    a("        /// rotations - nothing here survives a transform it should not.</summary>")
    a("        static readonly Vector3[] StrutNormal =")
    a("        {")
    for n in m["strut_normals"]:
        a(f"            {v3f(n)},")
    a("        };")
    a("")
    a("        static readonly Vector3[] StrutTangent =")
    a("        {")
    for t in m["strut_tangents"]:
        a(f"            {v3f(t)},")
    a("        };")
    a("")
    a("        /// <summary>The measured heart-to-heart link census: every integer delta from")
    a("        /// a heart to a shell neighbour (physical length under " + f"{SHELL_RADIUS}" + " edges). Closed")
    a("        /// under negation; the heart graph over these is measured connected. The")
    a("        /// colony's reproduction frontier walks exactly these steps.</summary>")
    a("        static readonly int[,] FrontierDeltas =")
    a("        {")
    for dl in m["frontier_deltas"]:
        a("            { " + ", ".join(f"{c}" for c in dl) + " },")
    a("        };")
    return "\n".join(lines)


def write_cs(block: str) -> bool:
    if not CS_PATH.exists():
        print(f"  (no {CS_PATH.name} yet - printing the block only)")
        return False
    text = CS_PATH.read_text()
    if CS_BEGIN not in text or CS_END not in text:
        print(f"  (markers missing in {CS_PATH.name} - printing the block only)")
        return False
    head = text[: text.index(CS_BEGIN) + len(CS_BEGIN)]
    tail = text[text.index(CS_END):]
    CS_PATH.write_text(head + "\n" + block + "\n" + tail)
    print(f"  wrote {CS_PATH}")
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="verify + compare the emit against the shipped C# block")
    ap.add_argument("--write", action="store_true", help="rewrite the C# table")
    args = ap.parse_args()

    print("=" * 78)
    print("Icosahedral quasilattice measurement (Ammann-Kramer-Neri, cut-and-project Z^6)")
    print("=" * 78)

    f = Failures()
    m = measure(f)
    block = emit(m)

    if f:
        print(f"FAILED {len(f)} check(s):")
        for label in f:
            print(f"  - {label}")
        return 1

    if args.check:
        if not CS_PATH.exists():
            print(f"[FAIL] {CS_PATH} does not exist")
            return 1
        text = CS_PATH.read_text()
        if CS_BEGIN not in text or CS_END not in text:
            print(f"[FAIL] markers missing in {CS_PATH.name}")
            return 1
        shipped = text[text.index(CS_BEGIN) + len(CS_BEGIN): text.index(CS_END)].strip("\n")
        if shipped != block:
            print("[FAIL] shipped table differs from a fresh measurement - regenerate with --write")
            return 1
        print("[ok  ] shipped table matches a fresh measurement to the character")
        return 0

    if args.write:
        write_cs(block)
        return 0

    print("-" * 78)
    print(block)
    return 0


if __name__ == "__main__":
    sys.exit(main())
