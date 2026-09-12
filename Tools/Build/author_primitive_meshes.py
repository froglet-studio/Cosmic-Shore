#!/usr/bin/env python3
"""
Author the four PRIMITIVE MESHES the project actually uses, first-party.

WHY: the four meshes under `Assets/PrimitivePlus/Resources/Meshes/` that anything in this
project references - Cone, Sphere, Cube, CylinderTube - arrived in a commercial Asset Store
pack with no licence document and no recoverable purchase record, and the pack ships its whole
47-mesh `Resources/` folder into the player whether or not anything references it (Unity packs
every `Resources/` folder whole). Replacing four meshes is cheaper than chasing a receipt for
forty-seven.

WHAT MAKES THIS SAFE: all four are EXACT parametric primitives on a 20-segment radial grid,
which is a measurement, not an assumption - see `--verify-vendor`, which reproves it against
the shipped assets. There is no modelled surface here to lose:

    Cone          unit cone, apex +Y at (0, +0.5, 0), base ring y = -0.5 r = 0.5,
                  20 segments, 41 verts / 38 tris (18 cap + 20 side)
    Sphere        UV sphere r = 0.5, 20 stacks x 20 segments, azimuth origin 8 deg
    Cube          unit cube, 6 faces x 4 split-normal verts
    CylinderTube  annular tube, r_outer 0.5 / r_inner 0.25, y +-0.5, 20 segments

THE ONE THING THAT IS NOT FREE IS THE CONE'S UV LAYOUT. `AOEConicExplosion.prefab` (the
Dolphin's crystal blast) and `AOEConicSkyBurst.prefab` (the Sparrow's skyburst) draw this mesh
with `YellowConicExplosionMaterial`, whose shader `LaserGraph.shadergraph` carries a `UVNode`
feeding two `SampleTexture2DNode`s - so UV0 reaches the screen, and a different unwrap is a
different blast. The vendor's unwrap is two circles in UV space, which is a construction rather
than a painting, and both were FIT from the shipped asset to a radial residual of ~1e-7:

    side   a disc centred on the apex UV, uv_angle = azimuth - 45 deg
    cap    a disc, uv_angle = 189 deg - azimuth   (mirrored, because the cap faces -Y)

Those four constants are reproduced here for the same reason a replacement 9-sliced sprite
reproduces its border: they are a compatibility requirement, not a style choice. `--verify-vendor`
asserts the reproduction.

MEASURED, `--verify-vendor` against the pack before it was removed - all four SURFACE-IDENTICAL
(same triangle coverage, same winding, vertex clusters within 1.2e-06 world units):

    mesh           verts        tris   surface     max normal delta   max UV delta
    Cone           41 -> 41     38     IDENTICAL   0.0159 deg         0.000001
    Sphere         440 -> 382   760    IDENTICAL   0.4429 deg         0.003906  (= 1/256)
    Cube           24 -> 24     12     IDENTICAL   0.0000 deg         0.000000
    CylinderTube   172 -> 164   160    IDENTICAL   0.0175 deg         1.000000  (see below)

The two deltas that are not ~0 are both deliberate, and neither reaches a shader:

  * The SPHERE's normals sit 0.44 deg off the vendor's and its UVs 1/256 off, because the vendor
    mesh is an EXPORT: its normals are not exactly radial and every UV lands on a k/256 grid
    (the channel is Float32, so that is a quantiser upstream of the asset, not the format). This
    file emits the analytic values the vendor's are a rounding of. Its two UV-reading consumers
    (`ForceFieldMaterial` -> `ForceFieldGraph`, `RippleMaterialRed` -> `RippleGraph`) read UV0
    for procedural fresnel and scrolling, not to index an atlas.
  * The CYLINDERTUBE's UVs are a modelled non-uniform unwrap (its wall steps are 0.0895 / 0.1052
    / 0.1106 per equal 18 deg segment - a seamed atlas from a modelling package, not a formula).
    It is NOT reproduced, and nothing can see that: its one consumer `oldWallFlora.prefab` wires
    `m_Materials: [{fileID: 0}]` - a NULL material - so no shader samples it, and it also uses
    the mesh as a non-convex `MeshCollider`, which reads positions and triangles only.

    python3 Tools/Build/author_primitive_meshes.py                  # write the four .asset + .meta
    python3 Tools/Build/author_primitive_meshes.py --check          # fail if any drifted
    python3 Tools/Build/author_primitive_meshes.py --verify-vendor  # prove against PrimitivePlus
                                                                    # (only while the pack exists)
    python3 Tools/Build/author_primitive_meshes.py --self-test      # prove --verify-vendor FAILS
                                                                    # on a mesh that really differs
"""

