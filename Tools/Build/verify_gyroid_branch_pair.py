#!/usr/bin/env python3
"""Prove the gyroid spindle's MIRRORED HALF-BRANCH PAIR against the shipped assets.

    python3 Tools/Build/verify_gyroid_branch_pair.py

Docs/ECOSYSTEM.md 34.12. The gyroid's branch used to be ONE mesh posed with its middle on the
prism, so every prism was skewered and the two sides of it showed different geometry. It is now
TWO half-branches meeting at the prism, mirrored about the prism plane.

Nothing here is hand-entered: the branch mesh's extents are read out of the source FBX and the
transforms out of the prefab YAML, so this re-proves the SHIPPED files rather than a description
of them. That distinction is the whole point - a measurement that is correct offline and then
hand-carried into an asset is exactly the step neither the measurement nor code review can see
(the octagon-table z-mirror corruption, 34.8 / the asset-surgery transcription trap).

Asserted:
  * the total span across the prism is unchanged from the single branch it replaces;
  * both halves' TIPS land on the spindle origin (the prism), so neither pierces it;
  * the pair is an exact mirror about the prism plane;
  * each half is exactly half the old branch's length;
  * the LATERAL scale is untouched, so the branch's shape and visual weight are preserved;
  * every gyroid flora points at the paired prefab, and Wall / Schwarz P still point at the
    single-branch one (34.8: a gyroid decision must not move Schwarz P's approved proportions).

Exits non-zero on any failure.
"""
import math
import os
import re
import struct
import sys
import zlib

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FBX = os.path.join(REPO, 'Assets/_Models/Fauna/bonita.fbx')
SPINDLES = os.path.join(REPO, 'Assets/_Prefabs/FloraAndFauna/Spindles')
SINGLE = os.path.join(SPINDLES, 'AssemblyBranch.prefab')
PAIRED = os.path.join(SPINDLES, 'GyroidBranch.prefab')
FLORA = os.path.join(REPO, 'Assets/_Prefabs/FloraAndFauna')

BRANCH_MESH_NAME = 'BezierCurve.001'
# Unity divides FBX vertices by 100 here: the file declares UnitScaleFactor 1.0 (centimetres) and
# bonita.fbx.meta has useFileUnits: 1 / globalScale: 1. Cross-checked independently against the
# sibling Branch.prefab, which is authored so the mesh's +z end lands on the spindle origin - true
# only at this factor (residual -0.0067u).
FBX_TO_UNITY = 0.01
EPS = 1e-3

failures = []


def check(ok, msg):
    print(('  [ok]   ' if ok else '  [FAIL] ') + msg)
    if not ok:
        failures.append(msg)


# --------------------------------------------------------------------------- FBX
class _Node:
    __slots__ = ('name', 'props', 'children')

    def __init__(self, name):
        self.name, self.props, self.children = name, [], []

    def find(self, n):
        return [c for c in self.children if c.name == n]


def _read_prop(f):
    t = f.read(1).decode('latin1')
    if t == 'Y': return struct.unpack('<h', f.read(2))[0]
    if t == 'C': return struct.unpack('<b', f.read(1))[0]
    if t == 'I': return struct.unpack('<i', f.read(4))[0]
    if t == 'F': return struct.unpack('<f', f.read(4))[0]
    if t == 'D': return struct.unpack('<d', f.read(8))[0]
    if t == 'L': return struct.unpack('<q', f.read(8))[0]
    if t in 'fdlib':
        n, enc, clen = struct.unpack('<III', f.read(12))
        raw = f.read(clen)
        if enc == 1:
            raw = zlib.decompress(raw)
        return list(struct.unpack('<%d%s' % (n, {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'b'}[t]), raw))
    if t in 'SR':
        return f.read(struct.unpack('<I', f.read(4))[0])
    raise ValueError('unknown FBX property type %r' % t)


