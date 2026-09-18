#!/usr/bin/env python3
"""The Mandelbulb flora's growth rule, transcribed independently of the C#.

This is the MODEL half of the species' tool trio. It reads the SHIPPED spherical-harmonic
table out of MandelbulbSurfaceTables.cs and walks the same rule the game walks, so a claim
this file makes about prism counts, sizes, volume or coverage is a claim about the shipped
plant rather than about a copy of the numbers.

ONE thing it deliberately does NOT promise: prism-for-prism agreement with the C#. The
walk is SEQUENTIAL and carries a turn gate, so it is chaotic in its last bits -- the C#
computes in float32 and Python in float64, and one step that lands a hair either side of
`dot(t, want) < maxTurnCos` ends a curve in one and not the other. The verifier therefore
proves the PURE functions exactly (reconstruction, frame, pose, the seed set -- none of
which amplify) and the walk by its statistics, and says so. Claiming bit-exactness across
two float widths on a chaotic recurrence would be claiming something no run could support.
"""
import math
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TABLE = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Environment",
                     "FloraAndFauna", "MandelbulbSurfaceTables.cs")

ELEMENTS = ("Charge", "Mass", "Space", "Time")

# Steering fields, in the order MandelbulbSurface.SteeringField declares them.
CONTOUR, ASCENT, DESCENT, AZIMUTH, MERIDIAN, GEODESIC = range(6)


# ── the shipped table ──────────────────────────────────────────────────────────

def load_tables(path=TABLE):
    """mean + three modes per element, straight out of the generated C#."""
    text = open(path).read()
    degree = int(re.search(r"public const int Degree = (\d+);", text).group(1))
    out = {}
    for name in ELEMENTS:
        m = re.search(r"public static readonly float\[\]\[\] %s =\s*\{(.*?)\n        \};"
                      % name, text, re.S)
        if not m:
            raise SystemExit(f"{path}: no basis block for {name}")
        vectors = []
        for block in re.findall(r"new float\[\]\s*\{(.*?)\}", m.group(1), re.S):
            vals = [float(v) for v in re.findall(r"(-?[0-9.eE+-]+)f", block)]
            vectors.append(vals)
        if len(vectors) != 4:
            raise SystemExit(f"{path}: {name} has {len(vectors)} vectors, expected 4")
        n = (degree + 1) ** 2
        for v in vectors:
            if len(v) != n:
                raise SystemExit(f"{path}: {name} vector has {len(v)} coefficients, expected {n}")
        out[name] = vectors
    return degree, out


# ── spherical harmonics ────────────────────────────────────────────────────────

def norm_table(degree):
    stride = degree + 1
    k = [0.0] * (stride * stride)
    for l in range(degree + 1):
        r = 1.0
        for m in range(l + 1):
            if m > 0:
                r /= (l + m) * (l - m + 1)
            k[l * stride + m] = math.sqrt((2 * l + 1) / (4.0 * math.pi) * r)
    return k


def legendre_table(degree, x, out):
    stride = degree + 1
    s = math.sqrt(max(0.0, 1.0 - x * x))
    out[0] = 1.0
    for m in range(degree + 1):
        if m > 0:
            out[m * stride + m] = out[(m - 1) * stride + (m - 1)] * -(2 * m - 1) * s
        if m < degree:
            out[(m + 1) * stride + m] = x * (2 * m + 1) * out[m * stride + m]
        for l in range(m + 2, degree + 1):
            out[l * stride + m] = ((2 * l - 1) * x * out[(l - 1) * stride + m]
                                   - (l + m - 1) * out[(l - 2) * stride + m]) / (l - m)


def compose(basis, w0, w1, w2):
    return [basis[0][i] + w0 * basis[1][i] + w1 * basis[2][i] + w2 * basis[3][i]
            for i in range(len(basis[0]))]


