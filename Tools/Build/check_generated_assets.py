#!/usr/bin/env python3
"""Audit every Unity asset added or changed since a base ref, against the COMPILED code.

WHY THIS EXISTS
    The Tools/Build/author_*.py generators write prefabs, ScriptableObjects, spawn profiles and
    scene overrides as YAML text that Unity never opened. Unity is lenient in exactly the wrong
    places when it loads that YAML:
      * a serialized key that names no field (a typo, a renamed field) is DROPPED in silence, so
        the feature reads its default and quietly does nothing;
      * an enum int that is not a member loads as that int, which no switch handles;
      * a reference to a guid nobody owns, or to a fileID the target file does not contain,
        loads as None ("Missing");
      * a reference to an asset of the wrong class loads as null ("Type mismatch");
      * an m_Script whose file name is not its class name, or whose class is not a
        MonoBehaviour / ScriptableObject, is "The associated script can not be loaded";
      * a duplicate .meta guid makes Unity re-guid one of the two files on import, which breaks
        every reference to it.
    None of these is a compile error and none shows up until someone opens the asset.

WHAT IT CHECKS (per changed asset; modified files report only findings their base lacked)
    yaml      %YAML 1.1 + %TAG !u! header, `--- !u!<class> &<fileID>` docs, each body parses
    meta      every changed file has a .meta; every .meta guid is unique across Assets/
    guid      every {fileID, guid} resolves to an asset; a fileID into a YAML asset exists there
    local     every {fileID: N} without a guid names a doc in the same file
    script    m_Script -> a .cs whose file name is a non-abstract, non-generic class deriving
              MonoBehaviour (on a GameObject) or ScriptableObject (in an .asset)
    field     every serialized key exists on the class or a base (recursing into [Serializable]
              classes/structs and lists of them; [FormerlySerializedAs] names count)
    enum      every enum-typed value is a declared member (not checked for [Flags])
    type      a reference in a field typed as a project class points at an object of that class
    override  PrefabInstance modifications: the target exists in the source prefab and the
              propertyPath's first segment is a field of the target's class
    deleted   no asset references the guid of a file deleted since the base
    swarm     the Swarm cell is in the Cell Selector's CellConfigs, its config points at its spawn
              profile, and every new fauna/flora config-data asset is listed by a spawn profile
              or a Spawn Matrix row (the bench-only configs spawn nowhere else)

    The schema (fields, bases, enums) comes from Roslyn binding Assembly-CSharp exactly as
    Tools/Build/unity_refcompile builds it - run that first (this script says so if you did not).

WHAT IT DOES NOT PROVE
    Field VALUES are not range-checked; references into packages whose .meta files are not in the
    repo (TMP, URP, Input System, ...) cannot be resolved here and are counted, not judged;
    fileIDs into binary/importer assets (FBX, textures, shaders) are not checked.

USAGE
    python3 Tools/Build/check_generated_assets.py [--base origin/bleeding-edge]
    python3 Tools/Build/check_generated_assets.py --self-test
Exit 0 = clean, 1 = findings, 2 = setup problem (no refcompile output).
"""
import argparse
import collections
import glob
import hashlib
import json
import os
import re
import subprocess
import sys

try:
    import yaml
    _Loader = getattr(yaml, "CSafeLoader", yaml.SafeLoader)
except ImportError:  # pragma: no cover
    sys.exit("check_generated_assets: PyYAML is required (pip install pyyaml)")

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TMP = os.environ.get("TMPDIR", "/tmp")
CACHE = os.environ.get("UNITY_REFCOMPILE_CACHE", os.path.join(TMP, "unity_refcompile_cache"))
DOTNET_ROOT = os.environ.get("DOTNET_ROOT", "/usr/lib/dotnet")
SCHEMA_SRC = os.path.join(ROOT, "Tools", "Build", "unity_refcompile", "Schema", "Program.cs")

YAML_EXT = (".asset", ".prefab", ".unity", ".mat", ".controller", ".anim", ".overrideController",
            ".playable", ".mask", ".physicMaterial", ".physicsMaterial2D", ".renderTexture",
            ".lighting", ".signal", ".preset", ".spriteatlas", ".flare", ".guiskin", ".fontsettings",
            ".mixer", ".cubemap", ".brush", ".terrainlayer")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$", re.M)
BUILTIN_GUIDS = {"0000000000000000e000000000000000", "0000000000000000f000000000000000",
                 "0000000000000000d000000000000000", "0000000000000000c000000000000000"}
MB_STANDARD = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
               "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
               "m_EditorClassIdentifier", "serializedVersion"}
UNITY_OBJECT = "UnityEngine.Object"


