"""Decisive chirality check of the BAKED C# octagon tables under Unity semantics.

The user suspects a handedness slip somewhere in the construction. This test consumes the
exact numbers shipped in GyroidOctagonData.cs (quaternions included - the one link the sim
never exercised, since the sim consumed matrices), composes them the way AssembledFlora
does (member.Rotation * SeedRotation, member.Position + member.Rotation * v), and checks
against the reference lattice walked from the bond table:

  A) SELF-CENTER: daughter seed pose -> seedPos + R_seed @ B[seedType] must equal the
     claimed neighbour centre (this is what RegisterDangerPrism recomputes on her side).
  B) MATING: the seed pose must coincide with a real reference-lattice prism - position,
     TYPE, and rotation (a chirality error leaves position near-right and rotation wrong).
  C) SUBTREE: a bond-table walk from the seed pose must land on the reference lattice
     everywhere (one bad handoff twins the whole subtree - this catches sub-degree drift).
"""
import re, json, collections
import numpy as np
from scipy.spatial import cKDTree

import os
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SEP = 3.0
SITES = ['TopRight', 'TopLeft', 'BottomLeft', 'BottomRight']
DANGER = {'GEs', 'DE', 'EG', 'EsD'}
# --- bond table (same parser as the measure script) ---
src = open(REPO + '/Assets/_Scripts/Controller/Assemblers/GyroidBondMateDataContainer.cs',
           encoding='utf-8-sig').read()
pat = re.compile(r'\(GyroidBlockType\.(\w+),\s*CornerSiteType\.(\w+)\),\s*new GyroidBondMateData\s*\{(.*?)\}\s*\}', re.S)
def vec(body, name):
    m = re.search(name + r'\s*=\s*new Vector3\(([^)]*)\)', body)
    return np.array([float(x.strip().rstrip('f')) for x in m.group(1).split(',')])
TABLE = {}
for m in pat.finditer(src):
    bt, site, body = m.groups()
    TABLE[(bt, site)] = dict(dp=vec(body, 'DeltaPosition'), du=vec(body, 'DeltaUp'),
                             df=vec(body, 'DeltaForward'),
                             child=re.search(r'BlockType\s*=\s*GyroidBlockType\.(\w+)', body).group(1))
assert len(TABLE) == 48

def look_rotation(f, u):
    f = np.asarray(f, float); n = np.linalg.norm(f)
    if n < 1e-9: return None
    f = f / n
    r = np.cross(u, f); rn = np.linalg.norm(r)
    if rn < 1e-9: return None
    r = r / rn
    return np.column_stack([r, np.cross(f, r), f])

def grow(pos, R, btype, site):
    e = TABLE.get((btype, site))
    if e is None: return None
    cp = pos + R.dot(e['dp'] * SEP)
    Rc = look_rotation(R.dot(e['df'] + [0, 0, 1.0]), R.dot(e['du'] + [0, 1.0, 0]))
    return None if Rc is None else (cp, Rc, e['child'])

def walk(seed_pos, seed_R, seed_type, budget):
    nodes = [(np.array(seed_pos, float), np.array(seed_R, float), seed_type)]
    pts = [np.array(seed_pos, float)]
    fr = collections.deque([0])
    while fr and len(nodes) < budget:
        i = fr.popleft(); pos, R, bt = nodes[i]
        for s in SITES:
            g = grow(pos, R, bt, s)
            if g is None: continue
            cp, cR, cbt = g
            t = cKDTree(np.array(pts))
            d, _ = t.query(cp)
            if d < 2.5 or len(nodes) >= budget: continue
            nodes.append((cp, cR, cbt)); pts.append(cp); fr.append(len(nodes) - 1)
    return nodes

# --- baked C# tables (parse GyroidOctagonData.cs verbatim) ---
oct_src = open(REPO + '/Assets/_Scripts/Controller/Assemblers/GyroidOctagonData.cs',
               encoding='utf-8-sig').read()
B = {}
for m in re.finditer(r'\{ GyroidBlockType\.(\w+), new Vector3\(([^)]*)\) \},', oct_src):
    B[m.group(1)] = np.array([float(x.strip().rstrip('f')) for x in m.group(2).split(',')])