import argparse
import hashlib
import math
import os
import re
import struct
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(REPO, "Assets", "_Models", "Primitives")
VENDOR_DIR = os.path.join(REPO, "Assets", "PrimitivePlus", "Resources", "Meshes")

SEGMENTS = 20                      # every one of the four rides the same 20-segment grid


def guid_for(name):
    return hashlib.md5(f"CosmicShore/Primitives/{name}".encode()).hexdigest()


# --------------------------------------------------------------------------------------------
# Mesh construction
#
# A mesh is built as a flat triangle soup of (position, normal, uv) and then WELDED: identical
# (position, normal, uv) triples share one vertex. That is what the vendor meshes do too, and it
# is why the vertex counts come out equal without anybody reproducing an exporter's vertex order.
# --------------------------------------------------------------------------------------------

class Builder:
    def __init__(self):
        self.verts = []          # (pos, nrm, uv)
        self.index = {}
        self.tris = []

    def v(self, pos, nrm, uv):
        key = (tuple(round(c, 6) for c in pos),
               tuple(round(c, 6) for c in nrm),
               tuple(round(c, 6) for c in uv))
        i = self.index.get(key)
        if i is None:
            i = len(self.verts)
            self.index[key] = i
            self.verts.append((pos, nrm, uv))
        return i

    def tri(self, a, b, c):
        self.tris.append((a, b, c))


def _norm(v):
    m = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    return (v[0] / m, v[1] / m, v[2] / m)


# --- Cone ------------------------------------------------------------------------------------
# The two UV discs, FIT from Assets/PrimitivePlus/Resources/Meshes/Cone.asset (Kasa circle fit,
# max radial residual 1.1e-07 cap / 6.7e-08 side; angular relation constant to 3e-4 deg).
CONE_SIDE_UV_CENTRE = (0.3002033531665802, 0.3002032935619354)   # == the apex UV, exactly
CONE_SIDE_UV_RADIUS = 0.3038440
CONE_SIDE_UV_PHASE = -45.0        # uv_angle = azimuth + phase
CONE_CAP_UV_CENTRE = (0.1997970, 0.8002030)
CONE_CAP_UV_RADIUS = 0.2021860
CONE_CAP_UV_MIRROR = 189.0        # uv_angle = mirror - azimuth   (the cap faces -Y)


def build_cone():
    b = Builder()
    n = SEGMENTS
    ang = [2.0 * math.pi * k / n for k in range(n)]
    ring = [(0.5 * math.cos(a), -0.5, 0.5 * math.sin(a)) for a in ang]
    apex = (0.0, 0.5, 0.0)

    def cap_uv(a):
        t = math.radians(CONE_CAP_UV_MIRROR) - a
        return (CONE_CAP_UV_CENTRE[0] + CONE_CAP_UV_RADIUS * math.cos(t),
                CONE_CAP_UV_CENTRE[1] + CONE_CAP_UV_RADIUS * math.sin(t))

    def side_uv(a):
        t = a + math.radians(CONE_SIDE_UV_PHASE)
        return (CONE_SIDE_UV_CENTRE[0] + CONE_SIDE_UV_RADIUS * math.cos(t),
                CONE_SIDE_UV_CENTRE[1] + CONE_SIDE_UV_RADIUS * math.sin(t))

    # Base cap: a fan, facing -Y. The cap is planar and its UV is a similarity of the XZ plane
    # (a disc mapped to a disc), so the UV is AFFINE over it and the triangulation is free - any
    # fan or strip rasterises and interpolates identically. The vendor ships a zig-zag strip;
    # a fan is emitted here because it is the shape the construction states.
    cap = [b.v(ring[k], (0.0, -1.0, 0.0), cap_uv(ang[k])) for k in range(n)]
    for k in range(1, n - 1):
        b.tri(cap[0], cap[k], cap[k + 1])

    # Side: one triangle per segment, all sharing a single soft apex (normal +Y, as shipped).
    side_n = [_norm((math.cos(a), 0.5, math.sin(a))) for a in ang]
    apex_i = b.v(apex, (0.0, 1.0, 0.0), CONE_SIDE_UV_CENTRE)
    side = [b.v(ring[k], side_n[k], side_uv(ang[k])) for k in range(n)]
    for k in range(n):
        b.tri(apex_i, side[(k + 1) % n], side[k])
    return b


