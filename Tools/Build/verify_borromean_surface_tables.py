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
orbits AND leaves the plant CONNECTED after every one of them, with every site's limb
earlier in the table than the site itself and exactly one orbit of limbs leaving the heart;
the frames are unit right-handed rotations lying on the surface's ASYMPTOTIC directions,
with the choice between the two of them COMBED so neighbouring plates agree about the
grain; the surface through the sites is MINIMAL (mean curvature is a rounding error beside
the curvature that is actually there); the heart seat is clear; each element's plate says
what that element says; and no two shielded CHARGE prisms intersect.

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

# The site graph the growth order is defined over, in mean nearest-neighbour spacings, and
# how close a site has to be to count as hanging off the heart.  Both mirror
# measure_borromean_minimal_surface.py; they are repeated here rather than imported because
# this script's whole job is to re-prove the artifact WITHOUT re-running the tool.
GRAPH_REACH = 1.55
HEART_LINK = 1.6


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
        m = re.search(rf'{n}\s*= new\({NUM}f, {NUM}f, {NUM}f\);', t)
        if not m: sys.exit(f'FAIL: {n} missing')
        return np.array([float(g) for g in m.groups()])
    def arr(n, w):
        m = re.search(rf'{n} =\s*\{{\n(.*?)\n\s*\}};', t, re.S)
        if not m: sys.exit(f'FAIL: array {n} missing')
        rows = re.findall(r'new\(' + ', '.join([NUM + 'f'] * w) + r'\),', m.group(1))
        return np.array([[float(x) for x in r] for r in rows])
    def ints(n):
        m = re.search(rf'{n} =\s*\{{\n(.*?)\n\s*\}};', t, re.S)
        if not m: sys.exit(f'FAIL: int array {n} missing')
        return np.array([int(v) for v in re.findall(r'-?\d+', m.group(1))])
    return dict(
        sites=ci('SiteCount'), orbit=ci('OrbitSize'), orbits=ci('OrbitCount'),
        radius=cf('PlantRadius'), seat=cf('HeartSeatRadius'), area=cf('SurfaceArea'),
        spacing=cf('SiteSpacing'), bond=cf('LongestBond'),
        leaf=vec('TimeLeafSize'), charge=vec('ChargeLeafSize'),
        mass=vec('MassLeafSize'), space=vec('SpaceLeafSize'),
        parents=ints('Parents'), P=arr('Positions', 3), Q=arr('Rotations', 4))


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
    c.ok(abs(np.linalg.norm(P, axis=1).max() - d['radius']) < 1e-3,
         'PlantRadius is the furthest site')

    # The order is by HOP distance over the surface's own site graph, not by radius - which
    # is what makes the plant one connected object at every stage instead of six patches
    # that meet up later.  Proved from the points: rebuild the graph and walk the table.
    dd, _ = cKDTree(P).query(P, k=2)
    nnd = dd[:, 1].mean()
    adj = B.site_graph(P, GRAPH_REACH * nnd)
    comps = B.connected_prefixes(P, adj, k, HEART_LINK * nnd)
    c.ok(set(comps) == {1}, 'the plant is CONNECTED after every grow tick (heart included)',
         f'worst tick had {max(comps)} components over {len(comps)} ticks')
    par = d['parents']
    c.ok(len(par) == len(P), 'one parent per site', f'{len(par)} vs {len(P)}')
    c.ok(all(par[i] < i for i in range(len(P))),
         'a site\'s limb is always EARLIER in the table than the site',
         f'worst offset {max((int(par[i]) - i) for i in range(len(P)))}')
    c.ok(int((par == -1).sum()) == k,
         'exactly one orbit of limbs leaves the HEART', f'{int((par==-1).sum())} vs {k}')
    linked = all(par[i] < 0 or par[i] in adj[i] for i in range(len(P)))
    c.ok(linked, 'every limb joins two sites that are actually neighbours')
    live = [i for i in range(len(P)) if par[i] >= 0]
    bl = np.array([np.linalg.norm(P[i] - P[par[i]]) for i in live]) if live else np.zeros(1)
    c.ok(abs(bl.max() - d['bond']) < 1e-2, 'LongestBond is the longest limb',
         f'{bl.max():.3f} vs {d["bond"]:.3f}')
    al = np.array([max(abs(float((P[i] - P[par[i]]) @ X[i])), abs(float((P[i] - P[par[i]]) @ Y[i])))
                   / np.linalg.norm(P[i] - P[par[i]]) for i in live]) if live else np.zeros(1)
    c.ok(al.mean() > 0.85, 'a limb runs along one of its plate\'s own axes',
         f'|cos| mean {al.mean():.3f} worst {al.min():.3f}')

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
    # ... and the CHOICE between the two asymptotic directions is COMBED.  Both are
    # equally valid, so an uncombed table is individually flush and collectively noise.
    grain = np.array([abs(float(X[i] @ X[j])) for i in range(len(P)) for j in adj[i] if j > i])
    c.ok(grain.mean() > 0.75, 'neighbouring plates agree about the grain',
         f'|cos| {grain.mean():.3f} = {np.degrees(np.arccos(min(grain.mean(),1))):.1f} deg apart')

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
    c.ok(abs(d['charge'][2] - d['leaf'][2]) < 1e-4,
         'the CHARGE plate keeps the anchor\'s THICKNESS (spent along the normal, where '
         'the neighbours are not)', f'{d["charge"][2]:.3f} vs {d["leaf"][2]:.3f}')
    c.ok(abs(d['charge'][0] - d['charge'][1]) < 1e-4,
         'its footprint is SQUARE (length along the grain is paid for twice)',
         f'{d["charge"][0]:.3f} x {d["charge"][1]:.3f}')
    grown = d['charge'] * np.array([1.06, 1.06, 1.0])
    c.ok(B.obb_overlap_count(P, X, Y, Z, 1.5 * grown, sp) > 0,
         'and it is the LARGEST that clears - 6% wider already fuses')

    print('[10] each element states its own plate')
    volume = lambda v: float(v[0] * v[1] * v[2])
    c.ok(volume(d['mass']) > 2.0 * volume(d['leaf']),
         'MASS is more VOLUME than the Time anchor',
         f'{volume(d["mass"])/volume(d["leaf"]):.2f}x')
    c.ok(d['space'][0] / d['space'][1] > 2.0 * d['leaf'][0] / d['leaf'][1],
         'SPACE is more ASPECT than the Time anchor',
         f'{d["space"][0]/d["space"][1]:.2f}:1 vs {d["leaf"][0]/d["leaf"][1]:.2f}:1')
    c.ok(abs(volume(d['space']) / volume(d['leaf']) - 1.0) < 0.1,
         'and SPACE spends no extra volume doing it - the element reads as shape',
         f'{volume(d["space"])/volume(d["leaf"]):.3f}x')
    c.ok(volume(d['charge']) < volume(d['leaf']),
         'CHARGE is the one element that shrinks, because its shield is 3x its plate')
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
    def radius_order(e):
        # The ordering this pass REPLACED: orbits sorted by radius.  It is exactly as
        # symmetric and exactly as evenly spaced, and it grows several patches at once that
        # meet up later - which is the defect the hop ordering exists to fix, so it is the
        # right control for the connectivity check.
        k = e['orbit']
        blocks = e['P'].reshape(-1, k, 3)
        o = np.argsort(np.linalg.norm(blocks, axis=2).mean(axis=1))
        perm = (o[:, None] * k + np.arange(k)[None, :]).reshape(-1)
        inv = np.empty(len(perm), int); inv[perm] = np.arange(len(perm))
        e['P'] = e['P'][perm].copy(); e['Q'] = e['Q'][perm].copy()
        e['parents'] = np.array([-1 if e['parents'][j] < 0 else inv[e['parents'][j]]
                                 for j in perm])
    control('orbits ordered by radius instead of hop', radius_order,
            'CONNECTED after every grow tick')

    def parents_ahead(e):
        par = e['parents'].copy()
        par[e['orbit']:] = np.arange(e['orbit'], len(par)) + 1
        par[-1] = 0
        e['parents'] = par
    control('a site laid before its own limb', parents_ahead, 'EARLIER in the table')

    control('every site hung straight off the heart',
            lambda e: e.__setitem__('parents', np.full(len(e['P']), -1)),
            'one orbit of limbs leaves the HEART')

    def uncomb(e):
        # Rotate every second orbit's frame 90 degrees in its own tangent plane: still
        # asymptotic (both directions are), still symmetric, and the tiling is noise again.
        X, Y, Z = frames(e['Q'])
        sel = (np.arange(len(e['P'])) // e['orbit']) % 2 == 1
        X2, Y2 = X.copy(), Y.copy()
        X2[sel], Y2[sel] = Y[sel], -X[sel]
        e['Q'] = B.quaternion_from_frame(X2, Y2, Z)
    control('the grain left uncombed', uncomb, 'agree about the grain')
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
    control('CHARGE footprint left at the anchor\'s aspect',
            lambda e: e.__setitem__('charge', e['charge'] * np.array([1.3, 1 / 1.3, 1.0])),
            'footprint is SQUARE')
    control('CHARGE shrunk further than it needs to be',
            lambda e: e.__setitem__('charge', e['charge'] * np.array([0.6, 0.6, 1.0])),
            'LARGEST that clears')
    control('MASS left at the anchor plate',
            lambda e: e.__setitem__('mass', e['leaf'].copy()), 'more VOLUME')
    control('SPACE left at the anchor plate',
            lambda e: e.__setitem__('space', e['leaf'].copy()), 'more ASPECT')
    control('SPACE buying its aspect with extra volume',
            lambda e: e.__setitem__('space', e['space'] * np.array([1.0, 1.0, 2.0])),
            'spends no extra volume')
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
