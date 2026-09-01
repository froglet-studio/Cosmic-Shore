#!/usr/bin/env python3
"""Author the SWORDFISH flagship fauna - every asset, from the artist's FBX.

    python3 Tools/Build/author_swordfish_fauna.py            # report only
    python3 Tools/Build/author_swordfish_fauna.py --check    # fail on drift (CI)
    python3 Tools/Build/author_swordfish_fauna.py --write    # author the assets

Docs/ECOSYSTEM.md §42 is the design record; this file is the SOURCE, the assets are the
BUILD.  Idempotent and deterministic: every GUID / fileID is md5("CosmicShore/<stable
name>"), so a re-run is byte-identical and a retune is one edit here plus a re-run.  The
whole result is validated in memory - every YAML document parses, every local reference
resolves, every id fits int64, the FBX round-trips - and only then written (/asset-surgery).

WHAT IT BUILDS, AND WHY EACH PIECE IS SHAPED THE WAY IT IS
-----------------------------------------------------------
1. `Assets/_Models/Fauna/SwordFish_A_Parts.fbx` - the artist's `SwordFish_A.fbx` with its ONE
   skinned mesh split into EIGHT bone-parts (bill, trunk, dorsal sail, anal fin, two pectorals,
   two tail lobes) and RE-CENTRED on the body.  The split is what makes the creature die the
   way the ecology law says a creature dies: one Spindle per part, so starvation withers the
   bill and tail lobes first and the trunk last (§26 - farthest-from-the-heart first,
   emergent from geometry), a shot-off fin evaporates on its own, and an element variant can
   re-skin the whole body through the ordinary `BodyMaterial` swap.  The artist's file is
   left untouched; re-export it and re-run.  The re-centring is not cosmetic: the model sits
   ~540 units from its own origin, and both the prefab's nested-instance pose and the
   heart-size measurement (author_lifeform_heart_sizes.py) read a node's distance from the
   root as body reach.

   The mesh is ALSO a drill.  The trunk+bill bone (`Bone.001`) rolls a full turn about the
   body axis every ~1.1 s in the swim cycle and ~1.7 turns/s in the charge take, while the
   fins and tail (their own bones) hold still - measured off the animation curves, not
   guessed.  Everything mounted on that bone orbits the body axis, which is why the trunk's
   prisms are three-fold symmetric flutes and the bill's are on-axis needles.

2. Twelve prisms, each placed from the geometry of the part it armours (bone-local, from the
   bind matrices): 3 tapering DangerBlock needles laid end to end along the bill (the sword -
   the creature's ONLY damage, through the ordinary danger-prism contact chain), 3 drill
   flutes around the trunk, one blade plate per fin and tail lobe.  Nothing overlaps; sizes
   are a fraction of the part's measured extents, never a constant.  Every prism hangs under
   a MOUNT (a child of its bone scaled 1/armature so prism scales are authored in world
   units) and names its part's Spindle explicitly.

3. The prefab (`Assets/_Prefabs/FloraAndFauna/SwordfishFauna.prefab`) nests the parts FBX the
   way Clawfish does, adds a Spindle to each part GameObject, the mounts to the bones, a
   dormant Mass heart (donor-cloned from the shark prefab so the schema is exact), and the
   network trio (NetworkObject / NetworkTransform / FaunaNetworkSync) - registered in
   DefaultNetworkPrefabs.

   Nested-FBX references need the FBX's imported object ids.  Unity generates those by
   hashing (fileIdsGeneration 2), an algorithm this repo has not reproduced, so the ids are
   PINNED through the importer's own `internalIDToNameTable` (the mechanism Unity itself uses
   to keep ids stable across renames).  FrogletTools > Ecology > Swordfish Flagship validates
   that every binding resolved after import and can rebind the prefab to the real ids if the
   table was not honoured - the one thing this script cannot verify without an editor.

4. Behaviour data: `SwordfishFaunaDataSO` (LightFaunaDataSO - the predator numbers) and
   `SwordfishStrikeData` (SwordfishStrikeDataSO - the vessel charge: aggro, telegraph, lunge,
   recover, cooldown, and one strike profile per ELEMENT so the four variants FEEL different
   without a per-element prefab).  Four canonical variants in `_SO_Assets/Lifeforms`, one
   Blob deployment config (SpreadElements over the four, NetworkSynced) that takes the
   shark's apex slot in the freestyle worlds' spawn profile.

5. The animator controller: Swim / Pursue (swim x1.6) / Tuck / ChargeHold (loop) / Flare,
   driven by two bools (`Pursuing`, `Charging`) from SwordfishChargeDriver.  The three charge
   states are sub-clips of the artist's `SwrdFsh_Charge` take, cut where its curves say the
   phases are (fins fold 0-1.75 s, hold, fins flare 7.7-8.9 s).
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import math
import re
import sys
from collections import Counter
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import fbx_binary as fb  # noqa: E402

INT64_MAX = 9223372036854775807

# ── Inputs ─────────────────────────────────────────────────────────────────────
SRC_FBX = ROOT / "Assets/_Models/Fauna/SwordFish_A.fbx"
SRC_FBX_META = SRC_FBX.with_suffix(".fbx.meta")
DONOR_PREFAB = ROOT / "Assets/_Models/Fauna/MassSharkFauna.prefab"        # crystal donor
SPAWN_PROFILE = ROOT / "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset"
NETWORK_PREFABS = ROOT / "Assets/DefaultNetworkPrefabs.asset"

# ── Outputs ────────────────────────────────────────────────────────────────────
PARTS_FBX = ROOT / "Assets/_Models/Fauna/SwordFish_A_Parts.fbx"
CONTROLLER = ROOT / "Assets/_Models/Fauna/SwordFish_A_Parts.controller"
PREFAB = ROOT / "Assets/_Prefabs/FloraAndFauna/SwordfishFauna.prefab"
DATA_SO = ROOT / "Assets/_SO_Assets/Light Fauna Data/SwordfishFaunaDataSO.asset"
STRIKE_SO = ROOT / "Assets/_SO_Assets/Light Fauna Data/SwordfishStrikeData.asset"
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
BLOB_CONFIG = ROOT / "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset"

SCRIPTS = {
    "SwordfishFauna":        "Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishFauna.cs",
    "SwordfishStrikeDataSO": "Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishStrikeDataSO.cs",
    "SwordfishChargeDriver": "Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishChargeDriver.cs",
    "SwordfishFlagshipTool": "Assets/_Scripts/Editor/SwordfishFlagshipTool.cs",
}

# ── Existing guids this build references (each owned by exactly one .meta) ─────
G_SPINDLE_SCRIPT = "8ec45d1233573f9409107a25ab23b0c9"
G_NETWORK_OBJECT = "d5a57f767e5e46a458fc5d3c628d0cbb"
G_NETWORK_TRANSFORM = "da52bf8bbc1de48cfb221a6ff30f7972"
G_FAUNA_NETWORK_SYNC = "818b214228314119900f4d9860f0762d"
G_STUDIO_EVENT_EMITTER = "9a6610d2e704f1648819acc8d7460285"
G_LIGHT_FAUNA_DATA_SO = "34ff5921d2e247ff9716e66242269962"
G_FAUNA_CONFIG_SO = "c778cfbe4dfc4c5c8401e40c17802311"
G_SPINDLE_MATERIAL = "4f44fa5c7514a2c45b5af7f45bc51acd"
G_CELL_RUNTIME_DATA = "8d4e8398eedc76c4dadb8604f89b9e1b"
G_DANGER_BLOCK = "aad956930fb933f4b82b992b1bc2b773"
G_DYNAMIC_HEALTH_BLOCK = "4a31e01f8b69a584ca245a1687beb4c2"
G_MASS_CRYSTAL = "cccc6ba7985893f43841fccfbb53dc71"
G_SHARK_PREFAB = "a67ba7ddaecf6624ab37cd9f5f2210a6"
G_BLOB_SHARK_CONFIG = "fb217959401746e1b09cac81ffce665b"
# Inside DangerBlock / DynamicHealthBlock (identical ids - one was saved from the other)
BLOCK_GO, BLOCK_TRANSFORM, BLOCK_SCALE_ANIMATOR = 5776304996075792891, 5222650486365209692, 6474806432604849291
DANGER_HEALTH_PRISM, DYNAMIC_HEALTH_PRISM = 5825274168078934928, 6313579230210663873
SHARK_CRYSTAL_INSTANCE = 5473277585626621325

# ── Design numbers (world units at prefab root scale 1; the root itself is BODY_SCALE) ───
BODY_SCALE = 0.6                       # Space / Time; Charge and Mass differ (VARIANTS below)
BILL_CUT_Z = 18.0                      # bill/trunk split, along the body axis (bill is +z)
PART_OF_BONE = {                       # anatomy, from the skin weights (Docs/ECOSYSTEM.md §42)
    "Bone.002": "Sail", "Bone.003": "AnalFin", "Bone.004": "PectoralL", "Bone.005": "PectoralR",
    "Bone": "TailUpper", "Bone.006": "TailLower",
}
PART_ORDER = ["Bill", "Trunk", "Sail", "AnalFin", "PectoralL", "PectoralR", "TailUpper", "TailLower"]
PART_BONE = {"Bill": "Bone.001", "Trunk": "Bone.001", "Sail": "Bone.002", "AnalFin": "Bone.003",
             "PectoralL": "Bone.004", "PectoralR": "Bone.005", "TailUpper": "Bone", "TailLower": "Bone.006"}

# Element identity: the creature's SIZE is the variant's (BaseBodyScale replaces the root scale,
# §40); its strike TIMING is the strike SO's per-element profile.
VARIANTS = {  # element -> (BaseBodyScale, StarvationSeconds)
    "Charge": (0.55, 60.0), "Mass": (0.68, 60.0), "Space": (0.60, 60.0), "Time": (0.60, 60.0),
}
ELEMENT_INDEX = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}

FPS = 25.0                              # the takes are PAL 25 fps (TimeMode 10)
KTIME = 46186158000.0


def guid(name: str) -> str:
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


def fid(name: str) -> int:
    """A stable, positive, in-range Unity fileID for a stable name."""
    h = int(hashlib.md5(f"CosmicShore/fileID/{name}".encode()).hexdigest()[:16], 16)
    return (h & 0x3FFFFFFFFFFFFFFF) | 0x1000000000000000     # 61-bit body, never tiny, < INT64_MAX


def fbx_id(name: str) -> int:
    """FBX object id in the same range the exporter uses (positive int32-ish)."""
    return 100_000_000 + int(hashlib.md5(f"CosmicShore/fbx/{name}".encode()).hexdigest()[:8], 16) % 1_900_000_000


def f(x: float) -> str:
    """Unity-style float: no exponent, no trailing zeros, '-0' avoided."""
    if abs(x) < 1e-9:
        return "0"
    s = f"{x:.7g}"
    if "e" in s:
        s = f"{x:.9f}".rstrip("0").rstrip(".")
    return s


# ═════════════════════════════════════════════════════════════════════════════
# 1. Read the artist's FBX and rebuild it as eight re-centred bone-parts
# ═════════════════════════════════════════════════════════════════════════════

def pv(v):
    return v.decode("utf-8", "replace").split("\x00")[0] if isinstance(v, bytes) else v


def prop70(node, key):
    for p in node.first("Properties70").children:
        if pv(p.props[0][1]) == key:
            return p
    return None


def prop70_vec(node, key):
    p = prop70(node, key)
    return np.array([v for _t, v in p.props][4:7], dtype=float)


def set_prop70_vec(node, key, vec):
    p = prop70(node, key)
    p.props = p.props[:4] + [("D", float(vec[0])), ("D", float(vec[1])), ("D", float(vec[2]))]


def rot_x(d):
    a = math.radians(d); c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_y(d):
    a = math.radians(d); c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rot_z(d):
    a = math.radians(d); c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def euler_xyz(e):
    """FBX eEulerXYZ as a column-vector rotation: Rz·Ry·Rx (validated to 0.0 against the bind
    pose at the swim take's first frame - the STATIC Lcl Rotation is not the rest pose)."""
    return rot_z(e[2]) @ rot_y(e[1]) @ rot_x(e[0])


def trs(t, r, s):
    m = np.eye(4)
    m[:3, :3] = euler_xyz(r) * np.asarray(s, dtype=float)
    m[:3, 3] = t
    return m


def fbx_matrix_to_col(arr16):
    """FBX stores row-major with the translation in the last ROW; we work column-vector."""
    return np.array(arr16, dtype=float).reshape(4, 4).T


def col_to_fbx_matrix(m):
    return [float(x) for x in np.asarray(m).T.reshape(16)]


class SourceModel:
    """Everything the split needs, read once from the artist's file."""

    def __init__(self, path: Path):
        self.nodes, self.version, self.footer = fb.read(str(path))
        self.objects = self._top("Objects")
        self.connections = self._top("Connections")
        self.definitions = self._top("Definitions")
        by_kind = {}
        for c in self.objects.children:
            by_kind.setdefault(c.name, []).append(c)
        self.by_id = {c.props[0][1]: c for c in self.objects.children if c.props and c.props[0][0] == "L"}
        models = {pv(c.props[1][1]): c for c in by_kind["Model"]}
        self.models = models
        self.mesh_model = models["Plane.110"]
        self.armature = models["Armature.024"]
        self.bones = {n: m for n, m in models.items() if pv(m.props[2][1]) == "LimbNode"}
        (self.geometry,) = by_kind["Geometry"]
        self.materials = by_kind["Material"]
        (self.pose,) = by_kind["Pose"]
        self.skin = [d for d in by_kind["Deformer"] if pv(d.props[2][1]) == "Skin"][0]
        self.clusters = {pv(d.props[1][1]): d for d in by_kind["Deformer"] if pv(d.props[2][1]) == "Cluster"}
        self.links = [[pv(v) for _t, v in c.props] for c in self.connections.children]

        # World frame of the mesh node (root-level, so its Lcl values ARE its world matrix).
        self.mesh_world = trs(prop70_vec(self.mesh_model, "Lcl Translation"),
                              prop70_vec(self.mesh_model, "Lcl Rotation"),
                              prop70_vec(self.mesh_model, "Lcl Scaling"))
        self.armature_world = trs(prop70_vec(self.armature, "Lcl Translation"),
                                  prop70_vec(self.armature, "Lcl Rotation"),
                                  prop70_vec(self.armature, "Lcl Scaling"))
        self.bone_world = {}     # bind pose, from the clusters - the only trustworthy source
        for name, cl in self.clusters.items():
            self.bone_world[name] = fbx_matrix_to_col(cl.first("TransformLink").props[0][1])

        # Geometry
        verts = np.array(self.geometry.first("Vertices").props[0][1], dtype=float).reshape(-1, 3)
        self.local_verts = verts
        pvi = self.geometry.first("PolygonVertexIndex").props[0][1]
        polys, cur = [], []
        for i in pvi:
            if i < 0:
                cur.append(-i - 1); polys.append(cur); cur = []
            else:
                cur.append(i)
        assert not cur, "PolygonVertexIndex does not end on a polygon terminator"
        self.polys = polys
        ln = self.geometry.first("LayerElementNormal")
        assert pv(ln.first("MappingInformationType").props[0][1]) == "ByPolygonVertex"
        assert pv(ln.first("ReferenceInformationType").props[0][1]) == "IndexToDirect"
        self.normals = np.array(ln.first("Normals").props[0][1], dtype=float).reshape(-1, 3)
        self.normal_index = list(ln.first("NormalsIndex").props[0][1])
        luv = self.geometry.first("LayerElementUV")
        assert pv(luv.first("ReferenceInformationType").props[0][1]) == "IndexToDirect"
        self.uv = list(luv.first("UV").props[0][1])
        self.uv_index = list(luv.first("UVIndex").props[0][1])
        assert len(self.normal_index) == len(pvi) == len(self.uv_index)

        # World-space vertices and the skin weights per vertex
        hv = np.hstack([verts, np.ones((len(verts), 1))])
        self.world_verts = (self.mesh_world @ hv.T).T[:, :3]
        self.weights = {}        # vertex -> {bone: w}
        for name, cl in self.clusters.items():
            idx = cl.first("Indexes").props[0][1]
            w = cl.first("Weights").props[0][1]
            for i, ww in zip(idx, w):
                self.weights.setdefault(i, {})[name] = float(ww)
        self.dominant = [max(self.weights[i].items(), key=lambda kv: kv[1])[0] for i in range(len(verts))]

        # Which cluster-matrix convention the exporter used - proved on the original, then
        # reused for the parts so Unity sees exactly the same skinning it saw before.
        tl = self.bone_world["Bone.001"]
        tr = fbx_matrix_to_col(self.clusters["Bone.001"].first("Transform").props[0][1])
        cands = {
            "inv(TL)@MW": np.linalg.inv(tl) @ self.mesh_world,
            "MW@inv(TL)": self.mesh_world @ np.linalg.inv(tl),
        }
        errs = {k: float(np.abs(v - tr).max()) for k, v in cands.items()}
        self.cluster_convention = min(errs, key=errs.get)
        assert errs[self.cluster_convention] < 1e-3, f"cluster Transform convention unproven: {errs}"

        # Animation: stacks and their spans (frames), and the Armature translation curves
        self.stacks = {pv(c.props[1][1]).split("|")[1]: c for c in by_kind["AnimationStack"]}
        self.take_frames = {}
        for take, stack in self.stacks.items():
            stop = prop70(stack, "LocalStop").props[-1][1]
            self.take_frames[take] = int(round(stop / KTIME * FPS))
        curve_nodes = {c.props[0][1] for c in by_kind["AnimationCurveNode"]}
        curves = {c.props[0][1]: c for c in by_kind["AnimationCurve"]}
        arm_id = self.armature.props[0][1]
        self.armature_t_curves = []
        for l in self.links:
            if l[0] == "OP" and l[1] in curve_nodes and l[2] == arm_id and l[3] == "Lcl Translation":
                for ll in self.links:
                    if ll[0] == "OP" and ll[2] == l[1] and ll[1] in curves:
                        self.armature_t_curves.append((ll[3], curves[ll[1]]))

    def _top(self, name):
        return [n for n in self.nodes if n.name == name][0]


def part_of_vertex(src: SourceModel, i: int, centroid: np.ndarray) -> str:
    bone = src.dominant[i]
    if bone == "Bone.001":
        return "Bill" if (src.world_verts[i] - centroid)[2] > BILL_CUT_Z else "Trunk"
    return PART_OF_BONE[bone]


class Part:
    __slots__ = ("name", "bone", "polys", "verts", "remap", "centroid", "world_verts",
                 "model_id", "geo_id", "skin_id", "cluster_ids", "clusters")

    def __init__(self, name):
        self.name = name
        self.bone = PART_BONE[name]
        self.polys = []          # (original polygon index, [original vertex ids])
        self.verts = []          # original vertex ids, in first-use order
        self.remap = {}
        self.clusters = {}       # bone -> [(local index, weight)]


def split_parts(src: SourceModel):
    """Assign polygons to parts by majority of their vertices' dominant bones."""
    centroid = src.world_verts.mean(axis=0)
    vpart = [part_of_vertex(src, i, centroid) for i in range(len(src.world_verts))]
    parts = {n: Part(n) for n in PART_ORDER}
    for pi, poly in enumerate(src.polys):
        votes = Counter(vpart[i] for i in poly)
        top = votes.most_common()
        best = [n for n, c in top if c == top[0][1]]
        name = vpart[poly[0]] if vpart[poly[0]] in best else best[0]
        part = parts[name]
        part.polys.append((pi, poly))
        for i in poly:
            if i not in part.remap:
                part.remap[i] = len(part.verts)
                part.verts.append(i)
    for part in parts.values():
        assert part.polys, f"part {part.name} received no polygons"
        part.world_verts = src.world_verts[part.verts]
        part.centroid = part.world_verts.mean(axis=0)
        for bone, cl in src.clusters.items():
            idx = cl.first("Indexes").props[0][1]
            w = cl.first("Weights").props[0][1]
            entries = [(part.remap[i], float(ww)) for i, ww in zip(idx, w) if i in part.remap]
            if entries:
                part.clusters[bone] = entries
        part.model_id = fbx_id(f"Model/{part.name}")
        part.geo_id = fbx_id(f"Geometry/{part.name}")
        part.skin_id = fbx_id(f"Skin/{part.name}")
        part.cluster_ids = {bone: fbx_id(f"Cluster/{part.name}/{bone}") for bone in part.clusters}
    # Conservation: every polygon lands in exactly one part, every vertex is used.
    assert sum(len(p.polys) for p in parts.values()) == len(src.polys)
    assert set().union(*(set(p.verts) for p in parts.values())) == set(range(len(src.world_verts)))
    return parts, centroid


def build_parts_fbx(src: SourceModel, parts: dict, centroid: np.ndarray):
    """A new node tree: the artist's file minus its one mesh, plus eight parts, re-centred."""
    nodes = copy.deepcopy(src.nodes)
    objects = [n for n in nodes if n.name == "Objects"][0]
    connections = [n for n in nodes if n.name == "Connections"][0]
    definitions = [n for n in nodes if n.name == "Definitions"][0]

    removed = {src.mesh_model.props[0][1], src.geometry.props[0][1], src.skin.props[0][1]}
    removed |= {cl.props[0][1] for cl in src.clusters.values()}
    keep_material = src.materials[0] if pv(src.materials[0].props[1][1]) == "Material.340" else src.materials[1]
    for m in src.materials:
        if m is not keep_material:
            removed.add(m.props[0][1])
    objects.children = [c for c in objects.children
                        if not (c.props and c.props[0][0] == "L" and c.props[0][1] in removed)]
    connections.children = [c for c in connections.children
                            if not ({c.props[1][1], c.props[2][1]} & removed)]

    shift = -centroid
    ids = {c.props[0][1]: c for c in objects.children if c.props and c.props[0][0] == "L"}

    # Re-centre: the armature, its bind matrices, the pose, and the constant root curves.
    arm = ids[src.armature.props[0][1]]
    set_prop70_vec(arm, "Lcl Translation", prop70_vec(arm, "Lcl Translation") + shift)
    for _axis, curve in src.armature_t_curves:
        cnode = ids[curve.props[0][1]]
        kv = cnode.first("KeyValueFloat")
        vals = kv.props[0][1]
        axis_i = "XYZ".index(_axis[-1])
        assert max(vals) - min(vals) < 1e-4, "Armature translation is animated; re-centring needs a curve shift"
        kv.props = [(kv.props[0][0], [float(v + shift[axis_i]) for v in vals])]
        d = cnode.first("Default")
        if d:
            d.props = [(d.props[0][0], float(d.props[0][1] + shift[axis_i]))]

    def shifted(arr16):
        m = fbx_matrix_to_col(arr16)
        m[:3, 3] += shift
        return col_to_fbx_matrix(m)

    pose = ids[src.pose.props[0][1]]
    mesh_pose_template = None
    kept_pose_nodes = []
    for pn in pose.find("PoseNode"):
        node_id = pn.first("Node").props[0][1]
        if node_id == src.mesh_model.props[0][1]:
            mesh_pose_template = pn
            continue
        mat = pn.first("Matrix")
        mat.props = [(mat.props[0][0], shifted(mat.props[0][1]))]
        kept_pose_nodes.append(pn)
    assert mesh_pose_template is not None

    # Templates cloned from the artist's own nodes so every serializer detail is theirs.
    model_t = src.mesh_model
    geo_t = src.geometry
    skin_t = src.skin
    cluster_t = src.clusters["Bone.001"]

    mesh_m3 = src.mesh_world[:3, :3]
    normal_xf = np.linalg.inv(mesh_m3).T
    material_id = keep_material.props[0][1]
    new_objects, new_conns, pose_nodes = [], [], []

    def conn(child, parent, kind=b"OO"):
        return fb.Node("C", [("S", kind), ("L", child), ("L", parent)])

    for name in PART_ORDER:
        part = parts[name]
        model_name = f"Swordfish_{name}"
        part_world = np.eye(4)
        part_world[:3, 3] = part.centroid + shift

        model = copy.deepcopy(model_t)
        model.props = [("L", part.model_id), ("S", f"{model_name}\x00\x01Model".encode()), ("S", b"Mesh")]
        set_prop70_vec(model, "Lcl Translation", part_world[:3, 3])
        set_prop70_vec(model, "Lcl Rotation", (0.0, 0.0, 0.0))
        set_prop70_vec(model, "Lcl Scaling", (1.0, 1.0, 1.0))
        new_objects.append(model)

        # Geometry in the part's world-aligned local frame.
        local = part.world_verts - part.centroid
        pvi, normals, uvs = [], [], []
        for pi, poly in part.polys:
            # polygon-vertex slots of this polygon in the ORIGINAL flat arrays
            start = sum(len(p) for p in src.polys[:pi])
            for k, vi in enumerate(poly):
                slot = start + k
                n = normal_xf @ src.normals[src.normal_index[slot]]
                n /= max(np.linalg.norm(n), 1e-9)
                normals.extend(float(x) for x in n)
                uvs.append(src.uv_index[slot])
                ri = part.remap[vi]
                pvi.append(-(ri + 1) if k == len(poly) - 1 else ri)
        geo = copy.deepcopy(geo_t)
        geo.props = [("L", part.geo_id), ("S", f"{model_name}\x00\x01Geometry".encode()), ("S", b"Mesh")]
        geo.drop("Edges")
        geo.first("Vertices").props = [("d", [float(x) for x in local.reshape(-1)])]
        geo.first("PolygonVertexIndex").props = [("i", pvi)]
        ln = geo.first("LayerElementNormal")
        ln.first("ReferenceInformationType").props = [("S", b"Direct")]
        ln.first("Normals").props = [("d", normals)]
        ln.drop("NormalsIndex")
        luv = geo.first("LayerElementUV")
        luv.first("UV").props = [("d", list(src.uv))]
        luv.first("UVIndex").props = [("i", uvs)]
        lm = geo.first("LayerElementMaterial")
        lm.first("MappingInformationType").props = [("S", b"AllSame")]
        lm.first("ReferenceInformationType").props = [("S", b"IndexToDirect")]
        lm.first("Materials").props = [("i", [0])]
        new_objects.append(geo)

        skin = copy.deepcopy(skin_t)
        skin.props = [("L", part.skin_id), ("S", f"{model_name}\x00\x01Deformer".encode()), ("S", b"Skin")]
        new_objects.append(skin)

        for bone, entries in part.clusters.items():
            cl = copy.deepcopy(cluster_t)
            cl.props = [("L", part.cluster_ids[bone]), ("S", f"{bone}\x00\x01SubDeformer".encode()), ("S", b"Cluster")]
            cl.first("Indexes").props = [("i", [e[0] for e in entries])]
            cl.first("Weights").props = [("d", [e[1] for e in entries])]
            tl = src.bone_world[bone].copy()
            tl[:3, 3] += shift
            if src.cluster_convention == "inv(TL)@MW":
                transform = np.linalg.inv(tl) @ part_world
            else:
                transform = part_world @ np.linalg.inv(tl)
            cl.first("Transform").props = [("d", col_to_fbx_matrix(transform))]
            cl.first("TransformLink").props = [("d", col_to_fbx_matrix(tl))]
            am = src.armature_world.copy()
            am[:3, 3] += shift
            cl.first("TransformAssociateModel").props = [("d", col_to_fbx_matrix(am))]
            new_objects.append(cl)
            new_conns.append(conn(part.cluster_ids[bone], part.skin_id))
            new_conns.append(conn(src.bones[bone].props[0][1], part.cluster_ids[bone]))

        new_conns.append(conn(part.model_id, 0))
        new_conns.append(conn(part.geo_id, part.model_id))
        new_conns.append(conn(part.skin_id, part.geo_id))
        new_conns.append(conn(material_id, part.model_id))

        pn = copy.deepcopy(mesh_pose_template)
        pn.first("Node").props = [("L", part.model_id)]
        pn.first("Matrix").props = [("d", col_to_fbx_matrix(part_world))]
        pose_nodes.append(pn)

    pose.children = [c for c in pose.children if c.name != "PoseNode"] + kept_pose_nodes + pose_nodes
    pose.first("NbPoseNodes").props = [("I", len(kept_pose_nodes) + len(pose_nodes))]

    # Objects keep the exporter's grouping: parts right after the armature's node attribute.
    insert_at = next(i for i, c in enumerate(objects.children) if c.name == "Model")
    objects.children[insert_at:insert_at] = new_objects
    connections.children.extend(new_conns)

    # Definitions: counts per type (the FBX SDK reads them as hints; keep them honest).
    counts = Counter(c.name for c in objects.children)
    total = 1  # GlobalSettings
    for ot in definitions.find("ObjectType"):
        kind = pv(ot.props[0][1])
        if kind == "GlobalSettings":
            continue
        n = counts.get(kind, 0)
        ot.first("Count").props = [("I", n)]
        total += n
    definitions.find("ObjectType")[:] = [ot for ot in definitions.find("ObjectType")
                                          if pv(ot.props[0][1]) == "GlobalSettings" or counts.get(pv(ot.props[0][1]), 0) > 0]
    definitions.children = [c for c in definitions.children
                            if c.name != "ObjectType" or pv(c.props[0][1]) == "GlobalSettings" or counts.get(pv(c.props[0][1]), 0) > 0]
    definitions.first("Count").props = [("I", total)]
    return nodes


# ═════════════════════════════════════════════════════════════════════════════
# 2. Prism placement - from the geometry of each part, in that part's bone frame
# ═════════════════════════════════════════════════════════════════════════════

MIRROR = np.diag([-1.0, 1.0, 1.0])     # FBX right-handed -> Unity left-handed (validated on the shark)


def quat_from_matrix(m):
    """(x, y, z, w) from a proper rotation matrix (Shepperd)."""
    t = np.trace(m)
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        return ((m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, 0.25 * s)
    if m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        return (0.25 * s, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s)
    if m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        return ((m[0, 1] + m[1, 0]) / s, 0.25 * s, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s)
    s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
    return ((m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, 0.25 * s, (m[1, 0] - m[0, 1]) / s)


def axis_rotation(axis, deg):
    axis = np.asarray(axis, dtype=float); axis /= np.linalg.norm(axis)
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    x, y, z = axis
    return np.array([
        [c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
        [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
        [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)]])


def frame(x, y, z):
    """Column frame from three (near-)orthonormal axes; z is trusted, x re-derived."""
    z = np.asarray(z, float); z /= np.linalg.norm(z)
    y = np.asarray(y, float); y -= z * (y @ z); y /= np.linalg.norm(y)
    x = np.cross(y, z)
    return np.column_stack([x, y, z])


class PrismSpec:
    __slots__ = ("name", "part", "kind", "rotation", "position", "size")

    def __init__(self, name, part, kind, rotation, position, size):
        self.name, self.part, self.kind = name, part, kind
        self.rotation, self.position, self.size = rotation, np.asarray(position, float), size


def fin_plate(name, part: Part, F, thickness, span_fraction=0.70, chord_fraction=0.60):
    """One blade plate along a fin's principal axes: local z = span (base -> tip), y = chord."""
    P = F[part.verts]
    c = P.mean(axis=0)
    _u, _s, vt = np.linalg.svd(P - c, full_matrices=False)
    span, chord = vt[0], vt[1]
    # base -> tip: span points away from the body axis line (x = y = 0)
    radial = np.array([c[0], c[1], 0.0])
    if span @ radial < 0:
        span = -span
    R = frame(np.cross(chord, span), chord, span)
    ps = (P - c) @ R[:, 2]
    pc = (P - c) @ R[:, 1]
    span_len = (ps.max() - ps.min()) * span_fraction
    chord_len = (pc.max() - pc.min()) * chord_fraction
    centre = c + R[:, 2] * (ps.min() + ps.max()) * 0.5 + R[:, 1] * (pc.min() + pc.max()) * 0.5
    return PrismSpec(name, part.name, "dynamic", R, centre, (thickness, chord_len, span_len))


def design_prisms(src: SourceModel, parts: dict, centroid: np.ndarray):
    F = src.world_verts - centroid          # body frame: +z bill, +y dorsal, x athwart (FBX-handed)
    specs = []

    # --- the sword: three tapering danger needles, end to end, on the bill's axis ---------
    bill = F[parts["Bill"].verts]
    z = bill[:, 2]
    tip = float(z.max())
    base = BILL_CUT_Z + 0.5
    # the bill's radius profile drives each needle's thickness (inside the mesh near the base,
    # the point breaking the surface at the tip)
    def radius_at(zz):
        sel = np.abs(z - zz) < 2.5
        return float(np.linalg.norm(bill[sel][:, :2], axis=1).max()) if sel.any() else 0.5
    cuts = [base, base + (tip - base) * 0.36, base + (tip - base) * 0.70, tip + 0.4]
    # A monotone taper, anchored on the measured bill: the base needle sits inside the
    # mesh (its half-diagonal under the local radius), the point breaks the surface at the
    # tip. The sparse ring topology of the bill makes a per-needle radius read jump around,
    # so the taper is a line between the two measured ends rather than three samples.
    r_base, r_tip = radius_at(cuts[0] + 2.0), max(0.35, radius_at(tip - 2.0))
    def thickness_at(zz):
        t = (zz - cuts[0]) / (tip - cuts[0])
        return max(0.75, (1.0 - t) * 0.55 * r_base + t * 1.5 * r_tip)
    thick = [thickness_at((cuts[k] + cuts[k + 1]) / 2) for k in range(3)]
    assert thick[0] > thick[1] > thick[2], f"bill needles must taper: {thick}"
    gap = 0.5
    for k in range(3):
        z0, z1 = cuts[k] + (gap / 2 if k else 0), cuts[k + 1] - (gap / 2 if k < 2 else 0)
        R = rot_z(30.0 * k)                       # stepped facets: the drill twinkles as it spins
        specs.append(PrismSpec(f"Needle{k + 1}", "Bill", "danger", R,
                               (0.0, 0.0, (z0 + z1) / 2), (thick[k], thick[k], z1 - z0)))

    # --- the drill: three flutes around the trunk, 120 degrees apart -----------------------
    trunk = F[parts["Trunk"].verts]
    tz = trunk[:, 2]
    z_lo, z_hi = float(tz.min()) + 6.0, BILL_CUT_Z - 10.0      # peduncle .. head
    length = z_hi - z_lo
    mid = (z_lo + z_hi) / 2
    body_r = float(np.linalg.norm(trunk[np.abs(tz - mid) < 4][:, :2], axis=1).max())
    flute_h = 0.40 * body_r                                    # radial height of a flute
    flute_r = 0.46 * body_r                                    # its centre's radius: outer edge ~0.66 r
    for k in range(3):
        theta = math.radians(90.0 + 120.0 * k)
        radial = np.array([math.cos(theta), math.sin(theta), 0.0])
        tangent = np.array([-math.sin(theta), math.cos(theta), 0.0])
        R = frame(tangent, radial, (0.0, 0.0, 1.0))
        R = axis_rotation(radial, 10.0) @ R          # screw skew - reads as a thread once it spins
        R = axis_rotation(R[:, 0], -5.0) @ R         # nose end tucked toward the axis (fusiform body)
        specs.append(PrismSpec(f"Flute{k + 1}", "Trunk", "dynamic", R,
                               radial * flute_r + np.array([0.0, 0.0, mid]), (1.6, flute_h, length)))

    # --- one blade per fin and tail lobe ---------------------------------------------------
    specs.append(fin_plate("SailBlade", parts["Sail"], F, 1.6))
    specs.append(fin_plate("AnalBlade", parts["AnalFin"], F, 1.6))
    specs.append(fin_plate("PectoralLBlade", parts["PectoralL"], F, 1.1, 0.68, 0.62))
    specs.append(fin_plate("PectoralRBlade", parts["PectoralR"], F, 1.1, 0.68, 0.62))
    specs.append(fin_plate("TailUpperBlade", parts["TailUpper"], F, 1.8, 0.70, 0.58))
    specs.append(fin_plate("TailLowerBlade", parts["TailLower"], F, 1.8, 0.70, 0.58))
    assert len(specs) == 12
    return specs


def overlap_check(specs):
    """No two prisms may overlap: OBB separating-axis test on every pair."""
    def corners(s):
        h = np.array(s.size) / 2
        pts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    pts.append(s.position + s.rotation @ (h * np.array([sx, sy, sz])))
        return np.array(pts)
    problems = []
    for i in range(len(specs)):
        for j in range(i + 1, len(specs)):
            a, b = specs[i], specs[j]
            axes = [a.rotation[:, k] for k in range(3)] + [b.rotation[:, k] for k in range(3)]
            for k in range(3):
                for l in range(3):
                    cr = np.cross(a.rotation[:, k], b.rotation[:, l])
                    if np.linalg.norm(cr) > 1e-6:
                        axes.append(cr / np.linalg.norm(cr))
            ca, cb = corners(a), corners(b)
            separated = False
            for ax in axes:
                pa, pb = ca @ ax, cb @ ax
                if pa.max() < pb.min() or pb.max() < pa.min():
                    separated = True
                    break
            if not separated:
                problems.append(f"{a.name} overlaps {b.name}")
    return problems


class Mount:
    __slots__ = ("part", "bone", "local_pos", "scale")


def mount_and_prism_transforms(src: SourceModel, parts: dict, centroid: np.ndarray, specs):
    """Bone-local mounts (one per part) and mount-local prism poses, in Unity handedness.

    prism_local(Unity) = Mx · (inv(TransformLink_bone) · P_fbxworld) · Mx  - the rule validated
    on the shark's bone-parented blocks (Docs/ECOSYSTEM.md §42).  The mount sits at the part's
    centroid, rotation identity in the bone frame, scaled 1/armature so prism scales below it
    are plain world units at root scale 1.
    """
    armature_scale = float(np.linalg.norm(src.armature_world[:3, 0]))
    mounts = {}
    for name in PART_ORDER:
        part = parts[name]
        tl = src.bone_world[part.bone]
        p = np.linalg.inv(tl) @ np.append(part.centroid, 1.0)
        m = Mount()
        m.part, m.bone = name, part.bone
        m.local_pos = MIRROR @ p[:3]
        m.scale = 1.0 / armature_scale
        mounts[name] = m

    placed = []
    for s in specs:
        part = parts[s.part]
        tl = src.bone_world[part.bone]
        r_tl = tl[:3, :3] / armature_scale
        assert abs(np.linalg.det(r_tl) - 1.0) < 1e-3
        world_pos = s.position + centroid                  # back into the artist's world
        p_bl = np.linalg.inv(tl) @ np.append(world_pos, 1.0)
        r_bl = r_tl.T @ s.rotation
        mount = mounts[s.part]
        mount_bl = np.linalg.inv(MIRROR) @ mount.local_pos  # undo the mirror for the subtraction
        p_ml = (p_bl[:3] - mount_bl) / mount.scale
        pos_u = MIRROR @ p_ml
        rot_u = MIRROR @ r_bl @ MIRROR
        assert abs(np.linalg.det(rot_u) - 1.0) < 1e-4
        placed.append((s, pos_u, quat_from_matrix(rot_u)))
    return mounts, placed


# ═════════════════════════════════════════════════════════════════════════════
# 3. Serialized assets
# ═════════════════════════════════════════════════════════════════════════════

G_PARTS_FBX = guid("model/SwordFish_A_Parts.fbx")
G_CONTROLLER = guid("controller/SwordFish_A_Parts")
G_PREFAB = guid("prefab/SwordfishFauna")
G_DATA_SO = guid("so/SwordfishFaunaDataSO")
G_STRIKE_SO = guid("so/SwordfishStrikeData")
G_BLOB_CONFIG = guid("config/Blob Swordfish Fauna Config Data")
G_VARIANT = {e: guid(f"lifeform/Swordfish Fauna {e}") for e in VARIANTS}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPTS}

# Pinned FBX object ids (class, name) -> id.  '//RootNode' is the importer's key for the root.
FBX_BONES = ["Armature.024", "Bone", "Bone.001", "Bone.002", "Bone.003", "Bone.004", "Bone.005", "Bone.006"]


ID_OVERRIDES_JSON = ROOT / "Tools/Build/swordfish_fbx_ids.json"


def _load_id_overrides() -> dict:
    """Ids Unity ACTUALLY assigned, recorded by FrogletTools > Ecology > Swordfish Flagship's
    Rebind when the pinned table was not honoured. Absent (the expected case) = pin our own."""
    if not ID_OVERRIDES_JSON.exists():
        return {}
    return {m.group(1): int(m.group(2))
            for m in re.finditer(r'"([^"]+)"\s*:\s*(-?\d+)', ID_OVERRIDES_JSON.read_text(encoding="utf-8"))}


_ID_OVERRIDES = _load_id_overrides()


def pinned(cls: int, name: str) -> int:
    return _ID_OVERRIDES.get(f"{cls}/{name}", fid(f"fbx/{cls}/{name}"))


def fbx_id_table(parts: dict):
    rows = [(1, "//RootNode"), (4, "//RootNode"), (95, "//RootNode")]
    for b in FBX_BONES:
        rows += [(1, b), (4, b)]
    for name in PART_ORDER:
        rows += [(1, f"Swordfish_{name}"), (4, f"Swordfish_{name}"), (137, f"Swordfish_{name}")]
    return [(cls, nm, pinned(cls, nm)) for cls, nm in rows]


CLIPS = [  # (clip name, take, first frame, last frame (None = take end), loop)
    ("SwrdFsh_Swim", "SwrdFsh_Move", 0, None, True),
    ("SwrdFsh_Tuck", "SwrdFsh_Charge", 0, 44, False),
    ("SwrdFsh_ChargeHold", "SwrdFsh_Charge", 44, 187, True),
    ("SwrdFsh_Flare", "SwrdFsh_Charge", 190, None, False),
]


def clip_id(name: str) -> int:
    h = int(hashlib.md5(f"CosmicShore/clip/{name}".encode()).hexdigest()[:16], 16)
    return h - (1 << 63) if h >= (1 << 63) else h      # signed, like Unity's own clip ids


def render_parts_meta(src: SourceModel, parts: dict) -> str:
    text = SRC_FBX_META.read_text(encoding="utf-8")
    text = re.sub(r"^guid: \w+$", f"guid: {G_PARTS_FBX}", text, count=1, flags=re.M)

    rows = ["  internalIDToNameTable:"]
    for cls, nm, i in fbx_id_table(parts):
        rows += ["  - first:", f"      {cls}: {i}", f"    second: {nm}"]
    for name, _take, _a, _b, _loop in CLIPS:
        rows += ["  - first:", f"      74: {clip_id(name)}", f"    second: {name}"]
    text = text.replace("  internalIDToNameTable: []", "\n".join(rows), 1)

    ext = ["  externalObjects:",
           "  - first:",
           "      type: UnityEngine:Material",
           "      assembly: UnityEngine.CoreModule",
           "      name: Material.340",
           f"    second: {{fileID: 2100000, guid: {G_SPINDLE_MATERIAL}, type: 2}}"]
    text = text.replace("  externalObjects: {}", "\n".join(ext), 1)

    clip_template = re.search(r"    - serializedVersion: 16\n.*?additiveReferencePoseFrame: 0\n", text, re.S).group(0)
    clips = []
    for name, take, first, last, loop in CLIPS:
        last = src.take_frames[take] if last is None else last
        block = clip_template
        block = re.sub(r"      name: .*\n", f"      name: {name}\n", block, count=1)
        block = re.sub(r"      takeName: .*\n", f"      takeName: Armature.024|{take}\n", block, count=1)
        block = re.sub(r"      internalID: .*\n", f"      internalID: {clip_id(name)}\n", block, count=1)
        block = re.sub(r"      firstFrame: .*\n", f"      firstFrame: {first}\n", block, count=1)
        block = re.sub(r"      lastFrame: .*\n", f"      lastFrame: {last}\n", block, count=1)
        block = re.sub(r"      loopTime: .*\n", f"      loopTime: {1 if loop else 0}\n", block, count=1)
        block = re.sub(r"      loopBlend: .*\n", f"      loopBlend: {1 if loop else 0}\n", block, count=1)
        clips.append(block)
    text = re.sub(r"    clipAnimations:\n(    - serializedVersion: 16\n.*?additiveReferencePoseFrame: 0\n)+",
                  "    clipAnimations:\n" + "".join(clips), text, count=1, flags=re.S)
    return text


def render_controller() -> str:
    states = [  # name, clip, speed
        ("Swim", "SwrdFsh_Swim", 1.0), ("Pursue", "SwrdFsh_Swim", 1.6),
        ("Tuck", "SwrdFsh_Tuck", 1.6), ("ChargeHold", "SwrdFsh_ChargeHold", 1.0),
        ("Flare", "SwrdFsh_Flare", 1.0),
    ]
    sid = {n: fid(f"animator/state/{n}") for n, _c, _s in states}
    transitions = [  # name, from, to, conditions [(mode, param)], hasExit, exitTime, duration
        ("SwimToPursue", "Swim", "Pursue", [(1, "Pursuing")], 0, 0.0, 0.25),
        ("PursueToSwim", "Pursue", "Swim", [(2, "Pursuing")], 0, 0.0, 0.35),
        ("SwimToTuck", "Swim", "Tuck", [(1, "Charging")], 0, 0.0, 0.12),
        ("PursueToTuck", "Pursue", "Tuck", [(1, "Charging")], 0, 0.0, 0.12),
        ("TuckToHold", "Tuck", "ChargeHold", [], 1, 1.0, 0.08),
        ("TuckToFlare", "Tuck", "Flare", [(2, "Charging")], 0, 0.0, 0.12),
        ("HoldToFlare", "ChargeHold", "Flare", [(2, "Charging")], 0, 0.0, 0.1),
        ("FlareToSwim", "Flare", "Swim", [], 1, 0.95, 0.3),
    ]
    tid = {n: fid(f"animator/transition/{n}") for n, *_ in transitions}
    sm = fid("animator/statemachine")
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
    for name, src_state, dst, conds, has_exit, exit_time, duration in transitions:
        out += [f"--- !u!1101 &{tid[name]}", "AnimatorStateTransition:", "  m_ObjectHideFlags: 1",
                "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}", f"  m_Name: {name}", "  m_Conditions:" + ("" if conds else " []")]
        for mode, param in conds:
            out += [f"  - m_ConditionMode: {mode}", f"    m_ConditionEvent: {param}", "    m_EventTreshold: 0"]
        out += ["  m_DstStateMachine: {fileID: 0}", f"  m_DstState: {{fileID: {sid[dst]}}}", "  m_Solo: 0",
                "  m_Mute: 0", "  m_IsExit: 0", "  serializedVersion: 3", f"  m_TransitionDuration: {f(duration)}",
                "  m_TransitionOffset: 0", f"  m_ExitTime: {f(exit_time)}", f"  m_HasExitTime: {has_exit}",
                "  m_HasFixedDuration: 1", "  m_InterruptionSource: 0", "  m_OrderedInterruption: 1",
                "  m_CanTransitionToSelf: 1"]
    for i, (name, clip, speed) in enumerate(states):
        outs = [t for t in transitions if t[1] == name]
        out += [f"--- !u!1102 &{sid[name]}", "AnimatorState:", "  serializedVersion: 6", "  m_ObjectHideFlags: 1",
                "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}", f"  m_Name: {name}", f"  m_Speed: {f(speed)}", "  m_CycleOffset: 0",
                "  m_Transitions:" + ("" if outs else " []")]
        for t in outs:
            out += [f"  - {{fileID: {tid[t[0]]}}}"]
        out += ["  m_StateMachineBehaviours: []", "  m_Position: {x: 50, y: 50, z: 0}", "  m_IKOnFeet: 0",
                "  m_WriteDefaultValues: 1", "  m_Mirror: 0", "  m_SpeedParameterActive: 0",
                "  m_MirrorParameterActive: 0", "  m_CycleOffsetParameterActive: 0", "  m_TimeParameterActive: 0",
                f"  m_Motion: {{fileID: {clip_id(clip)}, guid: {G_PARTS_FBX}, type: 3}}", "  m_Tag: ",
                "  m_SpeedParameter: ", "  m_MirrorParameter: ", "  m_CycleOffsetParameter: ", "  m_TimeParameter: "]
    out += ["--- !u!91 &9100000", "AnimatorController:", "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", "  m_Name: SwordFish_A_Parts", "  serializedVersion: 5",
            "  m_AnimatorParameters:"]
    for p in ("Pursuing", "Charging"):
        out += [f"  - m_Name: {p}", "    m_Type: 4", "    m_DefaultFloat: 0", "    m_DefaultInt: 0",
                "    m_DefaultBool: 0", "    m_Controller: {fileID: 9100000}"]
    out += ["  m_AnimatorLayers:", "  - serializedVersion: 5", "    m_Name: Base Layer",
            f"    m_StateMachine: {{fileID: {sm}}}", "    m_Mask: {fileID: 0}", "    m_Motions: []",
            "    m_Behaviours: []", "    m_BlendingMode: 0", "    m_SyncedLayerIndex: -1", "    m_DefaultWeight: 0",
            "    m_IKPass: 0", "    m_SyncedLayerAffectsTiming: 0", "    m_Controller: {fileID: 9100000}",
            f"--- !u!1107 &{sm}", "AnimatorStateMachine:", "  serializedVersion: 6", "  m_ObjectHideFlags: 1",
            "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}", "  m_Name: Base Layer", "  m_ChildStates:"]
    for i, (name, _c, _s) in enumerate(states):
        out += ["  - serializedVersion: 1", f"    m_State: {{fileID: {sid[name]}}}",
                f"    m_Position: {{x: 320, y: {40 + 90 * i}, z: 0}}"]
    out += ["  m_ChildStateMachines: []", "  m_AnyStateTransitions: []", "  m_EntryTransitions: []",
            "  m_StateMachineTransitions: {}", "  m_StateMachineBehaviours: []",
            "  m_AnyStatePosition: {x: 50, y: 20, z: 0}", "  m_EntryPosition: {x: 50, y: 120, z: 0}",
            "  m_ExitPosition: {x: 800, y: 120, z: 0}", "  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}",
            f"  m_DefaultState: {{fileID: {sid['Swim']}}}"]
    return "\n".join(out) + "\n"


# ── Prefab ─────────────────────────────────────────────────────────────────────

def mod(target, gid, path, value="", ref="{fileID: 0}"):
    return (f"    - target: {{fileID: {target}, guid: {gid},\n        type: 3}}\n"
            f"      propertyPath: {path}\n      value: {value}\n      objectReference: {ref}\n")


def transform_mods(target, gid, pos, rot, scale):
    out = ""
    for ax, v in zip("xyz", scale):
        out += mod(target, gid, f"m_LocalScale.{ax}", f(v))
    for ax, v in zip("xyz", pos):
        out += mod(target, gid, f"m_LocalPosition.{ax}", f(v))
    for ax, v in zip("wxyz", (rot[3], rot[0], rot[1], rot[2])):
        out += mod(target, gid, f"m_LocalRotation.{ax}", f(v))
    for ax in "xyz":
        out += mod(target, gid, f"m_LocalEulerAnglesHint.{ax}", "0")
    return out


def render_prefab(src: SourceModel, parts: dict, mounts, placed, donor_text: str):
    root_go, root_tf = fid("prefab/root/go"), fid("prefab/root/transform")
    c_fauna = fid("prefab/root/SwordfishFauna")
    c_emitter = fid("prefab/root/StudioEventEmitter")
    c_driver = fid("prefab/root/SwordfishChargeDriver")
    c_netobj = fid("prefab/root/NetworkObject")
    c_nettf = fid("prefab/root/NetworkTransform")
    c_netsync = fid("prefab/root/FaunaNetworkSync")
    fbx_inst = fid("prefab/fbx/instance")
    crystal_inst = fid("prefab/crystal/instance")

    def stripped(cls, src_id):
        return src_id ^ fbx_inst

    fbx_root_tf = stripped(4, pinned(4, "//RootNode"))
    bone_tf = {b: stripped(4, pinned(4, b)) for b in FBX_BONES}
    part_go = {n: stripped(1, pinned(1, f"Swordfish_{n}")) for n in PART_ORDER}
    part_smr = {n: stripped(137, pinned(137, f"Swordfish_{n}")) for n in PART_ORDER}
    spindle_id = {n: fid(f"prefab/spindle/{n}") for n in PART_ORDER}
    mount_go = {n: fid(f"prefab/mount/{n}/go") for n in PART_ORDER}
    mount_tf = {n: fid(f"prefab/mount/{n}/transform") for n in PART_ORDER}
    crystal_tf = 6588436222817790493 ^ crystal_inst

    Y = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]

    def mono_header(i, go, script_guid, enabled=1):
        return [f"--- !u!114 &{i}", "MonoBehaviour:", "  m_ObjectHideFlags: 0",
                "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {go}}}", f"  m_Enabled: {enabled}",
                "  m_EditorHideFlags: 0", f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}",
                "  m_Name: ", "  m_EditorClassIdentifier: "]

    # ── root ──
    Y += [f"--- !u!1 &{root_go}", "GameObject:", "  m_ObjectHideFlags: 0",
          "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
          "  m_PrefabAsset: {fileID: 0}", "  serializedVersion: 6", "  m_Component:"]
    for c in (root_tf, c_fauna, c_emitter, c_driver, c_netobj, c_nettf, c_netsync):
        Y += [f"  - component: {{fileID: {c}}}"]
    Y += ["  m_Layer: 0", "  m_Name: SwordfishFauna", "  m_TagString: Untagged", "  m_Icon: {fileID: 0}",
          "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1"]
    Y += [f"--- !u!4 &{root_tf}", "Transform:", "  m_ObjectHideFlags: 0",
          "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
          "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {root_go}}}", "  serializedVersion: 2",
          "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", "  m_LocalPosition: {x: 0, y: 0, z: 0}",
          f"  m_LocalScale: {{x: {f(BODY_SCALE)}, y: {f(BODY_SCALE)}, z: {f(BODY_SCALE)}}}",
          "  m_ConstrainProportionsScale: 1", "  m_Children:",
          f"  - {{fileID: {fbx_root_tf}}}", f"  - {{fileID: {crystal_tf}}}",
          "  m_Father: {fileID: 0}", "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}"]
    Y += mono_header(c_fauna, root_go, G_SCRIPT["SwordfishFauna"]) + [
        f"  cellData: {{fileID: 11400000, guid: {G_CELL_RUNTIME_DATA}, type: 2}}", "  domain: 0",
        "  goalUpdateInterval: 5", "  goalUpdateIntervalByAggression:", "  - 1", "  - 0.55", "  - 0.25",
        "  goalOrbitRadius: 60", "  Goal: {x: 0, y: 0, z: 0}", "  diet: 1", "  predationImmunitySeconds: 6",
        "  starvationSeconds: 60", f"  data: {{fileID: 11400000, guid: {G_DATA_SO}, type: 2}}", "  Phase: 0",
        f"  strikeData: {{fileID: 11400000, guid: {G_STRIKE_SO}, type: 2}}"]
    Y += mono_header(c_emitter, root_go, G_STUDIO_EVENT_EMITTER)
    Y[-1] = "  m_EditorClassIdentifier: FMODUnity::FMODUnity.StudioEventEmitter"
    Y += ["  CollisionTag: ", "  EventReference:", "    Guid:", "      Data1: 0", "      Data2: 0", "      Data3: 0",
          "      Data4: 0", "    Path: ", "  Event: ", "  EventPlayTrigger: 1", "  EventStopTrigger: 2",
          "  AllowFadeout: 1", "  TriggerOnce: 0", "  Preload: 0", "  NonRigidbodyVelocity: 0", "  Params: []",
          "  OverrideAttenuation: 0", "  OverrideMinDistance: 0", "  OverrideMaxDistance: 240"]
    Y += mono_header(c_driver, root_go, G_SCRIPT["SwordfishChargeDriver"]) + [
        "  animator: {fileID: 0}",
        f"  controller: {{fileID: 9100000, guid: {G_CONTROLLER}, type: 2}}",
        "  telegraphEvent:", "    Guid:", "      Data1: 0", "      Data2: 0", "      Data3: 0", "      Data4: 0",
        "    Path: ",
        "  lungeEvent:", "    Guid:", "      Data1: 0", "      Data2: 0", "      Data3: 0", "      Data4: 0",
        "    Path: "]
    Y += mono_header(c_netobj, root_go, G_NETWORK_OBJECT) + [
        "  GlobalObjectIdHash: 0", "  InScenePlacedSourceGlobalObjectIdHash: 0", "  DeferredDespawnTick: 0",
        "  Ownership: 1", "  AlwaysReplicateAsRoot: 0", "  SynchronizeTransform: 1",
        "  ActiveSceneSynchronization: 0", "  SceneMigrationSynchronization: 0", "  SpawnWithObservers: 1",
        "  DontDestroyWithOwner: 0", "  AutoObjectParentSync: 1", "  SyncOwnerTransformWhenParented: 1",
        "  AllowOwnerToParent: 0"]
    Y += mono_header(c_nettf, root_go, G_NETWORK_TRANSFORM) + [
        "  ShowTopMostFoldoutHeaderGroup: 1", "  NetworkTransformExpanded: 1", "  AutoOwnerAuthorityTickOffset: 1",
        "  PositionInterpolationType: 0", "  RotationInterpolationType: 0", "  ScaleInterpolationType: 0",
        "  PositionLerpSmoothing: 1", "  PositionMaxInterpolationTime: 0.1", "  RotationLerpSmoothing: 1",
        "  RotationMaxInterpolationTime: 0.1", "  ScaleLerpSmoothing: 1", "  ScaleMaxInterpolationTime: 0.1",
        "  AuthorityMode: 0", "  TickSyncChildren: 0", "  UseUnreliableDeltas: 1", "  SyncPositionX: 1",
        "  SyncPositionY: 1", "  SyncPositionZ: 1", "  SyncRotAngleX: 1", "  SyncRotAngleY: 1", "  SyncRotAngleZ: 1",
        "  SyncScaleX: 0", "  SyncScaleY: 0", "  SyncScaleZ: 0", "  PositionThreshold: 0.1", "  RotAngleThreshold: 1",
        "  ScaleThreshold: 0.01", "  UseQuaternionSynchronization: 0", "  UseQuaternionCompression: 0",
        "  UseHalfFloatPrecision: 1", "  InLocalSpace: 0", "  SwitchTransformSpaceWhenParented: 0",
        "  Interpolate: 1", "  SlerpPosition: 0"]
    Y += mono_header(c_netsync, root_go, G_FAUNA_NETWORK_SYNC) + ["  fauna: {fileID: 0}", "  despawnGraceSeconds: 0.5"]

    # ── mounts (plain GameObjects under the FBX's bones) and Spindles (on the FBX's part GOs) ──
    for name in PART_ORDER:
        m = mounts[name]
        Y += [f"--- !u!1 &{mount_go[name]}", "GameObject:", "  m_ObjectHideFlags: 0",
              "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
              "  m_PrefabAsset: {fileID: 0}", "  serializedVersion: 6", "  m_Component:",
              f"  - component: {{fileID: {mount_tf[name]}}}", "  m_Layer: 0", f"  m_Name: {name}Prisms",
              "  m_TagString: Untagged", "  m_Icon: {fileID: 0}", "  m_NavMeshLayer: 0",
              "  m_StaticEditorFlags: 0", "  m_IsActive: 1"]
        Y += [f"--- !u!4 &{mount_tf[name]}", "Transform:", "  m_ObjectHideFlags: 0",
              "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
              "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {mount_go[name]}}}",
              "  serializedVersion: 2", "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
              f"  m_LocalPosition: {{x: {f(m.local_pos[0])}, y: {f(m.local_pos[1])}, z: {f(m.local_pos[2])}}}",
              f"  m_LocalScale: {{x: {f(m.scale)}, y: {f(m.scale)}, z: {f(m.scale)}}}",
              "  m_ConstrainProportionsScale: 1", "  m_Children: []",
              f"  m_Father: {{fileID: {bone_tf[m.bone]}}}", "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}"]
        Y += mono_header(spindle_id[name], part_go[name], G_SPINDLE_SCRIPT) + [
            f"  RenderedObject: {{fileID: {part_smr[name]}}}", "  additionalRenderedObjects: []",
            "  parentSpindle: {fileID: 0}", "  LifeForm: {fileID: 0}", "  retainSpindle: 0", "  permanentWither: 1"]

    # ── the nested FBX instance ──
    Y += [f"--- !u!1001 &{fbx_inst}", "PrefabInstance:", "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
          "  m_Modification:", "    serializedVersion: 3", f"    m_TransformParent: {{fileID: {root_tf}}}",
          "    m_Modifications:"]
    mods = transform_mods(pinned(4, "//RootNode"), G_PARTS_FBX, (0, 0, 0), (0, 0, 0, 1), (1, 1, 1))
    mods += mod(pinned(1, "//RootNode"), G_PARTS_FBX, "m_Name", "SwordfishModel")
    mods += mod(pinned(95, "//RootNode"), G_PARTS_FBX, "m_Controller", "",
                f"{{fileID: 9100000, guid: {G_CONTROLLER}, type: 2}}")
    Y += [mods.rstrip("\n")]
    Y += ["    m_RemovedComponents: []", "    m_RemovedGameObjects: []", "    m_AddedGameObjects:"]
    for name in PART_ORDER:
        Y += [f"    - targetCorrespondingSourceObject: {{fileID: {pinned(4, mounts[name].bone)}, guid: {G_PARTS_FBX},",
              "        type: 3}", "      insertIndex: -1", f"      addedObject: {{fileID: {mount_tf[name]}}}"]
    Y += ["    m_AddedComponents:"]
    for name in PART_ORDER:
        Y += [f"    - targetCorrespondingSourceObject: {{fileID: {pinned(1, f'Swordfish_{name}')}, guid: {G_PARTS_FBX},",
              "        type: 3}", "      insertIndex: -1", f"      addedObject: {{fileID: {spindle_id[name]}}}"]
    Y += [f"  m_SourcePrefab: {{fileID: 100100000, guid: {G_PARTS_FBX}, type: 3}}"]

    def stripped_doc(cls, kind, local_id, src_id):
        return [f"--- !u!{cls} &{local_id} stripped", f"{kind}:",
                f"  m_CorrespondingSourceObject: {{fileID: {src_id}, guid: {G_PARTS_FBX},", "    type: 3}",
                f"  m_PrefabInstance: {{fileID: {fbx_inst}}}", "  m_PrefabAsset: {fileID: 0}"]

    Y += stripped_doc(4, "Transform", fbx_root_tf, pinned(4, "//RootNode"))
    for b in FBX_BONES:
        if b in {m.bone for m in mounts.values()}:
            Y += stripped_doc(4, "Transform", bone_tf[b], pinned(4, b))
    for name in PART_ORDER:
        Y += stripped_doc(1, "GameObject", part_go[name], pinned(1, f"Swordfish_{name}"))
        Y += stripped_doc(137, "SkinnedMeshRenderer", part_smr[name], pinned(137, f"Swordfish_{name}"))

    # ── the twelve prisms ──
    for spec, pos, rot in placed:
        gid_ = G_DANGER_BLOCK if spec.kind == "danger" else G_DYNAMIC_HEALTH_BLOCK
        hp = DANGER_HEALTH_PRISM if spec.kind == "danger" else DYNAMIC_HEALTH_PRISM
        inst = fid(f"prefab/prism/{spec.name}")
        Y += [f"--- !u!1001 &{inst}", "PrefabInstance:", "  m_ObjectHideFlags: 0", "  serializedVersion: 2",
              "  m_Modification:", "    serializedVersion: 3",
              f"    m_TransformParent: {{fileID: {mount_tf[spec.part]}}}", "    m_Modifications:"]
        m = transform_mods(BLOCK_TRANSFORM, gid_, pos, rot, spec.size)
        m += mod(BLOCK_TRANSFORM, gid_, "m_ConstrainProportionsScale", "0")
        m += mod(BLOCK_GO, gid_, "m_Name", f"{'DangerBlock' if spec.kind == 'danger' else 'DynamicHealthBlock'} {spec.name}")
        m += mod(BLOCK_GO, gid_, "m_StaticEditorFlags", "0")
        m += mod(hp, gid_, "spindle", "", f"{{fileID: {spindle_id[spec.part]}}}")
        m += mod(hp, gid_, "LifeForm", "", "{fileID: 0}")
        m += mod(BLOCK_SCALE_ANIMATOR, gid_, "usePrefabScaleAsDefaultTarget", "1")
        Y += [m.rstrip("\n"), "    m_RemovedComponents: []", "    m_RemovedGameObjects: []",
              "    m_AddedGameObjects: []", "    m_AddedComponents: []",
              f"  m_SourcePrefab: {{fileID: 100100000, guid: {gid_}, type: 3}}"]
        Y += [f"--- !u!4 &{BLOCK_TRANSFORM ^ inst} stripped", "Transform:",
              f"  m_CorrespondingSourceObject: {{fileID: {BLOCK_TRANSFORM}, guid: {gid_},", "    type: 3}",
              f"  m_PrefabInstance: {{fileID: {inst}}}", "  m_PrefabAsset: {fileID: 0}"]

    # ── the heart: the shark's dormant Mass crystal instance, donor-cloned and re-identified ──
    Y += [clone_crystal(donor_text, crystal_inst, root_tf)]
    return "\n".join(Y).rstrip("\n") + "\n"


def clone_crystal(donor_text: str, new_inst: int, parent_tf: int) -> str:
    docs = re.split(r"^(?=--- !u!)", donor_text, flags=re.M)
    inst_doc = next(d for d in docs if d.startswith(f"--- !u!1001 &{SHARK_CRYSTAL_INSTANCE}\n"))
    added = [int(x) for x in re.findall(r"addedObject: \{fileID: (\d+)\}", inst_doc)]
    strip_docs = [d for d in docs if re.search(rf"m_PrefabInstance: \{{fileID: {SHARK_CRYSTAL_INSTANCE}\}}", d)]
    added_docs = [d for d in docs if any(d.startswith(f"--- !u!114 &{a}\n") or d.startswith(f"--- !u!137 &{a}\n") for a in added)]
    assert len(added_docs) == len(added), "crystal donor: added-component docs missing"
    idmap = {SHARK_CRYSTAL_INSTANCE: new_inst}
    for d in strip_docs:
        old = int(re.match(r"--- !u!\d+ &(\d+)", d).group(1))
        src_id = int(re.search(r"m_CorrespondingSourceObject: \{fileID: (\d+)", d).group(1))
        assert old == src_id ^ SHARK_CRYSTAL_INSTANCE, "crystal donor: stripped id is not src ^ instance"
        idmap[old] = src_id ^ new_inst
    for a in added:
        idmap[a] = fid(f"prefab/crystal/added/{a}")
    block = inst_doc + "".join(strip_docs) + "".join(added_docs)
    block = re.sub(r"m_TransformParent: \{fileID: \d+\}", f"m_TransformParent: {{fileID: {parent_tf}}}", block, count=1)

    def remap(m):
        return f"{m.group(1)}{idmap.get(int(m.group(2)), int(m.group(2)))}{m.group(3)}"
    block = re.sub(r"(&|fileID: )(\d+)(\b)", remap, block)
    return block.rstrip("\n")


# ── ScriptableObjects ──────────────────────────────────────────────────────────

def so_header(name: str, script_guid: str, editor_class: str = "") -> list:
    return ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
            "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}",
            "  m_Enabled: 1", "  m_EditorHideFlags: 0",
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}", f"  m_Name: {name}",
            f"  m_EditorClassIdentifier: {editor_class}"]