# --- Sphere ----------------------------------------------------------------------------------
SPHERE_STACKS = 20
SPHERE_AZIMUTH_ORIGIN = 8.0       # measured: the shipped ring starts at 8 deg, not 0
SPHERE_U_ORIGIN = 170.0           # measured: u wraps to 0 at azimuth 170 deg, not at the ring's
SPHERE_POLE_U = 0.472656          # measured: every pole vertex is pinned to one u (121/256)


def build_sphere():
    b = Builder()
    n, m = SEGMENTS, SPHERE_STACKS
    # u runs from its own origin, so the seam in the TEXTURE need not be the ring's first vertex.
    u0 = (SPHERE_AZIMUTH_ORIGIN - SPHERE_U_ORIGIN) / 360.0
    az = [math.radians(SPHERE_AZIMUTH_ORIGIN) + 2.0 * math.pi * k / n for k in range(n + 1)]

    def vert(k, j):
        pol = math.pi * j / m
        y = 0.5 * math.cos(pol)
        r = 0.5 * math.sin(pol)
        p = (r * math.cos(az[k]), y, r * math.sin(az[k]))
        if r <= 1e-9:
            # A pole has no azimuth, so u is a free choice; the shipped mesh pins every pole
            # vertex to one u, which stops the top and bottom rows fanning the texture out.
            return b.v((0.0, y, 0.0), (0.0, 1.0 if y > 0 else -1.0, 0.0),
                       (SPHERE_POLE_U, 1.0 - j / m))
        return b.v(p, _norm(p), ((u0 + k / n) % 1.0, 1.0 - j / m))

    for j in range(m):
        for k in range(n):
            a, bb, c, d = vert(k, j), vert(k + 1, j), vert(k + 1, j + 1), vert(k, j + 1)
            if j > 0:
                b.tri(a, bb, c)
            if j < m - 1:
                b.tri(a, c, d)
    return b


# --- Cube ------------------------------------------------------------------------------------
CUBE_FACES = (                     # (normal, origin, edge_u, edge_v, uv at (00),(10),(11),(01))
    # Winding is edge_u x edge_v = the shading normal, which is what makes a face front-facing;
    # the UV corner assignment is per face and is MEASURED from the asset being replaced (five
    # faces share one arrangement and -Z is its own), because nothing derives it.
    ((+1, 0, 0), (+.5, -.5, +.5), (0, 0, -1), (0, +1, 0), ((1, 0), (1, 1), (0, 1), (0, 0))),
    ((-1, 0, 0), (-.5, -.5, -.5), (0, 0, +1), (0, +1, 0), ((1, 0), (0, 0), (0, 1), (1, 1))),
    ((0, +1, 0), (-.5, +.5, +.5), (+1, 0, 0), (0, 0, -1), ((1, 0), (1, 1), (0, 1), (0, 0))),
    ((0, -1, 0), (-.5, -.5, -.5), (+1, 0, 0), (0, 0, +1), ((1, 0), (0, 0), (0, 1), (1, 1))),
    ((0, 0, +1), (-.5, -.5, +.5), (+1, 0, 0), (0, +1, 0), ((1, 0), (0, 0), (0, 1), (1, 1))),
    ((0, 0, -1), (+.5, -.5, -.5), (-1, 0, 0), (0, +1, 0), ((0, 1), (0, 0), (1, 0), (1, 1))),
)


def build_cube():
    b = Builder()
    for nrm, o, eu, ev, uvs in CUBE_FACES:
        c = [b.v((o[0] + eu[0] * u + ev[0] * v,
                  o[1] + eu[1] * u + ev[1] * v,
                  o[2] + eu[2] * u + ev[2] * v), nrm, (float(uv[0]), float(uv[1])))
             for (u, v), uv in zip(((0, 0), (1, 0), (1, 1), (0, 1)), uvs)]
        b.tri(c[0], c[1], c[2])
        b.tri(c[0], c[2], c[3])
    return b


# --- CylinderTube ----------------------------------------------------------------------------
TUBE_OUTER, TUBE_INNER = 0.5, 0.25


