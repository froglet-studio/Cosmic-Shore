#!/usr/bin/env python3
"""
The Borromean minimal surface: geometry library shared by the measure and verify tools.

Self-contained; needs numpy + scipy (the same pair measure_gyroid_octagons.py assumes).
No scikit-image: the level set is extracted by a marching-TETRAHEDRA implementation
below, so this tool adds no dependency the repo does not already carry.

THE RINGS
---------
The Borromean rings are realized the canonical way: three congruent ellipses in mutually
perpendicular planes, semi-axes 1 and PHI, cyclic under (x,y,z) -> (z,x,y).  They are the
boundaries of three mutually perpendicular GOLDEN RECTANGLES whose twelve corners are the
vertices of an icosahedron - the textbook realization, and the only shape available: by
the Freedman-Skora theorem the Borromean rings cannot be built from three round CIRCLES
at all, so an ellipse is not a stylistic choice.

Any two of them are provably split: ring i+1 crosses ring i's plane at the two points
(0, +-PHI, 0) (cyclically), and PHI > 1 puts both OUTSIDE ring i's ellipse, so the pair
is unlinked.  All three together are not - that is what Borromean means.

THE SURFACE
-----------
A closed oriented curve subtends a SOLID ANGLE at every point of space, well defined
modulo 4*pi and jumping by exactly 4*pi across any surface the curve bounds.  Summing
over the three rings gives Omega(p), and EVERY level set {Omega = c (mod 4pi)} is
therefore an embedded surface whose boundary is the whole link - a Seifert surface
obtained with no diagram, no Seifert algorithm and no hand-built topology.

Measured, the level set comes out with Euler characteristic -3 over three boundary
loops, i.e. GENUS 1: the minimal-genus Seifert surface of the Borromean rings.  Relaxing
it to zero discrete mean curvature with the boundary pinned to the rings gives a genuine
minimal surface spanning them, of area 11.96 against 15.25 for three flat discs.

Three flat discs would each be minimal for their own ring and are NOT an alternative:
they intersect one another, and a soap film cannot cross itself.  Nor can the three
rings bound three DISJOINT discs - that would split the link, and the Borromean rings
are not split.  A connected spanning surface is forced, which is the whole reason this
object is interesting.

THE SYMMETRY, AND WHY IT IS ORDER 6 RATHER THAN 24
--------------------------------------------------
As an unoriented set the three rings are invariant under the pyritohedral group of order
24.  The SURFACE cannot be: half of those elements reverse some rings' orientations and
leave others alone, so Omega is neither preserved nor negated by them and they carry this
level set to a different one.  Measured (`stabiliser_of_oriented_link`), the subgroup that
fixes the oriented link up to a global reversal has order 6 for EVERY assignment of
orientations to the three rings - it is C3i: the 3-fold rotation about a body diagonal,
times the inversion.  That is the classic Borromean symmetry, and it is a fact about the
link rather than a limitation of the method.
"""
import numpy as np

PHI = (1.0 + 5.0 ** 0.5) / 2.0
TWO_PI = 2.0 * np.pi
FOUR_PI = 4.0 * np.pi

# ---------------------------------------------------------------------------------
#  The rings
# ---------------------------------------------------------------------------------

def ring(i, t):
    """Ring i in {0,1,2}.  Ring 0 lies in z=0 with semi-axis 1 along x and PHI along y;
    rings 1 and 2 are its images under the 3-cycle (x,y,z) -> (z,x,y)."""
    t = np.atleast_1d(np.asarray(t, float))
    base = np.stack([np.cos(t), PHI * np.sin(t), np.zeros_like(t)], axis=-1)
    for _ in range(i):
        base = base[..., [2, 0, 1]]
    return base

def gauss_linking(A, B):
    """Gauss double-integral linking number of two closed polylines."""
    a0, a1 = A, np.roll(A, -1, axis=0)
    b0, b1 = B, np.roll(B, -1, axis=0)
    da, db = a1 - a0, b1 - b0
    r = 0.5 * (a0 + a1)[:, None, :] - 0.5 * (b0 + b1)[None, :, :]
    n = np.linalg.norm(r, axis=-1) ** 3
    num = np.einsum('ijk,ijk->ij', np.cross(da[:, None, :], db[None, :, :]), r)
    return float(np.sum(num / n) / (4 * np.pi))

