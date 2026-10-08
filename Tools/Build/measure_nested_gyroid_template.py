#!/usr/bin/env python3
"""
Measure the GYROID FLORA's own tiling as one periodic cell, and emit it as the nested gyroid's base-sheet template
(Assets/_Scripts/Controller/Environment/FloraAndFauna/NestedGyroidTemplate.cs). Docs/ECOSYSTEM.md §58.7.

    python3 Tools/Build/measure_nested_gyroid_template.py           # report
    python3 Tools/Build/measure_nested_gyroid_template.py --check   # fail if the shipped table drifted
    python3 Tools/Build/measure_nested_gyroid_template.py --write   # emit the C#

Needs numpy + scipy (same as measure_gyroid_octagons.py).

WHAT IT MEASURES, and why each number is a measurement rather than a choice:
  1. walk the 48-entry bond table (GyroidBondMateDataContainer.cs) exactly as measure_gyroid_octagons.py does;
  2. recover the lattice's TRANSLATION symmetry from same-type, same-attitude prism pairs. It is body-centred
     cubic: three orthogonal translations of one length a (the gyroid's conventional cell) plus the body
     diagonals at a·√3/2 - the gyroid's own space-group lattice. a comes out at 120.00 (bond spacing 3);
  3. fit the cube frame (a PROPER rotation - a reflection would mirror every prism) and the offset that put the
     walked prisms on G = 0 of the standard gyroid sin x cos y + sin y cos z + sin z cos x, x = 2π·c/a;
  4. fold every prism into one cell, average each site's periodic images (the walk's chained rotations drift a
     little per hop, so a single image is noisier than the mean), and assert the structure:
       * exactly 576 sites, 48 of each of the 12 block types;
       * every site within |G| < 0.15 of the surface and its local +z within 15° of ∇G;
       * the four danger types (DE, EG, GEs, EsD) - the octagon rings - are 192 sites (24 rings of 8).
  5. find the 24 OCTAGON TILES - one gyroid flora plant each (Docs/ECOSYSTEM.md §32.7): the danger-only bond
     graph's 8-rings (their centres are where the crystals sit), and each site's OWNER, assigned by distance ALONG
     THE BOND GRAPH from the rings (multi-source Dijkstra). Straight-line nearest-centre is wrong on a curved
     surface: it hands 17 of 24 tiles a stray site across a channel (25.8u from its centre, 13.7u from the rest
     of its tile); the gyroid flora escapes that only because a prism grows from an existing branch, which IS the
     bond-graph distance. Asserted: 24 rings of 8, tiles of 22-28 sites (the gyroid flora's measured patches),
     every tile connected through its own bonds, exactly four neighbouring tiles each, symmetric.
The C# table stores, per site, its position in CELL units (fractions of a), its local +z (the surface normal,
signed as the flora built it) and +y in the cube frame, and its block type. NestedGyroidBuilder Newton-snaps each
site onto G = 0 exactly and carries it along ∇G/|∇G| to every nested level, so every sheet of the stack has the
gyroid flora's own loop subdivisions.
"""
import argparse, collections, heapq, os, re, sys
import numpy as np
from scipy.optimize import minimize
from scipy.spatial import cKDTree

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
assert os.path.isdir(os.path.join(ROOT, 'Assets')), f'repo root resolved to {ROOT}, which has no Assets/'
SRC = os.path.join(ROOT, 'Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs')
OUT = os.path.join(ROOT, 'Assets/_Scripts/Controller/Environment/FloraAndFauna/NestedGyroidTemplate.cs')
SEP = 3.0
SITES = ['TopRight', 'TopLeft', 'BottomLeft', 'BottomRight']
TOL = 2.5
DANGER = {'DE', 'EG', 'GEs', 'EsD'}
# GyroidBlockType's serialized values (GyroidAssembler.cs) - the table carries the type itself.
TYPE_ID = dict(AB=1, BC=2, CD=3, DE=4, EF=5, EG=6, BA=7, AF=8, FG=9, GEs=10, EsC=11, EsD=12)


