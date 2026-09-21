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
import struct

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

def _f32(v):
    """Round to the nearest float32: the game stores the reconstructed field as float[],
    so sampling the float64 reconstruction would model a field the game never has."""
    return struct.unpack("f", struct.pack("f", v))[0]


class Surface:
    def __init__(self, r, w, h):
        self.R, self.W, self.H = [_f32(v) for v in r], w, h
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


# ── critical points (the WATERSHED's seeds) ────────────────────────────────────
#
# Mirrors MandelbulbSurface.Gradient / Hessian / Eigen / FindCriticalPoints /
# FarthestPointOrder. The one stencil constant is FROZEN, as it is in the C#.

HESSIAN_STENCIL = 0.01
DEDUPE_DOT = 1.0 - 0.5 * 0.01 * 0.01
ORDER_SHARPNESS_TOL = 1e-6
ORDER_DISTANCE_TOL = 1e-6
ORDER_ANGLE_TOL = 1e-5

SADDLE, PEAK, PIT = 0, 1, 2


class CriticalPoint:
    __slots__ = ("kind", "theta", "phi", "radius", "sharpness", "dir", "e_valley", "e_ridge")

    def __init__(self, kind, theta, phi, radius, sharpness, d, e_valley, e_ridge):
        self.kind, self.theta, self.phi = kind, theta, phi
        self.radius, self.sharpness, self.dir = radius, sharpness, d
        self.e_valley, self.e_ridge = e_valley, e_ridge


def gradient(s, theta, phi):
    hT = math.pi / s.H * 0.75
    hP = 2.0 * math.pi / s.W * 0.75
    sT = max(math.sin(theta), 1e-4)
    gT = (s.sample(theta + hT, phi) - s.sample(theta - hT, phi)) / (2 * hT)
    gP = (s.sample(theta, phi + hP) - s.sample(theta, phi - hP)) / (2 * hP) / sT
    return gT, gP


def hessian(s, theta, phi):
    h = HESSIAN_STENCIL
    a1, b1 = gradient(s, theta + h, phi)
    a0, b0 = gradient(s, theta - h, phi)
    _, b3 = gradient(s, theta, phi + h)
    _, b2 = gradient(s, theta, phi - h)
    gT, _ = gradient(s, theta, phi)
    sT = max(math.sin(theta), 1e-4)
    ct = math.cos(theta)
    htt = (a1 - a0) / (2 * h)
    htp = (b1 - b0) / (2 * h)
    hpp = ((b3 - b2) / (2 * h)) / sT + (ct / sT) * gT
    return htt, htp, hpp


def _eigen_vector(htt, htp, l):
    vx, vy = htp, -(htt - l)
    if abs(vx) < 1e-12 and abs(vy) < 1e-12:
        return (1.0, 0.0)
    m = math.sqrt(vx * vx + vy * vy)
    return (vx / m, vy / m)


def eigen(htt, htp, hpp):
    tr, det = htt + hpp, htt * hpp - htp * htp
    r = math.sqrt(max(0.0, tr * tr * 0.25 - det))
    l1, l2 = tr * 0.5 + r, tr * 0.5 - r
    return l1, l2, _eigen_vector(htt, htp, l1), _eigen_vector(htt, htp, l2)


def _refine_critical_point(s, th, ph):
    for _ in range(12):
        gT, gP = gradient(s, th, ph)
        htt, htp, hpp = hessian(s, th, ph)
        det = htt * hpp - htp * htp
        if abs(det) < 1e-9:
            break
        dTh = -(hpp * gT - htp * gP) / det
        dPh = -(-htp * gT + htt * gP) / det
        n = math.sqrt(dTh * dTh + dPh * dPh)
        if n > 0.05:
            dTh *= 0.05 / n
            dPh *= 0.05 / n
        th += dTh
        ph += dPh / max(math.sin(th), 1e-4)        # the divisor is the NEW theta
        th = min(max(th, 1e-3), math.pi - 1e-3)
        ph = ((ph % (2 * math.pi)) + 2 * math.pi) % (2 * math.pi)
        if n < 1e-7:
            break
    fT, fP = gradient(s, th, ph)
    ok = math.sqrt(fT * fT + fP * fP) <= 2e-3
    return ok, th, ph


def find_critical_points(s):
    """Scan-ordered local minima of |grad R| on a 2x lattice (polar rows excluded),
    Newton-refined, classified, deduplicated on a 0.01 chord."""
    w2, h2 = 2 * s.W, 2 * s.H
    slope = [0.0] * (w2 * h2)
    for j in range(h2):
        th = (j + 0.5) / h2 * math.pi
        for i in range(w2):
            ph = i / w2 * 2 * math.pi
            gT, gP = gradient(s, th, ph)
            slope[j * w2 + i] = math.sqrt(gT * gT + gP * gP)
    found = []
    for j in range(1, h2 - 1):
        for i in range(w2):
            v = slope[j * w2 + i]
            minimum = True
            for dj in (-1, 0, 1):
                for di in (-1, 0, 1):
                    if dj == 0 and di == 0:
                        continue
                    ii = ((i + di) % w2 + w2) % w2
                    if slope[(j + dj) * w2 + ii] <= v:
                        minimum = False
                        break
                if not minimum:
                    break
            if not minimum:
                continue
            ok, th, ph = _refine_critical_point(s, (j + 0.5) / h2 * math.pi, i / w2 * 2 * math.pi)
            if not ok:
                continue
            htt, htp, hpp = hessian(s, th, ph)
            det, tr = htt * hpp - htp * htp, htt + hpp
            kind = SADDLE if det < 0 else (PEAK if tr < 0 else PIT)
            st = math.sin(th)
            d = (st * math.cos(ph), st * math.sin(ph), math.cos(th))
            if any(_dot(d, q.dir) > DEDUPE_DOT for q in found):
                continue
            l1, l2, v1, v2 = eigen(htt, htp, hpp)
            found.append(CriticalPoint(kind, th, ph, s.sample(th, ph),
                                       min(abs(l1), abs(l2)), d, v2, v1))
    return found


def _compare_angles(a, b):
    if abs(a.theta - b.theta) > ORDER_ANGLE_TOL:
        return -1 if a.theta < b.theta else 1
    if abs(a.phi - b.phi) > ORDER_ANGLE_TOL:
        return -1 if a.phi < b.phi else 1
    return 0


def farthest_point_order(saddles):
    order = []
    if not saddles:
        return order
    top = max(c.sharpness for c in saddles)
    s_tol = ORDER_SHARPNESS_TOL * max(top, 1e-12)
    n = len(saddles)
    taken = [False] * n
    first = 0
    for i in range(1, n):
        ds = saddles[i].sharpness - saddles[first].sharpness
        if ds > s_tol or (ds >= -s_tol and _compare_angles(saddles[i], saddles[first]) < 0):
            first = i
    taken[first] = True
    order.append(saddles[first])
    dmin = [1.0 - _dot(saddles[q].dir, saddles[first].dir) for q in range(n)]
    while len(order) < n:
        best = -1
        for i in range(n):
            if taken[i]:
                continue
            if best < 0:
                best = i
                continue
            dd = dmin[i] - dmin[best]
            if dd > ORDER_DISTANCE_TOL:
                best = i
                continue
            if dd < -ORDER_DISTANCE_TOL:
                continue
            ds = saddles[i].sharpness - saddles[best].sharpness
            if ds > s_tol:
                best = i
                continue
            if ds < -s_tol:
                continue
            if _compare_angles(saddles[i], saddles[best]) < 0:
                best = i
        taken[best] = True
        order.append(saddles[best])
        for q in range(n):
            if not taken[q]:
                dmin[q] = min(dmin[q], 1.0 - _dot(saddles[q].dir, saddles[best].dir))
    return order


def critical_points(surface):
    if getattr(surface, "_critical", None) is None:
        surface._critical = find_critical_points(surface)
    return surface._critical


def saddles(surface):
    if getattr(surface, "_saddles", None) is None:
        surface._saddles = farthest_point_order([c for c in critical_points(surface) if c.kind == SADDLE])
    return surface._saddles


def peaks(surface):
    """The PEAKS in farthest-point order: the gasket's level 0 (MandelbulbSurface.Surface.Peaks)."""
    if getattr(surface, "_peaks", None) is None:
        surface._peaks = farthest_point_order([c for c in critical_points(surface) if c.kind == PEAK])
    return surface._peaks


# ── APOLLONIA — the spherical Apollonian gasket (MandelbulbSurface.Growth.BuildGasket) ──

GASKET_RELAX_STEPS = 24
GASKET_OVERLAP_EPS = 1e-4
RING_MIN_SAMPLES = 16
RING_MAX_SAMPLES = 128
GASKET_RELAX_RATE_DEFAULT = 0.6


class Disc:
    # `parent` is the TANGENCY parent — one of the three discs whose gap this one was
    # inscribed in, and therefore a disc it TOUCHES, so the bond a limb has to span is the
    # shortest the gasket offers. -1 on a level-0 disc, which crowns a peak and reaches the
    # crystal through the crown chain instead (MandelbulbSurface.Growth.Disc).
    __slots__ = ("axis", "rho", "level", "lane", "parent")

    def __init__(self, axis, rho, level, lane=0, parent=-1):
        self.axis, self.rho, self.level, self.lane = axis, rho, level, lane
        self.parent = parent


def _angle(a, b):
    return math.acos(min(1.0, max(-1.0, _dot(a, b))))


def _tangent_toward(x, d):
    g = _sub(d, _mul(x, _dot(d, x)))
    return _norm(g) if _dot(g, g) > 1e-14 else (0.0, 0.0, 0.0)


