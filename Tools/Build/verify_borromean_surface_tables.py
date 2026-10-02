#!/usr/bin/env python3
"""
Re-prove the SHIPPED Borromean site tables - the cheap gate meant for CI.

    python3 Tools/Build/verify_borromean_surface_tables.py
    python3 Tools/Build/verify_borromean_surface_tables.py --self-test

`measure_borromean_minimal_surface.py` derives the tables from the implicit definition and
takes minutes: it extracts a level set on a 129^3 grid and solves a nonlinear cotangent
system.  This reads the numbers that actually SHIPPED and proves the claims made about
them from the points alone, in a few seconds and with no mesh.

WHY A SECOND SCRIPT AND NOT `--check`
-------------------------------------
`--check` re-runs the whole derivation and compares the output byte for byte, which
answers "would this tool emit this file again" - a question about the TOOL.  This answers
"is the shipped file a symmetric minimal surface spanning the Borromean rings, tiled so
that no prism interpenetrates another" - a question about the ARTIFACT, and the one that
survives a refactor of the tool.  It is also the step neither the measurement nor code
review can see: the transcription from a proven computation into an asset
(`Docs/ECOSYSTEM.md` 34.7 makes the same split for Schwarz P).

FOUR TABLES, ONE PER ELEMENT
----------------------------
Each element grows on its own tessellation, because a plate that does not overlap its
neighbours is bounded by how far apart the neighbours are: an element whose body is bigger
takes MORE ROOM PER SITE rather than a shrunken plate.  Room is bought two ways - by cutting
the membrane into fewer pieces, or by growing the MEMBRANE - and SPACE, whose identity is
room, does the second.  Every check below therefore runs four times, and two more run ACROSS
the four: the element contract (MASS the chunkiest plate, SPACE the furthest span at the
anchor's volume, CHARGE fitted to its shield), and the ordering that makes the design a rule
rather than four coincidences - the more room per site, the bigger the body it carries.

WHAT IS PROVED, AND WHAT IS NOT
-------------------------------
Proved here: the rings are Borromean; their oriented link's symmetry is exactly order 6;
every site set is EXACTLY a union of orbits of that group; growth runs outward in whole
orbits AND leaves the plant CONNECTED after every one of them, with every site's limb
earlier in the table than the site itself and exactly one orbit of limbs leaving the heart;
the frames are unit right-handed rotations lying on the surface's ASYMPTOTIC directions,
with the choice between the two of them COMBED so neighbouring plates agree about the
grain; the surface through the sites is MINIMAL (mean curvature is a rounding error beside
the curvature that is actually there); the heart seat is clear; NO TWO PRISMS OF AN ELEMENT
INTERPENETRATE, each plate clears its neighbours by the authored margin and is the LARGEST
of its shape that does (Charge measured against its 3x SHIELD, which is the body a Charge
plant actually shows); and each element's plate says what that element says.

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
ELEMENTS = ('Charge', 'Mass', 'Space', 'Time')

# The site graph the growth order is defined over, in mean nearest-neighbour spacings, and
# how close a site has to be to count as hanging off the heart.  Both mirror
# measure_borromean_minimal_surface.py; they are repeated here rather than imported because
# this script's whole job is to re-prove the artifact WITHOUT re-running the tool.
GRAPH_REACH = 1.55
HEART_LINK = 1.6

# The two halves of "the LARGEST that clears": the shipped plates leave a real gap, and
# nothing 10% bigger fits.  The gap PROVEN here is deliberately under the 3% the fit
# targets - a bisection converges ONTO its own boundary, so at exactly 3% the inflated
# plates are touching to within the fit's tolerance and rounding the table to five decimals
# is enough to tip a pair over.  Asserting the boundary of a fit is asserting the tolerance
# of the fit; what is worth asserting is that the gap is real.
MARGIN = 0.02
TIGHT = 1.10

# A shield swaps the plate for its CIRCUMSCRIBING octahedron, reaching 1.5 x leafSize from
# the centre (Docs/ECOSYSTEM.md 35).  So on a CHARGE plant the body that has to clear is
# three times the plate, and the plate itself clears by a mile - which is why the body each
# element is measured through is a property of the element and not a constant.
BODY = dict(Charge=1.5, Mass=0.5, Space=0.5, Time=0.5)


def read_table(path=TABLE):
    if not os.path.exists(path):
        sys.exit(f'FAIL: {os.path.relpath(path, ROOT)} does not exist - run '
                 'measure_borromean_minimal_surface.py --write')
    t = open(path).read()
    def ci(n):
        m = re.search(rf'public const int {n} = (-?\d+);', t)
        if not m: sys.exit(f'FAIL: const int {n} missing')
        return int(m.group(1))
    def cf(n):
        m = re.search(rf'public const float {n} = {NUM}f;', t)
        if not m: sys.exit(f'FAIL: const float {n} missing')
        return float(m.group(1))
    def arr(n, w):
        m = re.search(rf'{n} =\s*\{{\n(.*?)\n\s*\}};', t, re.S)
        if not m: sys.exit(f'FAIL: array {n} missing')
        rows = re.findall(r'new\(' + ', '.join([NUM + 'f'] * w) + r'\),', m.group(1))
        return np.array([[float(x) for x in r] for r in rows])
    def ints(n):
        m = re.search(rf'{n} =\s*\{{\n(.*?)\n\s*\}};', t, re.S)
        if not m: sys.exit(f'FAIL: int array {n} missing')
        return np.array([int(v) for v in re.findall(r'-?\d+', m.group(1))])

    tables = {}
    for e in ELEMENTS:
        # The instance is matched with its OWN array names in it, so a table wired to
        # another element's arrays - the one mistake four near-identical blocks invite -
        # fails to parse rather than verifying the wrong points.
        m = re.search(rf'SurfaceTable {e} = new SurfaceTable\(\s*'
                      rf'{e}Positions, {e}Rotations, {e}Parents,\s*'
                      rf'new Vector3\({NUM}f, {NUM}f, {NUM}f\), {NUM}f, {NUM}f, {NUM}f,\s*'
                      rf'{NUM}f\);', t)
        if not m: sys.exit(f'FAIL: the {e} SurfaceTable is missing, or is not wired to its '
                           f'own {e}Positions / {e}Rotations / {e}Parents')
        g = [float(x) for x in m.groups()]
        tables[e] = dict(name=e, leaf=np.array(g[:3]), radius=g[3], spacing=g[4], bond=g[5],
                         seat=g[6],
                         P=arr(f'{e}Positions', 3), Q=arr(f'{e}Rotations', 4),
                         parents=ints(f'{e}Parents'))
    return dict(orbit=ci('OrbitSize'), max_sites=ci('MaxSiteCount'),
                area=cf('SurfaceArea'), tables=tables)


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


def overlaps(P, X, Y, Z, leaf, body, grow=1.0):
    """How many pairs of an element's BODIES intersect at `grow` x their shipped size.

    The broadphase is built at the size being tested, never at the shipped one: a pair
    dropped from the candidate set reads as a pair that clears."""
    half = body * grow * np.asarray(leaf, float)
    pairs = B.candidate_pairs(P, 2.0 * float(np.linalg.norm(half)))
    return B.obb_overlap_count(P, X, Y, Z, half, pairs), len(pairs)


class Checks:
    def __init__(self):
        self.fails = []
    def ok(self, cond, label, detail=''):
        print(('  OK   ' if cond else '  FAIL ') + label + (('   ' + detail) if detail else ''))
        if not cond: self.fails.append(label)
        return cond


def verify_rings(c):
    print('[1] the rings are Borromean, and their oriented link has symmetry of order 6')
    t = np.linspace(0, B.TWO_PI, 600, endpoint=False)
    C = [B.ring(i, t) for i in range(3)]
    lk = [B.gauss_linking(C[i], C[j]) for i in range(3) for j in range(i + 1, 3)]
    sep = min(np.linalg.norm(C[i][:, None] - C[j][None], axis=-1).min()
              for i in range(3) for j in range(i + 1, 3))
    c.ok(max(abs(v) for v in lk) < 1e-6, 'pairwise linking numbers are 0',
         f'max |lk| {max(abs(v) for v in lk):.1e}')
    c.ok(sep > 0.5, 'the three rings are disjoint', f'min separation {sep:.4f}')
    G = np.array(B.stabiliser_of_oriented_link())
    c.ok(len(G) == 6, 'stabiliser order is 6', f'got {len(G)}')
    c.ok(len(B.pyritohedral()) == 24, 'the UNORIENTED rings carry order 24 '
                                      '(so 6 is a measurement, not a shortfall)')
    return G


def verify_element(d, t, G, c):
    """Everything that is true of ONE element's table."""
    P, Q, leaf, k = t['P'], t['Q'], t['leaf'], d['orbit']
    X, Y, Z = frames(Q)
    body, name = BODY[t['name']], t['name']
    print(f'\n-- {name}: {len(P)} sites, orbits of {k}, plate '
          f'{leaf[0]:.3f} x {leaf[1]:.3f} x {leaf[2]:.3f} --')

    print('[2] the site set is EXACTLY a union of orbits')
    tree = cKDTree(P)
    scale = t['radius'] if t['radius'] > 0 else 1.0
    resid = max(tree.query(P @ M.T)[0].max() for M in G) / scale
    c.ok(resid < 1e-6, f'{name}: invariant under every group element',
         f'residual {resid:.2e} of the plant radius')
    c.ok(len(P) % k == 0 and len(P) == len(Q),
         f'{name}: the table is a whole number of orbits', f'{len(P)} sites')

    print('[3] growth runs outward, one whole orbit at a time, and stays CONNECTED')
    worst = 0.0
    for b in P.reshape(-1, k, 3):          # each block IS one orbit of its first member
        img = np.concatenate([b[0:1] @ M.T for M in G])
        worst = max(worst, cKDTree(img).query(b)[0].max() / scale,
                    cKDTree(b).query(img)[0].max() / scale)
    c.ok(worst < 1e-6, f'{name}: every contiguous block of OrbitSize is one orbit',
         f'worst block residual {worst:.2e}')
    c.ok(abs(np.linalg.norm(P, axis=1).max() - t['radius']) < 1e-3,
         f'{name}: PlantRadius is the furthest site')

    # The order is by HOP distance over the surface's own site graph, not by radius - which
    # is what makes the plant one connected object at every stage instead of six patches
    # that meet up later.  Proved from the points: rebuild the graph and walk the table.
    dd, _ = cKDTree(P).query(P, k=2)
    nn = dd[:, 1]
    adj = B.site_graph(P, GRAPH_REACH * nn.mean())
    comps = B.connected_prefixes(P, adj, k, HEART_LINK * nn.mean())
    c.ok(set(comps) == {1}, f'{name}: CONNECTED after every grow tick (heart included)',
         f'worst tick had {max(comps)} components over {len(comps)} ticks')
    par = t['parents']
    c.ok(len(par) == len(P), f'{name}: one parent per site', f'{len(par)} vs {len(P)}')
    c.ok(all(par[i] < i for i in range(len(P))),
         f'{name}: a site\'s limb is always EARLIER in the table than the site',
         f'worst offset {max((int(par[i]) - i) for i in range(len(P)))}')
    c.ok(int((par == -1).sum()) == k,
         f'{name}: exactly one orbit of limbs leaves the HEART', f'{int((par==-1).sum())} vs {k}')
    c.ok(all(par[i] < 0 or par[i] in adj[i] for i in range(len(P))),
         f'{name}: every limb joins two sites that are actually neighbours')
    live = [i for i in range(len(P)) if par[i] >= 0]
    bl = np.array([np.linalg.norm(P[i] - P[par[i]]) for i in live]) if live else np.zeros(1)
    c.ok(abs(bl.max() - t['bond']) < 1e-2, f'{name}: LongestBond is the longest limb',
         f'{bl.max():.3f} vs {t["bond"]:.3f}')
    al = np.array([max(abs(float((P[i] - P[par[i]]) @ X[i])), abs(float((P[i] - P[par[i]]) @ Y[i])))
                   / np.linalg.norm(P[i] - P[par[i]]) for i in live]) if live else np.zeros(1)
    c.ok(al.mean() > 0.85, f'{name}: a limb runs along one of its plate\'s own axes',
         f'|cos| mean {al.mean():.3f} worst {al.min():.3f}')

    print('[4] the frames are unit right-handed rotations on the ASYMPTOTIC directions')
    c.ok(np.abs(np.linalg.norm(Q, axis=1) - 1).max() < 2e-5, f'{name}: unit quaternions',
         f'max |1-|q|| {np.abs(np.linalg.norm(Q,axis=1)-1).max():.1e}')
    det = np.einsum('ij,ij->i', np.cross(X, Y), Z)
    c.ok(np.abs(det - 1).max() < 1e-4, f'{name}: right-handed', f'min det {det.min():.6f}')
    orth = max(np.abs(np.einsum('ij,ij->i', X, Y)).max(),
               np.abs(np.einsum('ij,ij->i', X, Z)).max())
    c.ok(orth < 1e-4, f'{name}: orthonormal', f'max |dot| {orth:.1e}')
    q = quadric(P, X, Y, Z)
    H = np.abs(q[:, 0] + q[:, 1]) / 2.0                     # mean curvature
    K = np.abs(q[:, 2])                                     # the shear that IS there
    c.ok(K.mean() * t['spacing'] > 0.05, f'{name}: the surface is not flat - there is '
         'curvature for minimality to be a statement about',
         f'|shear| x spacing {K.mean()*t["spacing"]:.3f}')
    # 0.5 rather than something tighter because the ESTIMATOR is what bounds this, not the
    # surface: a quadric fitted to 14 neighbours of a point cloud moves by +-0.2 as that
    # count is varied, and it moves more on a coarse table, whose patch spans more surface.
    # The number that makes the check worth having is the separation - these land at
    # 0.17..0.28 and the same sites projected onto a SPHERE score 91..205.
    c.ok(H.mean() / max(K.mean(), 1e-12) < 0.5,
         f'{name}: mean curvature is small beside it (a sphere of the same sites scores ~100)',
         f'|H| / |shear| = {H.mean()/max(K.mean(),1e-12):.3f}')
    nx = np.abs(q[:, 0]).mean() / max(K.mean(), 1e-12)
    ny = np.abs(q[:, 1]).mean() / max(K.mean(), 1e-12)
    c.ok(max(nx, ny) < 0.5, f'{name}: normal curvature along both plate axes is near zero',
         f'along x {nx:.3f}, along y {ny:.3f} of the shear')
    # ... and the CHOICE between the two asymptotic directions is COMBED.  Both are
    # equally valid, so an uncombed table is individually flush and collectively noise.
    grain = np.array([abs(float(X[i] @ X[j])) for i in range(len(P)) for j in adj[i] if j > i])
    c.ok(grain.mean() > 0.75, f'{name}: neighbouring plates agree about the grain',
         f'|cos| {grain.mean():.3f} = {np.degrees(np.arccos(min(grain.mean(),1))):.1f} deg apart')

    print('[5] spacing and the heart seat')
    c.ok(abs(nn.mean() - t['spacing']) < 0.05 * t['spacing'],
         f'{name}: SiteSpacing is the mean nearest-neighbour distance',
         f'{nn.mean():.3f} vs {t["spacing"]:.3f}')
    c.ok(nn.min() > 0.4 * nn.mean(), f'{name}: no two sites collapse onto one another',
         f'min/mean {nn.min()/nn.mean():.3f}')
    # The seat is PER ELEMENT: an element that grows the membrane grows the alcove its
    # crystal sits in by the same factor, so one shared constant would be a floor for three
    # elements and a lie about the fourth.
    c.ok(np.linalg.norm(P, axis=1).min() >= t['seat'] - 1e-3,
         f'{name}: every site is clear of its OWN heart seat',
         f'closest {np.linalg.norm(P,axis=1).min():.2f} vs seat {t["seat"]:.2f}')

    print('[6] NO PRISM INTERPENETRATES ANOTHER - and the plate is the LARGEST that does not')
    # Measured through the body this element actually SHOWS: the plate for three of them,
    # and the circumscribing octahedron for Charge, which is three times its plate and is
    # what a Charge plant is wearing almost all the time (Flora.ResolveShieldPeriod floors
    # its shield cadence at 1 s).
    worn = 'plate' if body == 0.5 else 'SHIELD octahedron'
    n0, npairs = overlaps(P, X, Y, Z, leaf, body)
    c.ok(n0 == 0, f'{name}: no two {worn}s intersect', f'{n0} of {npairs} near pairs')
    nm, _ = overlaps(P, X, Y, Z, leaf, body, 1.0 + MARGIN)
    c.ok(nm == 0, f'{name}: and they clear by at least {int(100*MARGIN)}%, so the gap is a real one '
         'rather than a float epsilon', f'{nm} pairs')
    nt, _ = overlaps(P, X, Y, Z, leaf, body, TIGHT)
    c.ok(nt > 0, f'{name}: it is FITTED, not merely small - {int(100*(TIGHT-1))}% bigger collides',
         f'{nt} pairs')
    if body != 0.5:      # a Charge PLATE is a third of the body above and must also clear
        np_, _ = overlaps(P, X, Y, Z, leaf, 0.5)
        c.ok(np_ == 0, f'{name}: its bare plates clear too', f'{np_} pairs')