def render_data_so() -> str:
    Y = so_header("SwordfishFaunaDataSO", G_LIGHT_FAUNA_DATA_SO) + [
        "  detectionRadius: 130", "  separationRadius: 70", "  consumeRadius: 40", "  behaviorUpdateRate: 1.5",
        "  separationWeight: 20", "  goalWeight: 1.5", "  minSpeed: 30", "  maxSpeed: 42", "  rotationLerpSpeed: 4",
        "  feedingFacingAngle: 25", "  consumeHoldSeconds: 2", "  feedingClusterRadius: 12", "  maxClusterBites: 8",
        "  feedingBrakeSharpness: 4", "  pursuitSpeedMultiplier: 1.6", "  pursuitAgility: 3", "  attackRange: 16",
        "  territoryRadius: 700", "  territoryAnchorDistance: 450", "  huntIntervalSeconds: 24",
        "  huntDurationSeconds: 12", "  witherRingInterval: 0.3"]
    return "\n".join(Y) + "\n"


def render_strike_so() -> str:
    Y = so_header("SwordfishStrikeData", G_SCRIPT["SwordfishStrikeDataSO"]) + [
        "  aggroRadius: 280", "  strikeRange: 110", "  telegraphSeconds: 1.1", "  telegraphRetreatSpeed: 12",
        "  lungeSpeed: 150", "  lungeOvershoot: 30", "  lungeMaxSeconds: 1.5", "  lungeArriveRadius: 12",
        "  recoverSeconds: 2", "  recoverSpeedFraction: 0.3", "  strikeCooldownSeconds: 7",
        "  opposingDomainsOnly: 1", "  profiles:"]
    for element, lunge, telegraph, rng, cooldown in (
            ("Charge", 1.25, 0.8, 1.0, 1.0), ("Mass", 0.85, 1.15, 1.0, 1.2),
            ("Space", 1.0, 1.0, 1.35, 1.0), ("Time", 1.0, 1.0, 1.0, 0.5)):
        Y += [f"  - element: {ELEMENT_INDEX[element]}", f"    lungeSpeedMultiplier: {f(lunge)}",
              f"    telegraphMultiplier: {f(telegraph)}", f"    rangeMultiplier: {f(rng)}",
              f"    cooldownMultiplier: {f(cooldown)}"]
    return "\n".join(Y) + "\n"


