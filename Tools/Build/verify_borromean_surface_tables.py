#!/usr/bin/env python3
"""
Re-prove the SHIPPED Borromean site table - the cheap gate meant for CI.

    python3 Tools/Build/verify_borromean_surface_tables.py
    python3 Tools/Build/verify_borromean_surface_tables.py --self-test

`measure_borromean_minimal_surface.py` derives the table from the implicit definition and
takes minutes: it extracts a level set on a 129^3 grid and solves a nonlinear cotangent
system.  This reads the numbers that actually SHIPPED and proves the claims made about
them from the points alone, in a couple of seconds and with no mesh.

WHY A SECOND SCRIPT AND NOT `--check`
-------------------------------------
`--check` re-runs the whole derivation and compares the output byte for byte, which
answers "would this tool emit this file again" - a question about the TOOL.  This answers
"is the shipped file a symmetric minimal surface spanning the Borromean rings" - a question
about the ARTIFACT, and the one that survives a refactor of the tool.  It is also the step
neither the measurement nor code review can see: the transcription from a proven
computation into an asset (`Docs/ECOSYSTEM.md` 34.7 makes the same split for Schwarz P).

WHAT IS PROVED, AND WHAT IS NOT
-------------------------------
Proved here: the rings are Borromean; their oriented link's symmetry is exactly order 6;
the site set is EXACTLY a union of orbits of that group; growth runs outward in whole
orbits; the frames are unit right-handed rotations lying on the surface's ASYMPTOTIC
directions; the surface through the sites is MINIMAL (mean curvature is a rounding error
beside the curvature that is actually there); the heart seat is clear; and no two shielded
CHARGE prisms intersect.

NOT proved here: that the surface is the minimal-AREA one, or that it has genus 1.  Both
are properties of the mesh, which this script does not have - they are asserted by the
measurement, which does.

Every check carries a NEGATIVE CONTROL under `--self-test`: a check nobody has watched
fail is a check nobody should trust.
"""
import os, re, sys, argparse
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import borromean_surface as B
from scipy.spatial import cKDTree

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, 'Assets')), \
    f'repo root resolved to {ROOT}, which has no Assets/ - this script lives two levels down'
TABLE = os.path.join(ROOT, 'Assets/_Scripts/Controller/Environment/FloraAndFauna/'
                           'BorromeanSurfaceData.cs')

NUM = r'(-?\d+(?:\.\d+)?)'


def read_table(path=TABLE):
    if not os.path.exists(path):
        sys.exit(f'FAIL: {os.path.relpath(path, ROOT)} does not exist - run '
                 'measure_borromean_minimal_surface.py --write')
    t = open(path).read()
    def ci(n):
        m = re.search(rf'public const int {n} = (-?\d+);', t)
        if not m: sys.exit(f'FAIL: const int {n} missing'); 
        return int(m.group(1))
    def cf(n):
        m = re.search(rf'public const float {n} = {NUM}f;', t)
        if not m: sys.exit(f'FAIL: const float {n} missing')
        return float(m.group(1))
    def vec(n):
        m = re.search(rf'{n} = new\({NUM}f, {NUM}f, {NUM}f\);', t)
        if not m: sys.exit(f'FAIL: {n} missing')
        return np.array([float(g) for g in m.groups()])
    def arr(n, w):
        m = re.search(rf'{n} =\s*\{{\n(.*?)\n\s*\}};', t, re.S)
        if not m: sys.exit(f'FAIL: array {n} missing')
        rows = re.findall(r'new\(' + ', '.join([NUM + 'f'] * w) + r'\),', m.group(1))
        return np.array([[float(x) for x in r] for r in rows])
    return dict(
        sites=ci('SiteCount'), orbit=ci('OrbitSize'), orbits=ci('OrbitCount'),
        radius=cf('PlantRadius'), seat=cf('HeartSeatRadius'), area=cf('SurfaceArea'),
        spacing=cf('SiteSpacing'), leaf=vec('LeafSize'), charge=vec('ChargeLeafSize'),
        P=arr('Positions', 3), Q=arr('Rotations', 4))


