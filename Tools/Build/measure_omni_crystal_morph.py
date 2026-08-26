#!/usr/bin/env python3
"""
Proves the Squirrel's omni-crystal morph offline, against the SHIPPED assets.

The claim the runtime makes, and this script checks:

  * the omni crystal's cage (`Assets/_Models/OmniCrystalExport1_8-21-25.fbx`) is 122
    disjoint solids - 90 box struts, 20 triangular prisms, 12 pentagonal prisms - whose
    NON-QUAD faces number exactly 40 + 24 = **64**;
  * the Squirrel's omni-crystal hit lays ONE ring of 8 shielded prisms
    (`AOEShieldedRingSpawner` -> `SpawnableRings` -> `BoostRingBuilder`), and a shielded
    prism renders as a circumscribing octahedron - 8 triangular faces each, so **64**;
  * therefore every panel of the crystal becomes exactly one octahedron face, 1:1, with
    nothing invented and nothing left over. The 660 QUAD faces (the 90 struts plus the
    prisms' rims) are the "other faces": they collapse into the octahedron each solid was
    assigned to and are absorbed by it.

It also runs the exact face-grouping, solid-assignment and corner-mapping the C#
(`CrystalMorphMeshBuilder`) runs, so the runtime algorithm is proven before it ships -
and renders before / mid / after sheets so the choreography can be judged.

Run:  python3 Tools/Build/measure_omni_crystal_morph.py [--render]
"""
import collections
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
import fbx_binary as fb

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

CAGE_FBX = os.path.join(ROOT, "Assets/_Models/OmniCrystalExport1_8-21-25.fbx")

# --- authored numbers, each read from the asset named beside it -----------------------
CRYSTAL_ROOT_SCALE = 10.0      # Crystal.prefab, root m_LocalScale
RING_SEGMENTS      = 8         # AOERingSpawner.prefab, SpawnableRings.prismsPerRing
RING_RADIUS        = 8.2       # AOERingSpawner.prefab, SpawnableRings.ringRadius
RING_OFFSET        = 8.0       # SpawnableRings.initialOffset
PRISM_SCALE        = (1.8, 1.8, 7.5)   # SpawnableRings.prismScale
PRISM_BOX_SIZE     = (1.0, 1.0, 1.0)   # FastGrowPrism.prefab BoxCollider m_Size
SHIELD_SCALE       = 3.0       # OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE

WELD = 1e-4


# ---------------------------------------------------------------------------- geometry
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a):
    m = math.sqrt(dot(a, a))
    return (0.0, 0.0, 0.0) if m < 1e-12 else (a[0] / m, a[1] / m, a[2] / m)
def centroid(ps):
    n = len(ps)
    return (sum(p[0] for p in ps) / n, sum(p[1] for p in ps) / n, sum(p[2] for p in ps) / n)