def render_variant(element: str) -> str:
    scale, starvation = VARIANTS[element]
    path = LIFEFORMS / f"Swordfish Fauna {element}.asset"
    Y = so_header(f"Swordfish Fauna {element}", G_FAUNA_CONFIG_SO) + [
        f"  FaunaPrefab: {{fileID: {fid('prefab/root/SwordfishFauna')}, guid: {G_PREFAB}, type: 3}}",
        "  InitialSpawnCount: 1", "  PopulationSize: 1", "  SpawnProbability: 1", "  FeedsPerOffspring: 10",
        "  OffspringPerBirth: 1", "  ReproductionCooldownSeconds: 30", "  MaxLivePopulation: 2",
        f"  Element: {ELEMENT_INDEX[element]}", "  Variant:", "    Enabled: 1"]
    # The HEART is owned by author_lifeform_heart_sizes.py (§40.2: sized from the measured body,
    # anchored so nothing clips the reward cap). This generator carries whatever that script
    # authored rather than restating the law: first `--write` here, then `--write` there, and
    # both `--check`s stay green.
    if path.exists():
        m = re.search(r"^    HeartWorldScale: (.*)$", path.read_text(encoding="utf-8"), re.M)
        if m:
            Y.append(f"    HeartWorldScale: {m.group(1)}")
    Y += [f"    BaseBodyScale: {f(scale)}", f"    StarvationSeconds: {f(starvation)}"]
    return "\n".join(Y) + "\n"