def verify_contract(d, c):
    """What is true ACROSS the four tables: the element contract, and the ordering that
    makes per-element tessellation a rule rather than four coincidences."""
    T = d['tables']
    vol = lambda n: float(np.prod(T[n]['leaf']))
    asp = lambda n: T[n]['leaf'][0] / T[n]['leaf'][1]
    print('\n[7] each element states its own plate')
    c.ok(vol('Mass') > 2.0 * vol('Time'), 'MASS is more VOLUME than the Time anchor',
         f'{vol("Mass")/vol("Time"):.2f}x')
    c.ok(asp('Space') > 2.0 * asp('Time'), 'SPACE is more ASPECT than the Time anchor',
         f'{asp("Space"):.2f}:1 vs {asp("Time"):.2f}:1')
    c.ok(abs(vol('Space') / vol('Time') - 1.0) < 0.1,
         'and SPACE spends no extra volume doing it - the element reads as shape',
         f'{vol("Space")/vol("Time"):.3f}x')
    c.ok(vol('Charge') < vol('Time'),
         'CHARGE is the one element that shrinks, because its shield is 3x its plate')
    c.ok(abs(T['Charge']['leaf'][0] - T['Charge']['leaf'][1]) < 1e-4,
         'its footprint is SQUARE (length along the grain is paid for twice)',
         f'{T["Charge"]["leaf"][0]:.3f} x {T["Charge"]["leaf"][1]:.3f}')
    # MASS is the element that is CHUNKY, which is a claim about the plate's SHAPE and not
    # about its volume - the thinnest axis as a fraction of the longest.  It has to be
    # asserted separately from the volume above, because thickness is the free axis: a plate
    # could be given Mass's volume and still be a flat lozenge.
    #
    # CHARGE is excluded, and the exclusion is the point rather than a convenience: its
    # PLATE is a square slab only because the body it was fitted against is the
    # octahedron three times it (asserted below), so "how cube-like is the plate" is not a
    # statement about what a Charge plant looks like.  The comparison is between the three
    # elements whose body IS their plate.
    worn = tuple(n for n in ELEMENTS if BODY[n] == 0.5)
    chunk = lambda n: float(min(T[n]['leaf']) / max(T[n]['leaf']))
    c.ok(all(chunk('Mass') > chunk(n) for n in worn if n != 'Mass'),
         'MASS is the CHUNKIEST of the plates worn AS plates - nearest a cube, never one',
         '  '.join(f'{n} 1:{T[n]["leaf"][1]/T[n]["leaf"][0]:.2f}:{T[n]["leaf"][2]/T[n]["leaf"][0]:.2f}'
                   for n in worn))
    c.ok(chunk('Mass') < 0.9, 'and it stops short of one - a cube is not a plate',
         f'thinnest axis {chunk("Mass"):.2f} of the longest')
    # SPACE is the element that is ROOMY, and that is a claim about the PLANT rather than
    # the plate: it grows the membrane itself, so its two furthest prisms are further apart
    # than any other element's - while spending no extra volume doing it (checked above).
    span = lambda n: T[n]['radius']
    c.ok(all(span('Space') > 1.5 * span(n) for n in ELEMENTS if n != 'Space'),
         'SPACE spans the furthest - the element grows the MEMBRANE, not just the plate',
         '  '.join(f'{n} {2*span(n):.0f}' for n in ELEMENTS))

    print('[8] every element tiles the surface at its OWN spacing, and the order is a rule')
    sp = {n: T[n]['spacing'] for n in ELEMENTS}
    c.ok(len(set(round(v, 3) for v in sp.values())) == len(ELEMENTS),
         'the four tessellations are genuinely different',
         '  '.join(f'{n} {sp[n]:.2f}' for n in ELEMENTS))
    # THE DESIGN RULE, asserted rather than described: an element takes as much room per
    # site as the body it puts there needs.  Both sides are measured off the shipped
    # tables, so a future retune that breaks the rule fails here rather than shipping four
    # numbers nobody can explain.
    # THE DESIGN RULE.  It is stated in ROOM PER SITE rather than in orbit count, because an
    # element buys room two ways - by cutting the membrane into fewer pieces, or by growing
    # the membrane - and Space does the second, so it has a FINER cut than Mass or Charge
    # and still the most room of the four.
    reach = {n: BODY[n] * float(np.linalg.norm(T[n]['leaf'])) for n in ELEMENTS}
    by_reach = sorted(ELEMENTS, key=lambda n: reach[n])
    by_space = sorted(ELEMENTS, key=lambda n: sp[n])
    c.ok(by_reach == by_space,
         'the more room per site, the bigger the body it carries',
         ' < '.join(f'{n}({reach[n]:.2f}, room {sp[n]:.2f})' for n in by_reach))
    c.ok(d['max_sites'] == max(len(T[n]['P']) for n in ELEMENTS),
         'MaxSiteCount is the largest table', f'{d["max_sites"]}')


