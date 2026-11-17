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


def joints_of(key, spec, made):
    """The body cut at its hinges (the spec's `unity` block): (core vertices, core triangles, joints, prism homes).
    A joint is {name, parent, origin, axis, degrees, phase, V, T}; its mesh is in its own space (origin subtracted).
    A triangle goes to the last hinge its centre has passed, walking out from the body; before the first, to the core."""
    rows = {name: (v, t) for name, _d, _nv, _nt, v, t in SP.creature_mesh(key)}
    unity = spec.get("unity") or {}
    claimed, joints, home = set(), [], {}
    core_v, core_t = [], []

    def add(dst_v, dst_t, remap, tri, src, origin):
        out = []
        for i in tri:
            if i not in remap:
                remap[i] = len(dst_v)
                dst_v.append([src[i][c] - origin[c] for c in range(3)])
            out.append(remap[i])
        dst_t.append(tuple(out))

    for limb in unity.get("limbs", []):
        ax = 0 if limb["cut"] == "x" else 2
        sgn = 1.0 if limb["hinges"][0] >= 0 else -1.0
        seg = lambda c: sum(1 for h in limb["hinges"] if c * sgn >= h * sgn)
        chain = []
        for j, h in enumerate(limb["hinges"]):
            origin = [h, 0.0, 0.0] if ax == 0 else [0.0, 0.0, h]
            chain.append({"name": f"{limb['name']}{j}", "parent": chain[-1]["name"] if chain else None, "origin": origin, "axis": limb["axis"],
                          "degrees": limb["degrees"][j], "phase": limb["lag"] * j, "V": [], "T": [], "_map": {}})
        for w in limb["wires"]:
            claimed.add(w)
            V, T = rows[w]
            cmap = {}
            for tri in T:
                k = seg(sum(V[i][ax] for i in tri) / 3.0)
                if k == 0:
                    add(core_v, core_t, cmap, tri, V, [0.0, 0.0, 0.0])
                else:
                    jt = chain[k - 1]
                    add(jt["V"], jt["T"], jt["_map"].setdefault(w, {}), tri, V, jt["origin"])
        for p in made["prisms"]:
            if p["limb"] in limb["wires"]:
                k = seg(p["at"][ax])
                if k:
                    home[p["name"]] = chain[k - 1]
        joints += chain
    for name, (V, T) in rows.items():
        if name not in claimed:
            cmap = {}
            for tri in T:
                add(core_v, core_t, cmap, tri, V, [0.0, 0.0, 0.0])
    return core_v, core_t, joints, home


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


def body_mesh(key):
    """(vertices, triangles): every grown limb and membrane of the body as one mesh, in the creature's own space."""
    V, T = [], []
    for _name, _design, _nv, _nt, v, t in SP.creature_mesh(key):
        base = len(V)
        V += v
        T += [(a + base, b + base, c + base) for a, b, c in t]
    return V, T