NEI = collections.defaultdict(list)
cur = None
for line in oct_src.splitlines():
    m = re.search(r'\{ GyroidBlockType\.(\w+), new\[\]', line)
    if m: cur = m.group(1); continue
    m = re.search(r'new OctagonNeighbor\(new Vector3\(([^)]*)\), new Vector3\(([^)]*)\), '
                  r'new Quaternion\(([^)]*)\), GyroidBlockType\.(\w+)\)', line)
    if m and cur:
        f = lambda s: np.array([float(x.strip().rstrip('f')) for x in s.split(',')])
        NEI[cur].append(dict(center=f(m.group(1)), seedPos=f(m.group(2)),
                             quat=f(m.group(3)), seedType=m.group(4)))
assert all(len(NEI[t]) == 4 for t in DANGER), {t: len(NEI[t]) for t in DANGER}

def unity_quat_to_mat(q):
    x, y, z, w = q
    return np.array([
        [1 - 2*(y*y + z*z), 2*(x*y - z*w),     2*(x*z + y*w)],
        [2*(x*y + z*w),     1 - 2*(x*x + z*z), 2*(y*z - x*w)],
        [2*(x*z - y*w),     2*(y*z + x*w),     1 - 2*(x*x + y*y)]])

# --- reference lattice ---
print('walking reference lattice (2500 prisms)...')
ref = walk(np.zeros(3), np.eye(3), 'AB', 2500)
rp = np.array([n[0] for n in ref]); rt = [n[2] for n in ref]; rR = [n[1] for n in ref]
tree = cKDTree(rp)

# danger 8-rings in the reference (for full-ring member selection)
pairs = tree.query_pairs(8.8)
und = collections.defaultdict(set)
for a, b in pairs: und[a].add(b); und[b].add(a)
dang = [i for i in range(len(rp)) if rt[i] in DANGER]
# interior danger prisms only (full 4-neighbourhood) so every projection targets real lattice
interior = [i for i in dang if len(und[i]) == 4 and np.linalg.norm(rp[i]) < 60]
print(f'{len(rp)} reference prisms, {len(interior)} interior danger members tested')

rotA = collections.defaultdict(float)
worstA = worstB_pos = worstB_rot = worstC = 0.0
b_type_fail = 0
tested = 0
for i in interior:
    for n in NEI[rt[i]]:
        tested += 1
        claimed = rp[i] + rR[i].dot(n['center'])
        seedPos = rp[i] + rR[i].dot(n['seedPos'])
        R_s = rR[i].dot(unity_quat_to_mat(n['quat']))
        # A: self-centre coherence - what RegisterDangerPrism recomputes daughter-side
        errA = np.linalg.norm(seedPos + R_s.dot(B[n['seedType']]) - claimed)
        worstA = max(worstA, errA)
        # B: the seed must BE a reference prism (pos + type + rotation)
        d, j = tree.query(seedPos)
        worstB_pos = max(worstB_pos, d)
        if rt[j] != n['seedType']: b_type_fail += 1
        ang = np.degrees(np.arccos(np.clip((np.trace(rR[j].T.dot(R_s)) - 1) / 2, -1, 1)))
        worstB_rot = max(worstB_rot, ang)

print(f'\nA self-centre coherence : worst {worstA:.4f} u over {tested} projections')
print(f'B seed on lattice       : worst pos {worstB_pos:.4f} u, worst rot {worstB_rot:.4f} deg, type mismatches {b_type_fail}')

# C: subtree walks from a sample of baked seed poses vs reference
sample = interior[::7][:12]
for i in sample:
    n = NEI[rt[i]][0]
    seedPos = rp[i] + rR[i].dot(n['seedPos'])
    R_s = rR[i].dot(unity_quat_to_mat(n['quat']))
    sub = walk(seedPos, R_s, n['seedType'], 40)
    dmax = max(tree.query(p)[0] for p, _, _ in sub)
    worstC = max(worstC, dmax)
print(f'C subtree mating        : worst deviation from reference lattice {worstC:.4f} u '
      f'({len(sample)} subtrees x 40 prisms)')

ok = worstA < 1 and worstB_pos < 1 and worstB_rot < 2 and b_type_fail == 0 and worstC < 1
print('\nVERDICT:', 'BAKED TABLES + UNITY COMPOSITION ARE CHIRALITY-CLEAN' if ok else 'CHIRALITY DEFECT CONFIRMED IN THE BAKED PATH')
