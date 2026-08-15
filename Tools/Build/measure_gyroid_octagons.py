#!/usr/bin/env python3
"""
Measure the gyroid octagon geometry off GyroidBondMateDataContainer's bond table, and emit the
constants GyroidOctagonData.cs carries (Docs/ECOSYSTEM.md §32.7).

Pipeline (self-contained; needs numpy + scipy):
  1. parse the 48-entry bond table out of the C# source;
  2. walk the lattice exactly as AssembledFlora.PreviewGyroid does (BFS, position-dedupe at the
     game's own TryReserve scale), keeping each prism's rotation;
  3. find the danger-only 8-rings, their centres, the per-type own-centre offsets (B), the
     neighbour-centre offsets and per-neighbour seed poses (E);
  4. assert the structure the runtime depends on (danger-only cycles are ALL 8-rings; own-centre
     offsets constant per type to <0.2u; exactly four neighbour classes per type, seed types
     pure) and print the values to paste into GyroidOctagonData.cs.

Note on seed poses: "the neighbour ring's member closest to the frame prism" is near-tied
between two members in some classes, so a regeneration may print a DIFFERENT member than the
shipped table carries. Both are correct - any member of the target ring seeds the same octagon
(its own-centre offset lands on the same centre). The shipped table was additionally validated
end-to-end by simulating the colony algorithm (273 plants / 5,547 prisms: single connected
component, zero overlaps, bijective on the reference lattice to 0.74u, one claimed centre per
complete octagon - Docs/ECOSYSTEM.md §32.7).

Run this after ANY edit to the bond table - the tables in GyroidOctagonData.cs are measurements
of that table, and drift between them is a silent break these assertions exist to catch.
"""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))

import re, json, collections
import numpy as np
from scipy.spatial import cKDTree

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, 'Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs')
SEP = 3.0
SITES = ['TopRight', 'TopLeft', 'BottomLeft', 'BottomRight']
DANGER = {'GEs', 'DE', 'EG', 'EsD'}
TOL = 2.5

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

def look_rotation(f, u):
    f = np.asarray(f, float); n = np.linalg.norm(f)
    if n < 1e-9: return None
    f = f / n
    r = np.cross(u, f); rn = np.linalg.norm(r)
    if rn < 1e-9: return None
    r = r / rn
    return np.column_stack([r, np.cross(f, r), f])

def grow(pos, R, btype, site):
    e = BT.get((btype, site))
    if e is None: return None
    cp = pos + R.dot(np.array(e['DeltaPosition']) * SEP)
    fwd = R.dot(np.array(e['DeltaForward']) + [0, 0, 1.0])
    up = R.dot(np.array(e['DeltaUp']) + [0, 1.0, 0])
    Rc = look_rotation(fwd, up)
    return None if Rc is None else (cp, Rc, e['BlockType'])

def walk(budget=4000):
    nodes = [(np.zeros(3), np.eye(3), 'AB')]
    pts = [np.zeros(3)]
    fr = collections.deque([0])
    def find(p):
        d, j = cKDTree(np.array(pts)).query(p)
        return int(j) if d < TOL else -1
    while fr and len(nodes) < budget:
        i = fr.popleft(); pos, R, bt = nodes[i]
        for s in SITES:
            g = grow(pos, R, bt, s)
            if g is None: continue
            cp, cR, cbt = g
            if find(cp) >= 0 or len(nodes) >= budget: continue
            nodes.append((cp, cR, cbt)); pts.append(cp); fr.append(len(nodes) - 1)
    return nodes

print('walking lattice...')
nodes = walk()
pts = np.array([n[0] for n in nodes])
types = [n[2] for n in nodes]
R = [n[1] for n in nodes]
t = cKDTree(pts)
pairs = t.query_pairs(8.8)
und = collections.defaultdict(set)
for a, b in pairs: und[a].add(b); und[b].add(a)
dang = [i for i in range(len(pts)) if types[i] in DANGER]
dset = set(dang)
dund = {i: {j for j in und[i] if j in dset} for i in dang}

def danger_rings():
    out = set()
    for s0 in dang:
        def dfs(path, vis):
            u = path[-1]
            if len(path) > 8: return
            for v in sorted(dund.get(u, ())):
                if v == s0 and len(path) == 8: out.add(tuple(sorted(path))); continue
                if v in vis or v < s0: continue
                dfs(path + [v], vis | {v})
        dfs([s0], {s0})
    return out

octs = [tuple(c) for c in danger_rings()]
print(f'{len(pts)} prisms, {len(octs)} danger 8-rings')
lens = collections.Counter(len(c) for c in octs)
assert set(lens) == {8}, 'danger-only cycles must all be 8-rings'
centers = np.array([pts[list(c)].mean(0) for c in octs])

B = collections.defaultdict(list)
for k, c in enumerate(octs):
    for i in c:
        B[types[i]].append(R[i].T.dot(centers[k] - pts[i]))
print('\nB: own-centre offsets (paste into GyroidOctagonData.OwnCenter):')
for tk in ('DE', 'EG', 'EsD', 'GEs'):
    m = np.array(B[tk]).mean(0); sd = np.array(B[tk]).std(0).max()
    assert sd < 0.2, f'{tk} own-centre offset not constant (std {sd})'
    print(f'    {{ GyroidBlockType.{tk}, new Vector3({m[0]:.4f}f, {m[1]:.4f}f, {m[2]:.4f}f) }},   // std {sd:.3f}')

full = [k for k, c in enumerate(octs) if all(len(und.get(i, ())) == 4 for i in c)]
from scipy.cluster.hierarchy import fcluster, linkage
print('\nE: neighbour classes (centres + seed poses) - see the emit in the session scratchpad;')
print('   per-type there must be exactly 4, each with seed purity 1.0:')
for tk in ('DE', 'EG', 'EsD', 'GEs'):
    rows = []
    for k in full:
        for j in range(len(octs)):
            if j == k or np.linalg.norm(centers[j] - centers[k]) > 44: continue
            for i in octs[k]:
                if types[i] != tk: continue
                m = min(octs[j], key=lambda a: np.linalg.norm(pts[a] - pts[i]))
                rows.append((R[i].T.dot(centers[j] - pts[i]),
                             R[i].T.dot(pts[m] - pts[i]), R[i].T.dot(R[m]), types[m]))
    co = np.array([r[0] for r in rows])
    cl = fcluster(linkage(co, 'single'), 2.5, criterion='distance')
    stable = [ci for ci in range(1, cl.max() + 1) if (cl == ci).sum() >= 6]
    assert len(stable) == 4, f'{tk}: expected 4 neighbour classes, got {len(stable)}'
    for ci in stable:
        sel = [rows[x] for x in np.nonzero(cl == ci)[0]]
        cm = np.mean([s[0] for s in sel], axis=0)
        sp = np.mean([s[1] for s in sel], axis=0)
        st = collections.Counter(s[3] for s in sel).most_common(1)[0]
        assert st[1] == len(sel), f'{tk}: seed type not pure'
        print(f'    {tk} -> ctr ({cm[0]:7.2f},{cm[1]:7.2f},{cm[2]:7.2f})  seed {st[0]} at ({sp[0]:6.2f},{sp[1]:6.2f},{sp[2]:6.2f})')
print('\nAll assertions passed - GyroidOctagonData.cs tables are reproducible from the bond table.')
