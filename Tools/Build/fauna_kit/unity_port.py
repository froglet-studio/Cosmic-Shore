#!/usr/bin/env python3
"""Port a GROWN creature from its spec into Unity assets (the first step of /fauna-maker's "Into Unity").

    python3 Tools/Build/fauna_kit/unity_port.py ray            # write the assets
    python3 Tools/Build/fauna_kit/unity_port.py ray --check    # exit 1 when the assets on disk are not what the spec makes
    python3 Tools/Build/fauna_kit/unity_port.py --self-test

What it writes, and why this shape. A grown body becomes ONE mesh under ONE Spindle, with its prisms as that
Spindle's direct children and its heart at the root: the structure the QuadFish ships with (a Spindle wrapping one
mesh, HealthPrism children, a LightFauna root), so every system that already runs a QuadFish runs this body. The
prefab is the QuadFish's, taken apart and re-fitted (a donor: its components, network identity and heart wiring are
kept; its mesh, material, scale, prisms and swim driver are replaced), never typed from memory.

  Assets/_Models/Fauna/<Species>Body.asset                       the body mesh, a native Mesh (spindle.mesh, double-sided)
  Assets/_Prefabs/FloraAndFauna/<Species>.prefab                 the creature
  Assets/_SO_Assets/Light Fauna Data/<Species>FaunaDataSO.asset  the numbers LightFauna reads (speed, mouth)
  Assets/_SO_Assets/Lifeforms/<Species> Fauna <Element>.asset    the four element configs (body scale from the spec)
  Assets/DefaultNetworkPrefabs.asset                             the prefab's entry (a networked fauna)

A spec with a `unity` block is JOINTED: each limb it names is cut where it crosses its hinges, each piece hangs from a
plain joint transform (its skin a child, the next joint its first child), GrownLimbDriver turns the joints so a wave
runs out along the limb, and the game's attack finds the limbs and whips them. `klass` swaps LightFauna for the
species' own class in place.

What it does NOT do: the lab's per-vertex ripple and travelling light (a jointed limb bends at its hinges; the body
wears the shared SpindleMaterial); a carried up (LightFauna steers with world up); a slower change of speed (the
lab's glide); any cell's spawn profile; the Codex. Nothing here has been opened in the Unity editor
(Docs/UNITY_VERIFICATION_CHECKLIST.md).
"""
import hashlib
import math
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import body as BODY  # noqa: E402
import celestial as C  # noqa: E402
import prefab_read as PR  # noqa: E402
import spindle as SP  # noqa: E402

REPO = PR.REPO
DONOR = "Assets/_Prefabs/FloraAndFauna/QuadFish.prefab"
DONOR_DATA = "Assets/_SO_Assets/Light Fauna Data/QuadFishFaunaDataSO.asset"
DONOR_CFG = "Assets/_SO_Assets/Lifeforms/QuadFish Fauna {e}.asset"
MESH_TEMPLATE = "Assets/_Models/Testing/Prism.asset"
NETWORK = "Assets/DefaultNetworkPrefabs.asset"
SPINDLE_MATERIAL = "4f44fa5c7514a2c45b5af7f45bc51acd"      # SpindleMaterial.mat: the shared spindle look (/fauna section 4)
PRISM_TR, PRISM_GO = 5222650486365209692, 5776304996075792891   # a prism prefab's root Transform and GameObject
DANGER_BLOCK = "aad956930fb933f4b82b992b1bc2b773"           # Trails/DangerBlock.prefab
# the donor's own ids (read from QuadFish.prefab; verify() checks they are still there)
ROOT_GO, BODY_TR, NETOBJ, SWIM = 6455206541193087406, 9092437090346515695, 841515063310731916, 680405309579507621
ELEMENTS = ("Charge", "Mass", "Space", "Time")


def guid(name):
    return hashlib.md5(("fauna_kit:" + name).encode()).hexdigest()


def nid(name):
    return int(hashlib.md5(("fauna_kit:id:" + name).encode()).hexdigest()[:15], 16)


def read(rel):
    return open(os.path.join(REPO, rel)).read()


def meta_of(donor_rel, new_guid):
    return re.sub(r"guid: [0-9a-f]{32}", "guid: " + new_guid, read(donor_rel + ".meta"), count=1)


def num(v):
    s = "%.6f" % v
    s = s.rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


# ------------------------------------------------------------------ the mesh

SCRIPTS = "Assets/_Scripts/Controller/Environment/FloraAndFauna/"


def script_guid(name):
    return re.search(r"guid: (\w+)", read(SCRIPTS + name + ".cs.meta")).group(1)


def wire_rows(made):
    """{limb: (vertices, triangles, sway [6 a vertex], uv [2 a vertex])}: each grown limb's mesh with what rides it."""
    rows = {}
    for w in made["wires"]:
        if w.get("source") != "grown":
            continue
        V, T, src, U = SP.mesh_data(w)
        rows[w["name"]] = (V, T, [w["sway"][i] for i in src], U)
    return rows