def render_blob_config() -> str:
    Y = so_header("Blob Swordfish Fauna Config Data", G_FAUNA_CONFIG_SO) + [
        f"  FaunaPrefab: {{fileID: {fid('prefab/root/SwordfishFauna')}, guid: {G_PREFAB}, type: 3}}",
        "  InitialSpawnCount: 1", "  PopulationSize: 1", "  SpawnProbability: 1", "  NetworkSynced: 1",
        "  FeedsPerOffspring: 10", "  OffspringPerBirth: 1", "  ReproductionCooldownSeconds: 30",
        "  MaxLivePopulation: 2", "  SpreadElements: 1", "  ElementPalette:"]
    for element in ("Charge", "Mass", "Space", "Time"):
        Y += [f"  - {{fileID: 11400000, guid: {G_VARIANT[element]}, type: 2}}"]
    return "\n".join(Y) + "\n"


def asset_meta(g: str, main_id: int = 11400000) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: {main_id}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def prefab_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def script_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
            f"  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


def edit_spawn_profile(text: str) -> str:
    """The flagship takes the shark's apex slot in the freestyle worlds (every Blob-derived cell)."""
    new = f"  - {{fileID: 11400000, guid: {G_BLOB_CONFIG}, type: 2}}"
    if new in text:
        return text
    old = f"  - {{fileID: 11400000, guid: {G_BLOB_SHARK_CONFIG}, type: 2}}"
    assert text.count(old) == 1, "Blob Cell Spawn Profile: shark slot not found exactly once"
    return text.replace(old, new)