# ---------------------------------------------------------------------------------
#  The symmetry group
# ---------------------------------------------------------------------------------

def pyritohedral():
    """The order-24 group preserving the three rings as an unoriented SET."""
    C = np.array([[0, 0, 1], [1, 0, 0], [0, 1, 0]], float)
    D = [np.diag([1., 1., 1.]), np.diag([1., -1., -1.]),
         np.diag([-1., 1., -1.]), np.diag([-1., -1., 1.])]
    A4 = [d @ np.linalg.matrix_power(C, k) for d in D for k in range(3)]
    return [m for M in A4 for m in (M, -M)]

def stabiliser_of_oriented_link(eps=(1, 1, 1), nseg=96, probes=240, seed=3, tol=1e-6):
    """Group elements under which Omega is preserved or globally negated - i.e. those
    that carry the level set to ITSELF.  Returns the list of matrices."""
    rng = np.random.default_rng(seed)
    P = rng.normal(size=(probes, 3)) * 0.8
    t = np.linspace(0, TWO_PI, nseg, endpoint=False)
    R = [ring(i, t) for i in range(3)]
    w0 = sum(e * solid_angle(P, R[i]) for i, e in enumerate(eps))
    out = []
    for M in pyritohedral():
        w1 = sum(e * solid_angle(P @ M.T, R[i]) for i, e in enumerate(eps))
        dp = np.abs((w1 - w0 + TWO_PI) % FOUR_PI - TWO_PI).max()
        dm = np.abs((w1 + w0 + TWO_PI) % FOUR_PI - TWO_PI).max()
        if min(dp, dm) < tol:
            out.append(M)
    return out

# ---------------------------------------------------------------------------------
#  The solid-angle potential
# ---------------------------------------------------------------------------------

def solid_angle(P, poly, apex=None):
    """Signed solid angle (mod 4pi) subtended at each point of P by the closed polygon
    `poly`, summed over its triangle fan from `apex` (Van Oosterom-Strackee).  The apex
    choice moves only WHERE the 4pi discontinuity sits, never the value mod 4pi."""
    if apex is None: apex = np.zeros(3)
    a = apex[None, :] - P
    na = np.linalg.norm(a, axis=1)
    out = np.zeros(len(P))
    V0, V1 = poly, np.roll(poly, -1, axis=0)
    for k in range(len(poly)):
        b = V0[k][None, :] - P
        c = V1[k][None, :] - P
        nb = np.linalg.norm(b, axis=1); nc = np.linalg.norm(c, axis=1)
        num = np.einsum('ij,ij->i', a, np.cross(b, c))
        den = (na * nb * nc + np.einsum('ij,ij->i', a, b) * nc
               + np.einsum('ij,ij->i', a, c) * nb + np.einsum('ij,ij->i', b, c) * na)
        out += 2.0 * np.arctan2(num, den)
    return out

def omega(P, nseg=96):
    t = np.linspace(0, TWO_PI, nseg, endpoint=False)
    return sum(solid_angle(P, ring(i, t)) for i in range(3))

def omega_grid(g, nseg=96, chunk=50000):
    X, Y, Z = np.meshgrid(g, g, g, indexing='ij')
    P = np.stack([X, Y, Z], axis=-1).reshape(-1, 3)
    out = np.empty(len(P))
    for s in range(0, len(P), chunk):
        out[s:s+chunk] = omega(P[s:s+chunk], nseg)
    return out.reshape(len(g), len(g), len(g))

# ---------------------------------------------------------------------------------
#  Marching tetrahedra (numpy only)
# ---------------------------------------------------------------------------------

_TE = [(0,1),(0,2),(0,3),(1,2),(1,3),(2,3)]
_CASE = {1: [(0,1,2)], 14: [(0,1,2)], 2: [(0,3,4)], 13: [(0,3,4)],
         4: [(1,3,5)], 11: [(1,3,5)],  8: [(2,4,5)],  7: [(2,4,5)],
         3: [(1,2,4),(1,4,3)], 12: [(1,2,4),(1,4,3)],
         5: [(0,2,5),(0,5,3)], 10: [(0,2,5),(0,5,3)],
         9: [(0,1,5),(0,5,4)],  6: [(0,1,5),(0,5,4)]}