# --------------------------------------------------------------------------------------------
# file access (an overlay lets --self-test inject defects without touching the tree)
# --------------------------------------------------------------------------------------------
class Tree:
    def __init__(self, root, overlay=None, deleted=()):
        self.root = root
        self.overlay = dict(overlay or {})
        self.deleted = set(deleted)
        self._cache = {}

    def abs(self, rel):
        return os.path.join(self.root, rel)

    def exists(self, rel):
        if rel in self.deleted:
            return False
        return rel in self.overlay or os.path.exists(self.abs(rel))

    def read(self, rel):
        if rel in self.overlay:
            return self.overlay[rel]
        if rel not in self._cache:
            with open(self.abs(rel), encoding="utf-8-sig", errors="replace") as f:
                self._cache[rel] = f.read()
        return self._cache[rel]


def git(*args):
    return subprocess.run(["git"] + list(args), cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                          text=True).stdout


# --------------------------------------------------------------------------------------------
# guid index
# --------------------------------------------------------------------------------------------
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)


def build_guid_index(tree):
    """-> (guid -> [asset rel path], [(guid, paths)] duplicates)."""
    idx = collections.defaultdict(list)
    for d, dirs, files in os.walk(tree.abs("Assets")):
        for f in files:
            if f.endswith(".meta"):
                rel = os.path.relpath(os.path.join(d, f), tree.root)
                if rel in tree.deleted:
                    continue
                m = META_GUID.search(tree.read(rel))
                if m:
                    idx[m.group(1)].append(rel[:-5])
    for rel, text in tree.overlay.items():
        if rel.endswith(".meta"):
            m = META_GUID.search(text)
            if m and rel[:-5] not in idx[m.group(1)]:
                idx[m.group(1)].append(rel[:-5])
    dups = [(g, sorted(p)) for g, p in idx.items() if len(p) > 1]
    return idx, dups