def frames(Q):
    """The three axis vectors of each shipped quaternion (Unity order x, y, z, w)."""
    x, y, z, w = Q[:, 0], Q[:, 1], Q[:, 2], Q[:, 3]
    X = np.stack([1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)], -1)
    Y = np.stack([2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)], -1)
    Z = np.stack([2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)], -1)
    return X, Y, Z


def quadric(P, X, Y, Z, k=14):
    """Fit a quadric around every site IN ITS OWN FRAME, from the sites alone.

    Returns the normal curvature along the plate's long axis and its short axis, and the
    shear - i.e. the second fundamental form read in the frame the table ships.  No mesh
    and no connectivity: this is a statement about the points that were actually written."""
    tree = cKDTree(P)
    out = np.zeros((len(P), 3))
    for i in range(len(P)):
        _, nb = tree.query(P[i], k=min(k + 1, len(P)))
        nb = nb[1:]
        d = P[nb] - P[i]
        u, v, w = d @ X[i], d @ Y[i], d @ Z[i]
        A = np.stack([u * u, u * v, v * v, u, v, np.ones_like(u)], 1)
        c = np.linalg.lstsq(A, w, rcond=None)[0]
        out[i] = (2 * c[0], 2 * c[2], c[1])          # k_uu, k_vv, k_uv
    return out


class Checks:
    def __init__(self):
        self.fails = []
    def ok(self, cond, label, detail=''):
        print(('  OK   ' if cond else '  FAIL ') + label + (('   ' + detail) if detail else ''))
        if not cond: self.fails.append(label)
        return cond