_OFF = [(0,0,0),(0,0,1),(0,1,0),(0,1,1),(1,0,0),(1,0,1),(1,1,0),(1,1,1)]
_TETS = [(0,7,1,3),(0,7,3,2),(0,7,2,6),(0,7,6,4),(0,7,4,5),(0,7,5,1)]

def marching_tets(vol, spacing=1.0, origin=0.0, level=0.0):
    """Iso-surface of a regular scalar grid.  Each cube is split into 6 tetrahedra
    sharing the main diagonal, so the result is watertight by construction, and every
    vertex is keyed by the GRID EDGE it lies on, which welds duplicates with no
    tolerance.  Validated against a sphere: chi = 2, zero boundary edges, area
    converging to 4*pi*r^2."""
    n = vol.shape[0]
    f = vol - level
    lin = np.arange(n**3, dtype=np.int64).reshape(n, n, n)
    sl = lambda d: (slice(d[0], d[0]+n-1), slice(d[1], d[1]+n-1), slice(d[2], d[2]+n-1))
    cl = [lin[sl(o)].ravel() for o in _OFF]
    cv = [f[sl(o)].ravel() for o in _OFF]
    etris = []
    for tet in _TETS:
        v = [cv[c] for c in tet]; g = [cl[c] for c in tet]
        code = sum((v[i] > 0).astype(np.int64) << i for i in range(4))
        for c, tris in _CASE.items():
            m = code == c
            if not m.any(): continue
            emap = {}
            for e in sorted({x for tr in tris for x in tr}):
                a, b = _TE[e]
                ga, gb = g[a][m], g[b][m]
                emap[e] = np.minimum(ga, gb) * (n**3) + np.maximum(ga, gb)
            for tr in tris:
                etris.append(np.stack([emap[tr[0]], emap[tr[1]], emap[tr[2]]], axis=1))
    E = np.concatenate(etris, axis=0)
    uniq, inv = np.unique(E.ravel(), return_inverse=True)
    F = inv.reshape(-1, 3).astype(np.int64)
    ga, gb = uniq // (n**3), uniq % (n**3)
    xyz = lambda idx: np.stack([idx // (n*n), (idx % (n*n)) // n, idx % n], axis=1).astype(float)
    Pa, Pb = xyz(ga), xyz(gb)
    Va, Vb = f.ravel()[ga], f.ravel()[gb]
    t = Va / np.where(np.abs(Va - Vb) < 1e-30, 1e-30, Va - Vb)
    return (Pa + t[:, None] * (Pb - Pa)) * spacing + origin, F

# ---------------------------------------------------------------------------------
#  Mesh helpers
# ---------------------------------------------------------------------------------

def _sp():
    import scipy.sparse as sp, scipy.sparse.csgraph as csg
    return sp, csg

def biggest_component(V, F):
    sp, csg = _sp()
    E = np.vstack([F[:, [0,1]], F[:, [1,2]], F[:, [2,0]]])
    A = sp.coo_matrix((np.ones(len(E)), (E[:,0], E[:,1])), shape=(len(V),)*2)
    _, lab = csg.connected_components(A, directed=False)
    F = F[lab[F[:,0]] == np.argmax(np.bincount(lab))]
    used = np.unique(F); remap = -np.ones(len(V), int); remap[used] = np.arange(len(used))
    return V[used], remap[F]

def orient_consistently(V, F):
    """Propagate one winding across the mesh, then flip the whole thing so the normals
    point AWAY from the origin on average (a deterministic global choice)."""
    sp, csg = _sp()
    he = np.concatenate([F[:, [0,1]], F[:, [1,2]], F[:, [2,0]]])
    fid = np.tile(np.arange(len(F)), 3)
    key = np.sort(he, axis=1)
    order = np.lexsort((key[:,1], key[:,0]))
    key, he, fid = key[order], he[order], fid[order]
    pi = np.where(np.all(key[:-1] == key[1:], axis=1))[0]
    f0, f1 = fid[pi], fid[pi+1]
    w = np.where(np.all(he[pi] == he[pi+1], axis=1), -1, 1).astype(np.int8)
    A = sp.coo_matrix((w, (f0, f1)), shape=(len(F),)*2)
    A = (A + A.T).tocsr()
    sign = np.zeros(len(F), np.int8)
    for start in range(len(F)):
        if sign[start]: continue
        sign[start] = 1; stack = [start]
        while stack:
            a = stack.pop()
            s, e = A.indptr[a], A.indptr[a+1]
            for b, ww in zip(A.indices[s:e], A.data[s:e]):
                if sign[b]: continue
                sign[b] = sign[a] * (-1 if ww < 0 else 1); stack.append(b)
    Fo = F.copy(); Fo[sign < 0] = Fo[sign < 0][:, [0, 2, 1]]
    t = V[Fo]
    fn = np.cross(t[:,1]-t[:,0], t[:,2]-t[:,0])
    if np.einsum('ij,ij->i', fn, t.mean(axis=1)).sum() < 0:
        Fo = Fo[:, [0, 2, 1]]
    return Fo

def boundary_loops(V, F):
    ee = np.sort(np.vstack([F[:,[0,1]], F[:,[1,2]], F[:,[2,0]]]), axis=1)
    u, c = np.unique(ee, axis=0, return_counts=True)
    adj = {}
    for a, b in u[c == 1]:
        adj.setdefault(a, []).append(b); adj.setdefault(b, []).append(a)
    seen, loops = set(), []
    for s0 in adj:
        if s0 in seen: continue
        loop, cur, prev = [s0], s0, None
        seen.add(s0)
        while True:
            nxt = [x for x in adj[cur] if x != prev]
            if not nxt or nxt[0] == s0: break
            nxt = nxt[0]
            loop.append(nxt); seen.add(nxt); prev, cur = cur, nxt
        loops.append(np.array(loop))
    return loops

def area(V, F):
    t = V[F]
    return float(0.5 * np.linalg.norm(np.cross(t[:,1]-t[:,0], t[:,2]-t[:,0]), axis=1).sum())

def euler(V, F):
    E = np.unique(np.sort(np.vstack([F[:,[0,1]], F[:,[1,2]], F[:,[2,0]]]), axis=1), axis=0)
    return len(V) - len(E) + len(F)

def cot_laplacian(V, F):
    """Cotangent Laplacian and its row sums.  Weights are CLAMPED at zero: an obtuse
    triangle gives a negative cotangent, which destabilises the averaging iteration and
    can invert a triangle."""
    sp, _ = _sp()
    i, j, k = F[:,0], F[:,1], F[:,2]
    rows, cols, vals = [], [], []
    for (a, b, c) in ((i,j,k), (j,k,i), (k,i,j)):
        u, w = V[b] - V[a], V[c] - V[a]
        cr = np.linalg.norm(np.cross(u, w), axis=1)
        cot = np.maximum(np.einsum('ij,ij->i', u, w) / np.maximum(cr, 1e-12), 0.0)
        rows += [b, c]; cols += [c, b]; vals += [cot, cot]
    L = sp.coo_matrix((np.concatenate(vals),
                       (np.concatenate(rows), np.concatenate(cols))), shape=(len(V),)*2).tocsr()
    return L, np.asarray(L.sum(axis=1)).ravel()

def mean_curvature(V, F):
    L, d = cot_laplacian(V, F)
    return (L @ V - d[:, None] * V) / np.maximum(d, 1e-12)[:, None]

def vertex_normals(V, F):
    t = V[F]
    fn = np.cross(t[:,1]-t[:,0], t[:,2]-t[:,0])          # length 2*area -> area weighting
    N = np.zeros_like(V)
    for c in range(3):
        np.add.at(N, F[:, c], fn)
    return N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)

def uniform_laplacian(V, F):
    sp, _ = _sp()
    i, j, k = F[:,0], F[:,1], F[:,2]
    rows = np.concatenate([i,j,j,k,k,i]); cols = np.concatenate([j,i,k,j,i,k])
    A = sp.coo_matrix((np.ones(len(rows)), (rows, cols)), shape=(len(V),)*2).tocsr()
    A.data[:] = 1.0
    d = np.asarray(A.sum(axis=1)).ravel()
    return (A @ V) / np.maximum(d, 1e-12)[:, None] - V

# ---------------------------------------------------------------------------------
#  Boundary: nearest point on the rings
# ---------------------------------------------------------------------------------

class RingSnap:
    """Nearest point on the nearest ring.  32768 samples per ring is a spacing of
    ~2.6e-4 world units on a circumference of ~8.6 - far below any mesh tolerance
    here, so no polish step is needed."""
    def __init__(self, n=32768):
        from scipy.spatial import cKDTree
        self.t = np.linspace(0, TWO_PI, n, endpoint=False)
        self.P = np.concatenate([ring(i, self.t) for i in range(3)])
        self.i = np.repeat(np.arange(3), n)
        self.tt = np.tile(self.t, 3)
        self.tree = cKDTree(self.P)
    def __call__(self, Q):
        _, k = self.tree.query(Q)
        return self.P[k], self.i[k], self.tt[k]

def relax_minimal(V, F, snap, iters=40, mu=0.45, polish=8, report=0, log=print):
    """Relax to a DISCRETE MINIMAL SURFACE: the cotangent mean-curvature vector driven to
    zero with the boundary sliding on the rings.

    Each outer step SOLVES the cotangent system exactly rather than sweeping it.  The
    fixed point of `v_i = sum_j w_ij v_j / sum_j w_ij` is the discrete minimal surface, and
    iterating that assignment is a Jacobi sweep whose convergence rate collapses as the
    mesh refines - measured, 3000 sweeps on an 89k-face mesh were still 3% above the
    answer 5 sparse solves reach in under a second.  Same fixed point, two orders of
    magnitude cheaper, and no tuning constant.

    The system is re-BUILT each outer step because the cotangent weights are a function of
    the geometry: the problem is nonlinear, and only the inner solve is linear.

    `mu` adds a TANGENTIAL equalisation - each vertex toward its neighbours' centroid with
    the NORMAL component removed, which changes the mesh and not the surface.  Without it
    triangles drift and collapse; with it the vertex normals are imperfect enough to leak a
    little Laplacian shrinkage, so the last `polish` steps run with `mu = 0` to take it back
    out.  The area holding across that handover is what proves the shrinkage was removed
    rather than merely stopped."""
    import scipy.sparse.linalg as spl
    sp, _ = _sp()
    loops = boundary_loops(V, F)
    bset = np.zeros(len(V), bool)
    for lp in loops: bset[lp] = True
    inner = ~bset
    idx, bnd = np.where(inner)[0], np.where(bset)[0]
    for it in range(iters):
        tangential = mu if it < iters - polish else 0.0
        L, d = cot_laplacian(V, F)
        # A tiny ridge keeps the system non-singular: the clamped cotangent weights can
        # leave an all-obtuse vertex with a zero row, which is a legitimate mesh state and
        # an illegal matrix.
        A = (sp.diags(np.maximum(d, 1e-9)) - L).tocsr()
        Vn = V.copy()
        Vn[idx] = spl.spsolve(A[idx][:, idx].tocsc(), -(A[idx][:, bnd] @ V[bnd]))
        if tangential > 0:
            N = vertex_normals(Vn, F)
            u = uniform_laplacian(Vn, F)
            u -= np.einsum('ij,ij->i', u, N)[:, None] * N
            Vn[inner] += tangential * u[inner]
        for lp in loops:                     # equalise spacing along each boundary loop...
            mids = 0.5 * (V[np.roll(lp, 1)] + V[np.roll(lp, -1)])
            Vn[lp] = V[lp] + 0.5 * (mids - V[lp])
        Vn[bset], _, _ = snap(Vn[bset])      # ...then slide it back onto its ring
        V = Vn
        if report and ((it + 1) % report == 0 or it == iters - 1):
            m = np.linalg.norm(mean_curvature(V, F)[inner], axis=1)
            log(f'      step {it+1:3d}  area {area(V,F):9.5f}  |H| rms '
                f'{np.sqrt((m**2).mean()):.2e}' + ('  (polish)' if tangential == 0 else ''))
    return V, F


# ---------------------------------------------------------------------------------
#  Sampling the relaxed surface
# ---------------------------------------------------------------------------------

def area_samples(V, F, n, seed=7):
    """`n` points drawn uniformly BY AREA over the mesh, with the interpolated normal.

    Area-weighted rather than per-vertex because the relaxed mesh's vertex density is an
    artefact of the marching-tetrahedra grid, and a site layout inherited from that would
    be a picture of the grid rather than of the surface."""
    t = V[F]
    cr = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0])
    a = 0.5 * np.linalg.norm(cr, axis=1)
    rng = np.random.default_rng(seed)
    k = rng.choice(len(F), size=n, p=a / a.sum())
    u, v = rng.random(n), rng.random(n)
    m = u + v > 1.0                       # fold the far half of the square onto the triangle
    u[m], v[m] = 1.0 - u[m], 1.0 - v[m]
    P = t[k, 0] + u[:, None] * (t[k, 1] - t[k, 0]) + v[:, None] * (t[k, 2] - t[k, 0])
    VN = vertex_normals(V, F)
    w = np.stack([1.0 - u - v, u, v], axis=1)
    N = np.einsum('ij,ijk->ik', w, VN[F[k]])
    return P, N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)