def reconstruct(degree, coeffs, w, h):
    stride = degree + 1
    norm = norm_table(degree)
    p = [0.0] * (stride * stride)
    root2 = math.sqrt(2.0)
    out = [0.0] * (w * h)
    cos_cache = [[math.cos(m * (i / w) * 2.0 * math.pi) for i in range(w)]
                 for m in range(degree + 1)]
    sin_cache = [[math.sin(m * (i / w) * 2.0 * math.pi) for i in range(w)]
                 for m in range(degree + 1)]
    for j in range(h):
        th = (j + 0.5) / h * math.pi
        legendre_table(degree, math.cos(th), p)
        cm = [0.0] * stride
        sm = [0.0] * stride
        for l in range(degree + 1):
            for m in range(l + 1):
                b = norm[l * stride + m] * p[l * stride + m]
                if m == 0:
                    cm[0] += b * coeffs[l * (l + 1)]
                else:
                    cm[m] += root2 * b * coeffs[l * (l + 1) + m]
                    sm[m] += root2 * b * coeffs[l * (l + 1) - m]
        row = j * w
        for i in range(w):
            v = cm[0]
            for m in range(1, degree + 1):
                v += cm[m] * cos_cache[m][i] + sm[m] * sin_cache[m][i]
            out[row + i] = v
    return out


# ── the live surface ───────────────────────────────────────────────────────────

class Surface:
    def __init__(self, r, w, h):
        self.R, self.W, self.H = r, w, h
        self.mean_radius = sum(r) / max(1, len(r))

    def sample(self, theta, phi):
        w, h, r = self.W, self.H, self.R
        x = phi / (2.0 * math.pi) * w
        y = theta / math.pi * h - 0.5
        y = min(max(y, 0.0), h - 1.0001)
        i0 = math.floor(x)
        j0 = math.floor(y)
        fx, fy = x - i0, y - j0
        ia = int(i0) % w
        ib = (int(i0) + 1) % w
        ja = min(max(int(j0), 0), h - 1)
        jb = min(max(int(j0) + 1, 0), h - 1)
        a = r[ja * w + ia] * (1 - fx) + r[ja * w + ib] * fx
        b = r[jb * w + ia] * (1 - fx) + r[jb * w + ib] * fx
        return a * (1 - fy) + b * fy


def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def _mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2],
                          a[0] * b[1] - a[1] * b[0])
def _len(a): return math.sqrt(_dot(a, a))


def _norm(a):
    m = _len(a)
    return _mul(a, 1.0 / m) if m > 1e-9 else (0.0, 0.0, 0.0)


class Frame:
    __slots__ = ("position", "normal", "ascent", "dir", "e_theta", "e_phi",
                 "radius", "slope", "sin_theta")


def build_frame(s, theta, phi):
    f = Frame()
    hT = math.pi / s.H * 0.75
    hP = 2.0 * math.pi / s.W * 0.75
    st, ct = math.sin(theta), math.cos(theta)
    cp, sp = math.cos(phi), math.sin(phi)
    sT = max(st, 1e-4)

    r = s.sample(theta, phi)
    rT = (s.sample(theta + hT, phi) - s.sample(theta - hT, phi)) / (2 * hT)
    rP = (s.sample(theta, phi + hP) - s.sample(theta, phi - hP)) / (2 * hP)

    d = (st * cp, st * sp, ct)
    eT = (ct * cp, ct * sp, -st)
    eP = (-sp, cp, 0.0)

    s_th = _add(_mul(d, rT), _mul(eT, r))
    s_ph = _add(_mul(d, rP), _mul(eP, r * sT))
    n = _cross(s_th, s_ph)
    m = _len(n)
    n = _mul(n, 1.0 / m) if m > 1e-12 else d
    if _dot(n, d) < 0:
        n = _mul(n, -1.0)

    a = _add(_mul(eT, rT), _mul(eP, rP / sT))
    a = _sub(a, _mul(n, _dot(a, n)))
    slope = _len(a)
    a = _mul(a, 1.0 / slope) if slope > 1e-9 else eP

    f.position, f.normal, f.ascent = _mul(d, r), n, a
    f.dir, f.e_theta, f.e_phi = d, eT, eP
    f.radius, f.slope, f.sin_theta = r, slope, sT
    return f