def verify(d, c, label='shipped tables'):
    print(f'\n== {label}: ' + ', '.join(f'{n} {len(d["tables"][n]["P"])}' for n in ELEMENTS) +
          f' sites, orbits of {d["orbit"]} ==')
    G = verify_rings(c)
    for n in ELEMENTS:
        verify_element(d, d['tables'][n], G, c)
    verify_contract(d, c)
    return c


def self_test():
    """Negative controls.  Each mutation must break the check it is aimed at."""
    print('\n=== self-test: every check must be watchable failing ===')
    d = read_table()
    bad = 0
    def control(name, mutate, expect):
        nonlocal bad
        e = dict(d)
        e['tables'] = {n: {k: (v.copy() if isinstance(v, np.ndarray) else v)
                           for k, v in t.items()} for n, t in d['tables'].items()}
        mutate(e)
        c = Checks()
        import io, contextlib
        with contextlib.redirect_stdout(io.StringIO()):
            verify(e, c, name)
        hit = any(expect in f for f in c.fails)
        print(f'  {"OK  " if hit else "MISS"} {name}: ' +
              (f'fired ({expect!r})' if hit else f'did NOT fire - failures were {c.fails}'))
        if not hit: bad += 1

    T = lambda e, n='Time': e['tables'][n]

    control('one site nudged', lambda e: T(e)['P'].__setitem__((0, 0), T(e)['P'][0, 0] + 0.5),
            'invariant under every group element')

    def cross_orbit(e):
        # Swap one member of the first orbit with one of the second, frame and all.  Sorting
        # by radius would NOT do: every member of an orbit is the same distance from the
        # origin, so the table is already in radius order and a stable sort is a no-op - a
        # control has to break the thing the check is about, not something correlated with it.
        k = e['orbit']
        for a in ('P', 'Q'):
            T(e)[a][[0, k]] = T(e)[a][[k, 0]]
    control('a site swapped across orbits', cross_orbit, 'contiguous block of OrbitSize')

    def radius_order(e):
        # The ordering the growth pass REPLACED: orbits sorted by radius.  It is exactly as
        # symmetric and exactly as evenly spaced, and it grows several patches at once that
        # meet up later - which is the defect the hop ordering exists to fix, so it is the
        # right control for the connectivity check.
        k, t = e['orbit'], T(e)
        blocks = t['P'].reshape(-1, k, 3)
        o = np.argsort(np.linalg.norm(blocks, axis=2).mean(axis=1))
        perm = (o[:, None] * k + np.arange(k)[None, :]).reshape(-1)
        inv = np.empty(len(perm), int); inv[perm] = np.arange(len(perm))
        t['P'] = t['P'][perm].copy(); t['Q'] = t['Q'][perm].copy()
        t['parents'] = np.array([-1 if t['parents'][j] < 0 else inv[t['parents'][j]] for j in perm])
    control('orbits ordered by radius instead of hop', radius_order,
            'CONNECTED after every grow tick')

    def parents_ahead(e):
        t = T(e); par = t['parents'].copy()
        par[e['orbit']:] = np.arange(e['orbit'], len(par)) + 1
        par[-1] = 0
        t['parents'] = par
    control('a site laid before its own limb', parents_ahead, 'EARLIER in the table')

    control('every site hung straight off the heart',
            lambda e: T(e).__setitem__('parents', np.full(len(T(e)['P']), -1)),
            'one orbit of limbs leaves the HEART')

    def uncomb(e):
        # Rotate every second orbit's frame 90 degrees in its own tangent plane: still
        # asymptotic (both directions are), still symmetric, and the tiling is noise again.
        t = T(e)
        X, Y, Z = frames(t['Q'])
        sel = (np.arange(len(t['P'])) // e['orbit']) % 2 == 1
        X2, Y2 = X.copy(), Y.copy()
        X2[sel], Y2[sel] = Y[sel], -X[sel]
        t['Q'] = B.quaternion_from_frame(X2, Y2, Z)
    control('the grain left uncombed', uncomb, 'agree about the grain')

    def sphere(e):
        # the same sites projected onto a sphere of the same radius: still symmetric, still
        # evenly spaced, and NOT minimal.
        t = T(e)
        r = np.linalg.norm(t['P'], axis=1, keepdims=True)
        t['P'] = t['P'] / r * t['radius'] * 0.6
        X, Y, Z = frames(t['Q'])
        n = t['P'] / np.linalg.norm(t['P'], axis=1, keepdims=True)
        x = X - np.einsum('ij,ij->i', X, n)[:, None] * n
        x /= np.linalg.norm(x, axis=1, keepdims=True)
        y = np.cross(n, x)
        w = np.maximum(np.sqrt(np.maximum(0.0, 1 + x[:, 0] + y[:, 1] + n[:, 2])) / 2, 1e-6)
        t['Q'] = np.stack([(y[:, 2] - n[:, 1]) / (4 * w), (n[:, 0] - x[:, 2]) / (4 * w),
                           (x[:, 1] - y[:, 0]) / (4 * w), w], -1)
        t['Q'] /= np.linalg.norm(t['Q'], axis=1, keepdims=True)
    control('sites projected onto a sphere', sphere, 'mean curvature is small')

    control('quaternions denormalised', lambda e: T(e).__setitem__('Q', T(e)['Q'] * 1.3),
            'unit quaternions')
    control('a site inside the heart seat',
            lambda e: T(e)['P'].__setitem__(0, T(e)['P'][0] * 0.05), 'clear of its OWN heart seat')

    # ---- the guarantee this pass exists for
    control('plates left at the size they were AUTHORED at before they were fitted',
            lambda e: T(e).__setitem__('leaf', T(e)['leaf'] * 1.5),
            'no two plates intersect')
    control('plates grown just past the gap they were fitted to leave',
            lambda e: T(e).__setitem__('leaf', T(e)['leaf'] * 1.05),
            'clear by at least 2%')
    control('plates shrunk well inside what the tessellation allows',
            lambda e: T(e).__setitem__('leaf', T(e)['leaf'] * 0.6), 'it is FITTED')
    control('CHARGE fitted to its PLATE instead of its SHIELD',
            lambda e: T(e, 'Charge').__setitem__('leaf', T(e, 'Charge')['leaf'] * 2.5),
            'no two SHIELD octahedrons intersect')

    # ---- the element contract, and the rule behind the four tessellations
    control('MASS left at the anchor plate',
            lambda e: T(e, 'Mass').__setitem__('leaf', T(e)['leaf'].copy()), 'more VOLUME')
    control('SPACE left at the anchor plate',
            lambda e: T(e, 'Space').__setitem__('leaf', T(e)['leaf'].copy()), 'more ASPECT')
    control('SPACE buying its aspect with extra volume',
            lambda e: T(e, 'Space').__setitem__('leaf', T(e, 'Space')['leaf'] * np.array([1, 1, 2.0])),
            'spends no extra volume')
    control('CHARGE footprint left at the anchor\'s aspect',
            lambda e: T(e, 'Charge').__setitem__('leaf',
                T(e, 'Charge')['leaf'] * np.array([1.3, 1 / 1.3, 1.0])), 'footprint is SQUARE')

    def one_tiling(e):
        # Every element on the ANCHOR's tessellation - the shape this pass replaced.  It is
        # still symmetric, still connected and still grows in orbits; what it cannot do is
        # give four different bodies the room each of them needs.
        for n in ELEMENTS:
            if n == 'Time': continue
            for key in ('P', 'Q', 'parents', 'spacing', 'radius', 'bond', 'seat'):
                v = T(e)[key]
                T(e, n)[key] = v.copy() if isinstance(v, np.ndarray) else v
    control('every element on ONE shared tessellation', one_tiling,
            'the four tessellations are genuinely different')

    def misordered(e):
        # Swap the two tessellations, bodies and all: each element still clears on the tiling
        # it is standing on, and the RULE - room per site tracks the body that goes there -
        # is broken.  Charge's shield is the biggest body in the plant, so putting it on the
        # finest tiling is the mistake this check exists to catch.
        a, b = T(e, 'Charge'), T(e, 'Time')
        for key in ('P', 'Q', 'parents', 'spacing', 'radius', 'bond', 'seat'):
            a[key], b[key] = b[key], a[key]
    control('the biggest body put on the tightest tessellation', misordered,
            'the more room per site, the bigger the body')

    # ---- what THIS pass claims, each against the shape it replaced
    control('MASS left as a flat lozenge at the same volume',
            lambda e: T(e, 'Mass').__setitem__('leaf',
                T(e, 'Mass')['leaf'] * np.array([2.5, 1.0, 1 / 2.5])), 'CHUNKIEST of the plates')
    control('MASS taken all the way to a cube',
            lambda e: T(e, 'Mass').__setitem__('leaf',
                np.full(3, float(np.prod(T(e, 'Mass')['leaf'])) ** (1 / 3))),
            'a cube is not a plate')
    def anchor_sized(e):
        # Space shrunk back onto the anchor's membrane - a full SIMILARITY, so it is still
        # symmetric, still connected, still clears and still spends no extra volume.  The
        # only thing it loses is the reach, which is exactly what this element is for.
        t, k = T(e, 'Space'), T(e)['radius'] / T(e, 'Space')['radius']
        t['P'] = t['P'] * k
        for key in ('radius', 'spacing', 'bond', 'seat'):
            t[key] = t[key] * k
        t['leaf'] = t['leaf'] * k
    control('SPACE shrunk back onto the anchor-sized membrane', anchor_sized,
            'spans the furthest')

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
    print('\nOK: four symmetric minimal-surface tables on the Borromean rings, '
          'each tiled so that no prism interpenetrates another')
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