# ---------------------------------------------------------------------------------
#  Orbits
# ---------------------------------------------------------------------------------

def expand(X, G):
    """The union of the G-orbits of the rows of `X`, ORBIT-MAJOR: row `i*|G| + j` is
    representative `i` carried by `G[j]`.  Orbit-major is what lets a caller lay one whole
    orbit per growth tick by walking a contiguous slice."""
    X = np.atleast_2d(np.asarray(X, float))
    k = len(G)
    out = np.empty((len(X) * k, 3))
    for j, M in enumerate(G):
        out[j::k] = X @ M.T
    return out

def farthest_point_reps(P, n, seat, G):
    """`n` orbit REPRESENTATIVES by farthest-point sampling, where "farthest" is measured
    against every chosen representative's whole ORBIT.  Measuring against the orbit rather
    than the point is what spreads the SITES evenly instead of spreading the reps evenly
    and letting their images pile up on one another.

    The first representative is the surface point closest to the heart, so the growth order
    that follows from sorting by radius starts at the heart and works outward."""
    ok = np.linalg.norm(P, axis=1) >= seat
    Q = P[ok]
    if len(Q) < n:
        raise ValueError('the heart seat swallowed the surface')
    d = np.full(len(Q), np.inf)
    reps, nxt = [], int(np.argmin(np.linalg.norm(Q, axis=1)))
    for _ in range(n):
        r = Q[nxt]
        reps.append(r)
        for M in G:
            d = np.minimum(d, np.linalg.norm(Q - (r @ M.T), axis=1))
        nxt = int(np.argmax(d))
    return np.array(reps)