def edit_network_prefabs(text: str) -> str:
    entry = (f"  - Override: 0\n    Prefab: {{fileID: {fid('prefab/root/go')}, guid: {G_PREFAB},\n      type: 3}}\n"
             f"    SourcePrefabToOverride: {{fileID: 0}}\n    SourceHashToOverride: 0\n"
             f"    OverridingTargetPrefab: {{fileID: 0}}\n")
    if G_PREFAB in text:
        return text
    anchor = re.search(rf"  - Override: 0\n    Prefab: \{{fileID: \d+, guid: {G_SHARK_PREFAB},\n      type: 3\}}\n"
                       rf"    SourcePrefabToOverride: \{{fileID: 0\}}\n    SourceHashToOverride: 0\n"
                       rf"    OverridingTargetPrefab: \{{fileID: 0\}}\n", text)
    assert anchor, "DefaultNetworkPrefabs: shark entry not found"
    return text[:anchor.end()] + entry + text[anchor.end():]


# ═════════════════════════════════════════════════════════════════════════════
# 4. Validation
# ═════════════════════════════════════════════════════════════════════════════

def validate_yaml(path_label: str, text: str, external_ok=True):
    problems = []
    docs = re.findall(r"^--- !u!(\d+) &(-?\d+)( stripped)?\n", text, re.M)
    ids = [int(d[1]) for d in docs]
    if len(ids) != len(set(ids)):
        problems.append(f"{path_label}: duplicate fileIDs")
    for i in ids:
        if not (-INT64_MAX - 1 <= i <= INT64_MAX):
            problems.append(f"{path_label}: fileID {i} overflows int64")
    defined = set(ids)
    for m in re.finditer(r"fileID: (-?\d+)\}", text):
        i = int(m.group(1))
        if i == 0 or i in defined:
            continue
        # cross-asset references carry a guid on the same or the next line
        tail = text[m.start():m.start() + 120]
        if "guid:" in tail.split("}")[0] or re.match(r"fileID: -?\d+, guid", tail):
            continue
        problems.append(f"{path_label}: dangling local reference {i}")
    return problems