def inscribe(discs, ia, ib, ic, rate):
    """The disc tangent internally to the curvilinear triangle (a, b, c): a fixed-iteration
    relaxation equalising f_i(x) = angle(x, d_i) - rho_i. Sums in the order (a, b, c) and
    normalises once per iteration, like the C#."""
    a, b, c = discs[ia], discs[ib], discs[ic]
    x = _add(_add(a.axis, b.axis), c.axis)
    if _dot(x, x) < 1e-12:
        return None
    x = _norm(x)
    for _ in range(GASKET_RELAX_STEPS):
        fa = _angle(x, a.axis) - a.rho
        fb = _angle(x, b.axis) - b.rho
        fc = _angle(x, c.axis) - c.rho
        target = (fa + fb + fc) / 3.0
        step = (0.0, 0.0, 0.0)
        step = _add(step, _mul(_tangent_toward(x, a.axis), -(target - fa)))
        step = _add(step, _mul(_tangent_toward(x, b.axis), -(target - fb)))
        step = _add(step, _mul(_tangent_toward(x, c.axis), -(target - fc)))
        x = _add(x, _mul(step, rate))
        if _dot(x, x) < 1e-12:
            return None
        x = _norm(x)
    rho = min(_angle(x, a.axis) - a.rho, _angle(x, b.axis) - b.rho, _angle(x, c.axis) - c.rho)
    return x, rho


def build_gasket(surface, rules):
    """Level 0 from the surface's peaks (half the angle to the nearest neighbour), then the
    breadth-first Apollonian step. Returns (discs, lay_order, rho_ref) with every disc's
    lane = its size octave. Every ordering is a TOTAL key; nothing draws the Rng."""
    pk = peaks(surface)
    want = min(rules.disc_seeds, len(pk)) if rules.disc_seeds > 0 else len(pk)
    axes = [_norm(pk[i].dir) for i in range(want)]
    discs = []
    for i, ai in enumerate(axes):
        nn = math.pi / 3.0
        for j, aj in enumerate(axes):
            if j != i:
                nn = min(nn, _angle(ai, aj))
        discs.append(Disc(ai, 0.5 * nn, 0))

    def adjacent(a, b):
        return _angle(discs[a].axis, discs[b].axis) <= discs[a].rho + discs[b].rho + rules.disc_pad

    rate = rules.disc_relax_rate if rules.disc_relax_rate > 0 else GASKET_RELAX_RATE_DEFAULT
    start, count = 0, len(discs)
    levels = max(1, rules.gasket_levels)
    for lvl in range(1, levels):
        cand = []
        n = count
        for a in range(n):
            for b in range(a + 1, n):
                if not adjacent(a, b):
                    continue
                for c in range(b + 1, n):
                    if lvl > 1 and a < start and b < start and c < start:
                        continue
                    if not adjacent(a, c) or not adjacent(b, c):
                        continue
                    r = inscribe(discs, a, b, c, rate)
                    if r is None:
                        continue
                    x, rho = r
                    if rho < rules.disc_min_radius:
                        continue
                    cand.append(Disc(x, rho, lvl, parent=a))
        order = sorted(range(len(cand)), key=lambda i: (-cand[i].rho, i))
        start = count
        for i in order:
            x = cand[i]
            clash = False
            for j in range(count):
                if _angle(x.axis, discs[j].axis) < x.rho + discs[j].rho - GASKET_OVERLAP_EPS:
                    clash = True
                    break
            if clash:
                continue
            discs.append(x)
            count += 1

    rho_ref = 1e-6
    for d in discs:
        rho_ref = max(rho_ref, d.rho)
    lay = sorted(range(len(discs)), key=lambda i: (-discs[i].rho, discs[i].level, i))
    lanes = max(1, rules.gasket_levels)
    octave = rules.gasket_octave if rules.gasket_octave > 0 else 1.0
    for d in discs:
        o = math.log(rho_ref / max(d.rho, 1e-9), 2) / octave
        d.lane = min(lanes - 1, max(0, math.floor(o)))
    return discs, lay, rho_ref


def ring_samples_for(surface, rules, rho_ref):
    if rules.ring_samples > 0:
        return min(RING_MAX_SAMPLES, max(3, rules.ring_samples))
    circ = 2 * math.pi * math.sin(rho_ref) * surface.mean_radius
    n = int(round(circ / max(1e-6, rules.step)))
    return min(RING_MAX_SAMPLES, max(RING_MIN_SAMPLES, n))


def ring_points(surface, d, rho, samples, flatten):
    """The ring of one disc, closed form, CLOSED (the first point repeated)."""
    e1 = _cross(d, (0.0, 0.0, 1.0))
    if _dot(e1, e1) < 1e-10:
        e1 = _cross(d, (1.0, 0.0, 0.0))
    e1 = _norm(e1)
    e2 = _cross(d, e1)
    cr, sr = math.cos(rho), math.sin(rho)
    dirs, radii = [], []
    for k in range(samples):
        a = 2 * math.pi * k / samples
        u = _norm(_add(_mul(d, cr), _mul(_add(_mul(e1, math.cos(a)), _mul(e2, math.sin(a))), sr)))
        th, ph = _spherical(u)
        dirs.append(u)
        radii.append(surface.sample(th, ph))
    mean = sum(radii) / samples
    f = min(1.0, max(0.0, flatten))
    pts = [_mul(dirs[k], radii[k] * (1 - f) + mean * f) for k in range(samples)]
    pts.append(pts[0])
    return pts


# ── the growth rule ────────────────────────────────────────────────────────────

class Rules:
    """Mirrors MandelbulbSurface.GrowthRules field for field, in the same order the
    generator writes them into the prefab. APPEND only: the order is the wire format."""
    FIELDS = ("field", "swirl", "field_mix", "momentum", "step", "max_steps",
              "lanes", "lane_gap", "hop_seek", "hop_jitter", "seeds", "seed_spread",
              "max_turn", "r_min", "r_max", "min_run", "length_factor", "girth_taper",
              "twist",
              # THE FALL (Docs/ECOSYSTEM.md §53)
              "dive_count", "dive_step", "dive_angle", "dive_stop", "dive_max_steps",
              "dive_swirl", "dive_stride_ceiling", "dive_girth_floor", "dive_axis_align",
              "dive_descent",
              # THE WATERSHED (§53)
              "skeleton_seeds", "walk_step", "min_persistence", "girth_reference",
              # APOLLONIA (§54)
              "gasket_levels", "disc_seeds", "disc_pad", "disc_min_radius", "ring_shrink",
              "ring_flatten", "ring_girth_exponent", "ring_samples", "gasket_octave",
              "disc_relax_rate", "ring_girth_floor")
    INTS = ("field", "max_steps", "lanes", "seeds", "min_run",
            "dive_count", "dive_max_steps", "skeleton_seeds",
            "gasket_levels", "disc_seeds", "ring_samples")

    def __init__(self, *values):
        if len(values) != len(self.FIELDS):
            raise ValueError(f"expected {len(self.FIELDS)} rule values, got {len(values)}")
        for k, v in zip(self.FIELDS, values):
            setattr(self, k, v)
        for k in self.INTS:
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
    # `parent` is the index of the prism this one GROWS OUT OF (-1 = the heart). It is a
    # property of the growth ORDER, not of the address, which is why the shipped C# hands it
    # out of TryNext and keeps it out of PrismAddress: a prism's POSE must stay a pure
    # function of its own address.
    __slots__ = ("theta", "phi", "radial", "dive", "tan_a", "tan_b", "tan_r", "length",
                 "girth", "roll", "curve", "lane", "parent", "link")

    def __init__(self, theta, phi, radial, dive, tan_a, tan_b, tan_r, length, girth, roll,
                 curve, lane, parent=-1, link=False):
        self.theta, self.phi, self.radial, self.dive = theta, phi, radial, dive
        self.tan_a, self.tan_b, self.tan_r = tan_a, tan_b, tan_r
        self.length, self.girth = length, girth
        self.roll = roll
        self.curve, self.lane = curve, lane
        self.parent = parent
        # A CONNECTOR prism — a trunk's rise or a stem — rather than one of the curve's own.
        # It is a MODEL-SIDE LABEL: the shipped C# computes the same split (Emit's
        # `_emitConnectorSegments`) and the verifier proves the prisms themselves agree
        # address for address, but nothing in the harness output carries the label, so what
        # is proved is the geometry rather than the naming. Gates that ask a question about
        # a CURVE (where did this arm start, which saddle does it belong to) must drop these
        # or they answer about the limb that fed it.
        self.link = link


def pose(surface, a):
    f = build_frame(surface, a.theta, a.phi)
    n = f.normal
    fwd = _add(_mul(f.e_theta, a.tan_a), _mul(f.e_phi, a.tan_b))
    if a.tan_r != 0.0:
        # A DIVE prism: free-space heading taken whole, `up` hung off the RAY.
        fwd = _add(fwd, _mul(f.dir, a.tan_r))
        m = _len(fwd)
        fwd = _mul(fwd, 1.0 / m) if m > 1e-7 else f.dir
        w = _sub(f.dir, _mul(fwd, _dot(f.dir, fwd)))
        m2 = _len(w)
        if m2 > 1e-5:
            up_base = _mul(w, 1.0 / m2)
        else:
            w = _sub(n, _mul(fwd, _dot(n, fwd)))
            up_base = _norm(w) if _dot(w, w) > 1e-10 else f.e_theta
    else:
        fwd = _sub(fwd, _mul(n, _dot(fwd, n)))
        m = _len(fwd)
        fwd = _mul(fwd, 1.0 / m) if m > 1e-7 else f.e_phi
        up_base = n
    # HELICOIDAL TWIST about the curve's own tangent (MandelbulbSurface.Pose).
    roll = a.roll
    if roll:
        binormal = _cross(fwd, up_base)
        up = _add(_mul(up_base, math.cos(roll)), _mul(binormal, math.sin(roll)))
    else:
        up = up_base
    return _mul(f.dir, f.radius * (1.0 - a.dive) + a.radial), fwd, up


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
    if rules.skeleton_seeds != 0:
        return [build_frame(surface, c.theta, c.phi).position for c in saddles(surface)]
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