def parse_bond_table():
    src = open(SRC, encoding='utf-8-sig').read()
    pat = re.compile(r'\(GyroidBlockType\.(\w+),\s*CornerSiteType\.(\w+)\),\s*new GyroidBondMateData\s*\{(.*?)\}\s*\}', re.S)
    def vec(body, name):
        m = re.search(name + r'\s*=\s*new Vector3\(([^)]*)\)', body)
        return [float(x.strip().rstrip('f')) for x in m.group(1).split(',')]
    table = {}
    for m in pat.finditer(src):
        bt, site, body = m.groups()
        e = {f: vec(body, f) for f in ('DeltaPosition', 'DeltaUp', 'DeltaForward')}
        e['BlockType'] = re.search(r'BlockType\s*=\s*GyroidBlockType\.(\w+)', body).group(1)
        table[(bt, site)] = e
    assert len(table) == 48, f'expected 48 bond entries, parsed {len(table)}'
    return table


BT = parse_bond_table()


def look(f, u):
    f = np.asarray(f, float); f /= np.linalg.norm(f)
    r = np.cross(u, f); r /= np.linalg.norm(r)
    return np.column_stack([r, np.cross(f, r), f])


def walk(budget):
    nodes = [(np.zeros(3), np.eye(3), 'AB')]
    pts = [np.zeros(3)]
    frontier = collections.deque([0])
    while frontier and len(nodes) < budget:
        pos, R, bt = nodes[frontier.popleft()]
        for s in SITES:
            e = BT.get((bt, s))
            if e is None: continue
            cp = pos + R.dot(np.array(e['DeltaPosition']) * SEP)
            if np.min(np.linalg.norm(np.array(pts) - cp, axis=1)) < TOL or len(nodes) >= budget: continue
            cR = look(R.dot(np.array(e['DeltaForward']) + [0, 0, 1.0]), R.dot(np.array(e['DeltaUp']) + [0, 1.0, 0]))
            nodes.append((cp, cR, e['BlockType'])); pts.append(cp); frontier.append(len(nodes) - 1)
    return nodes


def G(q):
    return np.sin(q[:, 0]) * np.cos(q[:, 1]) + np.sin(q[:, 1]) * np.cos(q[:, 2]) + np.sin(q[:, 2]) * np.cos(q[:, 0])


def grad(q):
    sx, cx, sy, cy, sz, cz = (np.sin(q[:, 0]), np.cos(q[:, 0]), np.sin(q[:, 1]), np.cos(q[:, 1]),
                              np.sin(q[:, 2]), np.cos(q[:, 2]))
    return np.stack([cx * cy - sz * sx, cy * cz - sx * sy, cz * cx - sy * sz], 1)