# --------------------------------------------------------------------------------------------
# schema (Roslyn, via Tools/Build/unity_refcompile/Schema)
# --------------------------------------------------------------------------------------------
def load_schema(rsp):
    csc = sorted(glob.glob(os.path.join(DOTNET_ROOT, "sdk", "*", "Roslyn", "bincore", "csc.dll")))
    tools = os.path.join(CACHE, "tools")
    if not csc or not os.path.exists(os.path.join(tools, "Microsoft.CodeAnalysis.dll")):
        return None, "no Roslyn tools in %s - run `bash Tools/Build/unity_refcompile/run.sh` first" % tools
    dll = os.path.join(tools, "Schema.dll")
    fp = hashlib.sha1(open(SCHEMA_SRC, "rb").read()).hexdigest()
    if not os.path.exists(dll + ".stamp") or open(dll + ".stamp").read() != fp:
        ref = sorted(glob.glob(os.path.join(DOTNET_ROOT, "packs", "Microsoft.NETCore.App.Ref", "*", "ref", "net8.0")))[-1]
        r = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), csc[-1], "-nologo", "-noconfig", "-nostdlib",
                            "-langversion:latest", "-out:" + dll, SCHEMA_SRC,
                            "-r:" + os.path.join(tools, "Microsoft.CodeAnalysis.dll"),
                            "-r:" + os.path.join(tools, "Microsoft.CodeAnalysis.CSharp.dll")]
                           + ["-r:" + x for x in glob.glob(os.path.join(ref, "*.dll"))],
                           stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        if r.returncode != 0:
            return None, "Schema tool failed to build:\n" + r.stdout
        rt = sorted(os.listdir(os.path.join(DOTNET_ROOT, "shared", "Microsoft.NETCore.App")))[-1]
        json.dump({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": rt}}},
                  open(os.path.join(tools, "Schema.runtimeconfig.json"), "w"))
        open(dll + ".stamp", "w").write(fp)
    out = os.path.join(os.path.dirname(rsp), "schema.json")
    # Assembly-CSharp first (its references bring the package types), then every other assembly
    # whose sources live in Assets/ - parsed from SOURCE so their editor-only fields are seen too
    others = []
    for o in sorted(glob.glob(os.path.join(os.path.dirname(rsp), "*.rsp"))):
        if o != rsp and any(l.strip().strip('"').startswith(os.path.join(ROOT, "Assets") + os.sep) for l in open(o)):
            others.append(o)
    r = subprocess.run([os.path.join(DOTNET_ROOT, "dotnet"), dll, out, rsp] + others, stdout=subprocess.PIPE,
                       stderr=subprocess.STDOUT, text=True)
    if r.returncode != 0:
        return None, "Schema tool failed:\n" + r.stdout[-2000:]
    return Schema(json.load(open(out))), None


class Schema:
    def __init__(self, types):
        self.types = types
        self.by_file = collections.defaultdict(list)
        for full, t in types.items():
            for f in t["files"]:
                self.by_file[os.path.relpath(f, ROOT)].append(full)

    def get(self, full):
        return self.types.get(full)

    def derives(self, full, base):
        t = self.types.get(full)
        return bool(t) and (full == base or base in t["bases"])

    def fields(self, full):
        """name -> field info over the class and its bases (+ FormerlySerializedAs aliases)."""
        out = {}
        for n in [full] + list(self.types.get(full, {}).get("bases", [])):
            t = self.types.get(n)
            if not t:
                continue
            for f in t["fields"]:
                out.setdefault(f["name"], f)
                for old in f.get("former") or []:
                    out.setdefault(old, f)
        return out

    def script_class(self, cs_rel):
        """The class Unity binds to a script file: the one named like the file."""
        stem = os.path.splitext(os.path.basename(cs_rel))[0]
        cands = [n for n in self.by_file.get(cs_rel, []) if self.types[n]["name"] == stem
                 and self.types[n]["kind"] == "Class"]
        return cands[0] if cands else None

    def complex_type(self, full):
        """A [Serializable] class/struct Unity serializes inline (not a UnityEngine.Object)."""
        t = self.types.get(full)
        if not t or t["kind"] not in ("Class", "Struct") or UNITY_OBJECT in t["bases"]:
            return None
        return t if t["serializable"] or t["kind"] == "Struct" else None


# --------------------------------------------------------------------------------------------
# Unity YAML
# --------------------------------------------------------------------------------------------
class Doc:
    __slots__ = ("cls", "fid", "stripped", "line", "body", "kind", "data", "error")


def parse_unity_yaml(text, only=None):
    """-> (header_error or None, [Doc]). `only`: parse just these fileIDs' bodies (others: data None)."""
    lines = text.split("\n", 2)
    err = None
    if len(lines) < 2 or lines[0].strip() != "%YAML 1.1" or lines[1].strip() != "%TAG !u! tag:unity3d.com,2011:":
        err = "missing `%YAML 1.1` / `%TAG !u! tag:unity3d.com,2011:` header"
    docs = []
    heads = list(DOC_RE.finditer(text))
    # any `--- ` line that is not a well-formed Unity doc header
    for m in re.finditer(r"^---.*$", text, re.M):
        if not DOC_RE.match(m.group(0)):
            err = err or "malformed document header %r at line %d" % (m.group(0)[:60], text.count("\n", 0, m.start()) + 1)
    line, pos = 1, 0
    for i, m in enumerate(heads):
        d = Doc()
        d.cls, d.fid, d.stripped = int(m.group(1)), m.group(2), bool(m.group(3))
        line += text.count("\n", pos, m.start())
        pos = m.start()
        d.line = line
        d.body = text[m.end():heads[i + 1].start() if i + 1 < len(heads) else len(text)]
        d.kind, d.data, d.error = None, None, None
        if only is not None and d.fid not in only:
            docs.append(d)
            continue
        try:
            v = yaml.load(d.body, Loader=_Loader)
            if isinstance(v, dict) and len(v) == 1:
                d.kind, d.data = next(iter(v.items()))
                if d.data is None:
                    d.data = {}
            elif v is not None:
                d.error = "document body is not a single `ClassName:` mapping"
        except yaml.YAMLError as e:
            d.error = "YAML does not parse: %s" % str(e).replace("\n", " ")[:200]
        docs.append(d)
    return err, docs


class LazyDoc:
    def __init__(self, text, m, end):
        self.cls, self.fid, self.stripped = int(m.group(1)), m.group(2), bool(m.group(3))
        self._text, self._start, self._end, self._data = text, m.end(), end, False

    @property
    def data(self):
        if self._data is False:
            try:
                v = yaml.load(self._text[self._start:self._end], Loader=_Loader)
                self._data = next(iter(v.values())) if isinstance(v, dict) and len(v) == 1 else None
            except yaml.YAMLError:
                self._data = None
        return self._data


def iter_refs(node, path=""):
    """Yield (path, ref dict) for every {fileID: ...} mapping."""
    if isinstance(node, dict):
        if "fileID" in node and set(node) <= {"fileID", "guid", "type"}:
            yield path, node
            return
        for k, v in node.items():
            yield from iter_refs(v, path + "." + str(k) if path else str(k))
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from iter_refs(v, "%s[%d]" % (path, i))


# --------------------------------------------------------------------------------------------
# the audit
# --------------------------------------------------------------------------------------------
class Audit:
    def __init__(self, tree, schema, guids):
        self.tree, self.schema, self.guids = tree, schema, guids
        self._docs = {}
        self.unverified = collections.Counter()

    def docs(self, rel):
        if rel not in self._docs:
            text = self.tree.read(rel)
            self._docs[rel] = parse_unity_yaml(text) if text.startswith("%YAML") else (None, None)
        return self._docs[rel]

    def anchors(self, rel):
        """fid -> Doc for a REFERENCED file; bodies parse lazily (only the docs actually looked at)."""
        if rel in self._docs:
            _, docs = self._docs[rel]
            return None if docs is None else {d.fid: d for d in docs}
        key = ("anchors", rel)
        if key not in self._docs:
            text = self.tree.read(rel)
            if not text.startswith("%YAML"):
                self._docs[key] = None
            else:
                heads = list(DOC_RE.finditer(text))
                self._docs[key] = {m.group(2): LazyDoc(text, m, heads[i + 1].start() if i + 1 < len(heads) else len(text))
                                   for i, m in enumerate(heads)}
        return self._docs[key]

    def resolve(self, guid):
        p = self.guids.get(guid)
        return p[0] if p else None

    def class_of(self, rel, doc):
        """Full schema name of a MonoBehaviour doc's script class, or None."""
        if doc.cls != 114 or not isinstance(doc.data, dict):
            return None
        s = doc.data.get("m_Script") or {}
        if not isinstance(s, dict) or not s.get("guid"):
            return None
        cs = self.resolve(s["guid"])
        return self.schema.script_class(cs) if cs and cs.endswith(".cs") else None

    # ---- one file -------------------------------------------------------------------------
    def check_file(self, rel, only=None):
        """`only`: audit just these docs (a modified file's changed documents)."""
        out = []
        if not self.tree.exists(rel + ".meta"):
            out.append(("meta", rel, "has no .meta (Unity would mint a new guid and break every reference)"))
        if not rel.endswith(YAML_EXT):
            return out
        text = self.tree.read(rel)
        if not text.startswith("%YAML"):
            return out  # binary-serialized asset
        herr, docs = self.docs(rel) if only is None else parse_unity_yaml(text, only)
        if herr:
            out.append(("yaml", rel, herr))
        own = {d.fid for d in docs}
        seen = set()
        for d in docs:
            if d.fid in seen:
                out.append(("yaml", rel, "duplicate fileID &%s (line %d)" % (d.fid, d.line)))
            seen.add(d.fid)
            if d.error:
                out.append(("yaml", rel, "doc &%s (line %d): %s" % (d.fid, d.line, d.error)))
                continue
            if d.stripped or not isinstance(d.data, dict):
                continue
            where = "%s &%s" % (d.kind, d.fid)
            for path, ref in iter_refs(d.data):
                out += self.check_ref(rel, where, path, ref, own)
            if d.cls == 114:
                out += self.check_monobehaviour(rel, d, where)
            if d.cls == 1001:
                out += self.check_prefab_instance(rel, d, where)
        return out

    def check_ref(self, rel, where, path, ref, own):
        fid, guid = str(ref.get("fileID")), ref.get("guid")
        if not guid:
            if fid not in ("0",) and fid not in own:
                return [("local", rel, "%s.%s -> fileID %s is not a document in this file" % (where, path, fid))]
            return []
        if guid in BUILTIN_GUIDS:
            return []
        target = self.resolve(guid)
        if path.endswith("m_SourcePrefab") and fid == "100100000":
            return [] if target else [("guid", rel, "%s.%s -> source prefab guid %s resolves to no asset" % (where, path, guid))]
        if not target:
            return [("guid", rel, "%s.%s -> guid %s resolves to no asset in Assets/" % (where, path, guid))]
        if target.endswith(YAML_EXT) and self.tree.exists(target):
            anchors = self.anchors(target)
            if anchors is not None and fid not in anchors:
                return [("guid", rel, "%s.%s -> fileID %s does not exist in %s" % (where, path, fid, target))]
        return []

    def check_monobehaviour(self, rel, d, where):
        out = []
        s = d.data.get("m_Script")
        if not isinstance(s, dict) or not s.get("guid"):
            out.append(("script", rel, "%s has no m_Script (missing script)" % where))
            return out
        target = self.resolve(s["guid"])
        if not target:
            return out  # reported by check_ref
        if not target.endswith(".cs"):
            self.unverified["script in a DLL (%s)" % os.path.basename(target)] += 1
            return out
        if str(s.get("fileID")) != "11500000":
            out.append(("script", rel, "%s m_Script fileID %s is not 11500000 for a .cs script" % (where, s.get("fileID"))))
        full = self.schema.script_class(target)
        stem = os.path.splitext(os.path.basename(target))[0]
        if not full:
            if any(t in self.schema.by_file for t in [target]) or self.tree.exists(target):
                out.append(("script", rel, "%s m_Script %s declares no class named %s (Unity: 'The associated "
                            "script can not be loaded')" % (where, target, stem)))
            return out
        t = self.schema.get(full)
        on_go = isinstance(d.data.get("m_GameObject"), dict) and str(d.data["m_GameObject"].get("fileID")) != "0"
        need = "UnityEngine.MonoBehaviour" if on_go else "UnityEngine.ScriptableObject"
        if need not in t["bases"]:
            out.append(("script", rel, "%s m_Script class %s does not derive %s" % (where, full, need)))
        if t["isAbstract"] or t["isGeneric"]:
            out.append(("script", rel, "%s m_Script class %s is abstract or generic" % (where, full)))
        out += self.check_fields(rel, where, full, d.data, MB_STANDARD)
        return out

    def check_fields(self, rel, where, full, data, standard=frozenset(), depth=0):
        out = []
        fields = self.schema.fields(full)
        for k, v in data.items():
            if k in standard:
                continue
            f = fields.get(k)
            if f is None:
                out.append(("field", rel, "%s: `%s` is not a serialized field of %s (Unity drops it silently)"
                            % (where, k, full)))
                continue
            if f.get("serializeReference") or depth > 6:
                continue
            ftype, is_list = f["info"]["type"], f["info"]["array"]
            items = v if (is_list and isinstance(v, list)) else ([v] if not is_list else [])
            ft = self.schema.get(ftype)
            for i, item in enumerate(items):
                w = "%s.%s%s" % (where, k, "[%d]" % i if is_list else "")
                if ft and ft["kind"] == "Enum":
                    if isinstance(item, int) and item == 0 and 0 not in ft["enumMembers"].values():
                        # default(T): what Unity itself writes for an unset enum (the flora prefabs'
                        # `domain: 0`, assigned by the spawner at runtime)
                        self.unverified["enum left at default 0 with no 0 member"] += 1
                    elif not ft["flags"] and isinstance(item, int) and item not in ft["enumMembers"].values():
                        out.append(("enum", rel, "%s = %d is not a member of %s (members: %s)" % (
                            w, item, ftype, ", ".join("%s=%d" % kv for kv in sorted(ft["enumMembers"].items(), key=lambda x: x[1])))))
                elif self.schema.complex_type(ftype) and isinstance(item, dict) and not set(item) <= {"fileID", "guid", "type"}:
                    out += self.check_fields(rel, w, ftype, item, {"serializedVersion"}, depth + 1)
                elif ft and UNITY_OBJECT in ft["bases"] and isinstance(item, dict) and "fileID" in item:
                    out += self.check_ref_type(rel, w, ftype, item)
        return out

    def check_ref_type(self, rel, where, ftype, ref):
        guid, fid = ref.get("guid"), str(ref.get("fileID"))
        if fid == "0":
            return []
        target = self.resolve(guid) if guid else rel
        if not target or not target.endswith(YAML_EXT) or not self.tree.exists(target):
            return []
        anchors = self.anchors(target)
        doc = anchors.get(fid) if anchors else None
        if doc is None or doc.cls != 114:
            return []
        cls = self.class_of(target, doc)
        if cls and not self.schema.derives(cls, ftype):
            return [("type", rel, "%s references %s (%s) but the field is typed %s - Unity loads it as null "
                     "(Type mismatch)" % (where, target, cls, ftype))]
        return []

    def check_prefab_instance(self, rel, d, where):
        out = []
        mod = d.data.get("m_Modification") or {}
        src = d.data.get("m_SourcePrefab") or {}
        src_rel = self.resolve(src.get("guid")) if isinstance(src, dict) else None
        for m in mod.get("m_Modifications") or []:
            t = m.get("target") or {}
            pp = str(m.get("propertyPath", ""))
            trel = self.resolve(t.get("guid")) if t.get("guid") else None
            if not trel or not trel.endswith(YAML_EXT) or not self.tree.exists(trel):
                continue
            anchors = self.anchors(trel)
            tdoc = anchors.get(str(t.get("fileID"))) if anchors else None
            if tdoc is None:
                # objects of a prefab nested inside the source prefab have derived ids
                self.unverified["override target inside a nested prefab"] += 1
                continue
            cls = self.class_of(trel, tdoc)
            if not cls:
                continue
            head = pp.split(".")[0]
            if head in MB_STANDARD or head.startswith("m_"):
                continue
            if head not in self.schema.fields(cls):
                out.append(("override", rel, "%s: modification `%s` on %s (%s) names no field of that class"
                            % (where, pp, trel, cls)))
        return out


# --------------------------------------------------------------------------------------------
# project rules: the Swarm cell
# --------------------------------------------------------------------------------------------
SWARM_CELL = "Assets/_SO_Assets/Cell Configs/Swarm Cell/Swarm Cell Config.asset"
SWARM_PROFILE = "Assets/_SO_Assets/Cell Configs/Swarm Cell/Swarm Cell Spawn Profile.asset"
SPAWN_MATRIX_TOY = "Assets/_SO_Assets/Toys/Toy_SpawnMatrix.asset"
CELL_SELECTOR_SCENE = "Assets/_Scenes/Menu_Main.unity"


def meta_guid(tree, rel):
    if not tree.exists(rel + ".meta"):
        return None
    m = META_GUID.search(tree.read(rel + ".meta"))
    return m.group(1) if m else None


def check_swarm(audit, changed):
    tree, out = audit.tree, []
    cell, prof = meta_guid(tree, SWARM_CELL), meta_guid(tree, SWARM_PROFILE)
    if not cell or not prof:
        return [("swarm", SWARM_CELL, "Swarm cell config or spawn profile is missing")]
    # 1. the Cell Selector lists the Swarm cell (scene override on CellConfigs)
    text = tree.read(CELL_SELECTOR_SCENE)
    size = re.findall(r"propertyPath: CellConfigs\.Array\.size\s*\n\s*value: (\d+)", text)
    slots = re.findall(r"propertyPath: '?CellConfigs\.Array\.data\[(\d+)\]'?\s*\n\s*value:[^\n]*\n\s*objectReference: "
                       r"\{fileID: 11400000, guid: ([0-9a-f]{32})", text)
    hit = [int(i) for i, g in slots if g == cell]
    if not hit:
        out.append(("swarm", CELL_SELECTOR_SCENE, "no CellConfigs.Array.data[i] override references the Swarm cell "
                    "config (%s) - the Cell Selector cannot reach it" % cell))
    elif not size or max(hit) >= max(int(s) for s in size):
        out.append(("swarm", CELL_SELECTOR_SCENE, "the Swarm cell sits in CellConfigs[%d] but CellConfigs.Array.size is %s"
                    % (max(hit), size or "not overridden")))
    # 2. the cell config points at its spawn profile
    if prof not in tree.read(SWARM_CELL):
        out.append(("swarm", SWARM_CELL, "does not reference the Swarm Cell Spawn Profile"))
    # 3. every new fauna/flora config-data asset is listed by some spawn profile
    _, pdocs = audit.docs(SWARM_PROFILE)
    pcls = audit.class_of(SWARM_PROFILE, pdocs[0]) if pdocs else None
    if not pcls:
        return out + [("swarm", SWARM_PROFILE, "spawn profile has no resolvable script class")]
    lists = {k: f["info"]["type"] for k, f in audit.schema.fields(pcls).items()
             if f["info"]["array"] and k in ("SupportedFaunas", "SupportedFloras")}
    listed = set()
    for rel in changed + [SWARM_PROFILE]:
        if not rel.endswith(".asset") or not tree.exists(rel):
            continue
        _, docs = audit.docs(rel)
        for d in docs or []:
            if audit.class_of(rel, d) and audit.schema.derives(audit.class_of(rel, d), pcls):
                for k in lists:
                    for r in (d.data.get(k) or []):
                        if isinstance(r, dict) and r.get("guid"):
                            listed.add(r["guid"])
    # ...and every spawn profile already in the tree: a MODIFIED config (a generator re-tuning a
    # biome's species) is listed by a profile nobody touched, which the changed-files scan
    # above cannot see. Read straight from the profiles' list blocks, through the tree so a
    # self-test overlay still applies.
    so_root = os.path.join(ROOT, "Assets", "_SO_Assets")
    for dirpath, _, names in os.walk(so_root):
        for name in names:
            if not name.endswith(".asset"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, name), ROOT)
            if not tree.exists(rel):
                continue
            text = tree.read(rel)
            for key in ("SupportedFaunas:", "SupportedFloras:"):
                if key not in text:
                    continue
                block = re.match(r"(?:\n  [- ] .*)*", text.split(key, 1)[1])
                listed.update(re.findall(r"guid: ([0-9a-f]{32})", block.group(0)))
    # The Spawn Matrix toy is the other way a config spawns: its species rows release one exact
    # config on demand, and some configs exist ONLY for it (the bench swarm models in
    # Swarm Fauna/Bench/, authored by author_spawn_matrix_roster.py).
    if tree.exists(SPAWN_MATRIX_TOY):
        listed.update(re.findall(r"^    - \{fileID: 11400000, guid: ([0-9a-f]{32}), type: 2\}$",
                                 tree.read(SPAWN_MATRIX_TOY), re.M))
    for rel in changed:
        if not rel.endswith(".asset") or not tree.exists(rel):
            continue
        _, docs = audit.docs(rel)
        if not docs:
            continue
        cls = audit.class_of(rel, docs[0])
        for k, etype in lists.items():
            if cls and audit.schema.derives(cls, etype) and meta_guid(tree, rel) not in listed:
                out.append(("swarm", rel, "is a %s (%s) but neither a spawn profile's %s nor a Spawn "
                            "Matrix row lists it - it never spawns" % (etype.split(".")[-1], cls, k)))
    return out