def _field_direction(rules, frame, n, field):
    f = field
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
    if rules.skeleton_seeds == 0 and abs(rules.swirl) > 0.01:     # a separatrix has no swirl
        ang = rules.swirl * math.pi / 180.0
        c, s = math.cos(ang), math.sin(ang)
        v = _add(_add(_mul(v, c), _mul(_cross(n, v), s)),
                 _mul(n, _dot(n, v) * (1 - c)))
        v = _norm(v)
    return v


def append_dive(rules, p, t, step):
    """THE FALL: the log spiral (MandelbulbSurface.Growth.AppendDive)."""
    out = []
    psi = min(85.0, max(5.0, rules.dive_angle)) * math.pi / 180.0
    cps, sps = math.cos(psi), math.sin(psi)
    f = min(rules.dive_step, 0.9 * 2.0 * cps)
    stop = max(0.01, rules.dive_stop)
    align = min(1.0, max(0.0, rules.dive_axis_align))
    swirl = abs(rules.dive_swirl) > 0.01
    if swirl:
        a = rules.dive_swirl * math.pi / 180.0
        swirl_cos, swirl_sin = math.cos(a), math.sin(a)
    cap = max(2, rules.dive_max_steps)
    for _ in range(cap):
        r = _len(p)
        if r <= stop:
            break
        rhat = _mul(p, 1.0 / r)
        u = _sub(t, _mul(rhat, _dot(t, rhat)))
        if _dot(u, u) < 1e-12:
            u = _cross(rhat, (0.0, 0.0, 1.0))
            if _dot(u, u) < 1e-12:
                u = _cross(rhat, (1.0, 0.0, 0.0))
        u = _norm(u)
        if align > 0.0:
            az = _cross((0.0, 0.0, 1.0), rhat)
            if _dot(az, az) > 1e-12:
                az = _norm(az)
                if _dot(az, u) < -1e-3:      # dead band: see MandelbulbSurface.AppendDive
                    az = _mul(az, -1.0)
                blended = _add(_mul(u, 1.0 - align), _mul(az, align))
                if _dot(blended, blended) > 1e-12:
                    u = _norm(blended)
        t = _add(_mul(rhat, -cps), _mul(u, sps))
        if swirl:
            t = _norm(_add(_add(_mul(t, swirl_cos), _mul(_cross(rhat, t), swirl_sin)),
                           _mul(rhat, _dot(rhat, t) * (1.0 - swirl_cos))))
        s = f * r
        if rules.dive_stride_ceiling > 0.0:
            s = min(s, step * rules.dive_stride_ceiling)
        q = _add(p, _mul(t, s))
        rq = _len(q)
        if rq < stop:
            q = _mul(q, stop / max(rq, 1e-9))
            out.append(q)
            break
        p = q
        out.append(p)
    return out


# How close two seed-tree costs have to be before the INDEX decides. A Mandelbulb is highly
# symmetric, so its seeds come in ORBITS whose members sit at distances equal to the last bit
# (measured: four of Space's saddles were exactly 0.847114625 from the tree so far), and which
# one a greedy pick admits first is then decided by float WIDTH — the shipped float32 and this
# float64 model grew visibly different plants from the same seed. Same trap as Apollonia's lay
# order (Docs/ECOSYSTEM.md §54: every ordering is a TOTAL key), met from a second direction.
# See MandelbulbSurface.Growth.SeedCostEpsilon.
SEED_COST_EPSILON = 1e-5


def free_space_runs(prisms):
    """Split each curve's FREE-SPACE prisms into its leading RISE and its trailing DIVE,
    as index lists into `prisms`.

    `tan_r != 0` is the address field that says "this prism is in free space", and it used
    to say "this prism is in the dive" as well — true only while the heart was reached at the
    END of a curve and nowhere else. Since these species grow OUT of the crystal
    (Docs/ECOSYSTEM.md §50.6) the TRUNK is a rise: the same log spiral, run backwards, laid
    BEFORE the surface walk. So the separator is structural rather than a field test, and it
    is exact: within one curve `emit` lays rise, then surface, then dive, in that order, so
    the rise is a PREFIX and the dive a SUFFIX.

    Reading a rise as a dive is not a small error — a trunk runs from the heart OUT to the
    surface, so a gate that measures "where did this dive end" answers with the surface and
    reports the plant's one connection to its crystal as the one spiral that never arrived."""
    rise, dive = [], []
    start = 0
    n = len(prisms)
    while start < n:
        end = start
        curve = prisms[start].curve
        while end < n and prisms[end].curve == curve:
            end += 1
        i = start
        while i < end and prisms[i].tan_r != 0.0:
            i += 1
        j = end
        while j > i and prisms[j - 1].tan_r != 0.0:
            j -= 1
        rise.extend(range(start, i))
        dive.extend(range(j, end))
        start = end
    return rise, dive


def build_seed_tree(seeds):
    """A spanning tree over the seeds, rooted at seed 0 — Prim on great-circle distance
    (MandelbulbSurface.Growth.BuildSeedTree). Returns (parent, order); Prim's INSERTION
    ORDER is itself a valid growth order, because it only ever admits a seed whose parent
    is already in the tree, so `parent appears earlier than child` holds by construction."""
    n = len(seeds)
    parent = [-1] * n
    order = [0] * n
    if n == 0:
        return parent, order
    dirs = [_norm(v) if _dot(v, v) > 1e-18 else (0.0, 0.0, 1.0) for v in seeds]
    in_tree = [False] * n
    best = [float("inf")] * n
    best_from = [0] * n
    in_tree[0] = True
    for i in range(1, n):
        best[i] = -_dot(dirs[0], dirs[i])       # monotone in great-circle distance
    for placed in range(1, n):
        pick, pick_cost = -1, float("inf")
        for i in range(n):
            # Strictly cheaper by more than the epsilon, or tied and earlier in the table.
            if in_tree[i]:
                continue
            if pick >= 0 and best[i] >= pick_cost - SEED_COST_EPSILON:
                continue
            pick, pick_cost = i, best[i]
        if pick < 0:                            # degenerate (coincident) seed directions
            for i in range(n):
                if not in_tree[i]:
                    pick = i
                    break
        if pick < 0:
            break
        in_tree[pick] = True
        parent[pick] = best_from[pick]
        order[placed] = pick
        for i in range(n):
            if in_tree[i]:
                continue
            c = -_dot(dirs[pick], dirs[i])
            # Must IMPROVE by more than the epsilon: a tie keeps the parent already recorded.
            if c < best[i] - SEED_COST_EPSILON:
                best[i], best_from[i] = c, pick
    return parent, order


def build_gasket_tree(discs, lay):
    """The gasket's tree IS the gasket (MandelbulbSurface.Growth.BuildGasketTree): a child
    disc is inscribed against three discs it touches, so it already knows what it grows out
    of. Only the level-0 crowns need a rule, and theirs is the nearest crown laid before."""
    n = len(discs)
    parent = [-1] * n
    order = list(lay)
    placed_crowns = []
    for d in lay:
        if discs[d].level != 0:
            parent[d] = discs[d].parent
            continue
        best, best_cos = -1, -2.0
        for i in placed_crowns:
            c = _dot(discs[d].axis, discs[i].axis)
            if c > best_cos + SEED_COST_EPSILON:
                best_cos, best = c, i
        parent[d] = best                       # -1 for the first crown: the trunk
        placed_crowns.append(d)
    return parent, order