def mesh_asset(name, V, T):
    """A native Unity Mesh (the format of Assets/_Models/Testing/Prism.asset: position, normal, tangent, uv0; 48 bytes
    a vertex; 16-bit indices). Double-sided: every triangle is listed both ways, because a ribbon has one face."""
    if len(V) > 65535:
        raise SystemExit(f"{name}: {len(V)} vertices will not fit 16-bit indices")
    normals = [[0.0, 0.0, 0.0] for _ in V]
    for a, b, c in T:
        n = C.cross(C.sub(V[b], V[a]), C.sub(V[c], V[a]))
        for i in (a, b, c):
            normals[i] = C.add(normals[i], n)
    lo = [min(v[i] for v in V) for i in range(3)]
    hi = [max(v[i] for v in V) for i in range(3)]
    zspan = max(hi[2] - lo[2], 1e-9)
    data = bytearray()
    for v, n in zip(V, normals):
        n = C.unit(n) if C.norm(n) > 1e-12 else [0.0, 1.0, 0.0]
        data += struct.pack("<3f3f4f2f", v[0], v[1], v[2], n[0], n[1], n[2], 1.0, 0.0, 0.0, 1.0, (hi[2] - v[2]) / zspan, 0.0)
    idx = bytearray()
    for a, b, c in T:
        idx += struct.pack("<6H", a, b, c, a, c, b)
    t = read(MESH_TEMPLATE)
    centre = "{x: %s, y: %s, z: %s}" % tuple(num((lo[i] + hi[i]) / 2) for i in range(3))
    extent = "{x: %s, y: %s, z: %s}" % tuple(num((hi[i] - lo[i]) / 2) for i in range(3))
    t = re.sub(r"m_Name: .*", "m_Name: " + name, t, count=1)
    t = re.sub(r"indexCount: \d+", "indexCount: %d" % (len(T) * 6), t, count=1)
    t = re.sub(r"(firstVertex: 0\n\s+vertexCount: )\d+", r"\g<1>%d" % len(V), t, count=1)
    t = re.sub(r"m_VertexCount: \d+", "m_VertexCount: %d" % len(V), t, count=1)
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
    if len(blob) != size * 2 or size != nv * 48 or ni != int(re.search(r"indexCount: (\d+)", text).group(1)):
        raise SystemExit("the mesh asset's sizes do not agree with its data")
    return nv, ni // 3, size // max(nv, 1)


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
    _cv, _ct, joints, home = joints_of(key, spec, made)
    jid = {j["name"]: (nid(f"{species}:go:{j['name']}"), nid(f"{species}:tr:{j['name']}")) for j in joints}
    blocks, kids, under = "", [], {j["name"]: [] for j in joints}
    for i, p in enumerate(made["prisms"]):
        j = home.get(p["name"])
        doc, tr = prism_block(species, i, p, DANGER_BLOCK if p["kind"] == "danger" else health_guid,
                              jid[j["name"]][1] if j else BODY_TR, j["origin"] if j else (0.0, 0.0, 0.0))
        blocks += doc
        (under[j["name"]] if j else kids).append(tr)
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
    once(r"(m_Materials:\n  - \{fileID: 2100000, guid: )\w+", r"\g<1>" + SPINDLE_MATERIAL, "the body's material")
    once(r"(  data: \{fileID: 11400000, guid: )\w+", r"\g<1>" + guid("data:" + key), "LightFauna.data")
    import author_stoat_assets as stoat      # the one GlobalObjectId hash implementation (self-tested there)
    once(r"(--- !u!114 &%d\n(?:.*\n)*?  GlobalObjectIdHash: )\d+" % NETOBJ,
         r"\g<1>%d" % stoat.global_object_id_hash(guid("prefab:" + key), NETOBJ), "the network identity")
    if not joints:
        return t + blocks
    # the jointed body: each joint a plain transform (so the attack finds it as a limb), its skin a child that draws
    # its piece of the mesh, the next joint its FIRST child (the attack follows first children down a limb)
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
            docs += TR_DOC.format(tr=stri, go=sgo, x=0, y=0, z=0, kids=" []", father=tr)
            docs += re.sub(r"guid: \w+, type: 2", f"guid: {guid('mesh:' + key + ':' + j['name'])}, type: 2",
                           re.sub(r"m_GameObject: \{fileID: \d+\}", f"m_GameObject: {{fileID: {sgo}}}", re.sub(r"&\d+", f"&{smf}", mf, count=1)), count=1)
            docs += re.sub(r"m_GameObject: \{fileID: \d+\}", f"m_GameObject: {{fileID: {sgo}}}", re.sub(r"&\d+", f"&{smr}", mr, count=1))
        mine += under[j["name"]]
        docs += GO_DOC.format(go=go, layer=layer, name=j["name"], comps=f"  - component: {{fileID: {tr}}}\n")
        docs += TR_DOC.format(tr=tr, go=go, x=num(local[0]), y=num(local[1]), z=num(local[2]),
                              kids="".join(f"\n  - {{fileID: {k}}}" for k in mine) if mine else " []",
                              father=jid[parent][1] if parent else BODY_TR)
    roots = [jid[j["name"]][1] for j in joints if not j["parent"]]
    once(r"(--- !u!4 &%d\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)*)" % BODY_TR,
         lambda m: m.group(1) + "".join(f"  - {{fileID: {k}}}\n" for k in roots) + m.group(2), "the body's children (joints)")
    once(r"(  RenderedObject: \{fileID: \d+\}\n)", lambda m: m.group(1) + "  additionalRenderedObjects:\n" + "".join(f"  - {{fileID: {k}}}\n" for k in skins),
         "the Spindle's rendered object")
    unity = spec["unity"]
    drv = nid(f"{species}:driver")
    once(r"(--- !u!1 &%d\n(?:.*\n)*?  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO,
         lambda m: m.group(1) + f"  - component: {{fileID: {drv}}}\n", "the root's components")
    docs += (f"--- !u!114 &{drv}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n"
             f"  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
             f"  m_Script: {{fileID: 11500000, guid: {script_guid('GrownLimbDriver')}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n"
             "  joints:\n" + "".join(f"  - {{fileID: {jid[j['name']][1]}}}\n" for j in joints)
             + "  axes:\n" + "".join("  - {x: %s, y: %s, z: %s}\n" % tuple(num(v) for v in j["axis"]) for j in joints)
             + "  degrees:\n" + "".join(f"  - {num(j['degrees'])}\n" for j in joints)
             + "  phases:\n" + "".join(f"  - {num(j['phase'])}\n" for j in joints)
             + f"  idleHz: {num(unity.get('idle_hz', 0.38))}\n  fastHz: {num(unity.get('fast_hz', 0.9))}\n  idleSweepShare: 0.6\n"
             + f"  speedForFullBeat: {num(spec['possession']['top_speed'] * 1.2)}\n")
    if unity.get("klass"):      # the species' own class: the script guid is swapped in place, the component keeps its fileID
        light = re.search(r"guid: (\w+)", read(SCRIPTS + "LightFauna.cs.meta")).group(1)
        once(r"(m_Script: \{fileID: 11500000, guid: )%s" % light, r"\g<1>" + script_guid(unity["klass"]), "LightFauna's script")
        once(r"(  data: \{fileID: 11400000, guid: \w+, type: 2\}\n)", r"\g<1>" + f"  ramRadius: {num(unity.get('ram_radius', 20.0))}\n", "LightFauna.data (for ramRadius)")
    return t + blocks + docs


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
    t, n = re.subn(r"(FaunaPrefab: \{fileID: \d+, guid: )\w+", r"\g<1>" + guid("prefab:" + key), t, count=1)
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