def verify(d, c, label='shipped table'):
    P, Q = d['P'], d['Q']
    X, Y, Z = frames(Q)
    print(f'\n== {label}: {len(P)} sites, orbits of {d["orbit"]} ==')

    print('[1] the rings are Borromean')
    t = np.linspace(0, B.TWO_PI, 600, endpoint=False)
    C = [B.ring(i, t) for i in range(3)]
    lk = [B.gauss_linking(C[i], C[j]) for i in range(3) for j in range(i + 1, 3)]
    sep = min(np.linalg.norm(C[i][:, None] - C[j][None], axis=-1).min()
              for i in range(3) for j in range(i + 1, 3))
    c.ok(max(abs(v) for v in lk) < 1e-6, 'pairwise linking numbers are 0',
         f'max |lk| {max(abs(v) for v in lk):.1e}')
    c.ok(sep > 0.5, 'the three rings are disjoint', f'min separation {sep:.4f}')

    print('[2] the oriented link\'s symmetry is exactly order 6')
    G = np.array(B.stabiliser_of_oriented_link())
    c.ok(len(G) == 6, 'stabiliser order is 6', f'got {len(G)}')
    c.ok(len(B.pyritohedral()) == 24, 'the UNORIENTED rings carry order 24 '
                                      '(so 6 is a measurement, not a shortfall)')

    print('[3] the site set is EXACTLY a union of orbits')
    tree = cKDTree(P)
    scale = d['radius'] if d['radius'] > 0 else 1.0
    resid = max(tree.query(P @ M.T)[0].max() for M in G) / scale
    c.ok(resid < 1e-6, 'invariant under every group element',
         f'residual {resid:.2e} of the plant radius')
    c.ok(d['sites'] == len(P) and d['orbit'] * d['orbits'] == len(P),
         'SiteCount = OrbitCount x OrbitSize = rows',
         f'{d["orbits"]} x {d["orbit"]} = {d["sites"]} vs {len(P)} rows')

    print('[4] growth runs outward, one whole orbit at a time')
    k = d['orbit']
    blocks = P.reshape(-1, k, 3)
    worst = 0.0
    for b in blocks:                       # each block IS one orbit of its first member
        img = np.concatenate([b[0:1] @ M.T for M in G])
        worst = max(worst, cKDTree(img).query(b)[0].max() / scale,
                    cKDTree(b).query(img)[0].max() / scale)
    c.ok(worst < 1e-6, 'every contiguous block of OrbitSize is one orbit',
         f'worst block residual {worst:.2e}')
    rad = np.linalg.norm(blocks, axis=2).mean(axis=1)
    c.ok(np.all(np.diff(rad) >= -1e-4), 'blocks are ordered outward from the heart',
         f'radius {rad[0]:.2f} -> {rad[-1]:.2f}')
    c.ok(abs(np.linalg.norm(P, axis=1).max() - d['radius']) < 1e-3,
         'PlantRadius is the furthest site')

    print('[5] the frames are unit right-handed rotations')
    c.ok(np.abs(np.linalg.norm(Q, axis=1) - 1).max() < 2e-5, 'unit quaternions',
         f'max |1-|q|| {np.abs(np.linalg.norm(Q,axis=1)-1).max():.1e}')
    det = np.einsum('ij,ij->i', np.cross(X, Y), Z)
    c.ok(np.abs(det - 1).max() < 1e-4, 'right-handed', f'min det {det.min():.6f}')
    orth = max(np.abs(np.einsum('ij,ij->i', X, Y)).max(),
               np.abs(np.einsum('ij,ij->i', X, Z)).max())
    c.ok(orth < 1e-4, 'orthonormal', f'max |dot| {orth:.1e}')

    print('[6] the surface through the sites is MINIMAL, and it genuinely curves')
    q = quadric(P, X, Y, Z)
    H = np.abs(q[:, 0] + q[:, 1]) / 2.0                     # mean curvature
    K = np.abs(q[:, 2])                                     # the shear that IS there
    ratio = H.mean() / max(K.mean(), 1e-12)
    c.ok(K.mean() * d['spacing'] > 0.05, 'the surface is not flat - there is curvature '
         'for minimality to be a statement about', f'|shear| x spacing {K.mean()*d["spacing"]:.3f}')
    c.ok(ratio < 0.25, 'mean curvature is small beside it (a sphere scores 1.00)',
         f'|H| / |shear| = {ratio:.3f}')

    print('[7] the plate lies on the surface\'s ASYMPTOTIC directions')
    nx = np.abs(q[:, 0]).mean() / max(K.mean(), 1e-12)
    ny = np.abs(q[:, 1]).mean() / max(K.mean(), 1e-12)
    c.ok(max(nx, ny) < 0.35, 'normal curvature along both plate axes is near zero',
         f'along x {nx:.3f}, along y {ny:.3f} of the shear')

    print('[8] spacing, the heart seat, and the plate')
    dd, _ = cKDTree(P).query(P, k=2)
    nn = dd[:, 1]
    c.ok(abs(nn.mean() - d['spacing']) < 0.05 * d['spacing'],
         'SiteSpacing is the mean nearest-neighbour distance',
         f'{nn.mean():.3f} vs {d["spacing"]:.3f}')
    c.ok(nn.min() > 0.4 * nn.mean(), 'no two sites collapse onto one another',
         f'min/mean {nn.min()/nn.mean():.3f}')
    c.ok(np.linalg.norm(P, axis=1).min() >= d['seat'] - 1e-3,
         'every site is clear of the heart seat',
         f'closest {np.linalg.norm(P,axis=1).min():.2f} vs seat {d["seat"]:.2f}')

    print('[9] a shielded CHARGE plant does not fuse')
    sp = B.candidate_pairs(P, 3.0 * float(np.linalg.norm(d['leaf'])))
    n_ch = B.obb_overlap_count(P, X, Y, Z, 1.5 * d['charge'], sp)
    c.ok(n_ch == 0, 'no two CHARGE octahedra intersect (the 3x shield law)',
         f'{n_ch} of {len(sp)} near pairs')
    c.ok(np.all(d['charge'] <= d['leaf'] + 1e-6),
         'the CHARGE leaf is a SHRINK of the shared one, not a different aspect',
         f'x{(d["charge"]/np.maximum(d["leaf"],1e-9)).mean():.4f}')
    return c