def measure():
    nodes = walk(5000)
    P = np.array([n[0] for n in nodes]); Rs = np.array([n[1] for n in nodes]); types = np.array([n[2] for n in nodes])

    # 2. translations: same type, attitude within 6°.
    D = []
    for i in range(0, len(P), 7):
        for j in np.where(types == types[i])[0]:
            if j == i: continue
            c = np.clip((np.trace(Rs[i].T @ Rs[j]) - 1) / 2, -1, 1)
            if np.degrees(np.arccos(c)) < 6 and np.linalg.norm(P[j] - P[i]) < 150:
                D.append(P[j] - P[i])
    clusters = []
    for v in D:
        for c in clusters:
            if np.linalg.norm(c[0] / c[1] - v) < 3: c[0] += v; c[1] += 1; break
        else:
            clusters.append([v.copy(), 1])
    vecs = sorted([c[0] / c[1] for c in clusters if c[1] > 5], key=np.linalg.norm)
    diag = np.linalg.norm(vecs[0])
    edges = [v for v in vecs if abs(np.linalg.norm(v) - diag * 2 / np.sqrt(3)) < 1.0]
    axes = []
    for v in edges:
        if all(abs(np.dot(v, w)) / (np.linalg.norm(v) * np.linalg.norm(w)) < 0.05 for w in axes): axes.append(v)
    assert len(axes) == 3, f'expected three orthogonal cube translations, found {len(axes)}'
    a = float(np.mean([np.linalg.norm(v) for v in axes]))
    assert abs(diag - a * np.sqrt(3) / 2) < 0.5, 'shortest translation is not the BCC body diagonal'

    # 3. cube frame (proper) + offset onto G = 0.
    E = np.array(axes) / np.linalg.norm(axes, axis=1)[:, None]
    U, _, Vt = np.linalg.svd(E); E = U @ Vt
    import itertools
    best = None
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product([1, -1], repeat=3):
            M = np.array([E[perm[k]] * signs[k] for k in range(3)])
            if np.linalg.det(M) < 0: continue           # never a reflection
            f = lambda t, M=M: np.mean(G((P - t) @ M.T * (2 * np.pi / a)) ** 2)
            for s in range(4):
                r = minimize(f, np.random.RandomState(s).uniform(-60, 60, 3), method='Nelder-Mead',
                             options={'xatol': 1e-4, 'fatol': 1e-10, 'maxiter': 6000})
                if best is None or r.fun < best[0]: best = (r.fun, M, r.x)
    _, M, t = best

    # 4. fold into one cell, averaging periodic images.
    C = (P - t) @ M.T
    F = np.einsum('ij,njk->nik', M, Rs)
    W = np.mod(C, a)
    reps, members = [], []
    for i in range(len(W)):
        for k, r in enumerate(reps):
            d = W[i] - W[r]; d -= a * np.round(d / a)
            if np.linalg.norm(d) < TOL and types[i] == types[r]:
                members[k].append(i); break
        else:
            reps.append(i); members.append([i])
    sites = []
    for r, ms in zip(reps, members):
        base = W[r]
        offs = []
        for i in ms:
            d = W[i] - base; d -= a * np.round(d / a); offs.append(base + d)
        pos = np.mean(offs, 0)
        fwd = np.mean([F[i][:, 2] for i in ms], 0); fwd /= np.linalg.norm(fwd)
        up = np.mean([F[i][:, 1] for i in ms], 0); up -= np.dot(up, fwd) * fwd; up /= np.linalg.norm(up)
        sites.append((pos / a, fwd, up, types[r]))
    sites.sort(key=lambda s: (TYPE_ID[s[3]], round(s[0][0], 4), round(s[0][1], 4), round(s[0][2], 4)))

    q = np.array([s[0] for s in sites]) * 2 * np.pi
    g = G(q); n = grad(q); n /= np.linalg.norm(n, axis=1)[:, None]
    align = np.abs(np.einsum('ij,ij->i', n, np.array([s[1] for s in sites])))
    counts = collections.Counter(s[3] for s in sites)
    tiles = measure_tiles(sites)
    return dict(a=a, sites=sites, gmax=float(np.abs(g).max()), gmean=float(np.abs(g).mean()),
                align_min=float(align.min()), counts=counts, images=float(np.mean([len(m) for m in members])),
                **tiles)


BOND_LINK = 1.4 * 8.0 / 120.0   # a template bond, in cell units (bonds measure 7.8-8.3 at period 120)


def measure_tiles(sites):
    X = np.array([s[0] for s in sites])
    n = len(X)
    mind = lambda d: d - np.round(d)
    D = np.linalg.norm(mind(X[None, :, :] - X[:, None, :]), axis=2)
    nbr = [[j for j in range(n) if j != i and D[i, j] < BOND_LINK] for i in range(n)]
    danger = [i for i in range(n) if sites[i][3] in DANGER]
    dset = set(danger)
    seen, rings = set(), []
    for i in danger:
        if i in seen: continue
        comp, stack = [], [i]
        seen.add(i)
        while stack:
            u = stack.pop(); comp.append(u)
            for v in nbr[u]:
                if v in dset and v not in seen: seen.add(v); stack.append(v)
        rings.append(sorted(comp))
    rings.sort()
    centres = []
    for r in rings:
        base = X[r[0]]
        centres.append(np.mean([base + mind(X[j] - base) for j in r], 0))
    centres = np.mod(np.array(centres), 1.0)

    # Ownership by distance along the bond graph, ties by straight-line distance to the centre, then index.
    dist, owner, heap = [np.inf] * n, [-1] * n, []
    for k, r in enumerate(rings):
        for j in r:
            dist[j] = 0.0
            heapq.heappush(heap, (0.0, float(np.linalg.norm(mind(X[j] - centres[k]))), k, j))
    done = [False] * n
    while heap:
        d, _, k, u = heapq.heappop(heap)
        if done[u]: continue
        done[u] = True; owner[u] = k
        for v in nbr[u]:
            nd = d + D[u, v]
            if not done[v] and nd <= dist[v] + 1e-9:
                dist[v] = min(dist[v], nd)
                heapq.heappush(heap, (nd, float(np.linalg.norm(mind(X[v] - centres[k]))), k, v))
    shift = [np.round(centres[owner[i]] - X[i]) for i in range(n)]     # site image nearest its owner: X + shift

    tile_ok = True
    for k in range(len(rings)):
        mem = [i for i in range(n) if owner[i] == k]
        got, stack = {mem[0]}, [mem[0]]
        while stack:
            u = stack.pop()
            for v in mem:
                if v not in got and D[u, v] < BOND_LINK: got.add(v); stack.append(v)
        tile_ok &= len(got) == len(mem)

    neighbours = []
    for k in range(len(rings)):
        row = []
        for j in range(len(rings)):
            if j == k: continue
            d = centres[j] - centres[k]
            sh = -np.round(d)
            if np.linalg.norm(d + sh) < 44.0 / 120.0: row.append((j, sh))
        row.sort(key=lambda r: (r[0], tuple(r[1])))
        neighbours.append(row)
    symmetric = all(any(j2 == k and np.allclose(sh2, -sh) for j2, sh2 in neighbours[j])
                    for k in range(len(rings)) for j, sh in neighbours[k])
    sizes = collections.Counter(owner)
    return dict(rings=rings, centres=centres, owner=owner, shift=shift, tiles_connected=tile_ok,
                tile_sizes=sorted(sizes.values()), neighbours=neighbours, symmetric=symmetric)