def network(text, key):
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
    spec = C.load(key)
    errs = C.check(spec)
    if errs:
        raise SystemExit(f"{key} breaks the celestial laws: {errs[0]}")
    made = BODY.from_spec(spec)
    sp = spec["species"]
    V, T = body_mesh(key)
    core_v, core_t, joints, _home = joints_of(key, spec, made)
    mesh_meta = lambda g: re.sub(r"guid: \w+", "guid: " + g, read(MESH_TEMPLATE + ".meta"), count=1)
    out = {}
    for j in joints:
        if j["T"]:
            out[f"Assets/_Models/Fauna/{sp}{j['name']}.asset"] = mesh_asset(sp + j["name"], j["V"], j["T"])
            out[f"Assets/_Models/Fauna/{sp}{j['name']}.asset.meta"] = mesh_meta(guid("mesh:" + key + ":" + j["name"]))
    out.update({f"Assets/_Models/Fauna/{sp}Body.asset": mesh_asset(sp + "Body", core_v, core_t),
           f"Assets/_Models/Fauna/{sp}Body.asset.meta": re.sub(r"guid: \w+", "guid: " + guid("mesh:" + key), read(MESH_TEMPLATE + ".meta"), count=1),
           f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab": prefab(key, spec, made),
           f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab.meta": meta_of(DONOR, guid("prefab:" + key)),
           f"Assets/_SO_Assets/Light Fauna Data/{sp}FaunaDataSO.asset": data_so(key, spec),
           f"Assets/_SO_Assets/Light Fauna Data/{sp}FaunaDataSO.asset.meta": meta_of(DONOR_DATA, guid("data:" + key)),
           NETWORK: network(read(NETWORK), key)})
    for e in ELEMENTS:
        out[f"Assets/_SO_Assets/Lifeforms/{sp} Fauna {e}.asset"] = config(key, spec, e)
        out[f"Assets/_SO_Assets/Lifeforms/{sp} Fauna {e}.asset.meta"] = meta_of(DONOR_CFG.format(e=e), guid(f"config:{key}:{e}"))
    return spec, made, (V, T), out