def symmetric_cvt(P, reps, G, iters=60, seat=0.0):
    """Lloyd relaxation run on the ORBIT set and pulled back to representatives.

    Each pass assigns every surface sample to its nearest SITE (a point of the expanded
    orbit set), then moves each representative to the area centroid of its own cell
    averaged over the whole orbit: `sum_g g^-1 (centroid of the cell of rep.g)`, weighted
    by cell size.  Averaging over the orbit is what keeps a representative a
    representative, so the expanded set stays EXACTLY G-invariant at every pass - there is
    no drift and therefore no symmetrisation step that could mask one.

    The result is snapped to the nearest surface SAMPLE, which costs a quantisation of
    about 2% of the site spacing and buys the property that every shipped site lies
    exactly ON the relaxed surface rather than near it."""
    from scipy.spatial import cKDTree
    k = len(G)
    Ginv = [np.linalg.inv(M) for M in G]
    Q = P[np.linalg.norm(P, axis=1) >= seat]
    qtree = cKDTree(Q)
    reps = np.array(reps, float)
    for _ in range(iters):
        _, idx = cKDTree(expand(reps, G)).query(Q)
        acc = np.zeros((len(reps) * k, 3))
        cnt = np.zeros(len(reps) * k)
        np.add.at(acc, idx, Q)
        np.add.at(cnt, idx, 1.0)
        new = reps.copy()
        for i in range(len(reps)):
            tot, w = np.zeros(3), 0.0
            for j in range(k):
                c = cnt[i * k + j]
                if c <= 0: continue
                tot += (acc[i * k + j] / c) @ Ginv[j].T * c
                w += c
            if w > 0: new[i] = tot / w
        reps = Q[qtree.query(new)[1]]
    return reps