# --------------------------------------------------------------------------------------------
# driver
# --------------------------------------------------------------------------------------------
def changed_assets(base):
    rows = git("diff", "--name-status", "--no-renames", base + "...HEAD", "--", "Assets", ":!*.cs").splitlines()
    added, modified, deleted = [], [], []
    for r in rows:
        st, path = r.split("\t", 1)
        if path.endswith(".meta"):
            if st == "D":
                deleted.append(path)
            continue
        (added if st == "A" else modified if st == "M" else deleted if st == "D" else added).append(path)
    return added, modified, deleted


def run_audit(tree, schema, base, added, modified, deleted_metas, base_text):
    guids, dups = build_guid_index(tree)
    audit = Audit(tree, schema, guids)
    findings = [("meta", "Assets/", "guid %s is shared by %s" % (g, ", ".join(p))) for g, p in dups]
    for rel in added:
        if tree.exists(rel):
            findings += audit.check_file(rel)
    for rel in modified:
        if not tree.exists(rel):
            continue
        old_text = base_text(rel)
        if old_text is None:
            findings += audit.check_file(rel)
            continue
        # only the documents this change added or edited, and only findings their base lacked
        only = None
        if rel.endswith(YAML_EXT) and old_text.startswith("%YAML"):
            def bodies(t):
                hs = list(DOC_RE.finditer(t))
                return {m.group(2): t[m.start():hs[i + 1].start() if i + 1 < len(hs) else len(t)] for i, m in enumerate(hs)}
            b_old, b_new = bodies(old_text), bodies(tree.read(rel))
            only = {f for f, b in b_new.items() if b_old.get(f) != b}
        now = audit.check_file(rel, only)
        before_tree = Tree(tree.root, {rel: old_text}, tree.deleted)
        before_tree.overlay.update({k: v for k, v in tree.overlay.items() if k != rel})
        old = set(Audit(before_tree, schema, guids).check_file(rel, only))
        findings += [f for f in now if f not in old]
    # nothing may still reference a deleted file's guid
    for meta, guid in deleted_metas:
        hits = subprocess.run(["git", "grep", "-l", guid, "--", "Assets"], cwd=tree.root, stdout=subprocess.PIPE,
                              text=True).stdout.split()
        hits += [k for k, v in tree.overlay.items() if guid in v]
        for h in sorted(set(hits)):
            if h != meta and tree.exists(h) and guid in tree.read(h):
                findings.append(("deleted", h, "references %s (guid %s), deleted since the base" % (meta[:-5], guid)))
    findings += check_swarm(audit, added + modified)
    return findings, audit


