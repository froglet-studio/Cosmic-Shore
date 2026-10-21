#!/usr/bin/env python3
"""
Author the SPINDLE LOD meshes and the measured half of the spindle LOD profile.

    python3 Tools/Build/author_spindle_lod_meshes.py              # write meshes + profile
    python3 Tools/Build/author_spindle_lod_meshes.py --check      # fail if anything on disk drifted
    python3 Tools/Build/author_spindle_lod_meshes.py --report     # print the measurements only
    python3 Tools/Build/author_spindle_lod_meshes.py --self-test  # prove the checks fire

WHAT A SPINDLE IS, MEASURED. Every flora branch (Branch, GyroidBranch, AssemblyBranch,
QuasicrystalBranch) and the tadpole draw ONE mesh: `BezierCurve.001` inside
`Assets/_Models/Fauna/bonita.fbx`. It is not a tube. It is a BRAID: three strands of triangular
cross-section, 25 rings each, spiralling round a STRAIGHT centreline (the mean of the three ring
centres sits on the z axis to 0.013 of a unit at every ring). Each strand tapers 0.015 -> 0.151 ->
0.0125 units; the three spread apart, pinch back together at ring 12 and splay at the tip. 216
quads, 432 triangles, drawn by SpindleGraph's Voronoi lace (UV0: u runs the strand's length, v its
three faces), which works at the scale of WHOLE STRANDS (about 1.5 cells per strand).

Up close that is the most beautiful thing in a cell. Far away it is three sub-pixel strands: a
0.3-unit strand is one pixel wide at ~280 units (1080p, 60 degree FOV), so beyond that the braid
is 432 triangles a rasteriser shades as broken, sparkling dots while the shader's own distance
term (`_Distance`) alpha-clips it away by ~1,400 units. That is the cost and the look both.

WHAT THIS WRITES (Assets/_Models/Spindles/):

  SpindleBraidMid.asset   the SAME braid with every third ring (25 -> 9 rings per strand, 432 ->
                          144 triangles), exact original positions, normals and UV0 at the rings it
                          keeps, so SpindleGraph's lace reads the same. Two extra channels carry
                          the CONVERGENCE geomorph the mid tier runs:
                            TEXCOORD1.xyz  centreline point minus this ring's strand centre
                            TEXCOORD2.xyz  this vertex minus its strand centre (its radial arm)
                            TEXCOORD3.xyz  its strand's SPLAY: the unit direction the strand's tip
                                           leans off the centreline (the overt style fans along it)
                          so `SpindleLod.hlsl` can draw the three strands together into one thread
                          as the camera pulls away, continuously, with no pop.
  SpindleBraidLow.asset   the braid at every SIXTH ring (72 triangles), same channels: the overt
                          style's far-side recede, where the fan closes and the strands converge.
  SpindleThreadFar.asset  what the braid converges TO, at the lowest cost that still shows a spindle:
                          ONE faceted thread of triangular cross-section on the centreline, 3 rings
                          carrying the strand's taper (root, belly, tip: thin - thick - thin), 12
                          triangles. TEXCOORD1 = 0 (already converged), TEXCOORD2 = the radial arm.

and the MEASURED block of `Assets/Resources/SpindleLodProfile.asset` (the source mesh, the two LOD
meshes, the braid girth, the reference strand radius and the source mesh's expected bounds). The
tuning half of that asset (bands, look) is the designer's: this script writes it only when the
asset does not exist yet and never touches it afterwards.

UNITY'S IMPORT, REPRODUCED. `bakeAxisConversion: 0`, so the mesh stays in node space; right-handed
FBX -> left-handed Unity negates x (positions AND normals) and reverses winding; UnitScaleFactor 1
(centimetres) x useFileScale = 0.01. The node's own Lcl Scaling (0.1076) is NOT baked: the prefabs
reference the mesh sub-asset and carry their own transform. Because a wrong guess here would ship
meshes that do not line up with the braid, the profile records the bounds this conversion predicts
for the SOURCE mesh and `SpindleLodProfileSO` compares them against the imported mesh's real
bounds at runtime (available even on a non-readable mesh): a mismatch disables LOD for that mesh,
loudly, instead of drawing a thread beside the branch.
"""