def grow(surface, rules, seed, budget):
    """Lane-MAJOR: every seed lays its lane 0 before any seed lays its lane 1, so a plant
    that stops at its budget is the whole bulb drawn thinly rather than two seeds' worth of
    it drawn fully. Under `skeleton_seeds` the seeds are the surface's saddles and lanes
    0..3 are their four separatrices, interleaved valley+, ridge+, valley-, ridge-."""
    rng = Rng((seed * (2654435761 & 0x7FFFFFFF)) ^ 0x5bf03635)
    skeleton = rules.skeleton_seeds != 0
    gasket = rules.gasket_levels != 0
    if gasket:
        discs, lay, rho_ref = build_gasket(surface, rules)
        seeds = [build_frame(surface, *_spherical(d.axis)).position for d in discs]
        seed_parent, seed_order = build_gasket_tree(discs, lay)
    else:
        seeds = build_seeds(surface, rules)
        seed_parent, seed_order = build_seed_tree(seeds)
    sads = saddles(surface) if skeleton else None
    # THE PLANT'S SKELETON (MandelbulbSurface.Growth): where each seed's stem LANDS, and the
    # prism at the midpoint of its last run — which is exactly the point `hop` steps across
    # from, so lane L hangs off lane L-1 over a bond one hop long.
    seed_arrival = [-1] * len(seeds)
    lane_anchor = [-1] * len(seeds)
    # WHERE that arrival prism is. A stem must start at the prism it hangs off, not at the
    # seed's own point: for the walking species and the Watershed those are the same place (a
    # curve starts AT its seed), but a gasket seed is a disc CENTRE while its curve starts on
    # the disc's RIM, so a stem from the centre leaves the first limb spanning the parent
    # disc's whole radius with nothing in it (MandelbulbSurface.Growth._seedArrivalPoint).
    seed_arrival_point = [(0.0, 0.0, 0.0)] * len(seeds)
    lane_pos = list(seeds)
    lane_tan = [None] * len(seeds)
    lane_dead = [False] * len(seeds)
    max_turn_cos = math.cos(min(179.0, max(1.0, rules.max_turn)) * math.pi / 180.0)
    r_min, r_max = min(rules.r_min, rules.r_max), max(rules.r_min, rules.r_max)
    lanes = max(1, rules.lanes)
    walk_step = rules.walk_step if rules.walk_step > 0 else rules.step

    # THE FALL's owed set: STRIDED over the seed list, never a prefix (the seed list is
    # z-monotone, so a prefix is a polar cap).
    n_seeds = max(1, len(seeds))
    dive_owed = [False] * n_seeds
    dives_spent = 0
    if gasket:
        # The gasket strides over its LEVEL-0 discs in LAY order (the largest rings, laid first).
        top = [i for i in lay if discs[i].level == 0]
        dive_quota = min(max(0, rules.dive_count), len(top))
        if rules.dive_step > 0 and dive_quota > 0:
            for d in range(dive_quota):
                dive_owed[top[(d * len(top)) // dive_quota]] = True
    else:
        # Over the SEED LIST, which is z-monotone and therefore spread by construction, never
        # over the lay order — Prim's order is spatially coherent and a stride over a
        # nearest-neighbour walk spreads nothing (MandelbulbSurface.Growth.EnsureSeeds).
        dive_quota = min(max(0, rules.dive_count), len(seeds))
        if rules.dive_step > 0 and dive_quota > 0:
            for d in range(dive_quota):
                dive_owed[(d * len(seeds)) // dive_quota] = True

    def trace(start, seed_tangent, field, step):
        pts = []
        theta, phi = _spherical(start)
        t = None
        for _ in range(max(2, rules.max_steps)):
            fr = build_frame(surface, theta, phi)
            if fr.radius < r_min or fr.radius > r_max:
                break
            n = fr.normal
            fd = _field_direction(rules, fr, n, field)
            if t is not None:
                ahead = _sub(t, _mul(n, _dot(t, n)))
                if _dot(ahead, ahead) < 1e-16:
                    break
                ahead = _norm(ahead)
                if fd is not None:
                    d = fd
                    if _is_bipolar(field) and _dot(d, ahead) < 0:
                        d = _mul(d, -1.0)
                    # A separatrix is PURE gradient flow by construction (MandelbulbSurface.Trace).
                    mix = 1.0 if skeleton else min(1.0, max(0.0, rules.field_mix))
                    want = tuple(ahead[i] + (d[i] - ahead[i]) * mix for i in range(3))
                else:
                    want = ahead
                if _dot(want, want) < 1e-16:
                    break
                want = _norm(want)
                mom = 0.0 if skeleton else min(1.0, max(0.0, rules.momentum))
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
            q = _add(fr.position, _mul(t, step))
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
    # Emit SKIPS a zero-length segment, so "the nth point" and "the nth prism" are not the
    # same number. These are set before a batch and written back by it.
    anchor_for_next_emit = -1
    emit_connector_segments = 0
    emit_mark_point = 1 << 60
    emit_connector_end = -1
    emit_marked_prism = -1
    taper = rules.girth_taper if rules.girth_taper > 0 else 1.0
    reference = (max(2.0, rules.girth_reference) if rules.girth_reference > 0
                 else max(2.0, rules.max_steps * 0.5))
    twist_per_step = math.radians(rules.twist)
    g_floor = rules.dive_girth_floor if rules.dive_girth_floor > 0 else 1.0
    lg_lo = math.log(max(1e-6, max(0.01, rules.dive_stop)))

    def try_dive(k, pts):
        nonlocal dives_spent
        if rules.dive_step <= 0 or not dive_owed[k] or dives_spent >= dive_quota:
            return []
        if rules.dive_descent > 0 and _len(pts[0][0]) - _len(pts[-1][0]) < rules.dive_descent:
            return []
        end = pts[-1][0]
        tail = _sub(end, pts[-2][0])
        if _dot(tail, tail) <= 1e-18:
            return []
        # A gasket hands the dive the ring's own chord (the element's step), so walk_step stays
        # inert on that species (MandelbulbSurface.Growth.TryDive).
        dive = append_dive(rules, end, _norm(tail), rules.step if gasket else walk_step)
        if dive:
            dive_owed[k] = False
            dives_spent += 1
        return dive

    def emit(rise, pts, dive, girth_override=0.0):
        """One curve becomes prisms, and the chain of them is the plant's SKELETON — so this
        is also where a prism learns what it hangs off. Within a batch every prism's parent
        is its predecessor (a curve IS a chain); the batch's FIRST hangs off
        `anchor_for_next_emit` (MandelbulbSurface.Growth.Emit).

        The RISE is the Fall run backwards: free-space points laid BEFORE the surface run,
        carrying the curve out of the heart to where the surface walk begins."""
        nonlocal curve_count, emit_connector_end, emit_marked_prism, emit_connector_segments
        nonlocal emit_mark_point
        emit_connector_end = -1
        emit_marked_prism = -1
        base = len(out)
        u = min(1.0, max(0.0, (len(pts) - 1) / reference))
        girth = girth_override if girth_override > 0 else taper + (1.0 - taper) * u
        n_surface = len(pts)
        n_rise = len(rise)
        flat = list(rise) + [p[0] for p in pts] + list(dive)
        lg_hi = math.log(max(1e-6, _len(pts[-1][0])))
        dive_roll0 = 0.0
        # The rise pays the SAME seam the Fall does, and pays it UP FRONT: its prisms are
        # emitted BEFORE the transition that defines the angle.
        rise_roll0 = 0.0
        if n_rise > 0:
            ra, rb = flat[n_rise - 1], pts[0][0]
            rd = _sub(rb, ra)
            if _dot(rd, rd) > 1e-14:
                rfwd = _norm(rd)
                rfr = build_frame(surface, *_spherical(_mul(_add(ra, rb), 0.5)))
                up_ray = _sub(rfr.dir, _mul(rfwd, _dot(rfr.dir, rfwd)))
                up_surf = _sub(rfr.normal, _mul(rfwd, _dot(rfr.normal, rfwd)))
                if _dot(up_ray, up_ray) > 1e-10 and _dot(up_surf, up_surf) > 1e-10:
                    up_ray, up_surf = _norm(up_ray), _norm(up_surf)
                    rise_roll0 = math.atan2(_dot(_cross(up_ray, up_surf), rfwd),
                                            _dot(up_ray, up_surf))
        for i in range(len(flat) - 1):
            a, b = flat[i], flat[i + 1]
            delta = _sub(b, a)
            ln = _len(delta)
            if ln < 1e-7:
                continue
            fwd = _mul(delta, 1.0 / ln)
            centre = _mul(_add(a, b), 0.5)
            th, ph = _spherical(centre)
            fr = build_frame(surface, th, ph)
            cm = _len(centre)
            is_dive = len(dive) > 0 and i >= n_rise + n_surface - 1
            is_rise = i < n_rise
            g = girth
            roll = i * twist_per_step
            if is_rise:
                tan_r = _dot(fwd, fr.dir)
                dv = 1.0 - cm / max(1e-6, fr.radius)
                off = 0.0
                wr = 1.0 if lg_hi <= lg_lo else min(1.0, max(0.0, (math.log(max(1e-6, cm)) - lg_lo) / (lg_hi - lg_lo)))
                g = girth * (g_floor + (1.0 - g_floor) * wr)
                roll += rise_roll0
            elif is_dive:
                tan_r = _dot(fwd, fr.dir)
                dv = 1.0 - cm / max(1e-6, fr.radius)
                off = 0.0
                w = 1.0 if lg_hi <= lg_lo else min(1.0, max(0.0, (math.log(max(1e-6, cm)) - lg_lo) / (lg_hi - lg_lo)))
                g = girth * (g_floor + (1.0 - g_floor) * w)
                if i == n_surface - 1:
                    up_ray = _sub(fr.dir, _mul(fwd, _dot(fr.dir, fwd)))
                    up_surf = _sub(fr.normal, _mul(fwd, _dot(fr.normal, fwd)))
                    if _dot(up_ray, up_ray) > 1e-10 and _dot(up_surf, up_surf) > 1e-10:
                        up_ray, up_surf = _norm(up_ray), _norm(up_surf)
                        dive_roll0 = math.atan2(_dot(_cross(up_ray, up_surf), fwd), _dot(up_ray, up_surf))
                roll += dive_roll0
            else:
                tan_r, dv, off = 0.0, 0.0, cm - fr.radius
            parent = anchor_for_next_emit if len(out) == base else len(out) - 1
            here = len(out)
            if i < emit_connector_segments:
                emit_connector_end = here
            if emit_marked_prism < 0 and i >= emit_mark_point:
                emit_marked_prism = here
            out.append(Prism(th, ph, off, dv,
                             _dot(fwd, fr.e_theta), _dot(fwd, fr.e_phi), tan_r,
                             ln * max(0.05, rules.length_factor),
                             g, roll, curve_count, lane, parent,
                             i < emit_connector_segments))
        # A request is for ONE batch; a stale split would make the next curve's prisms read
        # as somebody's connector.
        emit_connector_segments = 0
        emit_mark_point = 1 << 60

    def build_trunk(seed_point, seed_tangent, step):
        """THE TRUNK — the Fall run backwards, from the heart out to the root seed. Reusing
        append_dive verbatim is the point: the rise cannot acquire a shape, a stride ceiling
        or a winding the Fall does not already have."""
        d = append_dive(rules, seed_point, seed_tangent or (0.0, 0.0, 0.0), step)
        # Reversed, and WITHOUT the seed itself: the curve's own first point is the seed.
        return list(reversed(d))

    def build_stem(a_pos, b_pos, step):
        """A STEM — the surface path from one seed to the next, a great-circle interpolation
        evaluated ON the membrane. The final point is left OFF: the curve that follows starts
        at the seed, and the stem's last segment is what arrives there."""
        a = _norm(a_pos) if _dot(a_pos, a_pos) > 1e-18 else (0.0, 0.0, 1.0)
        b = _norm(b_pos) if _dot(b_pos, b_pos) > 1e-18 else (0.0, 0.0, 1.0)
        cos = min(1.0, max(-1.0, _dot(a, b)))
        ang = math.acos(cos)
        arc = ang * max(1e-4, (_len(a_pos) + _len(b_pos)) * 0.5)
        steps = min(256, max(1, int(arc / max(1e-4, step)) + 1))
        sin_ang = math.sin(ang)
        run = []
        for i in range(steps):
            t = float(i) / steps
            if sin_ang > 1e-5:
                d = _add(_mul(a, math.sin((1.0 - t) * ang) / sin_ang),
                         _mul(b, math.sin(t * ang) / sin_ang))
            else:
                d = _add(_mul(a, 1.0 - t), _mul(b, t))
            if _dot(d, d) < 1e-18:
                continue
            fr = build_frame(surface, *_spherical(_norm(d)))
            run.append((_mul(fr.dir, fr.radius), fr.normal))
        return run

    def attachment_seed(k):
        """Which already-STANDING seed this one grows out of: its tree parent when that
        parent is standing, else the nearest standing seed by angle (a plant may not start a
        second component just because one branch failed). -1 only while nothing is standing,
        which happens exactly once per plant and is what the trunk is for."""
        parent = seed_parent[k] if k < len(seed_parent) else -1
        if 0 <= parent < len(seed_arrival) and seed_arrival[parent] >= 0:
            return parent
        me = seeds[k] if k < len(seeds) else (0.0, 0.0, 1.0)
        md = _norm(me) if _dot(me, me) > 1e-18 else (0.0, 0.0, 1.0)
        best, best_cos = -1, -2.0
        for i in range(min(len(seed_arrival), len(seeds))):
            if i == k or seed_arrival[i] < 0:
                continue
            d = seeds[i]
            if _dot(d, d) < 1e-18:
                continue
            c = _dot(md, _norm(d))
            if c > best_cos + SEED_COST_EPSILON:
                best_cos, best = c, i
        return best

    def prepare_connector(k, pts, step):
        """Decide what a curve hangs off and build the run that gets it there. Returns
        (rise, run, leading connector SEGMENT count)."""
        nonlocal anchor_for_next_emit, emit_connector_segments
        if lane_anchor[k] >= 0:
            # A later lane is one hop across from this seed's previous run. That hop IS the
            # bond; the prism at its origin was marked when that run was emitted.
            anchor_for_next_emit = lane_anchor[k]
            emit_connector_segments = 0
            return [], list(pts), 0
        # THIS SEED'S FIRST APPEARANCE — which is usually lane 0, but a seed whose early
        # curves were all dust makes it on a later lane, so the test is "have I laid anything
        # yet" rather than "is this lane 0".
        frm = attachment_seed(k)
        if frm >= 0:
            stem = build_stem(seed_arrival_point[frm], pts[0][0], step)
            rise = []
            anchor_for_next_emit = seed_arrival[frm]
        else:
            # THE TRUNK: exactly one curve per plant takes this branch, and it is the only
            # prism in the plant whose parent is the crystal itself.
            rise = build_trunk(pts[0][0], lane_tan[k], step)
            stem = []
            anchor_for_next_emit = -1
        run = list(stem) + list(pts)
        connector = len(rise) + len(stem)
        emit_connector_segments = connector
        return rise, run, connector

    if gasket:
        # APOLLONIA: one ring per disc in rho-descending order; the lane IS the size octave.
        samples = ring_samples_for(surface, rules, rho_ref)
        shrink = rules.ring_shrink if rules.ring_shrink > 0 else 1.0
        for idx in lay:
            if len(out) >= budget:
                break
            d = discs[idx]
            lane = d.lane
            pts = [(p, (0.0, 0.0, 0.0)) for p in
                   ring_points(surface, d.axis, d.rho * shrink, samples, rules.ring_flatten)]
            girth = (d.rho / rho_ref) ** rules.ring_girth_exponent if rules.ring_girth_exponent > 0 else 1.0
            girth = max(girth, min(1.0, max(0.0, rules.ring_girth_floor)))
            curve_count += 1
            # A ring arrives over a connector like every other curve: a STEM from the disc it
            # is inscribed against (a disc it TOUCHES, so the stem is short) or the TRUNK out
            # of the crystal for the first crown. The ring is then a closed chain hanging off
            # that arrival, which is what makes the gasket ONE object rather than a spray of
            # hoops.
            save_lane = lane
            lane = 0
            rise, run, connector = prepare_connector(idx, pts, rules.step)
            lane = save_lane
            base = len(out)
            emit_mark_point = connector
            emit(rise, run, try_dive(idx, pts), girth)
            seed_arrival[idx] = emit_connector_end if emit_connector_end >= 0 else base
            seed_arrival_point[idx] = pts[0][0]
        return out[:budget], curve_count, dives_spent

    while lane < lanes and len(out) < budget:
        progressed = False
        # TREE order, not table order: a seed's stem grows out of the seed it hangs off, so
        # its parent must already carry prisms. Prim's insertion order gives that by
        # construction (build_seed_tree).
        for oi in range(len(seeds)):
            k = seed_order[oi]
            if len(out) >= budget:
                break
            if skeleton:
                if lane >= 4:
                    break
                progressed = True
                sad = sads[k]
                ascend = (lane & 1) == 1
                sign = 1.0 if lane < 2 else -1.0
                field = ASCENT if ascend else DESCENT
                fr = build_frame(surface, sad.theta, sad.phi)
                e = sad.e_ridge if ascend else sad.e_valley
                tan = _mul(_add(_mul(fr.e_theta, e[0]), _mul(fr.e_phi, e[1])), sign)
                pts = trace(fr.position, tan, field, walk_step)
                if len(pts) < max(2, rules.min_run):
                    continue
                if rules.min_persistence > 0 and abs(_len(pts[-1][0]) - _len(pts[0][0])) < rules.min_persistence:
                    continue
                curve_count += 1
                # All four arms leave the SAME saddle, so the first arm laid is what the
                # other three hang off — and the saddle itself arrives over a connector. A
                # saddle whose first arm was dust has no arrival prism yet, so the next arm
                # to survive pays for the connector: the test is on seed_arrival, not lane.
                first = seed_arrival[k] < 0
                if first:
                    save_lane = lane
                    lane = 0
                    rise, run, connector = prepare_connector(k, pts, walk_step)
                    lane = save_lane
                else:
                    anchor_for_next_emit = seed_arrival[k]
                    rise, run, connector = [], list(pts), 0
                    emit_connector_segments = 0
                base = len(out)
                emit(rise, run, try_dive(k, pts))
                if first:
                    seed_arrival[k] = emit_connector_end if emit_connector_end >= 0 else base
                    seed_arrival_point[k] = pts[0][0]
                continue
            if lane_dead[k]:
                continue
            progressed = True
            pts = trace(lane_pos[k], lane_tan[k], rules.field, walk_step)
            if len(pts) >= max(2, rules.min_run):
                curve_count += 1
                # Lane 0 is the seed's first appearance, so it arrives over a connector — the
                # TRUNK out of the heart for the root seed, a STEM from its parent seed for
                # every other. Lane L>0 starts one hop across from lane L-1's midpoint.
                rise, run, connector = prepare_connector(k, pts, walk_step)
                mid = len(pts) // 2
                emit_mark_point = connector + mid
                base = len(out)
                emit(rise, run, try_dive(k, pts))
                if seed_arrival[k] < 0:
                    seed_arrival[k] = emit_connector_end if emit_connector_end >= 0 else base
                    seed_arrival_point[k] = pts[0][0]
                if emit_marked_prism >= 0:
                    lane_anchor[k] = emit_marked_prism
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
    return out[:budget], curve_count, dives_spent


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
# ── TWO SPECIES, ONE GROWTH RULE ──────────────────────────────────────────────────────
#
# Both trace curves over a baked spherical height field. They differ in ONE thing each,
# and that thing is the concept (Docs/ECOSYSTEM.md §52):
#
#   FRACTAL FOLIAGE  every prism ROLLS about its own curve tangent as the run advances, so
#                    a curve is a helix of plates rather than a flat band. Short, busy,
#                    many-laned runs: a dense twisted foliage.
#   CORAL BLOOM      no twist at all, and the curves are made to CONTINUE — high momentum,
#                    a low field mix, a long step ceiling and a long MINIMUM run, so only
#                    curves that traverse the structure survive and the ones that do cross
#                    each other. Fewer lanes and a wider gap: an open cage of smooth arcs.
#
# They share the surface family (one bake per element), which is deliberate: the two are
# visibly the same WORLD grown two different ways, which is what makes them read as two
# plants in one biome rather than two unrelated objects.
#
# Each species authors ONE neutral form and four CURVE FAMILIES. The four elemental prisms
# are DERIVED from the neutral by the fleet law (Docs/ECOSYSTEM.md §51) rather than typed
# per element - which is the whole point: the concept persists through all four elements
# while each element expresses itself.

# The fleet law, as ratios against TIME (the neutral form - Time's identity is the clock,
# not a shape). Read straight off FloraElementalForm's shipped constants:
#   Mass  1.8347 / 0.8778 = 2.0901      Space 0.4097 / 0.8778 = 0.4667
ELEMENT_VOLUME = {"Charge": 1.0, "Mass": 2.0901, "Space": 0.4667, "Time": 1.0}
ELEMENT_ANISOTROPY = {"Charge": 1.0, "Mass": 0.45, "Space": 2.11, "Time": 1.0}

# CHARGE is fitted against its ARMOUR, not its box: a Charge plant's leaves are shielded by
# law and a shield engages the octahedron CIRCUMSCRIBING the prism, reaching 1.5 x leafSize
# (Docs/ECOSYSTEM.md §35). Two dials, both fitted by measure_mandelbulb_flora.py --shields:
# a UNIFORM cross-section shrink (uniform so the species' own leaf ASPECT survives, which is
# what §51 requires of Charge), and a DASH - the prism laid shorter than the step that spaces
# it, which is the only lever that reaches fusion along a ribbon's OWN chain.
CHARGE_SHIELD_SHRINK = 0.60
CHARGE_DASH = 0.45

# GIRTH COMPENSATION - the one number here that is FITTED rather than derived, and the
# reason it has to exist is worth stating: the law sets the AUTHORED prism, but what a
# player sees is the plant, and every prism's cross-section is additionally multiplied by
# its curve's GIRTH - a taper keyed on how far that run got (Docs/ECOSYSTEM.md §50). The
# mean girth is therefore emergent from the curve family, it differs per element because
# the four curve families differ on purpose, and measured it INVERTED the ordering the law
# had just set (Space's long clean runs all reached full girth while Time's short fall
# lines sat near the taper floor, so Space's plant carried 1.3x Time's cumulative volume
# against an authored 0.47x).
#
# So each element carries one measured scalar on its cross-section that cancels its own
# mean girth, and the ordering the user can actually see - cumulative prism volume per
# PLANT - is then true rather than approximately true. Volume goes as the cross-section
# SQUARED (girth multiplies x and y, never the length), so the gain is a square root.
# Solved by `measure_mandelbulb_flora.py --fit-volume`; re-run it after ANY curve-family
# change, because that is what moves the mean girth.
# Measured. Note the SHAPE of these two rows, which is the finding rather than the numbers:
# FRACTAL FOLIAGE needs a real correction (0.60-1.01) because its four curve families are
# deliberately very different - a fall-line anemone and an open geodesic cage do not produce
# the same run-length distribution - while CORAL BLOOM barely moves (0.98-1.06) because its
# concept makes all four families uniformly long-running. A species whose elements differ a
# lot in HOW they grow will need this fit; one whose concept is the same growth everywhere
# very nearly does not.
VOLUME_GAIN = {
    "FractalFoliage": {"Charge": 0.7521, "Mass": 1.015,  "Space": 0.5995, "Time": 1.0},
    "CoralBloom":     {"Charge": 0.9369, "Mass": 0.7338, "Space": 0.8659, "Time": 1.0},
}

# Fields:      field swirl mix  mom  step  steps lanes gap  seek jit seeds spread turn rmin rmax run lenf taper twist
#
# `step`, `length_factor` and the cross-section are OVERWRITTEN per element by the law
# below - they are the prism, and the prism is what the elements redistribute. Everything
# else is the curve family, which is authored, because "the four read as four plants" is
# richness the law is deliberately silent about.
# THE FALL's mechanism, shared by every curve family of every species (the expression axes
# are per species: psi, the axis alignment, the swirl, the count). Docs/ECOSYSTEM.md §53.
FALL_SHARED = {
    "dive_step": 0.40,            # f: step as a fraction of the radius -> rho = sqrt(1 - 2f cos psi + f^2)
    "dive_stop": 0.045,           # 3.4 world units at shell 75, against a ~1.2 u crystal half-extent
    "dive_max_steps": 160,        # the descent needs ~80 steps under the stride ceiling
    "dive_stride_ceiling": 1.10,  # the dive's step may not exceed 1.1x the walk step: measured, without
                                  # a ceiling the prisms after the release are f*r long, 13x a surface
                                  # prism, and at 2.0 the longest dive prism out-ran every plant's longest
                                  # surface prism (a surface prism is the frame-to-frame CHORD, ~1.05-1.20x
                                  # the step) - the FALL stride gate, solved per element, wants <= 1.14
    "dive_girth_floor": 0.70,
}

SPECIES = {
    "FractalFoliage": dict(
        prefab="MandelbulbFlora",
        display="Mandelbulb Flora",
        # The concept: a helicoidal roll, accumulated step by step along each run.
        twist=12.0,
        girth_taper=0.40,
        # The neutral form (what TIME grows): cross-section across/through the curve, in
        # SURFACE units, and the step that is also the prism's length.
        neutral_cross=(0.0450, 0.0170),
        neutral_step=0.030,
        weight_spread=1.0,
        # THE FALL (Docs/ECOSYSTEM.md §53): its dives corkscrew, doubling down on the helicoid.
        #
        # CHARGE authors a WALK STEP, and it is an ARMOUR fit rather than a curve-family
        # choice. A Charge prism is laid at CHARGE_DASH x the chord that spaces it and its
        # shield reaches 1.5 x leafSize, so an armoured chain clears along its own axis only
        # while `3 x LengthFactor < 1` - and `rules_for` sets `LengthFactor = CHARGE_DASH x
        # step/walk_step`, which at walk_step 0 (= the law's step) is 0.45 and fuses. The
        # Fall is where that bites, because inside 0.15 R a dive is ALONE: measured, 59 of
        # the 60 interpenetrating armoured pairs in the core are one dive's own consecutive
        # prisms, so the core reads as a rod. 0.045 puts the ratio at 0.30, a clean 10%
        # under the 1/3 cliff (measured: 0.040 -> 38.7%, 0.042 -> 1.6%), and takes the core
        # from 45.8% to 4.2%. It is NOT reachable from the dive's own dials - the overlap
        # condition is `3 x LengthFactor` whatever the stride, which is why DiveStopRadius,
        # DiveGirthFloor and DiveCount all measured inert or worse (the girth floor thins
        # the cross-section, and an octahedron pair separated along its own axis does not
        # care about the cross-section: the count stayed at 59 while the denominator fell).
        # The plant reads as a BIGGER-dashed bead-work cage and the octahedra still close
        # 90% of the gap they are left.
        extra={
            "*": FALL_SHARED | {"dive_swirl": 8.0, "dive_axis_align": 1.0},
            "Charge": {"dive_count": 8, "dive_angle": 52, "walk_step": 0.045},
            "Mass":   {"dive_count": 8, "dive_angle": 58},
            "Space":  {"dive_count": 8, "dive_angle": 50},
            # SEVEN dives, not eight: at eight the dive share is 24.9% against a 25% ceiling,
            # which is a pass with no margin on the one member of the family the gate sees.
            "Time":   {"dive_count": 7, "dive_angle": 62},
        },
        curves={
            #           field swirl mix  mom  step  steps lanes gap  seek jit seeds spread turn rmin rmax run lenf taper twist
            "Charge": (0, 25, 0.90, 0.35, 0.040, 130, 60, 0.30, 0.3, 0.30, 70, 10, 30, 0.3, 2.0,  8, 0.45, 0.40, 0),
            "Mass":   (0, 55, 0.85, 0.35, 0.038, 140, 70, 0.34, 0.4, 0.25, 80,  9, 30, 0.3, 2.0,  8, 1.0, 0.40, 0),
            # SPACE's seed count is sized to the number of curves its budget can PAY FOR.
            # Its struts are ~149 prisms, so 2,800 buys ~22 curves however many seeds are
            # authored - and the Fall's owed set STRIDES the seed list, so a list three
            # times longer than the plant degenerates into a prefix: measured, 8 dives were
            # spent and only 3 landed inside the budget (38%, bound 60%). At 32 the stride
            # lands inside the laid plant and 7 of 8 arrive. The count is chosen over ELEVEN
            # members of the family rather than on the one the gate measures, because the
            # arrival turns out to be number-theoretic in (seeds, DiveCount, curves the
            # budget buys) rather than smooth: 32 holds 6-8 of 8 across the family, 36 drops
            # to 5, and 28 lands EVERY member on 4 (its stride puts three owners past the
            # last curve the budget pays for).
            "Space":  (5,  0, 0.00, 0.55, 0.050, 150, 55, 0.34, 0.2, 0.40, 32, 11, 26, 0.3, 2.1, 10, 1.0, 0.45, 0),
            # TIME's ascent is SWIRLED 15 deg. Pure steepest-ascent locks onto the power-12
            # bulb's terrace risers, which are steeper than 45 deg, so 81% of its surface
            # prisms pointed within 45 deg of the ray to the heart and the plant measured
            # 60.6% radial against the 55% sunburst bound - a hedgehog, not a foliage. The
            # swirl takes the walk off the cliff face without leaving the ascent field: the
            # curves still climb every crest, now as spirals, which is this species' own
            # helicoid statement made at the scale of the curve instead of the prism. It is
            # a CLIFF rather than a slope (0 -> 60.6%, 10 -> 5.6%, 15 -> 1.8%, 20 -> 0.9%),
            # because a swirled walk stops being able to climb a riser at all - so the swirl
            # is chosen on the RENDER rather than on the number, and 15 is where the fronds
            # read as helices while the star still reads as a star at arena distance (25 and
            # 35 curl the fronds into hooks and the silhouette goes fuzzy). Measured over
            # eleven members of the family it holds 1.2-2.3%, so it is not riding an edge.
            # Mix and momentum were measured as alternatives and are worse on both counts:
            # neither reaches the bound (58.2% at mix 0.7, 57.0% at momentum 0.5) and both
            # empty half the sphere's bands as the runs lengthen.
            "Time":   (1, 15, 0.95, 0.25, 0.030, 110, 80, 0.26, 0.0, 0.30, 90,  8, 60, 0.3, 2.0,  5, 1.0, 0.35, 0),
        },
    ),
    "CoralBloom": dict(
        prefab="CoralBloomFlora",
        display="Coral Bloom Flora",
        # The concept: NO twist - the curves themselves are the subject.
        twist=0.0,
        girth_taper=0.32,
        neutral_cross=(0.0300, 0.0140),
        neutral_step=0.045,
        # A tighter family spread than the foliage: this species' plants should read as
        # variations on one smooth form rather than as four different bulbs.
        weight_spread=0.55,
        # THE FALL: planar log spirals, in this species' smooth-arc language.
        extra={
            "*": FALL_SHARED | {"dive_swirl": 0.0, "dive_axis_align": 1.0},
            "Charge": {"dive_count": 8, "dive_angle": 56},
            "Mass":   {"dive_count": 8, "dive_angle": 60},
            "Space":  {"dive_count": 8, "dive_angle": 56},
            "Time":   {"dive_count": 8, "dive_angle": 64},
        },
        curves={
            # High momentum + a low field mix is what makes a curve CONTINUE; a long
            # min_run then discards everything that does not traverse the structure, so
            # what is left is long arcs that cross each other. Fewer lanes and a wider
            # gap keep the cage open enough to see the crossings through.
            #           field swirl mix  mom  step  steps lanes gap  seek jit seeds spread turn rmin rmax run lenf taper twist
            "Charge": (0, 15, 0.22, 0.93, 0.045, 260, 26, 0.55, 0.3, 0.20, 44, 13, 26, 0.3, 2.0, 30, 0.45, 0.55, 0),
            "Mass":   (0, 40, 0.20, 0.94, 0.045, 260, 28, 0.60, 0.4, 0.18, 28, 12, 26, 0.3, 2.0, 30, 1.0, 0.55, 0),
            "Space":  (5,  0, 0.00, 0.96, 0.045, 300, 22, 0.66, 0.2, 0.22, 40, 14, 22, 0.3, 2.2, 36, 1.0, 0.60, 0),
            # ASCENT (field 1), and a RETIREMENT RECORD: a CONTOUR field with its own swirl was
            # tried here twice on the theory that a fall-line field composed with this species'
            # high momentum runs every curve to a pole. Measured, every CONTOUR variant makes
            # the named symptom WORSE (max band 30.4-35.3% against ASCENT's 29.8%) and kills
            # the plant's radial spokes (15.3% -> 0.5-2.0%). A field MIX of 0.28 was also tried
            # (it widens the thinnest band from 2.4% to 3.7% of the plant) and REVERTED: it
            # moved the walk onto a float-width cliff where the shipped C# and the model
            # disagreed on the mean girth by 10% (curves 110 vs 125), i.e. a look tuned on the
            # model that the game would not lay. The mix stays 0.18 and the thinnest band's
            # 20% margin is accepted and stated.
            "Time":   (1,  0, 0.18, 0.95, 0.045, 240, 30, 0.52, 0.0, 0.20, 50, 11, 30, 0.3, 2.0, 14, 1.0, 0.50, 0),
        },
    ),
}

# ── THE WATERSHED — the discovery species (Docs/ECOSYSTEM.md §53) ──────────────────────────
#
# Every curve is a SEPARATRIX of the height field: it leaves a saddle along one of the
# saddle's Hessian eigen-directions and runs uphill to a peak or downhill to a pit. The
# plant is the surface's own Morse-Smale skeleton - a NET whose every face is a quadrilateral
# by theorem - and its peaks and pits sit in latitude rings of exactly (order - 1), each ring
# rotated half a lobe from the next: the fractal's exponent made countable. The valley arms
# that descend far enough take the Fall to the heart.
#
# Under `skeleton_seeds` the curve row's field / swirl / mix / momentum / hop / seed columns
# are UNREAD (asserted by the measure); what is authored per element is the WALK STEP (the
# skeleton's cost is fixed by the surface: Time's 149 saddles need a coarser walk than Mass's
# 32), the girth reference (a separatrix is short by construction, so MaxSteps/2 is a ceiling
# nothing reaches) and the Fall's per-element count, angle and descent gate.
SPECIES["Watershed"] = dict(
    prefab="WatershedFlora",
    display="Watershed Flora",
    concept="the Morse-Smale skeleton: separatrices out of every saddle, then the Fall",
    twist=0.0,
    girth_taper=0.40,
    neutral_cross=(0.0300, 0.0140),
    neutral_step=0.045,
    weight_spread=1.0,
    extra={
        "*": FALL_SHARED | {"skeleton_seeds": 1, "dive_swirl": 0.0, "dive_axis_align": 1.0,
                            "dive_stop": 0.060, "dive_stride_ceiling": 0.70},
        "Charge": {"walk_step": 0.065, "girth_reference": 10, "dive_count": 14, "dive_angle": 56, "dive_descent": 0.10},
        "Mass":   {"walk_step": 0.040, "girth_reference": 27, "dive_count": 12, "dive_angle": 60, "dive_descent": 0.10},
        "Space":  {"walk_step": 0.050, "girth_reference": 22, "dive_count": 12, "dive_angle": 56, "dive_descent": 0.06},
        "Time":   {"walk_step": 0.050, "girth_reference": 14, "dive_count": 7, "dive_angle": 62, "dive_descent": 0.05,
                   "dive_stride_ceiling": 0.80},
    },
    curves={
        # Only steps/lanes/turn/radius/run/taper are read; field, swirl, mix, momentum, hop and
        # seed columns are inert under skeleton_seeds and are authored 0/1 to say so.
        #           field swirl mix  mom  step  steps lanes gap  seek jit seeds spread turn rmin rmax run lenf taper twist
        "Charge": (2,  0, 1.00, 0.00, 0.050, 120,  4, 0.00, 0.0, 0.00,  0,  0, 60, 0.3, 2.0,  3, 1.0, 0.40, 0),
        "Mass":   (2,  0, 1.00, 0.00, 0.040, 120,  4, 0.00, 0.0, 0.00,  0,  0, 60, 0.3, 2.0,  3, 1.0, 0.40, 0),
        "Space":  (2,  0, 1.00, 0.00, 0.050, 120,  4, 0.00, 0.0, 0.00,  0,  0, 60, 0.3, 2.0,  3, 1.0, 0.40, 0),
        "Time":   (2,  0, 1.00, 0.00, 0.050, 120,  4, 0.00, 0.0, 0.00,  0,  0, 60, 0.3, 2.0,  3, 1.0, 0.40, 0),
    },
)
VOLUME_GAIN["Watershed"] = {"Charge": 1.0369, "Mass": 1.0819, "Space": 1.3362, "Time": 1.0}   # fitted: --fit-volume

# ── APOLLONIA — the self-similar species (Docs/ECOSYSTEM.md §54) ───────────────────────────
#
# The one fractal picture everyone recognises: circles packed tangent to circles, the gap
# between three of them filled by a smaller circle, and again. Level 0 is the bulb's OWN
# LOBES (the surface's peaks, each ring half the angle to its nearest neighbour, so the big
# rings crown the lobes and touch by construction); every later disc is inscribed in a
# curvilinear triangle of three mutually adjacent discs. Each disc is DRAWN as a ring of
# prisms lifted onto R(theta, phi), so a big ring crossing three lobes is a scalloped star and
# a small ring inside one lobe a clean circle - the surface deforms the shared motif by
# exactly how much of the bulb the motif spans. The largest rings release the Fall.
#
# Nothing here walks: field / swirl / mix / momentum / hop / seed / turn / radius / run are
# all UNREAD under gasket_levels (asserted by the measure). The neutral STEP is the reference
# ring's chord at N_ref = 50, so LengthFactor 1 means "the ring tiles exactly"; walk_step is
# 0 so the factor stays the Charge dash alone (a ring's prism length is set by its own
# geometry and must not also take a factor that assumes a walk step). RingSamples is
# DERIVED from the element's step, so the element's long axis is spent as ring COARSENESS:
# Space draws a coarse polygon of blades, Mass a fine one of bricks.
SPECIES["Apollonia"] = dict(
    prefab="ApolloniaFlora",
    display="Apollonia Flora",
    concept="the Apollonian gasket: rings packed tangent to rings, crowning the bulb's own lobes",
    twist=0.0,
    girth_taper=1.0,              # off: the rho ladder IS the scale ladder
    neutral_cross=(0.0340, 0.0150),
    neutral_step=0.063,           # the reference ring's chord, 2 pi sin(rho_ref) MeanRadius / 50
    weight_spread=1.0,
    extra={
        # THE TUNING IS A REDISTRIBUTION, NEVER A THICKENING. `neutral_cross` and
        # `neutral_step` are the LACE the panel accepted and no dial here may stand in for
        # them: a pass that tripled the cross made every gate green and the render a pile of
        # flat plates (reverted, 565d4677). Every number below moves material BETWEEN ring
        # sizes, or moves prisms between the rings and the Fall, at a fixed §51 volume.
        "*": FALL_SHARED | {
            "dive_swirl": 0.0, "dive_axis_align": 1.0,
            "dive_descent": 0.0,        # structural: a closed ring starts and ends at one radius
            # THE FALL HOLE's one real lever, and it is NOT MONOTONE - the hole is the gap a
            # REFUSED dive prism leaves, so it is a max over discrete claim refusals and it
            # jumps (Charge measured 2.60 / 10.19 / 1.30 / 4.20 at ceilings 1.0 / 0.7 / 0.5 /
            # 0.4 against a 2.27 bound). Never interpolate it; measure the value you ship.
            # It is also a FIT dial, because a shorter dive step lays more dive prisms.
            "dive_stride_ceiling": 0.50,
            "walk_step": 0.0,           # structural: the length factor is the dash alone
            "girth_reference": 1.0,
            "gasket_levels": 5, "disc_seeds": 13, "disc_pad": 0.60,
            # The girth ladder is authored as FLAT as the species allows (0.30 is the floor
            # under which a small ring stops being a ring and becomes a washer). The ladder
            # gate wants the small octaves wide, the concept wants them thin, and the
            # exponent is the whole of that argument: at 0.30 the smallest ring on the
            # widest-spanning element is 0.55 of the largest's width against 0.14 of its
            # chord. RING_GIRTH_FLOOR is consequently INERT on three of the four elements
            # (measured: sweeping Charge's 0.55 -> 0.20 moves no octave by 0.01 points,
            # because at exponent 0.30 no ring reaches it) and is left shared.
            "ring_shrink": 0.92, "ring_girth_exponent": 0.30, "ring_girth_floor": 0.55,
            "gasket_octave": 0.75,
        },
        # ring_shrink IS the gap between two tangent rings, so it is per element for the
        # reason the leaf is: the §51 law hands each element a different WIDTH on the same
        # circles. It is a LENGTH dial as well (a bigger ring has longer chords), which is
        # what carries a small octave over the 2 px legibility floor - and it is bounded on
        # both of the gates that watch a ring's own prisms: past ~1.0 the claim filter starts
        # refusing a ring's samples to its tangent neighbour's (ring integrity), and Space's
        # wide leaf hits the worst-pair bound first (0.97 measured s* 0.285 against 0.35).
        #
        # ring_flatten is the SCALLOP and it is THREE dials at once: the look, the chord
        # LENGTH (a lifted ring is longer than a flat one), and - through the volume fit -
        # the width of the OTHER THREE ELEMENTS, because Time is the neutral and everything
        # is fitted to Time's cumulative volume. Time is therefore authored at 0.70 on the
        # RENDER: 0.25 buys its octave 1 +17% and its own rings stop reading as rings (ring
        # integrity 0.96 -> 0.88, which the 0.80 gate passes and the eye does not).
        "Charge": {"disc_min_radius": 0.068, "ring_flatten": 0.70, "dive_count": 3,
                   "dive_angle": 56, "ring_shrink": 0.97, "dive_stride_ceiling": 0.50},
        "Mass":   {"disc_min_radius": 0.115, "ring_flatten": 0.70, "dive_count": 5,
                   "dive_angle": 60, "ring_shrink": 1.00, "dive_stride_ceiling": 0.45},
        "Space":  {"disc_min_radius": 0.044, "ring_flatten": 0.80, "dive_count": 8,
                   "dive_angle": 56, "ring_shrink": 0.92, "dive_stride_ceiling": 0.50},
        "Time":   {"disc_min_radius": 0.082, "ring_flatten": 0.70, "dive_count": 7,
                   "dive_angle": 62, "ring_shrink": 0.96, "dive_stride_ceiling": 0.70},
        # disc_min_radius is the FULL FORM dial and on MASS it is also the ladder's: its
        # octave 2 clears the 1% frame bar at every setting and the 2 px bar only once the
        # bottom of that octave is cut (43 discs -> 39 at 0.114, median 1.93 px -> 2.12 px),
        # which costs 220 prisms. They are bought back from the FALL rather than from a
        # smaller disc, because a dive prism is laid WITH the big rings and a small ring is
        # laid last - so topping the fit up with discs puts the prisms back where they
        # failed, while topping it up with dives leaves octave 2 exactly where it was
        # (measured: 8 dives at 0.70 and 5 at 0.45 both land fit 0.91-0.93 with octave 2
        # unmoved at 2.12 px; 5 is authored because the render reads the ARMS).
    },
    curves={
        # Every walk column is inert under gasket_levels and authored 0 to say so.
        #           field swirl mix  mom  step  steps lanes gap  seek jit seeds spread turn rmin rmax run lenf taper twist
        "Charge": (0,  0, 0.00, 0.00, 0.063,   0,  0, 0.00, 0.0, 0.00,  0,  0,  0, 0.0, 0.0,  0, 1.0, 1.00, 0),
        "Mass":   (0,  0, 0.00, 0.00, 0.063,   0,  0, 0.00, 0.0, 0.00,  0,  0,  0, 0.0, 0.0,  0, 1.0, 1.00, 0),
        "Space":  (0,  0, 0.00, 0.00, 0.063,   0,  0, 0.00, 0.0, 0.00,  0,  0,  0, 0.0, 0.0,  0, 1.0, 1.00, 0),
        "Time":   (0,  0, 0.00, 0.00, 0.063,   0,  0, 0.00, 0.0, 0.00,  0,  0,  0, 0.0, 0.0,  0, 1.0, 1.00, 0),
    },
)
VOLUME_GAIN["Apollonia"] = {"Charge": 1.0841, "Mass": 1.0128, "Space": 1.3532, "Time": 1.0}   # fitted: --fit-volume

SHELL_RADIUS = 75.0     # world radius of the surface's unit sphere
FIELD_WIDTH = 192       # runtime reconstruction lattice
# Live prisms per plant. Raised from 2,800 when these species started growing OUT of their
# crystal (Docs/ECOSYSTEM.md §50.6): a plant now carries its own LIMBS — a trunk spiralling
# out of the heart and a stem to every seed's first appearance — and MEASURED over the four
# species those connectors are 12% to 32% of a budget's worth of plant (worst: the Fractal
# Foliage's Time, whose 90 seeds each buy a stem). 4,150 is 2,800 / (1 - 0.3229), i.e. the
# budget at which the same amount of CURVE is laid as before, so every bound tuned against
# the curves still describes the same plant. These four species are in NO SpawnProfile, so
# the extra prisms cost no shipped cell anything; the always-on collider count is
# MaxLivePopulation and is UNCHANGED (one heart per plant, §50 / the /flora skill §6).
PRISM_BUDGET = 4150


# ...except where the FORM ITSELF is finite. A gasket is not a walk that keeps going until the
# budget stops it: its rings are the disc set, and the disc set is decided by `DiscMinRadius`.
# So Apollonia's budget is a MEASUREMENT of its own form (2,600-3,100 prisms after the claim
# across the four elements) rather than a dial on it, and pricing it at the walking species'
# 4,150 would advertise a plant of 4,150 colliders that lays two thirds of them.
PRISM_BUDGET_BY_SPECIES = {"Apollonia": 2900}


def budget_for(species):
    return PRISM_BUDGET_BY_SPECIES.get(species, PRISM_BUDGET)
WEIGHT_STEPS = 3
MAX_LIVE_POPULATION = 3
POPULATION_SIZE = 1


def elemental_prism(species, element):
    """This element's prism, DERIVED from the species' neutral form by the fleet law.

    Returns (cross_x, cross_y, step, length_factor). The law decomposes the neutral prism
    into a SIZE (its geometric mean) and a unit-volume SHAPE, scales the size by the
    element's volume ratio and raises the shape to its anisotropy exponent - which is
    volume-exact, so volume and aspect are independent dials and the species' own axis
    ORDER survives (FloraElementalForm.ShapeLeaf, Docs/ECOSYSTEM.md §51).

    The prism's THIRD axis is the step, because on this growth family the step IS the
    prism's length - so "Space's long axis" is a real long axis here rather than a
    dimension nothing renders.
    """
    spec = SPECIES[species]
    x, y = spec["neutral_cross"]
    z = spec["neutral_step"]
    volume = ELEMENT_VOLUME[element]
    anisotropy = ELEMENT_ANISOTROPY[element]

    mean = (x * y * z) ** (1.0 / 3.0)
    size = mean * volume ** (1.0 / 3.0)
    x2, y2, z2 = (size * (c / mean) ** anisotropy for c in (x, y, z))

    # Cancel this element's own emergent mean girth, so the ordering holds on the PLANT
    # and not only on the authored prism. Uniform on x and y, so the aspect is untouched.
    gain = VOLUME_GAIN.get(species, {}).get(element, 1.0)
    x2 *= gain
    y2 *= gain

    if element == "Charge":
        # Uniform, so the species' own aspect survives the armour fit.
        x2 *= CHARGE_SHIELD_SHRINK
        y2 *= CHARGE_SHIELD_SHRINK
        return x2, y2, z2, CHARGE_DASH
    return x2, y2, z2, 1.0


def rules_for(element, species="FractalFoliage"):
    """The shipped rule for one (species, element) - the authored curve family with the
    law's prism written over its step, length factor and twist, and the species' FALL and
    SKELETON columns (authored by name in `SPECIES[...]["extra"]`) written over the fields
    appended after the first release, which every curve row leaves at their default 0."""
    spec = SPECIES[species]
    values = list(spec["curves"][element])
    values += [0] * (len(Rules.FIELDS) - len(values))
    fields = Rules.FIELDS
    extra = dict(spec.get("extra", {}).get("*", {}))
    extra.update(spec.get("extra", {}).get(element, {}))
    for k, v in extra.items():
        values[fields.index(k)] = v
    _, _, step, length_factor = elemental_prism(species, element)
    values[fields.index("step")] = step
    # A skeleton species walks at its OWN step (the surface fixes its cost) and the law's
    # step is what a prism FILLS of it - so its LengthFactor is the ratio (Docs/ECOSYSTEM.md
    # §53); every other species walks at the law's step and the factor is the dash alone.
    walk = values[fields.index("walk_step")]
    values[fields.index("length_factor")] = length_factor * (step / walk if walk > 0 else 1.0)
    values[fields.index("twist")] = spec["twist"]
    # The girth taper is the SPECIES' texture - how much finer a scrap run is than a
    # structural one - so it is uniform across the four. Left per element it multiplies the
    # cross-section by an emergent, element-dependent mean and quietly re-authors the volume
    # ordering the law just set (measured: it inverted Space above Time).
    values[fields.index("girth_taper")] = spec["girth_taper"]
    return Rules(*values)


def cross_section_for(element, species="FractalFoliage"):
    x, y, _, _ = elemental_prism(species, element)
    return (x, y)


# Back-compat views for the readers that predate the split. RULES/CROSS_SECTION describe
# the FractalFoliage species, which is the one that shipped first.
RULES = {e: rules_for(e).as_list() for e in ("Charge", "Mass", "Space", "Time")}
CROSS_SECTION = {e: cross_section_for(e) for e in ("Charge", "Mass", "Space", "Time")}
WEIGHT_SPREAD = SPECIES["FractalFoliage"]["weight_spread"]


CLAIM_FACTOR = 0.70             # MandelbulbFlora.ClaimFactor
CONNECTOR_CLAIM_FACTOR = 1.6    # MandelbulbFlora.ConnectorClaimFactor — a LIMB yields


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
    # A CONNECTOR claims wider than a ribbon: two curves crossing is what a cage IS, a limb
    # driven through a ribbon is a wire through the plant, and a refused limb prism costs
    # nothing structural because the spindle spans from the nearest STANDING ancestor
    # (MandelbulbFlora.ConnectorClaimFactor).
    # Measuring a connector's radius on its STEP rather than on its prism (`length / LengthFactor`,
    # so a DASHED Charge limb claims as much room as its three siblings' do) was tried and is
    # NOT what ships: measured, it cost Apollonia's Charge 268 prisms and opened a new armour
    # failure at its core, because that species' limbs are numerous and short. The argument was
    # sound and the plant disagreed.
    radii = [max(0.25, (CONNECTOR_CLAIM_FACTOR if p.link else factor) * p.length * shell)
             for p in prisms]
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