def emit(m):
    f = lambda x: f'{x:.6f}f'
    lines = [
        '// GENERATED by Tools/Build/measure_nested_gyroid_template.py - do not edit by hand; re-run it with --write.',
        '// The GYROID FLORA\'s own tiling (GyroidBondMateDataContainer\'s 48-entry bond table, walked) folded into one',
        '// periodic cell of the standard gyroid sin x cos y + sin y cos z + sin z cos x, x = 2π·c. Docs/ECOSYSTEM.md §58.7.',
        '// Pure C# (System.Numerics, no UnityEngine) so Tools/Build/nested_gyroid_harness runs the shipped table.',
        'using System.Numerics;',
        '',
        'namespace CosmicShore.Gameplay',
        '{',
        '    public static class NestedGyroidTemplate',
        '    {',
        f'        /// <summary>The gyroid flora\'s period at bond spacing 3 (FloraVariantTuning.LatticeScale 1), measured: the',
        f'        /// cube edge of its body-centred translation lattice.</summary>',
        f'        public const float Period = {m["a"]:.4f}f;',
        '',
        f'        /// <summary>Sites per cell: 48 of each of the 12 block types.</summary>',
        f'        public const int SiteCount = {len(m["sites"])};',
        '',
        '        /// <summary>Block type per site (GyroidBlockType values; DE, EG, GEs, EsD are the danger octagon rings).</summary>',
        '        public static readonly byte[] BlockType =',
        '        {',
    ]
    ids = [TYPE_ID[s[3]] for s in m['sites']]
    for i in range(0, len(ids), 24):
        lines.append('            ' + ', '.join(str(x) for x in ids[i:i + 24]) + ',')
    lines += ['        };', '',
              '        /// <summary>Position in CELL units (multiply by the period); local +z (surface normal) and +y, cube frame.</summary>',
              '        public static readonly Vector3[] Position =', '        {']
    lines += [f'            new Vector3({f(p[0])}, {f(p[1])}, {f(p[2])}),' for p, _, _, _ in m['sites']]
    lines += ['        };', '', '        public static readonly Vector3[] Forward =', '        {']
    lines += [f'            new Vector3({f(v[0])}, {f(v[1])}, {f(v[2])}),' for _, v, _, _ in m['sites']]
    lines += ['        };', '', '        public static readonly Vector3[] Up =', '        {']
    lines += [f'            new Vector3({f(v[0])}, {f(v[1])}, {f(v[2])}),' for _, _, v, _ in m['sites']]
    lines += ['        };', '',
              f'        /// <summary>The octagon TILES: one gyroid flora plant each - its 8-ring of danger sites and the sites',
              f'        /// nearest it along the bond graph. Measured: sizes {m["tile_sizes"][0]}-{m["tile_sizes"][-1]}, every tile connected.</summary>',
              f'        public const int OctagonCount = {len(m["rings"])};', '',
              '        /// <summary>Each octagon\'s centre in CELL units, in [0, 1) - where its plant\'s crystal sits.</summary>',
              '        public static readonly Vector3[] OctagonCenter =', '        {']
    lines += [f'            new Vector3({f(c[0])}, {f(c[1])}, {f(c[2])}),' for c in m['centres']]
    lines += ['        };', '',
              '        /// <summary>The octagon that owns each site.</summary>',
              '        public static readonly byte[] SiteOwner =', '        {']
    for i in range(0, len(m['owner']), 24):
        lines.append('            ' + ', '.join(str(x) for x in m['owner'][i:i + 24]) + ',')
    lines += ['        };', '',
              '        /// <summary>Whole-cell shift that puts a site\'s image beside its owner\'s centre: Position + SiteShift.</summary>',
              '        public static readonly Vector3[] SiteShift =', '        {']
    lines += [f'            new Vector3({int(v[0])}, {int(v[1])}, {int(v[2])}),' for v in m['shift']]
    lines += ['        };', '',
              '        /// <summary>Each octagon\'s FOUR neighbouring tiles, four entries per octagon: the neighbour\'s index, and the',
              '        /// whole-cell shift that puts that neighbour\'s centre beside this one (OctagonCenter[n] + shift).</summary>',
              '        public static readonly byte[] NeighborOctagon =', '        {']
    flat = [j for row in m['neighbours'] for j, _ in row]
    for i in range(0, len(flat), 24):
        lines.append('            ' + ', '.join(str(x) for x in flat[i:i + 24]) + ',')
    lines += ['        };', '', '        public static readonly Vector3[] NeighborShift =', '        {']
    lines += [f'            new Vector3({int(sh[0])}, {int(sh[1])}, {int(sh[2])}),' for row in m['neighbours'] for _, sh in row]
    lines += ['        };', '',
              '        /// <summary>True for the four danger block types - the octagon rings of the gyroid flora.</summary>',
              '        public static bool IsDangerType(byte type) => type == 4 || type == 6 || type == 10 || type == 12;',
              '    }', '}', '']
    return '\n'.join(lines)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--write', action='store_true')
    args = ap.parse_args()
    m = measure()
    danger = sum(1 for s in m['sites'] if s[3] in DANGER)
    print(f'gyroid flora tiling: period a = {m["a"]:.3f}, {len(m["sites"])} sites per cell '
          f'({m["images"]:.1f} periodic images averaged per site), danger sites {danger} = {danger // 8} octagons')
    print(f'  on G = 0: |G| mean {m["gmean"]:.4f}, max {m["gmax"]:.4f};  local +z vs ∇G: worst |cos| {m["align_min"]:.4f}')
    ok = True
    def gate(name, cond):
        nonlocal ok
        print(f'  [{"PASS" if cond else "FAIL"}] {name}')
        ok &= cond
    gate('period is 120 at bond spacing 3', abs(m['a'] - 120.0) < 0.5)
    gate('576 sites, 48 of each of the 12 block types',
         len(m['sites']) == 576 and set(m['counts'].values()) == {48} and len(m['counts']) == 12)
    gate('every site on the surface (|G| < 0.15)', m['gmax'] < 0.15)
    gate('every site\'s +z within 15° of ∇G', m['align_min'] > np.cos(np.radians(15)))
    gate('192 danger sites (24 octagon rings)', danger == 192)
    gate('24 danger 8-rings (one gyroid flora plant each)', len(m['rings']) == 24 and all(len(r) == 8 for r in m['rings']))
    gate(f'tiles of 22-28 sites (measured {m["tile_sizes"][0]}-{m["tile_sizes"][-1]}), each connected through its own bonds',
         m['tile_sizes'][0] >= 22 and m['tile_sizes'][-1] <= 28 and m['tiles_connected'] and sum(m['tile_sizes']) == 576)
    gate('exactly four neighbouring tiles each, symmetric',
         all(len(r) == 4 for r in m['neighbours']) and m['symmetric'])
    if not ok: return 1
    text = emit(m)
    if args.write:
        open(OUT, 'w').write(text)
        print(f'wrote {os.path.relpath(OUT, ROOT)}')
        return 0
    if args.check:
        if not os.path.exists(OUT) or open(OUT).read() != text:
            print(f'FAIL: {os.path.relpath(OUT, ROOT)} drifted from the measurement - re-run with --write')
            return 1
        print('OK: the shipped template matches the measurement')
    return 0


if __name__ == '__main__':
    sys.exit(main())