def report(findings, audit, label):
    by = collections.defaultdict(list)
    for kind, rel, msg in findings:
        by[kind].append((rel, msg))
    for kind in sorted(by):
        print("[%s] %d" % (kind, len(by[kind])))
        for rel, msg in sorted(set(by[kind])):
            print("    %s: %s" % (rel, msg))
    for k, v in sorted(audit.unverified.items()):
        print("    (not judged: %d x %s)" % (v, k))
    print("%s: %s" % (label, "OK" if not findings else "%d FINDING(S)" % len(findings)))


def setup(args):
    if not args.refcompile_out:
        sys.path.insert(0, os.path.join(ROOT, "Tools", "Build", "unity_refcompile"))
        import build as refcompile  # noqa: E402  (tree_key only; importing has no side effects)
        args.refcompile_out = os.path.join(TMP, "unity_refcompile_out", refcompile.tree_key(), "player")
    rsp = os.path.join(args.refcompile_out, "Assembly-CSharp.rsp")
    if not os.path.exists(rsp):
        print("check_generated_assets: no %s - run `bash Tools/Build/unity_refcompile/run.sh` first "
              "(the field/enum/script checks need the compiled schema)" % rsp)
        sys.exit(2)
    srcs = [l.strip().strip('"') for l in open(rsp) if l.strip().endswith('.cs"') or l.strip().endswith(".cs")]
    if srcs and not all(os.path.abspath(x).startswith(ROOT + os.sep) for x in srcs[:50]):
        print("check_generated_assets: %s was built from another working tree - rerun unity_refcompile here" % rsp)
        sys.exit(2)
    schema, err = load_schema(rsp)
    if err:
        print("check_generated_assets: " + err)
        sys.exit(2)
    return schema


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--base", default="origin/bleeding-edge")
    ap.add_argument("--refcompile-out", default=None, help="default: unity_refcompile's player output for this tree")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    schema = setup(args)
    if args.self_test:
        return self_test(schema, args.base)
    added, modified, deleted = changed_assets(args.base)
    deleted_metas = []
    for m in deleted:
        if m.endswith(".meta"):
            g = META_GUID.search(git("show", "%s:%s" % (args.base, m)))
            if g:
                deleted_metas.append((m, g.group(1)))

    def base_text(rel):
        r = subprocess.run(["git", "show", "%s:%s" % (args.base, rel)], cwd=ROOT, stdout=subprocess.PIPE,
                           stderr=subprocess.DEVNULL)
        return r.stdout.decode("utf-8-sig", "replace") if r.returncode == 0 else None

    tree = Tree(ROOT)
    findings, audit = run_audit(tree, schema, args.base, added, modified, deleted_metas, base_text)
    print("audited %d added + %d modified assets, %d deleted file(s), since %s"
          % (len(added), len(modified), len(deleted_metas), args.base))
    report(findings, audit, "generated-asset audit")
    return 1 if findings else 0