def _read_node(f, ver):
    hdr = struct.unpack('<QQQ', f.read(24)) if ver >= 7500 else struct.unpack('<III', f.read(12))
    end, nprops, _ = hdr
    nlen = f.read(1)[0]
    if end == 0:
        return None
    node = _Node(f.read(nlen).decode('latin1'))
    for _ in range(nprops):
        node.props.append(_read_prop(f))
    while f.tell() < end:
        c = _read_node(f, ver)
        if c is None:
            break
        node.children.append(c)
    f.seek(end)
    return node


def branch_mesh_vertices():
    """The branch mesh's vertices, in Unity local units."""
    with open(FBX, 'rb') as f:
        assert f.read(21).startswith(b'Kaydara FBX Binary'), 'not a binary FBX'
        f.seek(23)
        ver = struct.unpack('<I', f.read(4))[0]
        root = _Node('__root__')
        while True:
            pos = f.tell()
            n = _read_node(f, ver)
            if n is None or f.tell() <= pos:
                break
            root.children.append(n)
    for o in root.find('Objects')[0].children:
        name = o.props[1].decode('latin1').split('\0')[0] if len(o.props) > 1 else ''
        if o.name == 'Geometry' and name == BRANCH_MESH_NAME:
            v = o.find('Vertices')[0].props[0]
            return [(v[i] * FBX_TO_UNITY, v[i + 1] * FBX_TO_UNITY, v[i + 2] * FBX_TO_UNITY)
                    for i in range(0, len(v), 3)]
    raise SystemExit('FAIL: %s not found in %s' % (BRANCH_MESH_NAME, FBX))


# ------------------------------------------------------------------------ prefab
CHILD_RE = (r'm_LocalRotation: \{x: (-?[\d.eE+]+), y: (-?[\d.eE+]+), z: (-?[\d.eE+]+), w: (-?[\d.eE+]+)\}',
            r'm_LocalPosition: \{x: (-?[\d.eE+]+), y: (-?[\d.eE+]+), z: (-?[\d.eE+]+)\}',
            r'm_LocalScale: \{x: (-?[\d.eE+]+), y: (-?[\d.eE+]+), z: (-?[\d.eE+]+)\}')


def branch_children(path):
    """(rotation, position, scale) for every non-root Transform document in a spindle prefab."""
    out = []
    for doc in open(path, encoding='utf-8-sig').read().split('--- !u!'):
        if not doc.startswith('4 &') or 'm_Father: {fileID: 0}' in doc:
            continue
        m = [re.search(r, doc) for r in CHILD_RE]
        assert all(m), 'unparsable Transform in %s' % path
        out.append(tuple(tuple(float(x) for x in g.groups()) for g in m))
    return out


def qrot_y(q, v):
    """Y component of `q` rotating `v` - the branch axis is the spindle's local Y."""
    x, y, z, w = q
    dot = x * v[0] + y * v[1] + z * v[2]
    s2 = w * w - (x * x + y * y + z * z)
    cross_y = z * v[0] - x * v[2]
    return 2 * dot * y + s2 * v[1] + 2 * w * cross_y


def span(child, zmin, zmax):
    """(tip_y, flare_y) of one branch child, in spindle-local space."""
    q, p, s = child
    return (qrot_y(q, (0, 0, zmax * s[2])) + p[1],
            qrot_y(q, (0, 0, zmin * s[2])) + p[1])