# ---------------------------------------------------------------------------------
#  Site frames
# ---------------------------------------------------------------------------------

def _fix_sign(v):
    """A deterministic sign for an eigenvector: largest-magnitude component positive."""
    return v if v[int(np.argmax(np.abs(v)))] >= 0 else -v

def rep_frames(reps, P, N, k=48):
    """A right-handed frame per representative: z is the surface normal, and x and y are
    the surface's two ASYMPTOTIC directions.

    A long flat plate belongs where the surface does not bend along it, and on a MINIMAL
    surface that direction exists and is free: the principal curvatures are equal and
    opposite, so normal curvature vanishes on the two directions bisecting the principal
    ones - and those two are orthogonal to each other, which is a property minimal surfaces
    alone have.  So both in-plane axes of the plate lie along a zero-normal-curvature
    direction, and that is why a flat rectangle sits flush on a saddle at all.

    The shape operator comes from a quadric fitted to the `k` nearest surface samples in
    the site's own tangent plane - a local measurement, so it needs no mesh connectivity."""
    reps = np.asarray(reps, float)
    from scipy.spatial import cKDTree
    tree = cKDTree(P)
    X = np.zeros_like(reps); Y = np.zeros_like(reps); Z = np.zeros_like(reps)
    for i, r in enumerate(reps):
        _, nb = tree.query(r, k=k)
        n = N[nb].sum(axis=0)
        if n @ N[nb[0]] < 0: n = -n
        n /= np.linalg.norm(n)
        a = np.array([1., 0., 0.]) if abs(n[0]) < 0.9 else np.array([0., 1., 0.])
        u = np.cross(n, a); u /= np.linalg.norm(u)
        v = np.cross(n, u)
        d = P[nb] - r
        uu, vv, ww = d @ u, d @ v, d @ n
        A = np.stack([uu ** 2, uu * vv, vv ** 2, uu, vv, np.ones_like(uu)], axis=1)
        c = np.linalg.lstsq(A, ww, rcond=None)[0]
        _, E = np.linalg.eigh(np.array([[2 * c[0], c[1]], [c[1], 2 * c[2]]]))
        s = _fix_sign(E[:, 1]) + _fix_sign(E[:, 0])        # bisector = asymptotic direction
        x = s[0] * u + s[1] * v
        x /= np.linalg.norm(x)
        Z[i], X[i], Y[i] = n, x, np.cross(n, x)
    return X, Y, Z