# --------------------------------------------------------------------------------------------
# --self-test: each control injects ONE defect into the real tonight's assets and must be caught
# --------------------------------------------------------------------------------------------
def self_test(schema, base):
    cfg = "Assets/_SO_Assets/Swarm Fauna/SwarmFaunaConfig.asset"
    tad = "Assets/_Prefabs/FloraAndFauna/SwarmTadpole.prefab"
    tree0 = Tree(ROOT)
    added = [cfg, tad, SWARM_PROFILE, SWARM_CELL,
             "Assets/_SO_Assets/Cell Configs/Swarm Cell/Swarm Inner Mass Swarm Fauna Config Data.asset"]
    plain_cs = None  # a script file whose same-named class is NOT a MonoBehaviour/ScriptableObject
    for full, t in schema.types.items():
        if t["kind"] == "Class" and t["files"] and UNITY_OBJECT not in t["bases"] and not t["isGeneric"]:
            f = os.path.relpath(t["files"][0], ROOT)
            if os.path.basename(f) == t["name"] + ".cs" and os.path.exists(os.path.join(ROOT, f + ".meta")):
                plain_cs = f
                break
    plain_guid = META_GUID.search(open(os.path.join(ROOT, plain_cs + ".meta")).read()).group(1)
    first_member = re.search(r"^  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n",
                             tree0.read(SWARM_PROFILE).split("SupportedFaunas:")[1], re.M).group(0)
    cell_guid = meta_guid(tree0, SWARM_CELL)

    def sub(rel, old, new, count=1):
        t = tree0.read(rel)
        assert old in t, "self-test fixture drifted: %r not in %s" % (old, rel)
        return {rel: t.replace(old, new, count)}

    controls = [
        ("field", "renamed serialized key", sub(cfg, "  TickHz:", "  TickHertz:")),
        ("enum", "enum value out of range", sub(cfg, "  Model: 0", "  Model: 97")),
        ("guid", "reference to an unknown guid", sub(cfg, "guid: d45a23e6bd2da304988606fba6c97628", "guid: 0123456789abcdef0123456789abcdef")),
        ("guid", "cross-file fileID missing in the target prefab", sub(cfg, "{fileID: 5945480239701989318,", "{fileID: 5945480239701989319,")),
        ("script", "m_Script whose class is not a MonoBehaviour/ScriptableObject",
         sub(cfg, "guid: d4e318482672e4a9533d4a2ac8c4bc10", "guid: " + plain_guid)),
        ("type", "reference to an asset of the wrong class",
         sub(cfg, "Theme: {fileID: 11400000, guid: d45a23e6bd2da304988606fba6c97628", "Theme: {fileID: 11400000, guid: " + meta_guid(tree0, SWARM_PROFILE))),
        ("local", "in-file fileID that names no document", {tad: re.sub(r"m_GameObject: \{fileID: (\d+)\}", "m_GameObject: {fileID: 42}", tree0.read(tad), 1)}),
        ("yaml", "broken YAML", sub(cfg, "  UnitScale: 2\n", "  UnitScale: [2\n")),
        ("yaml", "missing %TAG header", sub(cfg, "%TAG !u! tag:unity3d.com,2011:\n", "")),
        ("meta", "duplicate guid", {"Assets/_SO_Assets/Swarm Fauna/Dup.asset.meta": tree0.read(cfg + ".meta")}),
        # Dropped from BOTH: a Spawn Matrix row is a spawn path too, and every Swarm-cell species is on it.
        ("swarm", "species config dropped from the spawn profile and the Spawn Matrix",
         {**sub(SWARM_PROFILE, first_member, ""),
          SPAWN_MATRIX_TOY: tree0.read(SPAWN_MATRIX_TOY).replace("  " + first_member, "")}),
        ("swarm", "Swarm cell removed from the Cell Selector", sub(CELL_SELECTOR_SCENE, "guid: " + cell_guid, "guid: " + "f" * 32)),
    ]
    ok = True
    clean, _ = run_audit(Tree(ROOT), schema, base, added, [], [], lambda r: None)
    if clean:
        ok = False
        print("self-test: baseline is not clean:")
        for f in clean[:10]:
            print("    ", f)
    for kind, label, overlay in controls:
        found, _ = run_audit(Tree(ROOT, overlay), schema, base, added, [], [], lambda r: None)
        hit = [f for f in found if f[0] == kind]
        print("  [%s] %-60s %s" % ("ok" if hit else "MISSED", label, (hit[0][2][:110] if hit else "")))
        ok &= bool(hit)
    # a scene override (modified file, so the per-document baseline path) naming no field
    def base_scene(rel):
        r = subprocess.run(["git", "show", "%s:%s" % (base, rel)], cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        return r.stdout.decode("utf-8-sig", "replace") if r.returncode == 0 else None
    ov = sub(CELL_SELECTOR_SCENE, "propertyPath: 'CellConfigs.Array.data[12]'", "propertyPath: 'CellConfig.Array.data[12]'")
    found, _ = run_audit(Tree(ROOT, ov), schema, base, [], [CELL_SELECTOR_SCENE], [], base_scene)
    hit = [f for f in found if f[0] == "override"]
    print("  [%s] %-60s %s" % ("ok" if hit else "MISSED", "scene override naming no field (modified file)", hit[0][2][:110] if hit else ""))
    ok &= bool(hit)
    clean_scene, _ = run_audit(Tree(ROOT), schema, base, [], [CELL_SELECTOR_SCENE], [], base_scene)
    if [f for f in clean_scene if f[0] != "swarm"]:
        ok = False
        print("self-test: the unmodified scene reports findings:", clean_scene[:3])
    # a deleted file still referenced
    found, _ = run_audit(Tree(ROOT, deleted=[cfg + ".meta"]), schema, base, added[2:], [],
                         [(cfg + ".meta", meta_guid(tree0, cfg))], lambda r: None)
    hit = [f for f in found if f[0] == "deleted"]
    print("  [%s] %-60s %s" % ("ok" if hit else "MISSED", "reference to a deleted asset", hit[0][2][:110] if hit else ""))
    ok &= bool(hit)
    print("self-test " + ("passed" if ok else "FAILED"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