def main():
    pts = branch_mesh_vertices()
    zmin = min(p[2] for p in pts)
    zmax = max(p[2] for p in pts)
    print('branch mesh %s: %d verts, z in [%.6f, %.6f], length %.6f (Unity units)'
          % (BRANCH_MESH_NAME, len(pts), zmin, zmax, zmax - zmin))

    single = branch_children(SINGLE)
    paired = branch_children(PAIRED)
    print('\nAssemblyBranch.prefab (single, shared by Wall + Schwarz P): %d branch child(ren)' % len(single))
    print('GyroidBranch.prefab   (paired, gyroid only):                 %d branch child(ren)' % len(paired))

    print('\n--- geometry ---')
    check(len(single) == 1, 'the single-branch prefab still has exactly one branch child')
    check(len(paired) == 2, 'the paired prefab has exactly two branch children')
    if failures:
        return 1

    s_tip, s_flare = span(single[0], zmin, zmax)
    single_total = abs(s_flare - s_tip)
    spans = [span(c, zmin, zmax) for c in paired]
    lo = min(min(t, f) for t, f in spans)
    hi = max(max(t, f) for t, f in spans)
    paired_total = hi - lo
    print('  single: tip %+.4f  flare %+.4f  (span %.4f, prism at 0 - PIERCED)'
          % (s_tip, s_flare, single_total))
    for i, (t, f) in enumerate(spans):
        print('  pair %d: tip %+.4f  flare %+.4f  (length %.4f)' % (i, t, f, abs(f - t)))

    check(all(abs(t) < 1e-4 for t, _ in spans),
          'both tips land on the prism (max |y| = %.6fu)' % max(abs(t) for t, _ in spans))
    check(abs(spans[0][1] + spans[1][1]) < EPS,
          'exact mirror about the prism plane (%+.4f / %+.4f)' % (spans[0][1], spans[1][1]))
    check(abs(paired_total - single_total) < EPS,
          'total span preserved (%.4fu -> %.4fu)' % (single_total, paired_total))
    check(all(abs(abs(f - t) - single_total / 2) < EPS for t, f in spans),
          'each half is exactly half the single branch (%.4fu)' % (single_total / 2))
    check(all(abs(c[2][0] - 1) < 1e-6 and abs(c[2][1] - 1) < 1e-6 for c in paired),
          'lateral scale untouched (1, 1) - same shape, same visual weight')
    check(all(abs(c[2][2] - single[0][2][2] / 2) < 1e-6 for c in paired),
          'z-scale is half the single branch (%.4g -> %.4g)' % (single[0][2][2], paired[0][2][2]))

    # the pair must be reachable by the Spindle fade: a second renderer that is not registered
    # would POP in and out while the first animated (continuity of existence, ECOSYSTEM 0).
    body = open(PAIRED, encoding='utf-8-sig').read()
    renderers = set(re.findall(r'^--- !u!23 &(\d+)', body, re.M))
    primary = re.search(r'RenderedObject: \{fileID: (\d+)\}', body)
    extra = set(re.findall(r'additionalRenderedObjects:\n((?:  - \{fileID: \d+\}\n)+)', body))
    extra_ids = set(re.findall(r'\{fileID: (\d+)\}', ''.join(extra)))
    print('\n--- Spindle wiring ---')
    check(primary is not None, 'RenderedObject is assigned')
    driven = ({primary.group(1)} if primary else set()) | extra_ids
    check(driven == renderers,
          'every MeshRenderer is driven by Spindle (%d renderers, %d wired)' % (len(renderers), len(driven)))

    # scope: gyroid only.
    print('\n--- scope ---')
    guid_of = lambda p: re.search(r'guid: ([0-9a-f]{32})', open(p + '.meta').read()).group(1)
    paired_guid, single_guid = guid_of(PAIRED), guid_of(SINGLE)
    expect = {'GyroidFlora.prefab': paired_guid,
              'SchwarzPFlora.prefab': single_guid,
              'WallFlora.prefab': single_guid}
    for name, want in expect.items():
        p = os.path.join(FLORA, name)
        got = re.search(r'spindle: \{fileID: \d+, guid: ([0-9a-f]{32})', open(p).read())
        which = 'paired' if got and got.group(1) == paired_guid else 'single'
        check(got is not None and got.group(1) == want, '%-22s uses the %s branch' % (name, which))

    print('\n' + ('FAILED (%d)' % len(failures) if failures else 'ALL CHECKS PASSED'))
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main())