# ── the growth rule ────────────────────────────────────────────────────────────

class Rules:
    """Mirrors MandelbulbSurface.GrowthRules field for field, in the same order the
    generator writes them into the prefab."""
    FIELDS = ("field", "swirl", "field_mix", "momentum", "step", "max_steps",
              "lanes", "lane_gap", "hop_seek", "hop_jitter", "seeds", "seed_spread",
              "max_turn", "r_min", "r_max", "min_run", "length_factor", "girth_taper")

    def __init__(self, *values):
        if len(values) != len(self.FIELDS):
            raise ValueError(f"expected {len(self.FIELDS)} rule values, got {len(values)}")
        for k, v in zip(self.FIELDS, values):
            setattr(self, k, v)
        for k in ("field", "max_steps", "lanes", "seeds", "min_run"):
            setattr(self, k, int(getattr(self, k)))

    def as_list(self):
        return [getattr(self, k) for k in self.FIELDS]


class Rng:
    def __init__(self, seed):
        self.s = seed & 0xFFFFFFFF
        if self.s == 0:
            self.s = 1

    def next(self):
        s = self.s
        s ^= (s << 13) & 0xFFFFFFFF
        s ^= s >> 17
        s ^= (s << 5) & 0xFFFFFFFF
        self.s = s
        return (s & 0xFFFFFF) / 16777216.0


class Prism:
    __slots__ = ("theta", "phi", "radial", "tan_a", "tan_b", "length", "girth",
                 "curve", "lane")

    def __init__(self, theta, phi, radial, tan_a, tan_b, length, girth, curve, lane):
        self.theta, self.phi, self.radial = theta, phi, radial
        self.tan_a, self.tan_b = tan_a, tan_b
        self.length, self.girth = length, girth
        self.curve, self.lane = curve, lane


def pose(surface, a):
    f = build_frame(surface, a.theta, a.phi)
    n = f.normal
    fwd = _add(_mul(f.e_theta, a.tan_a), _mul(f.e_phi, a.tan_b))
    fwd = _sub(fwd, _mul(n, _dot(fwd, n)))
    m = _len(fwd)
    fwd = _mul(fwd, 1.0 / m) if m > 1e-7 else f.e_phi
    return _mul(f.dir, f.radius + a.radial), fwd, n


def _spherical(p):
    r = _len(p)
    if r < 1e-9:
        return 0.0, 0.0
    theta = math.acos(min(1.0, max(-1.0, p[2] / r)))
    phi = math.atan2(p[1], p[0])
    if phi < 0:
        phi += 2 * math.pi
    return theta, phi