def joints_of(key, spec, made):
    """The body cut at its hinges (the spec's `unity` block): (core, joints, prism homes). The core and every joint
    carry a mesh {V, T, S, U}. A joint is {name, parent, origin, axis, degrees, phase, V, T, S, U}.
    EVERY MESH STAYS IN THE BODY'S OWN SPACE (a joint's skin is set back by the joint's origin in the prefab), so
    anything the shader computes from a vertex's position is the same function on both sides of a cut: SpindleSway
    shears by z from the origin, and pieces in their own spaces tore 2.6 u apart at the first tail hinge.
    A triangle goes to the last hinge its centre has passed, walking out from the body; before the first, to the core."""
    rows = wire_rows(made)
    unity = spec.get("unity") or {}
    claimed, joints, home = set(), [], {}
    core = {"V": [], "T": [], "S": [], "U": []}

    def add(dst, remap, tri, row):
        V, _T, S, U = row
        out = []
        for i in tri:
            if i not in remap:
                remap[i] = len(dst["V"])
                dst["V"].append(list(V[i]))
                dst["S"].append(list(S[i]))
                dst["U"].append(list(U[i]))
            out.append(remap[i])
        dst["T"].append(tuple(out))

    for limb in unity.get("limbs", []):
        ax = 0 if limb["cut"] == "x" else 2
        sgn = 1.0 if limb["hinges"][0] >= 0 else -1.0
        seg = lambda c: sum(1 for h in limb["hinges"] if c * sgn >= h * sgn)
        chain = []
        for j, h in enumerate(limb["hinges"]):
            origin = [h, 0.0, 0.0] if ax == 0 else [0.0, 0.0, h]
            chain.append({"name": f"{limb['name']}{j}", "parent": chain[-1]["name"] if chain else None, "origin": origin, "axis": limb["axis"],
                          "degrees": limb["degrees"][j], "phase": limb["lag"] * j, "V": [], "T": [], "S": [], "U": [], "_map": {}})
        for w in limb["wires"]:
            if w not in rows:
                raise SystemExit(f"{spec['species']}: the unity limb {limb['name']} names a wire the body has not got: {w}")
            claimed.add(w)
            cmap = {}
            for tri in rows[w][1]:
                k = seg(sum(rows[w][0][i][ax] for i in tri) / 3.0)
                if k == 0:
                    add(core, cmap, tri, rows[w])
                else:
                    add(chain[k - 1], chain[k - 1]["_map"].setdefault(w, {}), tri, rows[w])
        for p in made["prisms"]:
            if p["limb"] in limb["wires"]:
                k = seg(p["at"][ax])
                if k:
                    home[p["name"]] = chain[k - 1]
        joints += chain
    for name, row in rows.items():
        if name not in claimed:
            cmap = {}
            for tri in row[1]:
                add(core, cmap, tri, row)
    return core, joints, home


GO_DOC = """--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comps}  m_Layer: {layer}
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
TR_DOC = """--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {x}, y: {y}, z: {z}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{kids}
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def body_mesh(made):
    """(vertices, triangles): every grown limb and membrane of the body as one mesh, in the creature's own space."""
    V, T = [], []
    for v, t, _s, _u in wire_rows(made).values():
        base = len(V)
        V += v
        T += [(a + base, b + base, c + base) for a, b, c in t]
    return V, T


STRIDE = 72      # bytes a vertex: position 12, normal 12, tangent 16, uv0 8, uv1 12 (the wave's sine part), uv2 12 (its cosine part)
CHANNEL = "    - stream: 0\n      offset: %d\n      format: 0\n      dimension: %d\n"


def mesh_asset(name, mesh, turn=None):
    """A native Unity Mesh, in the format of Assets/_Models/Testing/Prism.asset with two more channels: position,
    normal, tangent, uv0 (where SpindleGraph's lace lands: u along the strand, v round it), and uv1 / uv2, the sine
    and cosine parts of each vertex's WAVE (SpindleGrownSway.hlsl adds SwaySin sin t + SwayCos cos t). 16-bit
    indices. Double-sided: every triangle is listed both ways, because a ribbon has one face. The bounds are grown
    by the furthest the wave carries any vertex, so a body at the screen's edge is not culled with a wingtip in view."""
    V, T, S, U = mesh["V"], mesh["T"], mesh["S"], mesh["U"]
    if len(V) > 65535:
        raise SystemExit(f"{name}: {len(V)} vertices will not fit 16-bit indices")
    normals = [[0.0, 0.0, 0.0] for _ in V]
    for a, b, c in T:
        n = C.cross(C.sub(V[b], V[a]), C.sub(V[c], V[a]))
        for i in (a, b, c):
            normals[i] = C.add(normals[i], n)
    lo = [min(v[i] for v in V) for i in range(3)]
    hi = [max(v[i] for v in V) for i in range(3)]
    reach = [max(abs(sw[i]) + abs(sw[3 + i]) for sw in S) for i in range(3)]
    if turn:      # and by the furthest the body's answer to a turn can carry a vertex (GrownLimbDriver's clamps)
        r = turn["reach"]
        far = max((1.0 + turn["max_stroke"]) * max(abs(c) for c in sw) + 1.5 * turn["max_lag"] * C.norm(v) ** 2 / r
                  + turn["max_twist"] * abs(v[0]) * C.norm(v) / r for v, sw in zip(V, S))
        reach = [max(c, far) for c in reach]
    data = bytearray()
    for v, n, sw, uv in zip(V, normals, S, U):
        n = C.unit(n) if C.norm(n) > 1e-12 else [0.0, 1.0, 0.0]
        data += struct.pack("<3f3f4f2f3f3f", v[0], v[1], v[2], n[0], n[1], n[2], 1.0, 0.0, 0.0, 1.0, uv[0], uv[1], *sw)
    idx = bytearray()
    for a, b, c in T:
        idx += struct.pack("<6H", a, b, c, a, c, b)
    t = read(MESH_TEMPLATE)
    centre = "{x: %s, y: %s, z: %s}" % tuple(num((lo[i] + hi[i]) / 2) for i in range(3))
    extent = "{x: %s, y: %s, z: %s}" % tuple(num((hi[i] - lo[i]) / 2 + reach[i]) for i in range(3))
    t = re.sub(r"m_Name: .*", "m_Name: " + name, t, count=1)
    t = re.sub(r"indexCount: \d+", "indexCount: %d" % (len(T) * 6), t, count=1)
    t = re.sub(r"(firstVertex: 0\n\s+vertexCount: )\d+", r"\g<1>%d" % len(V), t, count=1)
    t = re.sub(r"m_VertexCount: \d+", "m_VertexCount: %d" % len(V), t, count=1)
    # the channel table, in Unity's fixed order: position, normal, tangent, colour, uv0 .. uv7, blend weights, blend indices
    table = re.search(r"    m_Channels:\n((?:    - stream: \d+\n      offset: \d+\n      format: \d+\n      dimension: \d+\n)+)", t)
    rows = re.findall(r"    - stream: \d+\n      offset: \d+\n      format: \d+\n      dimension: \d+\n", table.group(1))
    if len(rows) != 14 or rows[4] != CHANNEL % (40, 2) or rows[5] != CHANNEL % (0, 0):
        raise SystemExit(f"{MESH_TEMPLATE}: its channel table is not the one this tool extends (14 rows, uv0 at 40)")
    rows[5], rows[6] = CHANNEL % (48, 3), CHANNEL % (60, 3)
    t = t[:table.start(1)] + "".join(rows) + t[table.end(1):]
    t = re.sub(r"m_IndexBuffer: [0-9a-f]*", "m_IndexBuffer: " + idx.hex(), t, count=1)
    t = re.sub(r"m_DataSize: \d+", "m_DataSize: %d" % len(data), t, count=1)
    t = re.sub(r"_typelessdata: [0-9a-f]*", "_typelessdata: " + data.hex(), t, count=1)
    t = re.sub(r"m_Center: \{[^}]*\}", "m_Center: " + centre, t)
    t = re.sub(r"m_Extent: \{[^}]*\}", "m_Extent: " + extent, t)
    return t