def expand_frames(reps, x, y, z, G):
    """Carry each representative's site AND its frame around the orbit.

    Half the group's elements are IMPROPER (the surface's symmetry includes inversion), and
    mapping a right-handed frame by one gives a left-handed one, which is not a rotation
    and has no quaternion.  So y is re-derived as `z x x` after the map rather than carried:
    that flips y under an improper element, and a plate is a BOX, which is invariant under
    a flip of any one of its axes.  The plate geometry is therefore carried EXACTLY by the
    whole group; only the quaternion table is equivariant up to a symmetry of the box."""
    P = expand(reps, G)
    Xs, Zs = expand(x, G), expand(z, G)
    Xs /= np.linalg.norm(Xs, axis=1, keepdims=True)
    Zs /= np.linalg.norm(Zs, axis=1, keepdims=True)
    Xs -= np.einsum('ij,ij->i', Xs, Zs)[:, None] * Zs          # re-orthogonalise
    Xs /= np.linalg.norm(Xs, axis=1, keepdims=True)
    Ys = np.cross(Zs, Xs)
    return P, Xs, Ys, Zs

def quaternion_from_frame(X, Y, Z):
    """Unity quaternions (x, y, z, w) of the rotations whose COLUMNS are X, Y, Z - i.e.
    the rotation taking local +x/+y/+z onto the site's own axes."""
    m = np.stack([X, Y, Z], axis=2)                            # m[:, :, c] = axis c
    tr = m[:, 0, 0] + m[:, 1, 1] + m[:, 2, 2]
    q = np.zeros((len(X), 4))
    a = tr > 0
    if a.any():
        s = np.sqrt(tr[a] + 1.0) * 2.0
        q[a, 3] = 0.25 * s
        q[a, 0] = (m[a, 2, 1] - m[a, 1, 2]) / s
        q[a, 1] = (m[a, 0, 2] - m[a, 2, 0]) / s
        q[a, 2] = (m[a, 1, 0] - m[a, 0, 1]) / s
    for k in range(3):                                         # the three pivot branches
        p, r = (k + 1) % 3, (k + 2) % 3
        b = ~a & (m[:, k, k] >= m[:, p, p]) & (m[:, k, k] >= m[:, r, r])
        if not b.any(): continue
        s = np.sqrt(1.0 + m[b, k, k] - m[b, p, p] - m[b, r, r]) * 2.0
        q[b, 3] = (m[b, r, p] - m[b, p, r]) / s
        q[b, k] = 0.25 * s
        q[b, p] = (m[b, p, k] + m[b, k, p]) / s
        q[b, r] = (m[b, r, k] + m[b, k, r]) / s
    return q / np.linalg.norm(q, axis=1, keepdims=True)