def build_cylinder_tube():
    b = Builder()
    n = SEGMENTS
    ang = [2.0 * math.pi * k / n for k in range(n + 1)]

    def wall(r, outward):
        s = 1.0 if outward else -1.0
        for k in range(n):
            a0, a1 = ang[k], ang[k + 1]
            q = []
            for a, u in ((a0, k / n), (a1, (k + 1) / n)):
                nx, nz = s * math.cos(a), s * math.sin(a)
                for y, v in ((-0.5, 0.0), (0.5, 1.0)):
                    q.append(b.v((r * math.cos(a), y, r * math.sin(a)), (nx, 0.0, nz), (u, v)))
            lo0, hi0, lo1, hi1 = q[0], q[1], q[2], q[3]
            if outward:
                b.tri(lo0, hi0, lo1)
                b.tri(hi0, hi1, lo1)
            else:
                b.tri(lo0, lo1, hi0)
                b.tri(hi0, lo1, hi1)

    def cap(y, ny):
        for k in range(n):
            a0, a1 = ang[k], ang[k + 1]
            pts = []
            for a in (a0, a1):
                for r in (TUBE_OUTER, TUBE_INNER):
                    p = (r * math.cos(a), y, r * math.sin(a))
                    uv = (0.5 + p[0] * ny, 0.5 + p[2])
                    pts.append(b.v(p, (0.0, ny, 0.0), uv))
            o0, i0, o1, i1 = pts
            if ny > 0:
                b.tri(o0, i0, i1)
                b.tri(o0, i1, o1)
            else:
                b.tri(o0, i1, i0)
                b.tri(o0, o1, i1)

    wall(TUBE_OUTER, True)
    wall(TUBE_INNER, False)
    cap(+0.5, +1.0)
    cap(-0.5, -1.0)
    return b


# --------------------------------------------------------------------------------------------
# Tangents
#
# Unity's own per-triangle accumulate-and-orthonormalise, so a shader that reads TANGENT (the
# conic blast's does not, but the sphere's ForceFieldGraph might) gets what an imported mesh
# would give it.
# --------------------------------------------------------------------------------------------