def mesh_counts(text):
    """(vertices, triangles as drawn, bytes a vertex) read back from a Mesh asset's text."""
    nv = int(re.search(r"m_VertexCount: (\d+)", text).group(1))
    size = int(re.search(r"m_DataSize: (\d+)", text).group(1))
    blob = re.search(r"_typelessdata: ([0-9a-f]*)", text).group(1)
    ni = len(re.search(r"m_IndexBuffer: ([0-9a-f]*)", text).group(1)) // 4
    if len(blob) != size * 2 or size != nv * STRIDE or ni != int(re.search(r"indexCount: (\d+)", text).group(1)):
        raise SystemExit("the mesh asset's sizes do not agree with its data")
    return nv, ni // 3, size // max(nv, 1)


def mesh_waves(text):
    """[(position, the wave's six numbers)] read back from a Mesh asset's text."""
    blob = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]*)", text).group(1))
    out = []
    for i in range(0, len(blob), STRIDE):
        f = struct.unpack_from("<3f3f4f2f3f3f", blob, i)
        out.append((f[0:3], f[12:18]))
    return out


GROWN_MATERIAL = "Assets/_Graphics/Materials/Fauna/{sp}Spindle.mat"


def material(spec):
    """The form's own spindle material: SpindleMaterial with the shear sway off and the grown sway on at the form's
    tempo. Its colours are SpindleMaterial's own (a spindle is tinted by its domain at runtime like every other)."""
    t = read("Assets/_Graphics/Materials/SpindleMaterial.mat")
    t, n = re.subn(r"m_Name: SpindleMaterial\n", f"m_Name: {spec['species']}Spindle\n", t, count=1)
    t, m = re.subn(r"(?m)^    - _SwayAmplitude: .*$", "    - _SwayAmplitude: 0", t, count=1)
    t, k = re.subn(r"(?m)^(    - _GlossyReflections: .*\n)",
                   r"\g<1>" + f"    - _GrownFrequency: {num(2 * math.pi * spec['sway_hz'])}\n    - _GrownSway: 1\n", t, count=1)
    if (n, m, k) != (1, 1, 1):
        raise SystemExit("SpindleMaterial.mat moved: its name, _SwayAmplitude or _GlossyReflections row was not found")
    return t


# ------------------------------------------------------------------ the prefab