# ---------------------------------------------------------------------------------
#  Plate fitting
# ---------------------------------------------------------------------------------

def candidate_pairs(P, reach):
    """Every site pair within `reach` - the broadphase for the plate overlap test."""
    from scipy.spatial import cKDTree
    pr = cKDTree(P).query_pairs(float(reach), output_type='ndarray')
    return pr.astype(int) if len(pr) else np.zeros((0, 2), int)

def obb_overlap_count(P, X, Y, Z, half, pairs, eps=1e-9):
    """How many of `pairs` are boxes that actually intersect - exact separating-axis test
    over the 15 axes (3 + 3 face normals, 9 edge cross products)."""
    if len(pairs) == 0: return 0
    i, j = pairs[:, 0], pairs[:, 1]
    A = np.stack([X[i], Y[i], Z[i]], axis=1)
    Bm = np.stack([X[j], Y[j], Z[j]], axis=1)
    d = P[j] - P[i]
    h = np.asarray(half, float)
    axes = [A[:, 0], A[:, 1], A[:, 2], Bm[:, 0], Bm[:, 1], Bm[:, 2]]
    axes += [np.cross(A[:, p], Bm[:, q]) for p in range(3) for q in range(3)]
    sep = np.zeros(len(pairs), bool)
    for L in axes:
        n = np.linalg.norm(L, axis=1)
        live = n > 1e-8                       # a degenerate cross product is not an axis
        Ln = L / np.maximum(n, 1e-12)[:, None]
        ra = sum(h[k] * np.abs(np.einsum('ij,ij->i', A[:, k], Ln)) for k in range(3))
        rb = sum(h[k] * np.abs(np.einsum('ij,ij->i', Bm[:, k], Ln)) for k in range(3))
        sep |= live & (np.abs(np.einsum('ij,ij->i', d, Ln)) > ra + rb + eps)
    return int((~sep).sum())

def fit_shield_scale(P, X, Y, Z, leaf, pairs, tol=1e-4):
    """The largest uniform shrink of `leaf` whose SHIELDED prisms still clear one another.

    A shield swaps the prism for its CIRCUMSCRIBING octahedron, which reaches 1.5 x leafSize
    from the centre (Docs/ECOSYSTEM.md 35), so the body to clear is a box 3x the leaf.  The
    box is the octahedron's bounding box, so a clearance found here is CONSERVATIVE - two
    octahedra that clear their boxes certainly clear each other."""
    leaf = np.asarray(leaf, float)
    clear = lambda s: obb_overlap_count(P, X, Y, Z, 1.5 * leaf * s, pairs) == 0
    if clear(1.0): return 1.0
    lo, hi = 0.0, 1.0
    while hi - lo > tol:
        m = 0.5 * (lo + hi)
        if clear(m): lo = m
        else: hi = m
    return lo