import argparse
import hashlib
import math
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fbx_binary  # noqa: E402
from author_primitive_meshes import compute_tangents, fmt  # noqa: E402

REPO = os.path.dirname(os.path.dirname(HERE))
assert os.path.isdir(os.path.join(REPO, "Assets")), "REPO must be the project root"

FBX = "Assets/_Models/Fauna/bonita.fbx"
FBX_GUID = "c9a0b575455114844a1b087dd889626e"
SOURCE_MESH_FILE_ID = 1825875415147931077          # BezierCurve.001, as every spindle prefab references it
SOURCE_NODE = b"BezierCurve.001"
OUT_DIR = "Assets/_Models/Spindles"
MID_NAME = "SpindleBraidMid"
LOW_NAME = "SpindleBraidLow"
FAR_NAME = "SpindleThreadFar"
PROFILE = "Assets/Resources/SpindleLodProfile.asset"
PROFILE_SCRIPT = "Assets/_Scripts/ScriptableObjects/SpindleLodProfileSO.cs"
DONOR_MESH = "Assets/_Models/Testing/Prism.asset"

STRANDS = 3
RINGS = 25
CORNERS = 3
UNIT = 0.01                                     # UnitScaleFactor 1 (cm) x useFileScale
MID_RINGS = (0, 3, 6, 9, 12, 15, 18, 21, 24)    # every third ring; 12 (the pinch) and both ends kept
LOW_RINGS = (0, 6, 12, 18, 24)                  # every sixth ring: the overt style's far-side recede
FAR_RINGS = (0, 12, 24)                         # root, belly, tip: the spindle taper in 12 triangles

# The reference display the --report pixel figures assume. Only a REPORT: the shipped bands are in
# girths (unit-free) and the shader measures real pixels from the live camera.
REF_SCREEN_HEIGHT = 1080
REF_VFOV_DEG = 60.0


def guid_for(name):
    return hashlib.md5(f"CosmicShore/SpindleLod/{name}".encode()).hexdigest()


MID_GUID = guid_for(MID_NAME)
LOW_GUID = guid_for(LOW_NAME)
FAR_GUID = guid_for(FAR_NAME)
FOLDER_GUID = guid_for("folder")
PROFILE_GUID = guid_for("profile")
SCRIPT_GUID = guid_for("SpindleLodProfileSO")
# The template materials are authored by Tools/Shaders/wire_spindle_lod.py; same guid namespace.
MID_TEMPLATE_GUID = guid_for("SpindleLodMid")
FAR_TEMPLATE_GUID = guid_for("SpindleLodFar")
DEPTH_TEMPLATE_GUID = guid_for("SpindleDepth")


# --------------------------------------------------------------------------------------------
# Reading the braid
# --------------------------------------------------------------------------------------------

def _walk(nodes):
    for n in nodes:
        yield n
        yield from _walk(n.children)


def read_braid(path=FBX):
    """The braid as Unity imports it: per-vertex positions, and per polygon-corner normal + UV0."""
    nodes = fbx_binary.read(os.path.join(REPO, path))[0]
    geo = next(n for n in _walk(nodes)
               if n.name == "Geometry" and n.props and SOURCE_NODE in n.props[1][1] and n.first("Vertices"))
    raw = geo.first("Vertices").props[0][1]
    pos = [(-raw[i] * UNIT, raw[i + 1] * UNIT, raw[i + 2] * UNIT) for i in range(0, len(raw), 3)]
    idx = geo.first("PolygonVertexIndex").props[0][1]
    nrm_raw = geo.first("LayerElementNormal").first("Normals").props[0][1]
    uvl = geo.first("LayerElementUV")
    uv_raw = uvl.first("UV").props[0][1]
    uv_idx = uvl.first("UVIndex").props[0][1]

    quads = []
    corner = []
    for pv, vi in enumerate(idx):
        v = vi if vi >= 0 else ~vi
        n = (-nrm_raw[3 * pv], nrm_raw[3 * pv + 1], nrm_raw[3 * pv + 2])
        uv = (uv_raw[2 * uv_idx[pv]], uv_raw[2 * uv_idx[pv] + 1])
        corner.append((v, n, uv))
        if vi < 0:
            quads.append(corner)
            corner = []
    assert len(pos) == STRANDS * RINGS * CORNERS, f"braid has {len(pos)} vertices, expected 225"
    assert len(quads) == STRANDS * (RINGS - 1) * CORNERS, f"braid has {len(quads)} quads, expected 216"
    return pos, quads