def prism_block(species, i, p, block_guid, parent=BODY_TR, origin=(0.0, 0.0, 0.0)):
    p = dict(p, at=[p["at"][c] - origin[c] for c in range(3)])
    inst = nid(f"{species}:prism:{p['name']}")
    tr = (PRISM_TR ^ inst) & PR.MASK
    rows = [("m_RootOrder", str(i))]
    rows += [(f"m_LocalScale.{a}", num(p["size"][k])) for k, a in enumerate("xyz")]
    rows += [(f"m_LocalPosition.{a}", num(p["at"][k])) for k, a in enumerate("xyz")]
    rows += [(f"m_LocalRotation.{a}", num(p["q"][k])) for a, k in (("w", 3), ("x", 0), ("y", 1), ("z", 2))]
    mods = "".join(f"    - target: {{fileID: {PRISM_TR}, guid: {block_guid},\n        type: 3}}\n      propertyPath: {path}\n"
                   f"      value: {val}\n      objectReference: {{fileID: 0}}\n" for path, val in rows)
    mods += (f"    - target: {{fileID: {PRISM_GO}, guid: {block_guid},\n        type: 3}}\n      propertyPath: m_Name\n"
             f"      value: {p['name']}\n      objectReference: {{fileID: 0}}\n")
    doc = (f"--- !u!1001 &{inst}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
           f"    serializedVersion: 3\n    m_TransformParent: {{fileID: {parent}}}\n    m_Modifications:\n{mods}"
           f"    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
           f"  m_SourcePrefab: {{fileID: 100100000, guid: {block_guid}, type: 3}}\n"
           f"--- !u!4 &{tr} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {PRISM_TR}, guid: {block_guid},\n    type: 3}}\n"
           f"  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return doc, tr


def prefab(key, spec, made):
    species = spec["species"]
    t = read(DONOR)
    guids = PR.guid_index()
    head, *docs = re.split(r"(?m)^(?=--- !u!)", t)
    health_guid = None
    keep = []
    for d in docs:
        m = re.match(r"--- !u!(\d+) &(-?\d+)", d)
        cls, fid = m.group(1), int(m.group(2))
        src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", d)
        if cls == "1001" and src and "HealthBlock" in guids.get(src.group(1), ""):
            health_guid = src.group(1)
            continue                                  # the donor's fins go
        if " stripped" in d.split("\n")[0] and health_guid is None and "5222650486365209692" in d:
            pass
        if fid == SWIM:
            continue                                  # QuadFishSwimDriver drives the donor's fins
        keep.append(d)
    # the donor's stripped fin transforms go with their instances
    keep = [d for d in keep if not (" stripped" in d.split("\n")[0] and f"fileID: {PRISM_TR}, guid: {health_guid}" in d)]
    t = head + "".join(keep)
    _core, joints, home = joints_of(key, spec, made)
    gpu = sway_mode(spec) == "gpu"
    jid = {j["name"]: (nid(f"{species}:go:{j['name']}"), nid(f"{species}:tr:{j['name']}")) for j in joints}
    blocks, kids, under, riders = "", [], {j["name"]: [] for j in joints}, []
    for i, p in enumerate(made["prisms"]):
        j = home.get(p["name"])
        doc, tr = prism_block(species, i, p, DANGER_BLOCK if p["kind"] == "danger" else health_guid,
                              jid[j["name"]][1] if j else BODY_TR, j["origin"] if j else (0.0, 0.0, 0.0))
        blocks += doc
        (under[j["name"]] if j else kids).append(tr)
        if gpu:      # a prism rides its limb's wave, and the turn, where it sits in the body (GrownLimbDriver)
            riders.append((tr, p.get("sway") or [0.0] * 6, p["at"]))
    def once(pattern, repl, what):
        nonlocal t
        t, n = re.subn(pattern, repl, t, count=1)
        if n != 1:
            raise SystemExit(f"{DONOR}: {what} not found; the donor moved (update unity_port.py)")
    once(r"  - component: \{fileID: %d\}\n" % SWIM, "", "the swim driver's component line")
    once(r"m_Name: QuadFish\n", f"m_Name: {species}\n", "the root's name")
    once(r"m_Name: BezierCurve\n", "m_Name: Body\n", "the body's name")
    once(r"(--- !u!4 &6455206541193087393\n(?:.*\n)*?  m_LocalScale: )\{[^}]*\}", r"\1{x: 1, y: 1, z: 1}", "the root's scale")
    once(r"(--- !u!4 &%d\n(?:.*\n)*?  m_LocalScale: )\{[^}]*\}" % BODY_TR, r"\1{x: 1, y: 1, z: 1}", "the body's scale")
    once(r"(--- !u!4 &%d\n(?:.*\n)*?  m_Children:\n)(?:  - \{fileID: -?\d+\}\n)+" % BODY_TR,
         lambda m: m.group(1) + "".join(f"  - {{fileID: {k}}}\n" for k in kids), "the body's children")
    once(r"m_Mesh: \{fileID: -?\d+, guid: \w+, type: 3\}", f"m_Mesh: {{fileID: 4300000, guid: {guid('mesh:' + key)}, type: 2}}", "the body's mesh")
    once(r"(m_Materials:\n  - \{fileID: 2100000, guid: )\w+", r"\g<1>" + (guid("material:" + key) if gpu else SPINDLE_MATERIAL), "the body's material")
    once(r"(  data: \{fileID: 11400000, guid: )\w+", r"\g<1>" + guid("data:" + key.split(":")[0]), "LightFauna.data")
    import author_stoat_assets as stoat      # the one GlobalObjectId hash implementation (self-tested there)
    once(r"(--- !u!114 &%d\n(?:.*\n)*?  GlobalObjectIdHash: )\d+" % NETOBJ,
         r"\g<1>%d" % stoat.global_object_id_hash(guid("prefab:" + key), NETOBJ), "the network identity")
    # the jointed body: each joint a plain transform (so the attack finds it as a limb), its skin a child that draws
    # its piece of the mesh, the next joint its FIRST child (the attack follows first children down a limb). A skin is
    # set BACK by its joint's place, so its mesh stays in the body's own space (joints_of).
    layer = re.search(r"--- !u!1 &6730920616416142416\n(?:.*\n)*?  m_Layer: (\d+)", t).group(1)
    mf = re.search(r"--- !u!33 &\d+\n(?:.*\n)*?(?=--- )", t).group(0)
    mr = re.search(r"--- !u!23 &\d+\n(?:.*\n)*?(?=--- )", t).group(0)
    docs, skins = "", []
    for j in joints:
        go, tr = jid[j["name"]]
        nxt = [jid[c["name"]][1] for c in joints if c["parent"] == j["name"]]
        parent = j["parent"]
        local = [j["origin"][c] - (next(q for q in joints if q["name"] == parent)["origin"][c] if parent else 0.0) for c in range(3)]
        mine = list(nxt)
        if j["T"]:
            sgo, stri, smf, smr = (nid(f"{species}:{k}:{j['name']}") for k in ("skingo", "skintr", "skinmf", "skinmr"))
            mine.append(stri)
            skins.append(smr)
            docs += GO_DOC.format(go=sgo, layer=layer, name=j["name"] + "Skin",
                                  comps="".join(f"  - component: {{fileID: {c}}}\n" for c in (stri, smf, smr)))
            docs += TR_DOC.format(tr=stri, go=sgo, x=num(-j["origin"][0]), y=num(-j["origin"][1]), z=num(-j["origin"][2]), kids=" []", father=tr)
            docs += re.sub(r"guid: \w+, type: 2", f"guid: {guid('mesh:' + key + ':' + j['name'])}, type: 2",
                           re.sub(r"m_GameObject: \{fileID: \d+\}", f"m_GameObject: {{fileID: {sgo}}}", re.sub(r"&\d+", f"&{smf}", mf, count=1)), count=1)
            docs += re.sub(r"m_GameObject: \{fileID: \d+\}", f"m_GameObject: {{fileID: {sgo}}}", re.sub(r"&\d+", f"&{smr}", mr, count=1))
        mine += under[j["name"]]
        docs += GO_DOC.format(go=go, layer=layer, name=j["name"], comps=f"  - component: {{fileID: {tr}}}\n")
        docs += TR_DOC.format(tr=tr, go=go, x=num(local[0]), y=num(local[1]), z=num(local[2]),
                              kids="".join(f"\n  - {{fileID: {k}}}" for k in mine) if mine else " []",
                              father=jid[parent][1] if parent else BODY_TR)
    roots = [jid[j["name"]][1] for j in joints if not j["parent"]]
    if roots:
        once(r"(--- !u!4 &%d\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)*)" % BODY_TR,
             lambda m: m.group(1) + "".join(f"  - {{fileID: {k}}}\n" for k in roots) + m.group(2), "the body's children (joints)")
    if skins:
        once(r"(  RenderedObject: \{fileID: \d+\}\n)", lambda m: m.group(1) + "  additionalRenderedObjects:\n" + "".join(f"  - {{fileID: {k}}}\n" for k in skins),
             "the Spindle's rendered object")
    unity = spec.get("unity") or {}
    # GrownLimbDriver. With the wave in the shader ("gpu", the default) it turns no joint: it carries each prism along
    # the wave of the limb it sits on, from the same clock the shader reads. In "joints" mode it beats the joints.
    beat = [] if gpu else joints
    drv = nid(f"{species}:driver")
    once(r"(--- !u!1 &%d\n(?:.*\n)*?  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO,
         lambda m: m.group(1) + f"  - component: {{fileID: {drv}}}\n", "the root's components")
    rows = lambda label, items: f"  {label}:" + ("".join("\n  - " + i for i in items) if items else " []") + "\n"
    vec = lambda v: "{x: %s, y: %s, z: %s}" % tuple(num(c) for c in v)
    docs += (f"--- !u!114 &{drv}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
             f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
             f"  m_Script: {{fileID: 11500000, guid: {script_guid('GrownLimbDriver')}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n"
             + rows("joints", [f"{{fileID: {jid[j['name']][1]}}}" for j in beat])
             + rows("axes", [vec(j["axis"]) for j in beat])
             + rows("degrees", [num(j["degrees"]) for j in beat])
             + rows("phases", [num(j["phase"]) for j in beat])
             + f"  idleHz: {num(unity.get('idle_hz', spec['sway_hz']))}\n  fastHz: {num(unity.get('fast_hz', spec['sway_hz']))}\n  idleSweepShare: 0.6\n"
             + f"  speedForFullBeat: {num(spec['possession']['top_speed'] * 1.2)}\n"
             + rows("riders", [f"{{fileID: {tr}}}" for tr, _sw, _at in riders])
             + rows("riderSin", [vec(sw[0:3]) for _tr, sw, _at in riders])
             + rows("riderCos", [vec(sw[3:6]) for _tr, sw, _at in riders])
             + f"  waveHz: {num(spec['sway_hz'] if gpu else 0.0)}\n"
             + rows("riderHome", [vec(at) for _tr, _sw, at in riders]))
    if gpu:      # the turn (elements.TURN): the skins it is handed to, the body that banks, and the form's numbers
        turn = spec.get("turn") or EL_turn(spec)
        body_mr = int(re.match(r"--- !u!23 &(\d+)", mr).group(1))
        reach = BODY.reach(made)
        docs += (rows("skins", [f"{{fileID: {k}}}" for k in [body_mr] + skins])
                 + f"  bankRoot: {{fileID: {BODY_TR}}}\n  reach: {num(reach)}\n"
                 + f"  lagSeconds: {num(turn['lag'])}\n  maxLag: {num(turn['max_lag'])}\n  springHz: {num(turn['spring_hz'])}\n"
                 + f"  damping: {num(turn['damping'])}\n  strokeSeconds: {num(turn['stroke'])}\n  maxStroke: {num(turn['max_stroke'])}\n"
                 + f"  twistSeconds: {num(turn['twist'])}\n  maxTwist: {num(turn['max_twist'])}\n  bankSeconds: {num(turn['bank'])}\n"
                 + f"  maxBankDegrees: {num(turn['max_bank'])}\n")
    if unity.get("klass"):      # the species' own class: the script guid is swapped in place, the component keeps its fileID
        light = re.search(r"guid: (\w+)", read(SCRIPTS + "LightFauna.cs.meta")).group(1)
        once(r"(m_Script: \{fileID: 11500000, guid: )%s" % light, r"\g<1>" + script_guid(unity["klass"]), "LightFauna's script")
        pos = spec["possession"]      # the species' feel under a rider, as the lab flies it (RIDER_TURN 10, ease 2.5 are everyone's)
        mine = (f"  ramRadius: {num(unity.get('ram_radius', 20.0))}\n  riddenTurnScale: {num(pos.get('turn_rate', 10.0) / 10.0)}\n"
                f"  riddenSpeedEase: {num(pos.get('speed_ease', 2.5))}\n  carriesRiderUp: {1 if spec.get('steering_up') == 'carried' else 0}\n")
        once(r"(  data: \{fileID: 11400000, guid: \w+, type: 2\}\n)", r"\g<1>" + mine, "LightFauna.data (for the species' own fields)")
    if spec.get("eroded") == "authored":      # the Space form: grown at the law's girth, its prisms the law's needles (elements.py)
        once(r"(  data: \{fileID: 11400000, guid: \w+, type: 2\}\n)", r"\g<1>  spindleAuthoredEroded: 1\n", "LightFauna.data (for spindleAuthoredEroded)")
    return t + blocks + docs


def EL_turn(spec):
    import elements as EL
    return EL.turn_of(spec)


def sway_mode(spec):
    """Where the body's wave runs. "gpu" (the default): in the spindle shader, from the wave each mesh vertex carries;
    the joints only take the attack's whip. "joints": the old way, GrownLimbDriver beating the joints on the CPU and
    the shared SpindleMaterial; say `"sway": "joints"` in the spec's `unity` block to fall back to it."""
    return (spec.get("unity") or {}).get("sway", "gpu")


def data_so(key, spec):
    t = read(DONOR_DATA)
    pos = spec["possession"]
    t = re.sub(r"m_Name: .*", f"m_Name: {spec['species']}FaunaDataSO", t, count=1)
    for field, value in (("minSpeed", pos["top_speed"] * 0.6), ("maxSpeed", pos["top_speed"]), ("consumeRadius", pos["mouth_reach"]),
                         ("feedingClusterRadius", pos["mouth_reach"] * 0.3), ("attackRange", pos["mouth_reach"] * 0.4)):
        t, n = re.subn(r"(?m)^(  %s: ).*$" % field, r"\g<1>" + num(value), t, count=1)
        if n != 1:
            raise SystemExit(f"{DONOR_DATA}: no {field}")
    return t


def config(key, spec, e):
    t = read(DONOR_CFG.format(e=e))
    t = re.sub(r"m_Name: .*", f"m_Name: {spec['species']} Fauna {e}", t, count=1)
    t, n = re.subn(r"(FaunaPrefab: \{fileID: \d+, guid: )\w+", r"\g<1>" + guid("prefab:" + form_key(key, e)), t, count=1)
    if n != 1:
        raise SystemExit(f"{DONOR_CFG.format(e=e)}: no FaunaPrefab")
    scale = num(spec["elements"][e]["body_scale"])
    if re.search(r"(?m)^    BaseBodyScale: ", t):
        t = re.sub(r"(?m)^(    BaseBodyScale: ).*$", r"\g<1>" + scale, t, count=1)
    else:
        t, n = re.subn(r"(?m)^(  Variant:\n    Enabled: )\d\n", r"\g<1>1\n    BaseBodyScale: %s\n" % scale, t, count=1)
        if n != 1:
            raise SystemExit(f"{DONOR_CFG.format(e=e)}: no Variant block to state the body scale in")
    return t


def form_key(key, e):
    """The Time form is the base body and keeps the creature's own asset names; the others add their element."""
    return key if e == "Time" else f"{key}:{e}"


def network(text, key):
    for e in ELEMENTS:
        text = _network_one(text, form_key(key, e))
    return text


def _network_one(text, key):
    pg = guid("prefab:" + key)
    if pg in text:
        return text
    entry = (f"  - Override: 0\n    Prefab: {{fileID: {ROOT_GO}, guid: {pg},\n      type: 3}}\n"
             "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")
    m = re.search(r"(?m)^  List:\n", text)
    if not m:
        raise SystemExit(NETWORK + ": no List")
    return text[:m.end()] + entry + text[m.end():]


def emit(key):
    """([(form key, the form's spec, its body, its mesh, its own files)], the files every form shares)."""
    import elements as EL
    base = C.load(key)
    errs = C.check(base)
    if errs:
        raise SystemExit(f"{key} breaks the celestial laws: {errs[0]}")
    mesh_meta = lambda g: re.sub(r"guid: \w+", "guid: " + g, read(MESH_TEMPLATE + ".meta"), count=1)
    forms = []
    for e in ELEMENTS:
        fk = form_key(key, e)
        spec = EL.form(base, e)
        sp = base["species"] + ("" if e == "Time" else e)
        spec["species"] = sp
        made = BODY.from_spec(spec)
        V, T = body_mesh(made)
        core, joints, _home = joints_of(fk, spec, made)
        turn = dict(spec.get("turn") or EL_turn(spec), reach=BODY.reach(made)) if sway_mode(spec) == "gpu" else None
        out = {}
        for j in joints:
            if j["T"]:
                out[f"Assets/_Models/Fauna/{sp}{j['name']}.asset"] = mesh_asset(sp + j["name"], j, turn)
                out[f"Assets/_Models/Fauna/{sp}{j['name']}.asset.meta"] = mesh_meta(guid("mesh:" + fk + ":" + j["name"]))
        out[f"Assets/_Models/Fauna/{sp}Body.asset"] = mesh_asset(sp + "Body", core, turn)
        out[f"Assets/_Models/Fauna/{sp}Body.asset.meta"] = mesh_meta(guid("mesh:" + fk))
        if sway_mode(spec) == "gpu":
            out[GROWN_MATERIAL.format(sp=sp)] = material(spec)
            out[GROWN_MATERIAL.format(sp=sp) + ".meta"] = meta_of("Assets/_Graphics/Materials/SpindleMaterial.mat", guid("material:" + fk))
        out[f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab"] = prefab(fk, spec, made)
        out[f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab.meta"] = meta_of(DONOR, guid("prefab:" + fk))
        forms.append((fk, spec, made, (V, T), out))
    sp = base["species"]
    shared = {"Assets/_Graphics/Materials/Fauna.meta": read("Assets/_Graphics/Materials/Fauna.meta"),
              f"Assets/_SO_Assets/Light Fauna Data/{sp}FaunaDataSO.asset": data_so(key, base),
              f"Assets/_SO_Assets/Light Fauna Data/{sp}FaunaDataSO.asset.meta": meta_of(DONOR_DATA, guid("data:" + key)),
              NETWORK: network(read(NETWORK), key)}
    for e in ELEMENTS:
        shared[f"Assets/_SO_Assets/Lifeforms/{sp} Fauna {e}.asset"] = config(key, base, e)
        shared[f"Assets/_SO_Assets/Lifeforms/{sp} Fauna {e}.asset.meta"] = meta_of(DONOR_CFG.format(e=e), guid(f"config:{key}:{e}"))
    return forms, shared


def verify(key, spec, made, mesh, out):
    """What can be held without the editor. Every line is a thing that would otherwise fail silently in Unity."""
    problems = []
    sp = spec["species"]
    V, T = mesh
    counts = [mesh_counts(text) for path, text in out.items() if path.startswith("Assets/_Models/Fauna/") and path.endswith(".asset")]
    nv, nt = sum(c[0] for c in counts), sum(c[1] for c in counts)
    if nt != len(T) * 2 or nv < len(V) or any(c[2] != STRIDE for c in counts):
        problems.append(f"the mesh assets read back {nv} vertices and {nt} triangles; the spec makes {len(V)} (or more, at the cuts) and {len(T) * 2}")
    pf = out[f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab"]
    # every guid the prefab names has exactly one owner: a .meta in the repo, or one this run writes
    owners = dict(PR.guid_index())
    for path, text in out.items():
        if path.endswith(".meta"):
            owners[re.search(r"guid: (\w+)", text).group(1)] = path[:-5]
    donor_names = set(re.findall(r"guid: ([0-9a-f]{32})", read(DONOR)))      # package scripts (Netcode) live outside Assets/
    for g in sorted(set(re.findall(r"guid: ([0-9a-f]{32})", pf))):
        if g not in owners and g not in donor_names:
            problems.append(f"the prefab names guid {g}, which no asset owns")
    # the body it reads back as is the body the spec states
    tmp = os.path.join(REPO, f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab")
    on_disk = open(tmp).read() if os.path.exists(tmp) else None
    if on_disk == pf:
        root = PR.pose(PR.resolve(f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab"))
        got = {n.name: n for n in root.walk() if "HealthPrism" in n.scripts}
        for p in made["prisms"]:
            n = got.get(p["name"])
            if n is None:
                problems.append(f"prism {p['name']} is not in the prefab")
                continue
            if C.norm(C.sub(PR.world_pos(n), p["at"])) > 1e-3 or C.norm(C.sub(sorted(PR.world_scale(n)), sorted(p["size"]))) > 1e-3:
                problems.append(f"prism {p['name']} sits at {[round(v, 3) for v in PR.world_pos(n)]}, not {p['at']}")
            jointed = {j["name"] for j in joints_of(key, spec, made)[1]}
            if not (n.parent and ("Spindle" in n.parent.scripts or n.parent.name in jointed)):
                problems.append(f"prism {p['name']} hangs from neither the Spindle nor a joint: it would not move with the body")
        if len(got) != len(made["prisms"]):
            problems.append(f"{len(got)} prisms in the prefab, {len(made['prisms'])} in the spec")
        if not any("Crystal" in n.scripts for n in root.walk()):
            problems.append("no heart crystal in the prefab")
        if spec.get("unity"):
            import dossier as D
            limbs = D.attack_limbs(root)
            if len(limbs) < 3:
                problems.append(f"the game's attack finds {len(limbs)} limb(s) on the prefab (three or more whip; fewer lunge)")
            if spec["unity"].get("klass") and spec["unity"]["klass"] not in root.scripts:
                problems.append("the root is missing its own class")
        if "GrownLimbDriver" not in root.scripts:
            problems.append("the root is missing its limb driver")
        if "QuadFishSwimDriver" in [s for n in root.walk() for s in n.scripts]:
            problems.append("the donor's swim driver is still on the prefab")
    # the wave: every mesh vertex carries the wave the kit gave the wire vertex it was swept round, the material turns
    # the grown sway on at the form's tempo and the shear sway off, and the shader the material draws with has it wired
    core, cut, _home = joints_of(key, spec, made)
    worst, short = 0.0, []
    for name, part in [("Body", core)] + [(j["name"], j) for j in cut if j["T"]]:
        got = mesh_waves(out[f"Assets/_Models/Fauna/{sp}{name}.asset"])
        if len(got) != len(part["V"]):
            short.append(name)
            continue
        for (v, sw), want_v, want_s in zip(got, part["V"], part["S"]):
            worst = max(worst, max(abs(a - b) for a, b in zip(list(v) + list(sw), list(want_v) + list(want_s))))
    if short or worst > 1e-3:
        problems.append(f"the mesh assets do not carry the spec's vertices and waves: {short or ''} largest difference {worst:.4f}")
    if not any(any(abs(c) > 1e-6 for c in sw) for part in [core] + cut for sw in part["S"]):
        problems.append("no vertex of the body carries a wave: it would not move")
    if sway_mode(spec) == "gpu":
        mat = out.get(GROWN_MATERIAL.format(sp=sp), "")
        hz = re.search(r"_GrownFrequency: ([0-9.]+)", mat)
        if "_GrownSway: 1" not in mat or "_SwayAmplitude: 0\n" not in mat or not hz or abs(float(hz.group(1)) - 2 * math.pi * spec["sway_hz"]) > 1e-3:
            problems.append("the form's material does not turn the grown sway on at the form's tempo with the shear sway off")
        if guid("material:" + key) not in pf or SPINDLE_MATERIAL in pf:
            problems.append("a renderer of the prefab does not draw with the form's own material")
        sys.path.insert(0, os.path.join(REPO, "Tools", "Shaders"))
        import wire_spindle_grown_sway as GS
        for g in ("SpindleGraph", "SpindleDepthGraph"):      # what a body draws with near, and what SpindleLodSystem.AddBody swaps it to
            if GS.validate(GS.load_docs(os.path.join(REPO, f"Assets/_Graphics/Materials/Graphs/{g}.shadergraph")), True):
                problems.append(f"{g} has no grown sway wired: run Tools/Shaders/wire_spindle_grown_sway.py, then wire_spindle_lod.py")
        if pf.count("  riders:") != 1 or "waveHz: 0\n" in pf:
            problems.append("the limb driver carries no riders or no tempo")
        homes = re.search(r"  riderHome:\n((?:  - \{[^}]*\}\n)+)", pf)
        seated = len(re.findall(r"  - ", homes.group(1))) if homes else 0
        if seated != len(made["prisms"]) or pf.count("  skins:") != 1 or f"bankRoot: {{fileID: {BODY_TR}}}" not in pf or "lagSeconds:" not in pf:
            problems.append(f"the limb driver cannot answer a turn: {seated} rider homes for {len(made['prisms'])} prisms, or no skins, bank root or turn numbers")
        n_skins = 1 + sum(1 for j in cut if j["T"])
        listed = re.search(r"  skins:\n((?:  - \{fileID: -?\d+\}\n)+)", pf)
        if not listed or listed.group(1).count("fileID") != n_skins:
            problems.append(f"the limb driver is handed {listed.group(1).count('fileID') if listed else 0} skins; the body draws with {n_skins}")
    # Space: the config says ErodeSpindle as every species' does; a jointed body is authored eroded and its prefab says so
    flagged = "spindleAuthoredEroded: 1" in pf
    if flagged != (spec.get("eroded") == "authored"):
        problems.append("only the Space form's prefab says spindleAuthoredEroded, and it must")
    if spec.get("element") == "Space" and on_disk == pf and not flagged:
        import erosion
        tear = erosion.runtime(sp)[1]
        if tear > erosion.GAP:
            problems.append(f"the game's Space erosion would tear this body {tear:.1f} u at its joints: author the form eroded (elements.py)")
    if "GlobalObjectIdHash: 2720972683" in pf:
        problems.append("the prefab still carries the donor's network identity")
    return problems


def self_test():
    fails = []
    forms, _shared = emit("ray")
    key, spec, made, mesh, out = next(f for f in forms if f[1]["species"] == "Ray")      # the Time form: the base body
    if verify(key, spec, made, mesh, out):
        fails.append("the ray as emitted does not verify: " + "; ".join(verify(key, spec, made, mesh, out)[:2]))
    if len({o for f in forms for o in f[4]}) != sum(len(f[4]) for f in forms):
        fails.append("two element forms write the same file")
    bad = dict(out)
    body = "Assets/_Models/Fauna/RayBody.asset"
    bad[body] = re.sub(r"m_VertexCount: (\d+)", lambda m: "m_VertexCount: %d" % (int(m.group(1)) - 1), bad[body], count=1)
    try:
        if not verify(key, spec, made, mesh, bad):
            fails.append("a mesh asset with the wrong vertex count verified")
    except SystemExit:
        pass
    bad = dict(out)
    mat = GROWN_MATERIAL.format(sp="Ray")
    bad[mat] = bad[mat].replace("_GrownSway: 1", "_GrownSway: 0", 1)
    if not any("grown sway" in p for p in verify(key, spec, made, mesh, bad)):
        fails.append("a material with the grown sway off verified")
    bad = dict(out)
    tip = "Assets/_Models/Fauna/RayVaneA2.asset"
    blob = bytearray(bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]*)", bad[tip]).group(1)))
    blob[48:72] = bytes(24)      # the first vertex's wave, flattened
    bad[tip] = re.sub(r"_typelessdata: [0-9a-f]*", "_typelessdata: " + blob.hex(), bad[tip], count=1)
    if not any("waves" in p for p in verify(key, spec, made, mesh, bad)):
        fails.append("a mesh whose vertex lost its wave verified")
    bad = dict(out)
    pf = "Assets/_Prefabs/FloraAndFauna/Ray.prefab"
    bad[pf] = bad[pf].replace(guid("mesh:ray"), "0123456789abcdef0123456789abcdef", 1)
    if not any("no asset owns" in p for p in verify(key, spec, made, mesh, bad)):
        fails.append("a prefab naming a mesh guid nobody owns verified")
    return fails


def main(argv):
    if "--self-test" in argv:
        fails = self_test()
        for f in fails:
            print("FAIL:", f)
        print("unity_port self-test:", "ok" if not fails else f"{len(fails)} problem(s)")
        return 1 if fails else 0
    keys = [a for a in argv if not a.startswith("-")]
    if not keys:
        print(__doc__)
        return 2
    bad = 0
    for key in keys:
        forms, shared = emit(key)
        files = dict(shared)
        for f in forms:
            files.update(f[4])
        if "--dry" in argv:      # make everything and check what can be checked in memory; write nothing
            drift = []
            print(f"dry run: {len(files)} file(s) would be written")
        elif "--check" in argv:
            drift = [p for p, text in files.items() if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p)).read() != text]
            for p in drift:
                print("DRIFT", p)
        else:
            drift = []
            n = 0
            for p, text in files.items():
                full = os.path.join(REPO, p)
                if not os.path.exists(full) or open(full).read() != text:
                    open(full, "w").write(text)
                    n += 1
            # what an earlier run of this tool wrote for this species and this run no longer makes (a limb that left a form)
            mine = re.compile(r"^(%s)[A-Z][A-Za-z]*\d\.asset(\.meta)?$" % "|".join(re.escape(f[1]["species"]) for f in forms))
            folder = os.path.join(REPO, "Assets/_Models/Fauna")
            gone = [f for f in sorted(os.listdir(folder)) if mine.match(f) and f"Assets/_Models/Fauna/{f}" not in files]
            for f in gone:
                os.remove(os.path.join(folder, f))
            print(f"wrote {n} file(s)" + (f", removed {len(gone)} this species no longer makes" if gone else ""))
        for fk, spec, made, mesh, out in forms:
            problems = verify(fk, spec, made, mesh, dict(out, **{k: v for k, v in shared.items() if k.endswith(".meta")}))
            for p in problems:
                print("FAIL", spec["species"], p)
            print(f"{spec['species']}: {len(mesh[0])} vertices, {len(mesh[1]) * 2} triangles (double-sided), {len(made['prisms'])} prisms; "
                  + ("ok" if not problems else f"{len(problems)} problem(s)"))
            bad += bool(problems)
        base = C.load(key)
        if not base.get("forms"):
            print(f"FAIL {base['species']}: it has no element forms of its own (/fauna-maker step 6.5); a creature goes to Unity as four")
            bad += 1
        for e in ELEMENTS:
            cfg = shared[f"Assets/_SO_Assets/Lifeforms/{base['species']} Fauna {e}.asset"]
            want = base["elements"][e]["body_scale"]
            got = re.search(r"(?m)^    BaseBodyScale: ([0-9.]+)", cfg)
            if not got or abs(float(got.group(1)) - want) > 1e-4:
                print(f"FAIL {base['species']} Fauna {e}: BaseBodyScale {got.group(1) if got else 'missing'}, the element law says {want}")
                bad += 1
            if (e == "Space") != bool(re.search(r"(?m)^    ErodeSpindle: 1", cfg)):
                print(f"FAIL {base['species']} Fauna {e}: ErodeSpindle is on for Space and only for Space")
                bad += 1
        bad += bool(drift)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