class DSU:
    def __init__(self, n): self.p = list(range(n))
    def find(self, a):
        while self.p[a] != a:
            self.p[a] = self.p[self.p[a]]
            a = self.p[a]
        return a
    def union(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb: self.p[ra] = rb


# ------------------------------------------------------------------- read the cage FBX
def read_cage():
    nodes, _version, _footer = fb.read(CAGE_FBX)
    top = {n.name: n for n in nodes}
    geo = top["Objects"].find("Geometry")[0]
    V = geo.first("Vertices").props[0][1]
    I = geo.first("PolygonVertexIndex").props[0][1]
    pts = [(V[3 * i], V[3 * i + 1], V[3 * i + 2]) for i in range(len(V) // 3)]
    polys, cur = [], []
    for i in I:
        if i < 0:
            cur.append(-i - 1)
            polys.append(cur)
            cur = []
        else:
            cur.append(i)
    return pts, polys


def import_like_unity(pts, polys):
    """
    Emulate Unity's FBX import for the one property the runtime depends on: hard-edged
    normals split a vertex per POLYGON, and an n-gon is fan-triangulated. Returns an
    unshared (vertex-per-corner) triangle soup, exactly the shape the C# rebuilds into.
    """
    verts, tris = [], []
    for poly in polys:
        base = len(verts)
        p = [pts[i] for i in poly]
        n = norm(cross(sub(p[1], p[0]), sub(p[2], p[0])))
        verts.extend((q, n) for q in p)
        for k in range(1, len(poly) - 1):
            tris.append((base, base + k, base + k + 1))
    return verts, tris


# ----------------------------------------------------- the algorithm the C# also runs
def weld(verts):
    key_of, ids = {}, []
    for pos, _n in verts:
        k = (round(pos[0] / WELD), round(pos[1] / WELD), round(pos[2] / WELD))
        if k not in key_of: key_of[k] = len(key_of)
        ids.append(key_of[k])
    return ids, len(key_of)


def group_faces(verts, tris):
    """
    Faces STRUCTURALLY, solids by position.

    A face is one imported polygon, and the importer's own vertex splitting is what
    identifies it: two triangles cut from the same polygon reference the very same vertex
    INDICES, and two triangles from different polygons cannot, because the importer split
    those corners apart. Measured on this FBX: all 724 polygons carry exactly ONE normal
    across their corners (max intra-polygon normal angle 0.000 deg) and UVs are indexed
    per corner, so no polygon is split internally and no two polygons weld together.

    Do NOT group by plane. 120 of this cage's 300 side quads are non-planar by 5.21 deg
    (Docs: the charge-crystal finding), so a coplanarity test cuts 60 quads in half and
    reports 160 "triangle" panels instead of 40 - which is exactly what a tight angle
    threshold did on the first pass here.

    A solid is a connected component over WELDED positions - that grouping is only used
    to keep a solid's parts travelling to the same octahedron, where welding is what we
    want.
    """
    wid, nweld = weld(verts)
    solids = DSU(nweld)
    for a, b, c in tris:
        solids.union(wid[a], wid[b])
        solids.union(wid[a], wid[c])

    polys = DSU(len(verts))
    for a, b, c in tris:
        polys.union(a, b)
        polys.union(a, c)

    faces = collections.OrderedDict()
    for t in tris:
        faces.setdefault(polys.find(t[0]), []).append(t)
    return wid, solids, faces


def classify(verts, faces, wid, solids):
    panels, fillers = [], []
    for key, ts in faces.items():
        uniq = {i for t in ts for i in t}
        rec = {"solid": solids.find(wid[ts[0][0]]), "tris": ts, "corners": len(uniq),
               "centroid": centroid([verts[i][0] for t in ts for i in t])}
        (panels if len(uniq) != 4 else fillers).append(rec)
    return panels, fillers


# ------------------------------------------------------------- the ring of 8 octahedra
def ring_octahedra():
    """The 8 shielded prisms the Squirrel's hit lays, in crystal-local space.

    Crystal-local: the crystal sits at the vessel's position with identity rotation and a
    uniform root scale, so the ring's own frame is (vessel forward = +Z).
    """
    a = PRISM_BOX_SIZE[0] * 0.5 * SHIELD_SCALE * PRISM_SCALE[0]
    b = PRISM_BOX_SIZE[1] * 0.5 * SHIELD_SCALE * PRISM_SCALE[1]
    c = PRISM_BOX_SIZE[2] * 0.5 * SHIELD_SCALE * PRISM_SCALE[2]

    octs = []
    for i in range(RING_SEGMENTS):
        ang = i * (2.0 * math.pi / RING_SEGMENTS)
        radial = (math.cos(ang), math.sin(ang), 0.0)
        centre = add((0.0, 0.0, RING_OFFSET), mul(radial, RING_RADIUS))
        # BoostRingBuilder: LookRotation(forward = +Z, up = radial).
        fwd = (0.0, 0.0, 1.0)
        up = radial
        right = norm(cross(up, fwd))
        axes = (right, up, fwd)          # local x, y, z in ring space

        px = mul(axes[0], a); py = mul(axes[1], b); pz = mul(axes[2], c)
        v = {"+x": add(centre, px), "-x": sub(centre, px),
             "+y": add(centre, py), "-y": sub(centre, py),
             "+z": add(centre, pz), "-z": sub(centre, pz)}
        # OctahedronMeshGenerator: one face per octant.
        faces = []
        for sx in (1, -1):
            for sy in (1, -1):
                for sz in (1, -1):
                    tri = [v["+x" if sx > 0 else "-x"],
                           v["+y" if sy > 0 else "-y"],
                           v["+z" if sz > 0 else "-z"]]
                    if sx * sy * sz < 0: tri[1], tri[2] = tri[2], tri[1]
                    faces.append({"oct": i, "corners": tri, "centroid": centroid(tri)})
        octs.append({"index": i, "centre": centre, "faces": faces})
    return octs


# ------------------------------------------------------------------------- assignment
def assign(panels, fillers, octs, crystal_scale):
    """Solids -> octahedra (balanced over panel-carrying solids), then panels -> faces."""
    # Panel-carrying solids, in crystal-local (scaled) space.
    by_solid = collections.defaultdict(list)
    for p in panels: by_solid[p["solid"]].append(p)
    panel_solids = list(by_solid.keys())

    def dir_of(pt): return norm(mul(pt, crystal_scale))
    oct_dir = [norm(o["centre"]) for o in octs]

    per_oct = len(panel_solids) // len(octs)
    scored = []
    for s in panel_solids:
        c = centroid([p["centroid"] for p in by_solid[s]])
        d = dir_of(c)
        for k, od in enumerate(oct_dir):
            scored.append((-dot(d, od), s, k))
    scored.sort()
    taken, counts, solid_oct = set(), collections.Counter(), {}
    for _score, s, k in scored:
        if s in taken or counts[k] >= per_oct: continue
        taken.add(s); counts[k] += 1; solid_oct[s] = k

    # Panels -> the 8 faces of their solid's octahedron, greedy by angular fit.
    face_taken = set()
    pairs = []
    for s, ps in by_solid.items():
        k = solid_oct[s]
        for p in ps:
            pd = dir_of(p["centroid"])
            for fi, f in enumerate(octs[k]["faces"]):
                fd = norm(sub(f["centroid"], octs[k]["centre"]))
                pairs.append((-dot(pd, fd), id(p), (k, fi), p, f))
    pairs.sort(key=lambda x: (x[0], x[1]))
    used_panel = set()
    for _sc, pid, fkey, p, f in pairs:
        if pid in used_panel or fkey in face_taken: continue
        used_panel.add(pid); face_taken.add(fkey)
        p["target"] = f
    # Fillers -> the centre of the nearest octahedron (absorbed).
    for fl in fillers:
        d = dir_of(fl["centroid"])
        k = max(range(len(octs)), key=lambda i: dot(d, oct_dir[i]))
        fl["target_point"] = octs[k]["centre"]
    return solid_oct


# ------------------------------------------------------------------------------- main
def main():
    pts, polys = read_cage()
    verts, tris = import_like_unity(pts, polys)
    wid, solids, faces = group_faces(verts, tris)
    panels, fillers = classify(verts, faces, wid, solids)

    solid_count = len({solids.find(w) for w in wid})
    by_corner = collections.Counter(p["corners"] for p in panels)
    print("cage        : %d control points, %d polygons, %d triangles"
          % (len(pts), len(polys), len(tris)))
    print("solids      : %d" % solid_count)
    print("face groups : %d  (panels %d, quad fillers %d)"
          % (len(faces), len(panels), len(fillers)))
    print("panels      : %s" % dict(sorted(by_corner.items())))

    octs = ring_octahedra()
    oct_faces = sum(len(o["faces"]) for o in octs)
    print("ring        : %d octahedra x 8 faces = %d target faces" % (len(octs), oct_faces))

    assert solid_count == 122, solid_count
    assert by_corner[3] == 40 and by_corner[5] == 24, by_corner
    assert len(panels) == 64, len(panels)
    assert len(fillers) == 660, len(fillers)
    assert oct_faces == 64
    print("\nPANEL CENSUS OK: 40 triangles + 24 pentagons = 64 = 8 octahedra x 8 faces (1:1).")

    crystal_scale = CRYSTAL_ROOT_SCALE
    assign(panels, fillers, octs, crystal_scale)
    unmatched = [p for p in panels if "target" not in p]
    assert not unmatched, "%d panels unmatched" % len(unmatched)
    used = {(p["target"]["oct"], id(p["target"])) for p in panels}
    assert len(used) == 64, len(used)
    per_oct = collections.Counter(p["target"]["oct"] for p in panels)
    print("assignment  : every panel matched; per octahedron %s" % sorted(per_oct.values()))
    assert set(per_oct.values()) == {8}, per_oct

    cage_r = max(math.sqrt(dot(mul(p, crystal_scale), mul(p, crystal_scale))) for p in pts)
    print("crystal     : cage radius %.2f world units (root scale %.0f)" % (cage_r, crystal_scale))
    print("ring        : radius %.1f, %.1f ahead; octahedron semi-axes %.2f x %.2f x %.2f"
          % (RING_RADIUS, RING_OFFSET,
             PRISM_BOX_SIZE[0] * .5 * SHIELD_SCALE * PRISM_SCALE[0],
             PRISM_BOX_SIZE[1] * .5 * SHIELD_SCALE * PRISM_SCALE[1],
             PRISM_BOX_SIZE[2] * .5 * SHIELD_SCALE * PRISM_SCALE[2]))

    if "--render" in sys.argv:
        render(verts, panels, fillers, crystal_scale)
    print("\nOK")


def render(verts, panels, fillers, crystal_scale):
    """
    Draws the choreography. The corner map here is deliberately SIMPLIFIED (corner k -> target
    corner k mod 3) — the shipped one anchors three source corners to the target's three and
    slides the rest along its edges, and is asserted by `CrystalMorphMeshBuilderTests`. Read
    this sheet for the motion, not for the exact corner correspondence.
    """
    try:
        import numpy as np
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("(render skipped: numpy/matplotlib not installed)")
        return

    def frame(t):
        segs, cols = [], []
        for rec, target_of in ((panels, lambda r, i: r["target"]["corners"][i % 3]),
                               (fillers, lambda r, i: r["target_point"])):
            for r in rec:
                for tri in r["tris"]:
                    src = [mul(verts[i][0], crystal_scale) for i in tri]
                    dst = [target_of(r, k) for k in range(3)]
                    pt = [tuple((1 - t) * s[a] + t * d[a] for a in range(3))
                          for s, d in zip(src, dst)]
                    segs.append(pt + [pt[0]])
                    cols.append("#7fd4ff" if r in panels else "#3a5a70")
        return segs, cols

    fig, axes = plt.subplots(1, 4, figsize=(22, 6), subplot_kw={"projection": "3d"})
    for ax, t in zip(axes, (0.0, 0.35, 0.7, 1.0)):
        segs, cols = frame(t)
        for s, c in zip(segs, cols):
            xs = [p[0] for p in s]; ys = [p[2] for p in s]; zs = [p[1] for p in s]
            ax.plot(xs, ys, zs, color=c, linewidth=0.35)
        ax.set_title("t = %.2f" % t)
        ax.set_xlim(-12, 12); ax.set_ylim(-4, 22); ax.set_zlim(-12, 12)
        ax.set_axis_off()
        ax.view_init(elev=18, azim=-62)
    out = os.path.join(ROOT, "Tools/Build/omni_crystal_morph.png")
    plt.tight_layout(); plt.savefig(out, dpi=110, facecolor="#0b0f14")
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