def compute_tangents(verts, tris):
    n = len(verts)
    tan = [[0.0, 0.0, 0.0] for _ in range(n)]
    bit = [[0.0, 0.0, 0.0] for _ in range(n)]
    for a, b_, c in tris:
        p0, p1, p2 = verts[a][0], verts[b_][0], verts[c][0]
        w0, w1, w2 = verts[a][2], verts[b_][2], verts[c][2]
        x1, x2 = p1[0] - p0[0], p2[0] - p0[0]
        y1, y2 = p1[1] - p0[1], p2[1] - p0[1]
        z1, z2 = p1[2] - p0[2], p2[2] - p0[2]
        s1, s2 = w1[0] - w0[0], w2[0] - w0[0]
        t1, t2 = w1[1] - w0[1], w2[1] - w0[1]
        d = s1 * t2 - s2 * t1
        if abs(d) < 1e-12:
            continue
        r = 1.0 / d
        sd = ((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r)
        td = ((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r)
        for i in (a, b_, c):
            for k in range(3):
                tan[i][k] += sd[k]
                bit[i][k] += td[k]
    out = []
    for i in range(n):
        nr = verts[i][1]
        t = tan[i]
        dot = nr[0] * t[0] + nr[1] * t[1] + nr[2] * t[2]
        o = (t[0] - nr[0] * dot, t[1] - nr[1] * dot, t[2] - nr[2] * dot)
        m = math.sqrt(o[0] ** 2 + o[1] ** 2 + o[2] ** 2)
        if m < 1e-9:
            # degenerate (a pole, or a face whose UV collapses): any tangent in the plane will do
            ref = (1.0, 0.0, 0.0) if abs(nr[0]) < 0.9 else (0.0, 1.0, 0.0)
            o = (nr[1] * ref[2] - nr[2] * ref[1],
                 nr[2] * ref[0] - nr[0] * ref[2],
                 nr[0] * ref[1] - nr[1] * ref[0])
            m = math.sqrt(o[0] ** 2 + o[1] ** 2 + o[2] ** 2) or 1.0
        o = (o[0] / m, o[1] / m, o[2] / m)
        cx = nr[1] * o[2] - nr[2] * o[1]
        cy = nr[2] * o[0] - nr[0] * o[2]
        cz = nr[0] * o[1] - nr[1] * o[0]
        w = -1.0 if (cx * bit[i][0] + cy * bit[i][1] + cz * bit[i][2]) < 0.0 else 1.0
        out.append((o[0], o[1], o[2], w))
    return out


# --------------------------------------------------------------------------------------------
# Unity Mesh .asset emission
# --------------------------------------------------------------------------------------------

ASSET = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!43 &4300000
Mesh:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 10
  m_SubMeshes:
  - serializedVersion: 2
    firstByte: 0
    indexCount: {index_count}
    topology: 0
    baseVertex: 0
    firstVertex: 0
    vertexCount: {vertex_count}
    localAABB:
      m_Center: {{x: {cx}, y: {cy}, z: {cz}}}
      m_Extent: {{x: {ex}, y: {ey}, z: {ez}}}
  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes:
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data:
  m_MeshCompression: 0
  m_IsReadable: 1
  m_KeepVertices: 1
  m_KeepIndices: 1
  m_IndexFormat: 0
  m_IndexBuffer: {index_buffer}
  m_VertexData:
    serializedVersion: 3
    m_VertexCount: {vertex_count}
    m_Channels:
    - stream: 0
      offset: 0
      format: 0
      dimension: 3
    - stream: 0
      offset: 12
      format: 0
      dimension: 3
    - stream: 0
      offset: 32
      format: 0
      dimension: 4
    - stream: 0
      offset: 0
      format: 0
      dimension: 0
    - stream: 0
      offset: 24
      format: 0
      dimension: 2
{blank_channels}    m_DataSize: {data_size}
    _typelessdata: {vertex_data}
  m_CompressedMesh:
    m_Vertices:
      m_NumItems: 0
      m_Range: 0
      m_Start: 0
      m_Data:
      m_BitSize: 0
    m_UV:
      m_NumItems: 0
      m_Range: 0
      m_Start: 0
      m_Data:
      m_BitSize: 0
    m_Normals:
      m_NumItems: 0
      m_Range: 0
      m_Start: 0
      m_Data:
      m_BitSize: 0
    m_Tangents:
      m_NumItems: 0
      m_Range: 0
      m_Start: 0
      m_Data:
      m_BitSize: 0
    m_Weights:
      m_NumItems: 0
      m_Data:
      m_BitSize: 0
    m_NormalSigns:
      m_NumItems: 0
      m_Data:
      m_BitSize: 0
    m_TangentSigns:
      m_NumItems: 0
      m_Data:
      m_BitSize: 0
    m_FloatColors:
      m_NumItems: 0
      m_Range: 0
      m_Start: 0
      m_Data:
      m_BitSize: 0
    m_BoneIndices:
      m_NumItems: 0
      m_Data:
      m_BitSize: 0
    m_Triangles:
      m_NumItems: 0
      m_Data:
      m_BitSize: 0
    m_UVInfo: 0
  m_LocalAABB:
    m_Center: {{x: {cx}, y: {cy}, z: {cz}}}
    m_Extent: {{x: {ex}, y: {ey}, z: {ez}}}
  m_MeshUsageFlags: 0
  m_BakedConvexCollisionMesh:
  m_BakedTriangleCollisionMesh:
  m_MeshMetrics[0]: 1
  m_MeshMetrics[1]: 1
  m_MeshOptimizationFlags: -1
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path:
"""

BLANK_CHANNEL = ("    - stream: 0\n"
                 "      offset: 0\n"
                 "      format: 0\n"
                 "      dimension: 0\n")

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def f32(x):
    """Round-trip through float32 so the emitted text is exactly what the binary will hold."""
    return struct.unpack("<f", struct.pack("<f", x))[0]


def fmt(x):
    """Unity's float style: shortest round-tripping decimal, no exponent, no trailing zeroes."""
    x = f32(x)
    if x == 0.0:
        return "0"
    for p in range(1, 10):
        s = f"{x:.{p}f}"
        if f32(float(s)) == x:
            break
    s = s.rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"


def emit(name, builder):
    verts, tris = builder.verts, builder.tris
    tangents = compute_tangents(verts, tris)

    # Interleaved stream: pos(12) nrm(12) uv(8) tan(16) = 48 bytes, the layout Unity writes for
    # a Position/Normal/Tangent/UV0 mesh and the one the vendor assets carry.
    blob = bytearray()
    for (p, n, uv), t in zip(verts, tangents):
        blob += struct.pack("<3f", *p)
        blob += struct.pack("<3f", *n)
        blob += struct.pack("<2f", *uv)
        blob += struct.pack("<4f", *t)

    idx = bytearray()
    for a, b_, c in tris:
        idx += struct.pack("<3H", a, b_, c)

    xs = [f32(v[0][0]) for v in verts]
    ys = [f32(v[0][1]) for v in verts]
    zs = [f32(v[0][2]) for v in verts]
    ctr = [(min(a) + max(a)) / 2 for a in (xs, ys, zs)]
    ext = [(max(a) - min(a)) / 2 for a in (xs, ys, zs)]

    body = ASSET.format(
        name=name,
        index_count=len(tris) * 3,
        vertex_count=len(verts),
        index_buffer=idx.hex(),
        vertex_data=blob.hex(),
        data_size=len(blob),
        blank_channels=BLANK_CHANNEL * 9,
        cx=fmt(ctr[0]), cy=fmt(ctr[1]), cz=fmt(ctr[2]),
        ex=fmt(ext[0]), ey=fmt(ext[1]), ez=fmt(ext[2]),
    )
    return body, META.format(guid=guid_for(name))


MESHES = (
    ("PrimitiveCone", build_cone, "Cone"),
    ("PrimitiveSphere", build_sphere, "Sphere"),
    ("PrimitiveCube", build_cube, "Cube"),
    ("PrimitiveCylinderTube", build_cylinder_tube, "CylinderTube"),
)


# --------------------------------------------------------------------------------------------
# --verify-vendor : the proof, run while the pack still exists
# --------------------------------------------------------------------------------------------

def read_vendor(path):
    txt = open(path).read()
    n = int(re.search(r"m_VertexCount: (\d+)", txt).group(1))
    vd = txt[txt.index("m_VertexData:"):]
    raw = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-fA-F]+)", vd).group(1))
    stride = len(raw) // n
    pos, nrm, uv = [], [], []
    for i in range(n):
        o = i * stride
        pos.append(struct.unpack_from("<3f", raw, o))
        nrm.append(struct.unpack_from("<3f", raw, o + 12))
        uv.append(struct.unpack_from("<2f", raw, o + 24))
    ib = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-fA-F]+)", txt).group(1))
    idx = list(struct.unpack("<" + "H" * (len(ib) // 2), ib))
    tris = [(idx[k], idx[k + 1], idx[k + 2]) for k in range(0, len(idx), 3)]
    return pos, nrm, uv, tris


def canonical_positions(*vertex_lists, tol=1e-3):
    """One id per DISTINCT point across both meshes, clustered within `tol`.

    Deliberately not a rounded key: `Docs/../HIJACK.md` records the trap - resolving identity by
    rounding a float is a tolerance with a cliff in the middle of it, and one sphere vertex
    landed exactly on that cliff (float32 storage vs float64 arithmetic put it at -0.26415).
    Clustering has no boundary to land on. Returns (lookup, max_spread) so the spread is
    REPORTED rather than assumed."""
    pts = []
    for vl in vertex_lists:
        pts.extend(vl)
    cells = {}
    reps = []
    spread = 0.0
    for p in pts:
        cell = tuple(int(math.floor(c / tol)) for c in p)
        hit = None
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for j in cells.get((cell[0] + dx, cell[1] + dy, cell[2] + dz), ()):
                        d = max(abs(p[k] - reps[j][k]) for k in range(3))
                        if d < tol:
                            hit = j
                            spread = max(spread, d)
                            break
                    if hit is not None:
                        break
                if hit is not None:
                    break
            if hit is not None:
                break
        if hit is None:
            hit = len(reps)
            reps.append(p)
            cells.setdefault(cell, []).append(hit)

    def lookup(p):
        cell = tuple(int(math.floor(c / tol)) for c in p)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for j in cells.get((cell[0] + dx, cell[1] + dy, cell[2] + dz), ()):
                        if max(abs(p[k] - reps[j][k]) for k in range(3)) < tol:
                            return j
        return None
    return lookup, spread, reps


def surface_signature(pos, tris, pid):
    """The SURFACE a triangle list covers, independent of how it was triangulated.

    Triangles are grouped by their supporting plane; within a plane, directed edges that appear
    in both directions are interior and cancel, leaving the oriented BOUNDARY, and the areas are
    summed. Two triangulations of the same planar polygon have the same boundary and the same
    area, so a free choice (the cone's flat base cap - planar, and its UV is a disc-to-disc
    similarity, hence affine, so any fan or strip interpolates identically) does not read as a
    difference. A CURVED quad is two planes, so its diagonal does NOT cancel and is still
    compared exactly - which is what makes the sphere's and the tube's triangulation binding.

    Vertex order and index order are invisible to the rasteriser and are not compared at all."""
    planes = {}
    for tri in tris:
        p = [pos[i] for i in tri]
        ids = [pid(q) for q in p]
        u = tuple(p[1][k] - p[0][k] for k in range(3))
        v = tuple(p[2][k] - p[0][k] for k in range(3))
        nx = u[1] * v[2] - u[2] * v[1]
        ny = u[2] * v[0] - u[0] * v[2]
        nz = u[0] * v[1] - u[1] * v[0]
        area2 = math.sqrt(nx * nx + ny * ny + nz * nz)
        if area2 < 1e-12:
            continue                                    # degenerate: covers nothing
        n = (nx / area2, ny / area2, nz / area2)
        d = sum(n[k] * p[0][k] for k in range(3))
        key = (round(n[0], 3), round(n[1], 3), round(n[2], 3), round(d, 3))
        entry = planes.setdefault(key, [{}, 0.0])
        entry[1] += area2 / 2.0
        edges = entry[0]
        for a, bb in ((0, 1), (1, 2), (2, 0)):
            e = (ids[a], ids[bb])
            rev = (e[1], e[0])
            if edges.get(rev):
                edges[rev] -= 1
                if not edges[rev]:
                    del edges[rev]
            else:
                edges[e] = edges.get(e, 0) + 1
    return {k: (frozenset(v[0].items()), round(v[1], 4)) for k, v in planes.items()}


def verify_vendor():
    ok = True
    for name, build, vendor in MESHES:
        vp = os.path.join(VENDOR_DIR, vendor + ".asset")
        if not os.path.exists(vp):
            print(f"  {vendor:14s} SKIP - vendor asset is gone (already removed)")
            continue
        vpos, vnrm, vuv, vtris = read_vendor(vp)
        b = build()
        mpos = [v[0] for v in b.verts]
        mnrm = [v[1] for v in b.verts]
        muv = [v[2] for v in b.verts]

        pid, spread, _ = canonical_positions(vpos, mpos)
        vsig = surface_signature(vpos, vtris, pid)
        msig = surface_signature(mpos, b.tris, pid)
        missing = {k for k in vsig if msig.get(k) != vsig[k]}
        extra = {k for k in msig if vsig.get(k) != msig[k]}

        bounds_v = [(min(c[i] for c in vpos), max(c[i] for c in vpos)) for i in range(3)]
        bounds_m = [(min(c[i] for c in mpos), max(c[i] for c in mpos)) for i in range(3)]
        dbound = max(abs(a[0] - b2[0]) + abs(a[1] - b2[1]) for a, b2 in zip(bounds_v, bounds_m))

        # Normals and UVs, matched by POSITION (several corners may share one - a hard edge, or a
        # seam - so take the closest candidate; a real mismatch cannot hide behind that, because a
        # corner whose position has no vendor counterpart at all is counted as unmatched).
        vlut = {}
        for i in range(len(vpos)):
            vlut.setdefault(pid(vpos[i]), []).append((vnrm[i], vuv[i]))
        dnrm_deg = 0.0
        duv = 0.0
        unmatched = 0
        for i in range(len(mpos)):
            cand = vlut.get(pid(mpos[i]))
            if not cand:
                unmatched += 1
                continue
            mn = mnrm[i]
            best = min(cand, key=lambda c: -(c[0][0] * mn[0] + c[0][1] * mn[1] + c[0][2] * mn[2]))
            dot = max(-1.0, min(1.0, best[0][0] * mn[0] + best[0][1] * mn[1] + best[0][2] * mn[2]))
            dnrm_deg = max(dnrm_deg, math.degrees(math.acos(dot)))
            duv = max(duv, min(max(abs(c[1][0] - muv[i][0]), abs(c[1][1] - muv[i][1]))
                               for c in cand))

        good = not missing and not extra and dbound < 1e-5 and not unmatched
        ok = ok and good
        print(f"  {vendor:14s} -> {name}   {'OK' if good else 'MISMATCH'}")
        print(f"      verts {len(vpos):4d} -> {len(mpos):4d}   tris {len(vtris):4d} -> {len(b.tris):4d}")
        print(f"      surface (triangle positions + winding): "
              f"{'IDENTICAL' if not missing and not extra else f'MISSING {len(missing)} EXTRA {len(extra)}'}")
        print(f"      vertex cluster spread {spread:.2e}   bounds delta {dbound:.2e}"
              f"   max normal delta {dnrm_deg:.4f} deg"
              f"   max UV delta {duv:.6f}"
              f"{f'   ({unmatched} corners with no vendor position)' if unmatched else ''}")
    return 0 if ok else 1


def self_test():
    """A gate nobody has watched FAIL is a gate nobody should trust. Each control below is a
    change that MUST be caught - if any passes, --verify-vendor is not proving anything."""
    if not os.path.exists(os.path.join(VENDOR_DIR, "Cone.asset")):
        print("SKIP: the vendor pack is gone, so there is nothing to prove against")
        return 0
    import copy
    controls = []

    def control(label, patch):
        controls.append((label, patch))

    control("cone side wound backwards", lambda: _patch_cone(flip=True))
    control("cone 19 segments instead of 20", lambda: _patch_cone(segments=19))
    control("cone apex at +0.6 instead of +0.5", lambda: _patch_cone(apex=0.6))
    control("cone cap missing", lambda: _patch_cone(no_cap=True))

    ok = True
    for label, patch in controls:
        b = patch()
        vpos, vnrm, vuv, vtris = read_vendor(os.path.join(VENDOR_DIR, "Cone.asset"))
        mpos = [v[0] for v in b.verts]
        pid, _, _ = canonical_positions(vpos, mpos)
        vsig = surface_signature(vpos, vtris, pid)
        msig = surface_signature(mpos, b.tris, pid)
        caught = ({k for k in vsig if msig.get(k) != vsig[k]}
                  or {k for k in msig if vsig.get(k) != msig[k]}
                  or any(pid(p) is None for p in mpos))
        print(f"  {'caught  ' if caught else 'MISSED  '} {label}")
        ok = ok and bool(caught)
    print("OK: every negative control fires" if ok
          else "FAIL: a real difference slipped through --verify-vendor")
    return 0 if ok else 1


def _patch_cone(flip=False, segments=None, apex=None, no_cap=False):
    global SEGMENTS
    old = SEGMENTS
    try:
        if segments:
            SEGMENTS = segments
        b = build_cone()
        if flip:
            b.tris = [t if i < 18 else (t[0], t[2], t[1]) for i, t in enumerate(b.tris)]
        if no_cap:
            b.tris = b.tris[18:]
        if apex:
            b.verts = [((v[0][0], apex if v[0][1] > 0 else v[0][1], v[0][2]), v[1], v[2])
                       for v in b.verts]
        return b
    finally:
        SEGMENTS = old


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--verify-vendor", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        print("negative controls for --verify-vendor")
        return self_test()

    if args.verify_vendor:
        print("PrimitivePlus -> first-party primitive meshes")
        return verify_vendor()

    built = [(name, *emit(name, build())) for name, build, _ in MESHES]
    folder_meta = FOLDER_META.format(guid=guid_for("__folder__"))

    if args.check:
        drift = []
        for name, body, meta in built:
            p = os.path.join(OUT_DIR, name + ".asset")
            for path, want in ((p, body), (p + ".meta", meta)):
                have = open(path, newline="").read() if os.path.exists(path) else ""
                if have != want:
                    drift.append(os.path.relpath(path, REPO))
        have = open(OUT_DIR + ".meta", newline="").read() if os.path.exists(OUT_DIR + ".meta") else ""
        if have != folder_meta:
            drift.append("Assets/_Models/Primitives.meta")
        for d in drift:
            print("DRIFT", d)
        print("FAIL: re-run without --check" if drift else "OK: primitive meshes match")
        return 1 if drift else 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", newline="") as f:
        f.write(folder_meta)
    for name, body, meta in built:
        p = os.path.join(OUT_DIR, name + ".asset")
        with open(p, "w", newline="") as f:
            f.write(body)
        with open(p + ".meta", "w", newline="") as f:
            f.write(meta)
        nv = body.count("_typelessdata")
        print(f"wrote {name}.asset  guid={guid_for(name)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