def vid(strand, ring, c):
    return (strand * RINGS + ring) * CORNERS + c


def ring_centre(pos, s, k):
    pts = [pos[vid(s, k, c)] for c in range(CORNERS)]
    return tuple(sum(p[j] for p in pts) / CORNERS for j in range(3))


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _scale(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _len(a):
    return math.sqrt(_dot(a, a))


def _norm(a):
    m = _len(a) or 1.0
    return _scale(a, 1.0 / m)


def measure(pos):
    """Centreline, per-ring strand radius and spread, girth, decimation error."""
    centres = [[ring_centre(pos, s, k) for k in range(RINGS)] for s in range(STRANDS)]
    centreline = [tuple(sum(centres[s][k][j] for s in range(STRANDS)) / STRANDS for j in range(3))
                  for k in range(RINGS)]
    radius = [sum(_len(_sub(pos[vid(s, k, c)], centres[s][k])) for s in range(STRANDS) for c in range(CORNERS))
              / (STRANDS * CORNERS) for k in range(RINGS)]
    spread = [sum(_len(_sub(centres[s][k], centreline[k])) for s in range(STRANDS)) / STRANDS for k in range(RINGS)]
    axis_off = max(math.hypot(c[0], c[1]) for c in centreline)
    girth = 2.0 * max(_len(_sub(pos[vid(s, k, c)], (0.0, 0.0, centreline[k][2])))
                      for s in range(STRANDS) for k in range(RINGS) for c in range(CORNERS))
    r_ref = sum(radius) / len(radius)

    # Decimation error: every dropped ring's vertices against the straight segment between the two
    # kept rings either side, interpolated by ring index (the mesh's own parameter).
    dec_err = 0.0
    for s in range(STRANDS):
        for a, b in zip(MID_RINGS, MID_RINGS[1:]):
            for k in range(a + 1, b):
                t = (k - a) / (b - a)
                for c in range(CORNERS):
                    pa, pb = pos[vid(s, a, c)], pos[vid(s, b, c)]
                    lerp = _add(pa, _scale(_sub(pb, pa), t))
                    dec_err = max(dec_err, _len(_sub(pos[vid(s, k, c)], lerp)))
    lo = [min(p[j] for p in pos) for j in range(3)]
    hi = [max(p[j] for p in pos) for j in range(3)]
    return dict(centres=centres, centreline=centreline, radius=radius, spread=spread, axis_off=axis_off,
                girth=girth, r_ref=r_ref, dec_err=dec_err, lo=lo, hi=hi)


# --------------------------------------------------------------------------------------------
# Building the two LOD meshes
# --------------------------------------------------------------------------------------------

def _corner_lookup(quads):
    """(vertex, face) -> (normal, uv), where face = the cross-section face a polygon corner belongs to."""
    look = {}
    for q in quads:
        rings = sorted({(v // CORNERS) % RINGS for v, _, _ in q})
        cs = sorted({v % CORNERS for v, _, _ in q})
        face = (cs[0], cs[1]) if cs != [0, 2] else (2, 0)
        for v, n, uv in q:
            look[(v, face, rings[0])] = (n, uv)
    return look


def _emit_quad(out, corners):
    """corners: 4 (pos, nrm, uv, uv1, uv2) in ring-a-c0, ring-a-c1, ring-b-c1, ring-b-c0 order.
    Emits two triangles wound so that cross(b-a, c-a) agrees with the vertex normals - the rule
    Unity's own RecalculateNormals follows for a front face."""
    base = len(out["v"])
    out["v"].extend(corners)
    tri_a = (base, base + 1, base + 2)
    tri_b = (base, base + 2, base + 3)
    for tri in (tri_a, tri_b):
        p = [corners[i - base][0] for i in tri]
        n = _add(_add(corners[tri[0] - base][1], corners[tri[1] - base][1]), corners[tri[2] - base][1])
        if _dot(_cross(_sub(p[1], p[0]), _sub(p[2], p[0])), n) < 0.0:
            tri = (tri[0], tri[2], tri[1])
        out["t"].append(tri)


def build_mid(pos, quads, m, rings=MID_RINGS):
    look = _corner_lookup(quads)
    out = {"v": [], "t": []}
    faces = ((0, 1), (1, 2), (2, 0))
    # Each strand's SPLAY: the unit direction its tip already leans away from the centreline (the
    # authored braid splays 0.69 there). The overt style fans a strand along it, root-anchored, so the
    # effect amplifies a shape the braid already has instead of inventing one. One constant per
    # strand: a direction that turned with the spiral would curl a parted strand into a coil.
    tip = RINGS - 1
    splay = []
    for s in range(STRANDS):
        d = _sub(m["centres"][s][tip], m["centreline"][tip])
        splay.append(_norm((d[0], d[1], 0.0)))
    for s in range(STRANDS):
        for a, b in zip(rings, rings[1:]):
            for c0, c1 in faces:
                corners = []
                for ring, c, nrm_ring in ((a, c0, a), (a, c1, a), (b, c1, b - 1), (b, c0, b - 1)):
                    v = vid(s, ring, c)
                    n, uv = look[(v, (c0, c1), nrm_ring)]
                    centre = m["centres"][s][ring]
                    to_axis = _sub(m["centreline"][ring], centre)
                    radial = _sub(pos[v], centre)
                    corners.append((pos[v], n, uv, to_axis, radial, splay[s]))
                _emit_quad(out, corners)
    return out


def build_far(m):
    out = {"v": [], "t": []}
    # A fixed triangular frame on the straight centreline. The strands' own cross-sections spin
    # with the braid; the converged thread does not need to, and a fixed frame keeps it 24 tris.
    angles = [math.radians(90.0 + 120.0 * i) for i in range(CORNERS)]
    rings = []
    for k in FAR_RINGS:
        z = m["centreline"][k][2]
        r = m["radius"][k]
        rings.append([((r * math.cos(t), r * math.sin(t), z), (r * math.cos(t), r * math.sin(t), 0.0))
                      for t in angles])
    for ka, kb, ra, rb in zip(FAR_RINGS, FAR_RINGS[1:], rings, rings[1:]):
        for c0 in range(CORNERS):
            c1 = (c0 + 1) % CORNERS
            quad = [ra[c0], ra[c1], rb[c1], rb[c0]]
            # Flat facet normal: the face is planar (two parallel edges of one ring pair).
            n = _norm(_cross(_sub(quad[1][0], quad[0][0]), _sub(quad[3][0], quad[0][0])))
            mid = _scale(_add(_add(quad[0][0], quad[1][0]), _add(quad[2][0], quad[3][0])), 0.25)
            outward = (mid[0], mid[1], 0.0)
            if _dot(n, outward) < 0.0:
                n = _scale(n, -1.0)
            u0 = ka / (RINGS - 1)
            u1 = kb / (RINGS - 1)
            v0 = c0 / 2.0
            v1 = 1.0 if c1 == 0 else c1 / 2.0
            uvs = [(u0, v0), (u0, v1), (u1, v1), (u1, v0)]
            corners = [(p, n, uv, (0.0, 0.0, 0.0), radial, (0.0, 0.0, 0.0)) for (p, radial), uv in zip(quad, uvs)]
            _emit_quad(out, corners)
    return out


# --------------------------------------------------------------------------------------------
# Unity Mesh .asset text (the donor-clone layout author_omni_crystal_triangles.py proved)
# --------------------------------------------------------------------------------------------

def _f32(x):
    return struct.unpack("<f", struct.pack("<f", x))[0]


def _v3(a):
    return "{x: %s, y: %s, z: %s}" % tuple(repr(float(_f32(c))) for c in a)


def mesh_text(mesh, name):
    verts = mesh["v"]
    tris = mesh["t"]
    tangent_input = [(p, n, uv) for p, n, uv, _, _, _ in verts]
    tangents = compute_tangents(tangent_input, tris)
    vbuf = bytearray()
    for (p, n, uv, uv1, uv2, uv3), t in zip(verts, tangents):
        # stride 84: position, normal, tangent, uv0, then TEXCOORD1/2/3.xyz.
        vbuf += struct.pack("<3f3f4f2f3f3f3f", *p, *n, *t, *uv, *uv1, *uv2, *uv3)
    indices = [i for tri in tris for i in tri]
    ibuf = struct.pack("<%dH" % len(indices), *indices)
    lo = [min(v[0][k] for v in verts) for k in range(3)]
    hi = [max(v[0][k] for v in verts) for k in range(3)]
    ctr = [(lo[k] + hi[k]) / 2 for k in range(3)]
    ext = [(hi[k] - lo[k]) / 2 for k in range(3)]
    used = {0: (0, 3), 1: (12, 3), 2: (24, 4), 4: (40, 2), 5: (48, 3), 6: (60, 3), 7: (72, 3)}
    channels = "".join(
        "    - stream: 0\n      offset: %d\n      format: 0\n      dimension: %d\n"
        % used.get(i, (0, 0)) for i in range(14))
    donor = open(os.path.join(REPO, DONOR_MESH)).read()
    head = donor[:donor.index("  m_SubMeshes:")].replace("m_Name: Prism", f"m_Name: {name}")
    tail = donor[donor.index("  m_CompressedMesh:"):]
    tail = re.sub(r"  m_LocalAABB:\n    m_Center: .*\n    m_Extent: .*\n",
                  f"  m_LocalAABB:\n    m_Center: {_v3(ctr)}\n    m_Extent: {_v3(ext)}\n", tail)
    return (head
            + "  m_SubMeshes:\n  - serializedVersion: 2\n    firstByte: 0\n"
            + f"    indexCount: {len(indices)}\n    topology: 0\n    baseVertex: 0\n"
            + f"    firstVertex: 0\n    vertexCount: {len(verts)}\n    localAABB:\n"
            + f"      m_Center: {_v3(ctr)}\n      m_Extent: {_v3(ext)}\n"
            + donor[donor.index("  m_Shapes:"):donor.index("  m_IndexBuffer:")]
            + f"  m_IndexBuffer: {ibuf.hex()}\n"
            + "  m_VertexData:\n    serializedVersion: 3\n"
            + f"    m_VertexCount: {len(verts)}\n    m_Channels:\n{channels}"
            + f"    m_DataSize: {len(vbuf)}\n    _typelessdata: {vbuf.hex()}\n"
            + tail)


MESH_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 4300000
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

PROFILE_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


# --------------------------------------------------------------------------------------------
# The profile: a tuning half (designer-owned, written once) and a measured half (owned here)
# --------------------------------------------------------------------------------------------

PROFILE_HEAD = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: SpindleLodProfile
  m_EditorClassIdentifier:
  style: 1
  lodEnabled: 1
  tickIntervalSeconds: 0.2
  maxTransitionsPerFrame: 384
  batchFarThreads: 1
  nearEndGirths: 100
  midEndGirths: 420
  hysteresis: 0.08
  convergeStartGirths: 110
  farFadeStartGirths: 1300
  farFadeEndGirths: 1900
  overtThreadStartNorm: 10
  overtFadeStartNorm: 15.625
  overtFadeEndNorm: 22.5
  chargeStrength: 0.175
  otherStrength: 0.05
  minThreadPixels: 1.25
  threadBase: 0.35
  threadFacet: 0.55
  threadGlow: 1.35
  threadAlpha: 0.85
  depthOnBodies: 1
  switchRingStrength: 0.05
  switchRingDistance: 200000000
  switchRingSway: 0
"""


def measured_block(m):
    lo, hi = m["lo"], m["hi"]
    ctr = [(lo[k] + hi[k]) / 2 for k in range(3)]
    ext = [(hi[k] - lo[k]) / 2 for k in range(3)]
    return (
        f"  midTemplate: {{fileID: 2100000, guid: {MID_TEMPLATE_GUID}, type: 2}}\n"
        f"  farTemplate: {{fileID: 2100000, guid: {FAR_TEMPLATE_GUID}, type: 2}}\n"
        f"  depthTemplate: {{fileID: 2100000, guid: {DEPTH_TEMPLATE_GUID}, type: 2}}\n"
        "  meshSets:\n"
        f"  - source: {{fileID: {SOURCE_MESH_FILE_ID}, guid: {FBX_GUID}, type: 3}}\n"
        f"    mid: {{fileID: 4300000, guid: {MID_GUID}, type: 2}}\n"
        f"    low: {{fileID: 4300000, guid: {LOW_GUID}, type: 2}}\n"
        f"    far: {{fileID: 4300000, guid: {FAR_GUID}, type: 2}}\n"
        f"    girth: {fmt(m['girth'])}\n"
        f"    strandRadius: {fmt(m['r_ref'])}\n"
        f"    expectedSourceCenter: {{x: {fmt(ctr[0])}, y: {fmt(ctr[1])}, z: {fmt(ctr[2])}}}\n"
        f"    expectedSourceExtent: {{x: {fmt(ext[0])}, y: {fmt(ext[1])}, z: {fmt(ext[2])}}}\n"
    )


MEASURED_RE = re.compile(r"  midTemplate: .*\n(?:  [a-zA-Z]\w*Template: .*\n)*  meshSets:\n(?:  - .*\n|    .*\n)*")


def profile_text(existing, m):
    block = measured_block(m)
    if existing is None:
        return PROFILE_HEAD.format(script=SCRIPT_GUID) + block
    if MEASURED_RE.search(existing):
        return MEASURED_RE.sub(lambda _: block, existing, count=1)
    assert "meshSets:" not in existing, "the profile's measured block is no longer in the shape this script writes"
    return existing.rstrip("\n") + "\n" + block


# --------------------------------------------------------------------------------------------
# Checks
# --------------------------------------------------------------------------------------------

def validate_braid(pos, braid, rings):
    """A decimated braid's own contract: the triangle count its rings imply, and every vertex one of the
    braid's own (so the lace and the strand path stay the authored ones)."""
    errs = []
    if len(braid["t"]) != STRANDS * (len(rings) - 1) * CORNERS * 2:
        errs.append(f"mid: {len(braid['t'])} triangles for {len(rings)} rings")
    kept = {pos[vid(s, k, c)] for s in range(STRANDS) for k in rings for c in range(CORNERS)}
    if any(v[0] not in kept for v in braid["v"]):
        errs.append("mid: a vertex is not one of the braid's own")
    return errs


def validate(pos, quads, m, mid, far):
    errs = []
    if m["axis_off"] > 0.02:
        errs.append(f"the braid's centreline left the z axis by {m['axis_off']:.4f} (a straight thread no longer fits)")
    for name, mesh in (("mid", mid), ("far", far)):
        for tri in mesh["t"]:
            p = [mesh["v"][i][0] for i in tri]
            n = _add(_add(mesh["v"][tri[0]][1], mesh["v"][tri[1]][1]), mesh["v"][tri[2]][1])
            if _dot(_cross(_sub(p[1], p[0]), _sub(p[2], p[0])), n) <= 0.0:
                errs.append(f"{name}: a triangle faces away from its own normals")
                break
        for p, _, _, to_axis, radial, _ in mesh["v"]:
            # The geomorph's whole contract: the vertex = strand centre + radial, and the strand
            # centre + to_axis lies on the centreline (x = y = 0, within the measured axis offset).
            centre = _sub(p, radial)
            target = _add(centre, to_axis)
            if math.hypot(target[0], target[1]) > m["axis_off"] + 1e-6:
                errs.append(f"{name}: a vertex's convergence target is off the centreline")
                break
    splays = {tuple(round(c, 6) for c in v[5]) for v in mid["v"]}
    if len(splays) != STRANDS or any(abs(_len(s) - 1.0) > 1e-5 for s in splays):
        errs.append(f"mid: expected {STRANDS} unit splay directions, found {len(splays)}")
    if len(mid["t"]) != STRANDS * (len(MID_RINGS) - 1) * CORNERS * 2:
        errs.append(f"mid: {len(mid['t'])} triangles")
    if len(far["t"]) != (len(FAR_RINGS) - 1) * CORNERS * 2:
        errs.append(f"far: {len(far['t'])} triangles")
    # The mid mesh keeps the braid's own vertices at every ring it keeps - the lace must not move.
    kept = {pos[vid(s, k, c)] for s in range(STRANDS) for k in MID_RINGS for c in range(CORNERS)}
    if any(v[0] not in kept for v in mid["v"]):
        errs.append("mid: a vertex is not one of the braid's own")
    # Converged mid == far, ring for ring: the strand centres land on the centreline the far
    # thread is built on, and the far thread's radius is the strands' own.
    for k in FAR_RINGS:
        for s in range(STRANDS):
            c = _add(m["centres"][s][k], _sub(m["centreline"][k], m["centres"][s][k]))
            if _len(_sub(c, m["centreline"][k])) > 1e-9:
                errs.append("converged strand centre is not the centreline")
    return errs


def report(m, mid, far):
    px_per_unit_at_1 = REF_SCREEN_HEIGHT / (2.0 * math.tan(math.radians(REF_VFOV_DEG) / 2.0))
    thick = 2.0 * max(m["radius"])
    low_tris = STRANDS * (len(LOW_RINGS) - 1) * CORNERS * 2
    print(f"braid: 432 tris -> mid {len(mid['t'])} tris ({len(mid['v'])} verts) -> low {low_tris} tris -> far thread {len(far['t'])} tris ({len(far['v'])} verts)")
    print(f"centreline off the z axis by at most {m['axis_off']:.4f}; girth {m['girth']:.4f}; mean strand radius {m['r_ref']:.4f}")
    print(f"strand radius by ring: {' '.join(f'{r:.3f}' for r in m['radius'])}")
    print(f"strand spread by ring: {' '.join(f'{s:.3f}' for s in m['spread'])}")
    print(f"mid decimation error {m['dec_err']:.4f} units: half a pixel at {m['dec_err'] * px_per_unit_at_1 / 0.5:.0f} units "
          f"({m['dec_err'] * px_per_unit_at_1 / 0.5 / m['girth']:.0f} girths) on the reference display")
    print(f"thickest strand ({thick:.3f}) is one pixel at {thick * px_per_unit_at_1:.0f} units "
          f"({thick * px_per_unit_at_1 / m['girth']:.0f} girths); the whole braid at {m['girth'] * px_per_unit_at_1:.0f} units")


def outputs():
    pos, quads = read_braid()
    m = measure(pos)
    mid = build_mid(pos, quads, m)
    low = build_mid(pos, quads, m, LOW_RINGS)
    far = build_far(m)
    errs = validate(pos, quads, m, mid, far)
    errs += [e.replace("mid:", "low:") for e in validate_braid(pos, low, LOW_RINGS)]
    profile_path = os.path.join(REPO, PROFILE)
    existing = open(profile_path).read() if os.path.exists(profile_path) else None
    files = {
        f"{OUT_DIR}.meta": FOLDER_META.format(guid=FOLDER_GUID),
        f"{OUT_DIR}/{MID_NAME}.asset": mesh_text(mid, MID_NAME),
        f"{OUT_DIR}/{MID_NAME}.asset.meta": MESH_META.format(guid=MID_GUID),
        f"{OUT_DIR}/{LOW_NAME}.asset": mesh_text(low, LOW_NAME),
        f"{OUT_DIR}/{LOW_NAME}.asset.meta": MESH_META.format(guid=LOW_GUID),
        f"{OUT_DIR}/{FAR_NAME}.asset": mesh_text(far, FAR_NAME),
        f"{OUT_DIR}/{FAR_NAME}.asset.meta": MESH_META.format(guid=FAR_GUID),
        PROFILE: profile_text(existing, m),
    }
    if existing is None:
        files[PROFILE + ".meta"] = PROFILE_META.format(guid=PROFILE_GUID)
    return files, errs, (m, mid, far)


def script_guid_errors():
    meta = os.path.join(REPO, PROFILE_SCRIPT + ".meta")
    if not os.path.exists(meta):
        return [f"{PROFILE_SCRIPT}.meta is missing"]
    if f"guid: {SCRIPT_GUID}" not in open(meta).read():
        return [f"{PROFILE_SCRIPT}.meta does not carry guid {SCRIPT_GUID}, which the profile references"]
    return []


def self_test():
    pos, quads = read_braid()
    m = measure(pos)
    mid = build_mid(pos, quads, m)
    far = build_far(m)
    assert not validate(pos, quads, m, mid, far), "the shipped construction must validate"
    failures = 0
    # 1. A flipped triangle must be caught.
    bad = {"v": mid["v"], "t": [(t[0], t[2], t[1]) if i == 0 else t for i, t in enumerate(mid["t"])]}
    failures += not any("faces away" in e for e in validate(pos, quads, m, bad, far))
    # 2. A convergence target pushed off the centreline must be caught.
    v0 = mid["v"][0]
    bad = {"v": [(v0[0], v0[1], v0[2], _add(v0[3], (0.1, 0.0, 0.0)), v0[4], v0[5])] + mid["v"][1:], "t": mid["t"]}
    failures += not any("off the centreline" in e for e in validate(pos, quads, m, bad, far))
    # 3. A mid vertex that is not one of the braid's own (the lace would move) must be caught.
    bad = {"v": [((v0[0][0] + 0.01, v0[0][1], v0[0][2]),) + v0[1:]] + mid["v"][1:], "t": mid["t"]}
    failures += not any("not one of the braid's own" in e for e in validate(pos, quads, m, bad, far))
    # 4. A braid whose centreline leaves the axis must be caught.
    bent = dict(m)
    bent["axis_off"] = 0.05
    failures += not any("left the z axis" in e for e in validate(pos, quads, bent, mid, far))
    # 5. The profile's measured block is replaced in place and the tuning half survives.
    tuned = profile_text(None, m).replace("nearEndGirths: 100", "nearEndGirths: 99")
    again = profile_text(tuned, m)
    failures += "nearEndGirths: 99" not in again or again.count("meshSets:") != 1
    print("self-test:", "OK (5 negative controls fire)" if failures == 0 else f"FAILED ({failures})")
    return 1 if failures else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    files, errs, (m, mid, far) = outputs()
    errs += script_guid_errors()
    if args.report:
        report(m, mid, far)
        return 0
    if errs:
        for e in errs:
            print("ERROR:", e)
        return 1
    drift = []
    for rel, text in files.items():
        path = os.path.join(REPO, rel)
        on_disk = open(path).read() if os.path.exists(path) else None
        if on_disk != text:
            drift.append(rel)
            if not args.check:
                os.makedirs(os.path.dirname(path), exist_ok=True)
                with open(path, "w", newline="\n") as fh:
                    fh.write(text)
    if args.check:
        if drift:
            print("DRIFT:", ", ".join(drift))
            return 1
        print("spindle LOD meshes and profile: OK")
        return 0
    print("wrote:" if drift else "no change:", ", ".join(drift) if drift else "all files current")
    report(m, mid, far)
    return 0


if __name__ == "__main__":
    sys.exit(main())