def validate_prefab(text: str, placed):
    problems = validate_yaml("prefab", text)
    docs = {int(m.group(2)): (int(m.group(1)), m.group(3)) for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)( stripped)?\n", text, re.M)}
    bodies = dict(zip(docs.keys(), re.split(r"^--- !u!\d+ &-?\d+(?: stripped)?\n", text, flags=re.M)[1:]))
    # every component listed by a GameObject points back at it
    for gid_, (cls, _s) in docs.items():
        if cls != 1 or _s:
            continue
        for c in re.findall(r"  - component: \{fileID: (\d+)\}", bodies[gid_]):
            back = re.search(r"m_GameObject: \{fileID: (\d+)\}", bodies.get(int(c), ""))
            if not back or int(back.group(1)) != gid_:
                problems.append(f"prefab: component {c} does not point back at GameObject {gid_}")
    # every stripped id is src ^ instance
    for i, (cls, s) in docs.items():
        if not s:
            continue
        src_id = int(re.search(r"m_CorrespondingSourceObject: \{fileID: (\d+)", bodies[i]).group(1))
        inst = int(re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", bodies[i]).group(1))
        if src_id ^ inst != i:
            problems.append(f"prefab: stripped {i} is not src ^ instance")
    n_prisms = len(re.findall(rf"m_SourcePrefab: \{{fileID: 100100000, guid: ({G_DANGER_BLOCK}|{G_DYNAMIC_HEALTH_BLOCK})", text))
    if n_prisms != len(placed):
        problems.append(f"prefab: {n_prisms} prism instances, expected {len(placed)}")
    if text.count(f"guid: {G_DANGER_BLOCK}, type: 3}}\n  m_SourcePrefab") != 0:
        pass
    return problems


def validate_parts_fbx(nodes, src: SourceModel, parts: dict, tmp_path: Path):
    problems = []
    fb.write(str(tmp_path), nodes, src.version, src.footer)
    back, version, _footer = fb.read(str(tmp_path))
    if fb.tree_signature(back) != fb.tree_signature(nodes):
        problems.append("parts FBX does not round-trip")
    objects = [n for n in back if n.name == "Objects"][0]
    geos = objects.find("Geometry")
    if len(geos) != len(PART_ORDER):
        problems.append(f"parts FBX has {len(geos)} geometries, expected {len(PART_ORDER)}")
    total_polys = 0
    for g in geos:
        pvi = g.first("PolygonVertexIndex").props[0][1]
        nv = len(g.first("Vertices").props[0][1]) // 3
        polys = sum(1 for i in pvi if i < 0)
        total_polys += polys
        if any((-i - 1 if i < 0 else i) >= nv for i in pvi):
            problems.append(f"geometry {pv(g.props[1][1])}: polygon index out of range")
        nn = len(g.first("LayerElementNormal").first("Normals").props[0][1]) // 3
        if nn != len(pvi):
            problems.append(f"geometry {pv(g.props[1][1])}: {nn} normals for {len(pvi)} polygon vertices")
    if total_polys != len(src.polys):
        problems.append(f"parts FBX carries {total_polys} polygons, source has {len(src.polys)}")
    # skin weights per part-vertex sum to what the source had
    for part in parts.values():
        sums = np.zeros(len(part.verts))
        for bone, entries in part.clusters.items():
            for li, w in entries:
                sums[li] += w
        orig = np.array([sum(src.weights[i].values()) for i in part.verts])
        if np.abs(sums - orig).max() > 1e-5:
            problems.append(f"part {part.name}: skin weights not conserved")
    ids = [c.props[0][1] for c in objects.children if c.props and c.props[0][0] == "L"]
    if len(ids) != len(set(ids)):
        problems.append("parts FBX: duplicate object ids")
    conns = [n for n in back if n.name == "Connections"][0]
    known = set(ids) | {0}
    for c in conns.children:
        if c.props[1][1] not in known or c.props[2][1] not in known:
            problems.append(f"parts FBX: connection to unknown id {c.props[1][1]} -> {c.props[2][1]}")
            break
    return problems


# ═════════════════════════════════════════════════════════════════════════════
# 5. Main
# ═════════════════════════════════════════════════════════════════════════════

def build(report=True):
    src = SourceModel(SRC_FBX)
    # Codec sanity on the artist's file itself: a lossy codec would corrupt silently.
    assert fb.tree_signature(fb.read(str(SRC_FBX))[0]) == fb.tree_signature(src.nodes)
    extent = src.world_verts.max(axis=0) - src.world_verts.min(axis=0)
    assert abs(extent[2] - 102.8) < 1.0 and abs(extent[1] - 55.2) < 1.0, f"unexpected body extent {extent}"

    parts, centroid = split_parts(src)
    nodes = build_parts_fbx(src, parts, centroid)
    specs = design_prisms(src, parts, centroid)
    overlaps = overlap_check(specs)
    mounts, placed = mount_and_prism_transforms(src, parts, centroid, specs)

    donor_text = DONOR_PREFAB.read_text(encoding="utf-8")
    outputs = {
        PARTS_FBX.with_suffix(".fbx.meta"): render_parts_meta(src, parts),
        CONTROLLER: render_controller(),
        CONTROLLER.with_suffix(".controller.meta"): asset_meta(G_CONTROLLER, 9100000),
        PREFAB: render_prefab(src, parts, mounts, placed, donor_text),
        PREFAB.with_suffix(".prefab.meta"): prefab_meta(G_PREFAB),
        DATA_SO: render_data_so(),
        DATA_SO.with_suffix(".asset.meta"): asset_meta(G_DATA_SO),
        STRIKE_SO: render_strike_so(),
        STRIKE_SO.with_suffix(".asset.meta"): asset_meta(G_STRIKE_SO),
        BLOB_CONFIG: render_blob_config(),
        BLOB_CONFIG.with_suffix(".asset.meta"): asset_meta(G_BLOB_CONFIG),
        SPAWN_PROFILE: edit_spawn_profile(SPAWN_PROFILE.read_text(encoding="utf-8")),
        NETWORK_PREFABS: edit_network_prefabs(NETWORK_PREFABS.read_text(encoding="utf-8")),
    }
    for element in VARIANTS:
        p = LIFEFORMS / f"Swordfish Fauna {element}.asset"
        outputs[p] = render_variant(element)
        outputs[p.with_suffix(".asset.meta")] = asset_meta(G_VARIANT[element])
    for key, rel in SCRIPTS.items():
        outputs[ROOT / (rel + ".meta")] = script_meta(G_SCRIPT[key])

    problems = list(overlaps)
    problems += validate_prefab(outputs[PREFAB], placed)
    for p, t in outputs.items():
        if p.suffix in (".asset", ".controller"):
            problems += validate_yaml(p.name, t)
    problems += validate_parts_fbx(nodes, src, parts, PARTS_FBX.with_name("SwordFish_A_Parts.roundtrip.tmp"))
    PARTS_FBX.with_name("SwordFish_A_Parts.roundtrip.tmp").unlink(missing_ok=True)
    meta_ids = re.findall(r"^      \d+: (-?\d+)$", outputs[PARTS_FBX.with_suffix(".fbx.meta")], re.M)
    if len(meta_ids) != len(set(meta_ids)):
        problems.append("parts FBX meta: duplicate pinned ids")
    for rel in SCRIPTS.values():
        if not (ROOT / rel).exists():
            problems.append(f"missing script {rel} (write it before the assets that reference it)")

    if report:
        print(f"parts: " + ", ".join(f"{n} {len(parts[n].polys)}p/{len(parts[n].verts)}v" for n in PART_ORDER))
        print(f"centroid (artist world) {np.round(centroid, 2)}; cluster convention {src.cluster_convention}")
        print(f"take frames: {src.take_frames}")
        print("prisms (world units at root scale 1):")
        for spec, pos, rot in placed:
            print(f"  {spec.name:15s} {spec.kind:7s} on {spec.part:10s} size {np.round(spec.size, 2)} "
                  f"body-pos {np.round(spec.position, 2)}")
        for name in PART_ORDER:
            m = mounts[name]
            print(f"  mount {name:10s} bone {m.bone:9s} local {np.round(m.local_pos, 4)} scale {m.scale:.6f}")
    return outputs, nodes, src, problems


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    outputs, nodes, src, problems = build(report=not args.check)
    if problems:
        print("\nFAIL:")
        for p in problems:
            print("  * " + p)
        return 1

    changed = [p for p, t in outputs.items() if not p.exists() or p.read_text(encoding="utf-8") != t]
    fbx_changed = True
    if PARTS_FBX.exists():
        cur = fb.read(str(PARTS_FBX))[0]
        fbx_changed = fb.tree_signature(cur) != fb.tree_signature(nodes)
    if args.write:
        for p, t in outputs.items():
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(t, encoding="utf-8")
        fb.write(str(PARTS_FBX), nodes, src.version, src.footer)
        print(f"\nwrote {len(outputs)} text assets + {PARTS_FBX.relative_to(ROOT)}")
        return 0
    if args.check:
        if changed or fbx_changed:
            print("FAIL: assets differ from what this script authors:")
            for p in changed:
                print("  * " + str(p.relative_to(ROOT)))
            if fbx_changed:
                print("  * " + str(PARTS_FBX.relative_to(ROOT)))
            print("  run with --write")
            return 1
        print("OK: every swordfish asset matches the generator")
        return 0
    print(f"\n{len(changed)} text assets{' + the parts FBX' if fbx_changed else ''} would change (run --write)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