def build_seeds(surface, rules):
    """Spread over the WHOLE sphere. The Fibonacci walk is monotonic in z, so a PREFIX of
    it is a polar cap -- an NMS that stopped at `want` grew 78% of one plant's prisms into
    the top eighth of the sphere by area. The NMS runs over every candidate and the result
    is STRIDED down."""
    min_cos = math.cos(min(179.0, max(1.0, rules.seed_spread)) * math.pi / 180.0)
    want = max(1, rules.seeds)
    probe = max(want * 4, 256)
    ga = math.pi * (3 - math.sqrt(5))
    dirs = []
    for i in range(probe):
        z = 1 - (i + 0.5) / probe * 2
        theta = math.acos(min(1.0, max(-1.0, z)))
        phi = (ga * i) % (2 * math.pi)
        if phi < 0:
            phi += 2 * math.pi
        st = math.sin(theta)
        d = (st * math.cos(phi), st * math.sin(phi), math.cos(theta))
        if any(_dot(d, q) > min_cos for q in dirs):
            continue
        dirs.append(d)
    take = min(want, len(dirs))
    seeds = []
    for k in range(take):
        d = dirs[(k * len(dirs)) // take]
        theta = math.acos(min(1.0, max(-1.0, d[2])))
        phi = math.atan2(d[1], d[0])
        if phi < 0:
            phi += 2 * math.pi
        seeds.append(build_frame(surface, theta, phi).position)
    return seeds


def _is_bipolar(field):
    return field != ASCENT and field != DESCENT


def _field_direction(rules, frame, n):
    f = rules.field
    if f == CONTOUR:
        v = _cross(n, frame.ascent)
    elif f == ASCENT:
        v = frame.ascent
    elif f == DESCENT:
        v = _mul(frame.ascent, -1.0)
    elif f == AZIMUTH:
        v = (-frame.position[1], frame.position[0], 0.0)
    elif f == MERIDIAN:
        v = frame.e_theta
    else:
        return None
    v = _sub(v, _mul(n, _dot(v, n)))
    if _dot(v, v) < 1e-16:
        return None
    v = _norm(v)
    if abs(rules.swirl) > 0.01:
        ang = rules.swirl * math.pi / 180.0
        c, s = math.cos(ang), math.sin(ang)
        v = _add(_add(_mul(v, c), _mul(_cross(n, v), s)),
                 _mul(n, _dot(n, v) * (1 - c)))
        v = _norm(v)
    return v


def grow(surface, rules, seed, budget):
    """Lane-MAJOR: every seed lays its lane 0 before any seed lays its lane 1, so a plant
    that stops at its budget is the whole bulb drawn thinly rather than two seeds' worth of
    it drawn fully."""
    rng = Rng((seed * (2654435761 & 0x7FFFFFFF)) ^ 0x5bf03635)
    seeds = build_seeds(surface, rules)
    lane_pos = list(seeds)
    lane_tan = [None] * len(seeds)
    lane_dead = [False] * len(seeds)
    max_turn_cos = math.cos(min(179.0, max(1.0, rules.max_turn)) * math.pi / 180.0)
    r_min, r_max = min(rules.r_min, rules.r_max), max(rules.r_min, rules.r_max)
    lanes = max(1, rules.lanes)

    def trace(start, seed_tangent):
        pts = []
        theta, phi = _spherical(start)
        t = None
        for _ in range(max(2, rules.max_steps)):
            fr = build_frame(surface, theta, phi)
            if fr.radius < r_min or fr.radius > r_max:
                break
            n = fr.normal
            fd = _field_direction(rules, fr, n)
            if t is not None:
                ahead = _sub(t, _mul(n, _dot(t, n)))
                if _dot(ahead, ahead) < 1e-16:
                    break
                ahead = _norm(ahead)
                if fd is not None:
                    d = fd
                    if _is_bipolar(rules.field) and _dot(d, ahead) < 0:
                        d = _mul(d, -1.0)
                    mix = min(1.0, max(0.0, rules.field_mix))
                    want = tuple(ahead[i] + (d[i] - ahead[i]) * mix for i in range(3))
                else:
                    want = ahead
                if _dot(want, want) < 1e-16:
                    break
                want = _norm(want)
                mom = min(1.0, max(0.0, rules.momentum))
                want = tuple(want[i] + (t[i] - want[i]) * mom for i in range(3))
                want = _sub(want, _mul(n, _dot(want, n)))
                if _dot(want, want) < 1e-16:
                    break
                want = _norm(want)
            elif seed_tangent is not None:
                a = _sub(seed_tangent, _mul(n, _dot(seed_tangent, n)))
                if _dot(a, a) < 1e-16:
                    return pts
                want = _norm(a)
            else:
                want = fd if fd is not None else fr.e_phi

            if t is not None and _dot(t, want) < max_turn_cos:
                break           # -> dust

            t = want
            pts.append((fr.position, n))
            q = _add(fr.position, _mul(t, rules.step))
            theta, phi = _spherical(q)
        return pts

    def hop(p, n, t):
        b = _cross(n, t)
        b = (1.0, 0.0, 0.0) if _dot(b, b) < 1e-16 else _norm(b)
        d = _mul(b, -1.0) if rng.next() < 0.5 else b
        if rules.hop_jitter > 0:
            j = rules.hop_jitter * (rng.next() * 2 - 1)
            d = _add(d, _mul(b, j))
            d = b if _dot(d, d) < 1e-16 else _norm(d)
        q = _add(p, _mul(d, rules.lane_gap))
        th, ph = _spherical(q)
        fr = build_frame(surface, th, ph)
        best, best_r = fr.position, fr.radius
        if rules.hop_seek > 0:
            span = rules.lane_gap * rules.hop_seek
            for i in range(1, 5):
                for sgn in (-1, 1):
                    c = _add(best, _mul(d, span * i / 4.0 * sgn))
                    ct, cp = _spherical(c)
                    fr2 = build_frame(surface, ct, cp)
                    if fr2.radius > best_r:
                        best_r, best = fr2.radius, fr2.position
        return best

    out = []
    curve_count = 0
    lane = 0
    taper = rules.girth_taper if rules.girth_taper > 0 else 1.0
    while lane < lanes and len(out) < budget:
        progressed = False
        for k in range(len(seeds)):
            if len(out) >= budget:
                break
            if lane_dead[k]:
                continue
            progressed = True
            pts = trace(lane_pos[k], lane_tan[k])
            if len(pts) >= max(2, rules.min_run):
                curve_count += 1
                reference = max(2.0, rules.max_steps * 0.5)
                u = min(1.0, max(0.0, (len(pts) - 1) / reference))
                girth = taper + (1.0 - taper) * u
                for i in range(len(pts) - 1):
                    a, b = pts[i][0], pts[i + 1][0]
                    delta = _sub(b, a)
                    ln = _len(delta)
                    if ln < 1e-7:
                        continue
                    fwd = _mul(delta, 1.0 / ln)
                    centre = _mul(_add(a, b), 0.5)
                    th, ph = _spherical(centre)
                    fr = build_frame(surface, th, ph)
                    out.append(Prism(th, ph, _len(centre) - fr.radius,
                                     _dot(fwd, fr.e_theta), _dot(fwd, fr.e_phi),
                                     ln * max(0.05, rules.length_factor),
                                     girth, curve_count, lane))
                mid = len(pts) // 2
                nxt = min(len(pts) - 1, mid + 1)
                tv = _sub(pts[nxt][0], pts[mid][0])
                lane_tan[k] = _norm(tv) if _dot(tv, tv) > 1e-18 else (1.0, 0.0, 0.0)
                lane_pos[k] = hop(pts[mid][0], pts[mid][1], lane_tan[k])
            else:
                p0 = lane_pos[k]
                n0 = _norm(p0) if _dot(p0, p0) > 1e-12 else (0.0, 1.0, 0.0)
                b0 = _cross(n0, (0.0, 0.0, 1.0))
                if _dot(b0, b0) < 1e-12:
                    b0 = _cross(n0, (1.0, 0.0, 0.0))
                hopped = hop(p0, n0, _norm(b0))
                if _dot(_sub(hopped, p0), _sub(hopped, p0)) < 1e-10:
                    lane_dead[k] = True
                lane_pos[k] = hopped
        lane += 1
        if not progressed:
            break
    return out[:budget], curve_count


def surface_for(element, w0=0.0, w1=0.0, w2=0.0, width=192, tables=None, degree=None):
    if tables is None:
        degree, tables = load_tables()
    coeffs = compose(tables[element], w0, w1, w2)
    h = width // 2
    return Surface(reconstruct(degree, coeffs, width, h), width, h)


# ── what this species IS ───────────────────────────────────────────────────────
#
# Authored HERE rather than in the generator, because three tools have to agree about it:
# the generator writes it into the prefab, the measurement prices it, and the verifier
# feeds it to the shipped C#. One source, three readers.
#
# Every element grows a different fractal ORDER (its element, Docs/ECOSYSTEM.md §40 — the
# table lives in the BAKE) and a different CURVE FAMILY, so the four read as four plants
# rather than four sizes of one:
#
#   Charge  contour, swirled   — a tangled coral cage
#   Mass    contour, hard swirl— spiralling bracts
#   Space   geodesic           — an open wire cage of long straight struts
#   Time    ascent             — fall lines from every crest, a radiant anemone
#
# Fields:  field swirl mix  mom  step  steps lanes gap  seek jit seeds spread turn rmin rmax run lenf taper
RULES = {
    "Charge": (0, 25, 0.90, 0.35, 0.040, 130, 60, 0.30, 0.3, 0.30, 70, 10, 30, 0.3, 2.0,  8, 0.45, 0.40),
    "Mass":   (0, 55, 0.85, 0.35, 0.038, 140, 70, 0.34, 0.4, 0.25, 80,  9, 30, 0.3, 2.0,  8, 1.0, 0.40),
    "Space":  (5,  0, 0.00, 0.55, 0.050, 150, 55, 0.34, 0.2, 0.40, 65, 11, 26, 0.3, 2.1, 10, 1.0, 0.45),
    "Time":   (1,  0, 0.95, 0.25, 0.030, 110, 80, 0.26, 0.0, 0.30, 90,  8, 60, 0.3, 2.0,  5, 1.0, 0.35),
}

# Ribbon cross-section in SURFACE units (x across the curve, y through it). Length comes
# from the curve. CHARGE is fitted against its ARMOUR, not its box: a Charge plant's leaves
# are shielded by law and a shield engages the octahedron CIRCUMSCRIBING the prism, reaching
# 1.5 x leafSize (Docs/ECOSYSTEM.md §35). Solved by measure_mandelbulb_flora.py --shields.
CROSS_SECTION = {
    "Charge": (0.0275, 0.0100),
    "Mass":   (0.0500, 0.0180),
    "Space":  (0.0550, 0.0200),
    "Time":   (0.0450, 0.0170),
}

SHELL_RADIUS = 75.0     # world radius of the surface's unit sphere
FIELD_WIDTH = 192       # runtime reconstruction lattice
PRISM_BUDGET = 2800     # live prisms per plant
WEIGHT_SPREAD = 1.0
WEIGHT_STEPS = 3
MAX_LIVE_POPULATION = 3
POPULATION_SIZE = 1


def rules_for(element):
    return Rules(*RULES[element])



CLAIM_FACTOR = 0.70   # MandelbulbFlora.ClaimFactor


def claim_filter(prisms, centres, factor=None, shell=SHELL_RADIUS):
    """What MandelbulbFlora.Claim does, applied offline so a measurement describes the plant
    the game LAYS rather than the candidates the walk produced. A prism is refused when a
    prism already laid sits within `factor * its own length` — under 1, so a curve's own
    chain (exactly one length apart) always clears by construction.

    The hash grid uses ONE cell size for every prism. Keying it on each prism's own radius
    is the bug that makes a spatial hash silently stop working: a prism inserted under one
    cell size is looked up under another and never found, so the filter reports a clean
    result while refusing almost nothing.

    One difference from the game, stated: the game's claim also sees OTHER plants' prisms,
    because it goes through PrismSpatialIndex. This is intra-plant only, so it is an upper
    bound on what a plant lays in a crowded cell and exact for one standing alone."""
    import math as _m
    if factor is None:
        factor = CLAIM_FACTOR
    if not prisms:
        return []
    radii = [max(0.25, factor * p.length * shell) for p in prisms]
    cell = max(radii)
    kept_p, kept_c, grid = [], [], {}
    for p, c, r in zip(prisms, centres, radii):
        key = tuple(int(_m.floor(c[a] / cell)) for a in range(3))
        ok = True
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for q in grid.get((key[0] + dx, key[1] + dy, key[2] + dz), ()):
                        d = _sub(c, kept_c[q])
                        if _dot(d, d) < r * r:
                            ok = False
                            break
                    if not ok:
                        break
                if not ok:
                    break
            if not ok:
                break
        if not ok:
            continue
        grid.setdefault(key, []).append(len(kept_c))
        kept_p.append(p)
        kept_c.append(c)
    return kept_p
