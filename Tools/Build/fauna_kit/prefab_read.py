#!/usr/bin/env python3
"""Flatten a Unity prefab (nested prefab and model instances resolved) into one tree of posed nodes.

    python3 Tools/Build/fauna_kit/prefab_read.py Assets/_Prefabs/FloraAndFauna/QuadFish.prefab   # print the tree

A READER (it writes nothing). It is the half of the fauna dossier that answers "where is everything on this
creature" without the editor: every GameObject with its local and world pose, the scripts on it (by class name), and
the mesh it draws. What it resolves:
- plain documents (GameObject, Transform, MonoBehaviour, MeshFilter, SkinnedMeshRenderer);
- nested prefab instances, recursively, with their overrides (pose, name, active). An object of a nested instance
  is addressed from outside by `source fileID XOR instance fileID` (measured on the shipped prefabs: every stripped
  document obeys it; --self-test asserts it), which is how an override or a child finds its target;
- model (FBX) instances: the hierarchy comes from fbx_model.py; the root pose from the instance's overrides. An
  FBX's inner fileIDs are name hashes this reader does not derive (/fauna section 7), so a child parented to an
  inner model node is attached to the model root and FLAGGED (`unresolved_parent`), never guessed.
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, HERE)
import fbx_model as fm  # noqa: E402

MASK = (1 << 63) - 1
_GUIDS = None


def guid_index():
    """guid -> repo-relative asset path (every .meta under Assets/)."""
    global _GUIDS
    if _GUIDS is None:
        _GUIDS = {}
        for dp, _dn, fn in os.walk(os.path.join(REPO, "Assets")):
            for f in fn:
                if not f.endswith(".meta"):
                    continue
                try:
                    head = open(os.path.join(dp, f), errors="replace").read(200)
                except OSError:
                    continue
                m = re.search(r"guid: ([0-9a-f]{32})", head)
                if m:
                    _GUIDS[m.group(1)] = os.path.relpath(os.path.join(dp, f)[:-5], REPO)
    return _GUIDS


def split_docs(text):
    """[(class id, fileID, stripped, body)] in file order."""
    out = []
    parts = re.split(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", text, flags=re.M)
    for i in range(1, len(parts), 4):
        out.append((parts[i], int(parts[i + 1]), bool(parts[i + 2]), parts[i + 3]))
    return out


def _ref(body, key):
    m = re.search(r"^\s*%s: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?" % re.escape(key), body, re.M)
    return (int(m.group(1)), m.group(2)) if m else (0, None)


def _vec(body, key, n):
    m = re.search(r"^\s*%s: \{([^}]*)\}" % re.escape(key), body, re.M)
    if not m:
        return None
    vals = dict((k.strip(), float(v)) for k, v in (kv.split(":") for kv in m.group(1).split(",")))
    return [vals.get(a, 0.0) for a in "xyzw"[:n]]


def _mods(body):
    """[(target fileID, target guid, propertyPath, value, objectReference fileID)] of a PrefabInstance."""
    out = []
    lines = body.split("\n")
    i = 0
    while i < len(lines):
        m = re.match(r"\s*- target: \{fileID: (-?\d+), guid: ([0-9a-f]{32})", lines[i])
        if m:
            path = value = None
            obj = 0
            j = i + 1
            while j < len(lines) and not re.match(r"\s*- target:", lines[j]) and not re.match(r"\s{4}m_\w+:", lines[j]):
                pm = re.match(r"\s*propertyPath: (.*)$", lines[j])
                vm = re.match(r"\s*value: ?(.*)$", lines[j])
                om = re.match(r"\s*objectReference: \{fileID: (-?\d+)", lines[j])
                if pm:
                    path = pm.group(1).strip()
                elif vm:
                    value = vm.group(1).strip()
                elif om:
                    obj = int(om.group(1))
                j += 1
            out.append((int(m.group(1)), m.group(2), path, value, obj))
            i = j
        else:
            i += 1
    return out


class Node:
    __slots__ = ("name", "t", "q", "s", "parent", "children", "scripts", "mesh", "active", "ids", "source",
                 "unresolved_parent", "world", "fields", "bone", "overrides", "inferred_parent")

    def __init__(self, name):
        self.name = name
        self.t, self.q, self.s = [0.0, 0.0, 0.0], [0.0, 0.0, 0.0, 1.0], [1.0, 1.0, 1.0]
        self.parent = None
        self.children = []
        self.scripts = []          # class names of the MonoBehaviours on it
        self.fields = {}           # class name -> the MonoBehaviour's body text
        self.mesh = None           # (asset path, mesh name or fileID)
        self.active = True
        self.ids = {}              # fileID as seen from the file being read -> 'go' | 'tr' | 'comp'
        self.source = None         # the prefab or model this node came from
        self.unresolved_parent = False
        self.world = None
        self.bone = False          # a model's LimbNode
        self.overrides = {}        # propertyPath -> value, for the overrides that are not a pose, a name or active
        self.inferred_parent = None

    def add(self, child):
        child.parent = self
        self.children.append(child)

    def walk(self):
        yield self
        for c in self.children:
            yield from c.walk()

    def path(self):
        return (self.parent.path() + "/" if self.parent else "") + self.name


def _from_fbx(path):
    """A model file as a node tree (Unity names its root after the file; one top node becomes the root)."""
    model = fm.load(os.path.join(REPO, path))
    base = os.path.splitext(os.path.basename(path))[0]
    by_mesh = {me["node"]: me for me in model["meshes"]}

    def build(mid):
        m = model["nodes"][mid]
        n = Node(m["name"])
        n.t, n.q, n.s = list(m["t"]), list(m["q"]), list(m["s"])
        n.source = path
        n.bone = m["kind"] == "LimbNode"
        if mid in by_mesh:
            n.mesh = (path, by_mesh[mid]["name"])
        for c in m["children"]:
            n.add(build(c))
        return n

    roots = model["roots"]
    if len(roots) == 1:
        root = build(roots[0])
        root.name = base
    else:
        root = Node(base)
        root.source = path
        for r in roots:
            root.add(build(r))
    return root


def resolve(path, _depth=0):
    """The flattened tree of a prefab or model asset (repo-relative path)."""
    if path.lower().endswith(".fbx"):
        return _from_fbx(path)
    guids = guid_index()
    docs = split_docs(open(os.path.join(REPO, path), errors="replace").read())
    by_id = {fid: (cls, stripped, body) for cls, fid, stripped, body in docs}
    go_node, tr_node = {}, {}
    exposed = {}      # fileID (as seen in THIS file) -> Node, for transforms; and gos
    exposed_go = {}

    # 1 own GameObjects and Transforms
    for cls, fid, stripped, body in docs:
        if cls == "1" and not stripped:
            m = re.search(r"^\s*m_Name: (.*)$", body, re.M)
            n = Node(m.group(1).strip() if m else "?")
            a = re.search(r"^\s*m_IsActive: (\d)", body, re.M)
            n.active = (a.group(1) == "1") if a else True
            n.source = path
            go_node[fid] = n
            exposed_go[fid] = n
    for cls, fid, stripped, body in docs:
        if cls in ("4", "224") and not stripped:
            go, _ = _ref(body, "m_GameObject")
            n = go_node.get(go)
            if n is None:
                continue
            n.t = _vec(body, "m_LocalPosition", 3) or n.t
            n.q = _vec(body, "m_LocalRotation", 4) or n.q
            n.s = _vec(body, "m_LocalScale", 3) or n.s
            tr_node[fid] = n
            exposed[fid] = n

    # 2 nested instances
    inst_root = {}
    model_root_target = {}
    for cls, fid, stripped, body in docs:
        if cls != "1001":
            continue
        _f, sguid = _ref(body, "m_SourcePrefab")
        src = guids.get(sguid)
        if not src or _depth > 8:
            continue
        sub = resolve(src, _depth + 1)
        is_model = src.lower().endswith(".fbx")
        inner = {}       # inner exposed id -> node (transforms and gos share one id space here)
        for n in sub.walk():
            for iid in n.ids:
                inner[iid] = n
        mods = _mods(body)
        root_tr_target = None
        if is_model:
            for tfid, _g, p, _v, _o in mods:
                if p and p.startswith("m_LocalPosition"):
                    root_tr_target = tfid
                    break
        if is_model:
            model_root_target[fid] = root_tr_target
        for tfid, _g, p, v, _o in mods:
            if p is None:
                continue
            target = inner.get(tfid)
            if target is None and is_model and tfid == root_tr_target:
                target = sub
            if target is None and p == "m_Name" and is_model:
                target = sub
            if target is None:
                continue
            try:
                if p.startswith("m_LocalPosition."):
                    target.t["xyz".index(p[-1])] = float(v)
                elif p.startswith("m_LocalRotation."):
                    target.q["xyzw".index(p[-1])] = float(v)
                elif p.startswith("m_LocalScale."):
                    target.s["xyz".index(p[-1])] = float(v)
                elif p == "m_Name":
                    target.name = v
                elif p == "m_IsActive":
                    target.active = v == "1"
                elif not p.startswith("m_"):
                    target.overrides[p] = v
            except (ValueError, IndexError):
                pass
        # re-expose the instance's objects under this file's ids
        for n in sub.walk():
            n.ids = {((iid ^ fid) & MASK): kind for iid, kind in n.ids.items()}
            for nid, kind in n.ids.items():
                (exposed if kind == "tr" else exposed_go)[nid] = n
        inst_root[fid] = (sub, _ref(body, "m_TransformParent")[0], is_model)

    # 3 stripped documents name objects of an instance; a model's inner ids cannot be derived, so they flag
    stripped_model_tr = {}
    for cls, fid, stripped, body in docs:
        if not stripped:
            continue
        inst, _ = _ref(body, "m_PrefabInstance")
        if inst in inst_root and inst_root[inst][2] and cls in ("4", "224"):
            if _ref(body, "m_CorrespondingSourceObject")[0] == model_root_target.get(inst):
                exposed[fid] = inst_root[inst][0]          # the model's own root transform: placed, not guessed
            else:
                stripped_model_tr[fid] = inst_root[inst][0]
        if inst in inst_root and inst_root[inst][2] and cls == "1":
            exposed_go.setdefault(fid, inst_root[inst][0])

    # 3b a rig's constraints name the model's inner bones: a MonoBehaviour on a GameObject called "D2" whose
    # m_ConstrainedObject is a stripped model transform says that transform is the bone ending in "D2". This is how a
    # prism parented to a bone inside a model instance is placed exactly (the Brittlestar's ten prisms).
    named_model_tr = {}
    for cls, fid, stripped, body in docs:
        if cls != "114" or stripped:
            continue
        m = re.search(r"m_ConstrainedObject: \{fileID: (-?\d+)\}", body)
        if not m or int(m.group(1)) not in stripped_model_tr:
            continue
        rig_go = go_node.get(_ref(body, "m_GameObject")[0])
        if rig_go is None:
            continue
        model_root = stripped_model_tr[int(m.group(1))]
        hits = [n for n in model_root.walk() if n.bone and n.source == model_root.source and n.name.endswith(rig_go.name)]
        if len(hits) == 1:
            named_model_tr[int(m.group(1))] = hits[0]

    # 4 parenting
    root = None
    for fid, n in tr_node.items():
        father, _ = _ref(by_id[fid][2], "m_Father")
        if father == 0:
            root = n
        elif father in exposed:
            exposed[father].add(n)
        elif father in named_model_tr:
            n.inferred_parent = "rig constraint"
            named_model_tr[father].add(n)
        elif father in stripped_model_tr:
            n.unresolved_parent = True
            stripped_model_tr[father].add(n)
    for fid, (sub, parent, _is_model) in inst_root.items():
        if parent == 0:
            root = root or sub
        elif parent in exposed:
            exposed[parent].add(sub)
        elif parent in named_model_tr:
            sub.inferred_parent = "rig constraint"
            named_model_tr[parent].add(sub)
        elif parent in stripped_model_tr:
            sub.unresolved_parent = True
            stripped_model_tr[parent].add(sub)

    # 4b children in the order Unity keeps them (m_Children), so "the first child" means what it means in the game
    for fid, n in tr_node.items():
        m = re.search(r"m_Children:\n((?:\s*- \{fileID: -?\d+\}\n)+)", by_id[fid][2])
        if not m:
            continue
        order = [int(v) for v in re.findall(r"fileID: (-?\d+)", m.group(1))]
        rank = {}
        for i, cid in enumerate(order):
            node = exposed.get(cid) or named_model_tr.get(cid)
            if node is not None:
                rank[id(node)] = i
        n.children.sort(key=lambda c: rank.get(id(c), 10 ** 6))

    # 5 components
    for cls, fid, stripped, body in docs:
        if cls not in ("114", "33", "137") or stripped:
            continue
        go, _ = _ref(body, "m_GameObject")
        n = exposed_go.get(go)
        if n is None:
            continue
        if cls == "114":
            _f, sg = _ref(body, "m_Script")
            name = os.path.splitext(os.path.basename(guids.get(sg, sg or "?")))[0]
            n.scripts.append(name)
            n.fields[name] = body
        else:
            mf, mg = _ref(body, "m_Mesh")
            if mg:
                n.mesh = (guids.get(mg, mg), mf)

    # 6 the ids this tree exposes to whoever nests it
    for fid, n in go_node.items():
        n.ids[fid] = "go"
    for fid, n in tr_node.items():
        n.ids[fid] = "tr"
    if root is None and tr_node:
        root = next(iter(tr_node.values()))
    return root


def pose(root, include_root=False):
    """Fill every node's world matrix. The root is the identity unless include_root (a creature's own space)."""
    def go(n, parent_world):
        loc = fm.trs(n.t, n.q, n.s)
        n.world = loc if parent_world is None else fm.mmul(parent_world, loc)
        for c in n.children:
            go(c, n.world)
    root_local = fm.trs(root.t, root.q, root.s) if include_root else fm.ident()
    root.world = root_local
    for c in root.children:
        go(c, root.world)
    return root


def world_scale(n):
    w = n.world
    return [sum(w[i][j] ** 2 for i in range(3)) ** 0.5 for j in range(3)]


def world_pos(n):
    return [n.world[0][3], n.world[1][3], n.world[2][3]]


def self_test():
    """Every stripped document of a nested PREFAB instance in the shipped fauna obeys source XOR instance."""
    fails, seen = [], 0
    guids = guid_index()
    for rel in ("Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab", "Assets/_Prefabs/FloraAndFauna/QuadFish.prefab",
                "Assets/_Models/Fauna/MassSharkFauna.prefab", "Assets/_Prefabs/FloraAndFauna/Bees/BeeFauna.prefab"):
        docs = split_docs(open(os.path.join(REPO, rel)).read())
        inst_src = {}
        for cls, fid, stripped, body in docs:
            if cls == "1001":
                inst_src[fid] = guids.get(_ref(body, "m_SourcePrefab")[1], "")
        for cls, fid, stripped, body in docs:
            if not stripped:
                continue
            inst, _ = _ref(body, "m_PrefabInstance")
            corr, _g = _ref(body, "m_CorrespondingSourceObject")
            if inst_src.get(inst, "").endswith(".prefab"):
                seen += 1
                if ((corr ^ inst) & MASK) != fid:
                    fails.append(f"{rel}: stripped &{fid} is not {corr} XOR {inst}")
    if seen == 0:
        fails.append("no stripped documents were examined: the check did nothing")
    # negative control: the rule must be able to fail
    if ((12345 ^ 67890) & MASK) == 12345:
        fails.append("the XOR rule cannot fail")
    return fails, seen


def main(argv):
    if "--self-test" in argv:
        fails, seen = self_test()
        for f in fails:
            print("FAIL:", f)
        print(f"prefab_read self-test: {seen} stripped documents obey source XOR instance" if not fails
              else f"prefab_read self-test: {len(fails)} problem(s)")
        return 1 if fails else 0
    if not argv:
        print(__doc__)
        return 2
    root = pose(resolve(argv[0]), include_root="--root" in argv)

    def show(n, d):
        ws = world_scale(n)
        wp = world_pos(n)
        extra = (" [" + ",".join(n.scripts) + "]" if n.scripts else "") + (f" mesh={n.mesh[1]}" if n.mesh else "") \
            + ("" if n.active else " INACTIVE") + (" ?parent" if n.unresolved_parent else "")
        print("  " * d + f"{n.name} p={[round(v, 2) for v in wp]} s={[round(v, 2) for v in ws]}{extra}")
        for c in n.children:
            show(c, d + 1)
    show(root, 0)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
