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

What it does NOT do (each is a later step, listed in Docs/UNITY_VERIFICATION_CHECKLIST.md): the per-vertex wave and
the travelling light (the body wears the shared SpindleMaterial, so it sways by the game's one shear); a carried up
(LightFauna steers with world up); the creature's own ride (glide, feeding on the wing); any cell's spawn profile,
the Codex, the Spawn Matrix. Nothing here has been opened in the Unity editor.
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
    if len(blob) != size * 2 or ni != int(re.search(r"indexCount: (\d+)", text).group(1)):
        raise SystemExit("the mesh asset's sizes do not agree with its data")
    return nv, ni // 3, size // max(nv, 1)


# ------------------------------------------------------------------ the prefab

def prism_block(species, i, p, block_guid):
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
           f"    serializedVersion: 3\n    m_TransformParent: {{fileID: {BODY_TR}}}\n    m_Modifications:\n{mods}"
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
    blocks, kids = "", []
    for i, p in enumerate(made["prisms"]):
        doc, tr = prism_block(species, i, p, DANGER_BLOCK if p["kind"] == "danger" else health_guid)
        blocks += doc
        kids.append(tr)

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
    return t + blocks


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
    out = {f"Assets/_Models/Fauna/{sp}Body.asset": mesh_asset(sp + "Body", V, T),
           f"Assets/_Models/Fauna/{sp}Body.asset.meta": re.sub(r"guid: \w+", "guid: " + guid("mesh:" + key), read(MESH_TEMPLATE + ".meta"), count=1),
           f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab": prefab(key, spec, made),
           f"Assets/_Prefabs/FloraAndFauna/{sp}.prefab.meta": meta_of(DONOR, guid("prefab:" + key)),
           f"Assets/_SO_Assets/Light Fauna Data/{sp}FaunaDataSO.asset": data_so(key, spec),
           f"Assets/_SO_Assets/Light Fauna Data/{sp}FaunaDataSO.asset.meta": meta_of(DONOR_DATA, guid("data:" + key)),
           NETWORK: network(read(NETWORK), key)}
    for e in ELEMENTS:
        out[f"Assets/_SO_Assets/Lifeforms/{sp} Fauna {e}.asset"] = config(key, spec, e)
        out[f"Assets/_SO_Assets/Lifeforms/{sp} Fauna {e}.asset.meta"] = meta_of(DONOR_CFG.format(e=e), guid(f"config:{key}:{e}"))
    return spec, made, (V, T), out


def verify(key, spec, made, mesh, out):
    """What can be held without the editor. Every line is a thing that would otherwise fail silently in Unity."""
    problems = []
    sp = spec["species"]
    V, T = mesh
    nv, nt, stride = mesh_counts(out[f"Assets/_Models/Fauna/{sp}Body.asset"])
    if (nv, nt, stride) != (len(V), len(T) * 2, 48):
        problems.append(f"the mesh asset reads back {nv} vertices, {nt} triangles, {stride} bytes a vertex; the spec makes {len(V)}, {len(T) * 2}, 48")
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
            if not (n.parent and "Spindle" in n.parent.scripts):
                problems.append(f"prism {p['name']} is not a direct child of the Spindle: it would not sway (/fauna section 4)")
        if len(got) != len(made["prisms"]):
            problems.append(f"{len(got)} prisms in the prefab, {len(made['prisms'])} in the spec")
        if not any("Crystal" in n.scripts for n in root.walk()):
            problems.append("no heart crystal in the prefab")
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
    bad[key] = bad[key].replace("m_VertexCount: %d" % len(mesh[0]), "m_VertexCount: %d" % (len(mesh[0]) - 1), 1)
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