def verify(key, spec, made, mesh, out):
    """What can be held without the editor. Every line is a thing that would otherwise fail silently in Unity."""
    problems = []
    sp = spec["species"]
    V, T = mesh
    counts = [mesh_counts(text) for path, text in out.items() if path.startswith("Assets/_Models/Fauna/") and path.endswith(".asset")]
    nv, nt = sum(c[0] for c in counts), sum(c[1] for c in counts)
    if nt != len(T) * 2 or nv < len(V) or any(c[2] != 48 for c in counts):
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
            jointed = {j["name"] for j in joints_of(key, spec, made)[2]}
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
            if "GrownLimbDriver" not in root.scripts or (spec["unity"].get("klass") and spec["unity"]["klass"] not in root.scripts):
                problems.append("the root is missing its limb driver or its own class")
        if "QuadFishSwimDriver" in [s for n in root.walk() for s in n.scripts]:
            problems.append("the donor's swim driver is still on the prefab")
    if "GlobalObjectIdHash: 2720972683" in pf:
        problems.append("the prefab still carries the donor's network identity")
    return problems


def self_test():
    fails = []
    spec, made, mesh, out = emit("ray")
    if verify("ray", spec, made, mesh, out):
        fails.append("the ray as emitted does not verify: " + "; ".join(verify("ray", spec, made, mesh, out)[:2]))
    bad = dict(out)
    key = "Assets/_Models/Fauna/RayBody.asset"
    bad[key] = re.sub(r"m_VertexCount: (\d+)", lambda m: "m_VertexCount: %d" % (int(m.group(1)) - 1), bad[key], count=1)
    try:
        if not verify("ray", spec, made, mesh, bad):
            fails.append("a mesh asset with the wrong vertex count verified")
    except SystemExit:
        pass
    bad = dict(out)
    key = "Assets/_Prefabs/FloraAndFauna/Ray.prefab"
    bad[key] = bad[key].replace(guid("mesh:ray"), "0123456789abcdef0123456789abcdef", 1)
    if not any("no asset owns" in p for p in verify("ray", spec, made, mesh, bad)):
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
        spec, made, mesh, out = emit(key)
        if "--check" in argv:
            drift = [p for p, text in out.items() if not os.path.exists(os.path.join(REPO, p)) or open(os.path.join(REPO, p)).read() != text]
            for p in drift:
                print("DRIFT", p)
        else:
            for p, text in out.items():
                full = os.path.join(REPO, p)
                if not os.path.exists(full) or open(full).read() != text:
                    open(full, "w").write(text)
                    print("wrote", p)
            drift = []
        problems = verify(key, spec, made, mesh, out)
        for p in problems:
            print("FAIL", p)
        print(f"{spec['species']}: {len(mesh[0])} vertices, {len(mesh[1]) * 2} triangles (double-sided), {len(made['prisms'])} prisms; "
              + ("ok" if not (drift or problems) else f"{len(drift)} drifted, {len(problems)} problem(s)"))
        bad += bool(drift or problems)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