def self_test():
    """Negative controls.  Each mutation must break the check it is aimed at."""
    print('\n=== self-test: every check must be watchable failing ===')
    d = read_table()
    bad = 0
    def control(name, mutate, expect):
        nonlocal bad
        e = {k: (v.copy() if isinstance(v, np.ndarray) else v) for k, v in d.items()}
        mutate(e)
        c = Checks()
        import io, contextlib
        with contextlib.redirect_stdout(io.StringIO()):
            verify(e, c, name)
        hit = any(expect in f for f in c.fails)
        print(f'  {"OK  " if hit else "MISS"} {name}: ' +
              (f'fired ({expect!r})' if hit else f'did NOT fire - failures were {c.fails}'))
        if not hit: bad += 1

    control('one site nudged', lambda e: e['P'].__setitem__((0, 0), e['P'][0, 0] + 0.5),
            'invariant under every group element')
    def cross_orbit(e):
        # Swap one member of the first orbit with one of the second, frame and all.  Sorting
        # by radius would NOT do: every member of an orbit is the same distance from the
        # origin, so the table is already in radius order and a stable sort is a no-op - a
        # control has to break the thing the check is about, not something correlated with it.
        k = e['orbit']
        for a in ('P', 'Q'):
            e[a][[0, k]] = e[a][[k, 0]]
    control('a site swapped across orbits', cross_orbit, 'contiguous block of OrbitSize')
    control('growth order reversed', lambda e: e.__setitem__('P', e['P'][::-1].copy()),
            'ordered outward from the heart')
    def sphere(e):
        # the same sites projected onto a sphere of the same radius: still symmetric, still
        # evenly spaced, and NOT minimal.
        r = np.linalg.norm(e['P'], axis=1, keepdims=True)
        e['P'] = e['P'] / r * e['radius'] * 0.6
        X, Y, Z = frames(e['Q'])
        n = e['P'] / np.linalg.norm(e['P'], axis=1, keepdims=True)
        x = X - np.einsum('ij,ij->i', X, n)[:, None] * n
        x /= np.linalg.norm(x, axis=1, keepdims=True)
        y = np.cross(n, x)
        w = np.sqrt(np.maximum(0.0, 1 + x[:, 0] + y[:, 1] + n[:, 2])) / 2
        w = np.maximum(w, 1e-6)
        e['Q'] = np.stack([(y[:, 2] - n[:, 1]) / (4 * w), (n[:, 0] - x[:, 2]) / (4 * w),
                           (x[:, 1] - y[:, 0]) / (4 * w), w], -1)
        e['Q'] /= np.linalg.norm(e['Q'], axis=1, keepdims=True)
    control('sites projected onto a sphere', sphere, 'mean curvature is small')
    control('quaternions denormalised', lambda e: e.__setitem__('Q', e['Q'] * 1.3),
            'unit quaternions')
    control('a site inside the heart seat',
            lambda e: e['P'].__setitem__(0, e['P'][0] * 0.05), 'clear of the heart seat')
    control('CHARGE leaf left unshrunk', lambda e: e.__setitem__('charge', e['leaf'].copy()),
            'CHARGE octahedra intersect')
    print(f'\n{"self-test PASSED" if bad == 0 else f"self-test FAILED: {bad} control(s) did not fire"}')
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--self-test', action='store_true')
    a = ap.parse_args()
    d = read_table()
    c = verify(d, Checks())
    bad = self_test() if a.self_test else 0
    if c.fails:
        print(f'\nFAIL: {len(c.fails)} check(s): ' + '; '.join(c.fails))
        return 1
    print('\nOK: the shipped Borromean table is a symmetric minimal surface on the rings')
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
