#!/usr/bin/env python3
"""The Code Atlas: the whole repository as one graph the Loop Atlas map can zoom into, down to every value.

Garrett, 2026-11-19: "lets get the entire codebase, and every value, inspectable by zooming into the right place. one
trick is to have non unique locations i.e. equivalent locations ... a graph can always be forced into a tree if you
split up its nodes into non unique nodes that appear in more than one place on the tree."

So the graph is stored ONCE, as canonical nodes with their relations, and the map turns it into a tree while you zoom:
every node's ring holds its containment children AND a copy of each neighbour (its type, its base class, the assets
that use it, the loop it implements, the folder it lives in...). A copy is the same node: zooming into any copy shows the
same record and the same rings. A cycle (A uses B, B uses A) unrolls into a chain A > B > A > B ... that never ends but
stays inside A's ring, because each ring is a fixed fraction of its parent (Docs/LoopAtlas/CODE.md).

  python3 Tools/Build/code_atlas.py build [--out DIR]   # extract, write DIR (default Docs/LoopAtlas/code-out, ignored)
  python3 Tools/Build/code_atlas.py build --strict      # ...and fail if a hand link in code/links.yaml matches nothing
  python3 Tools/Build/code_atlas.py stats               # what one build holds, by kind, without writing
  python3 Tools/Build/code_atlas.py self-test           # the parser and the linker refuse what they must

Node ids are canonical and readable; the prefix is the kind:
  d:<dir>  f:<file>  n:<namespace>  t:<Full.Type>  m:<Full.Type>.<member>  o:<file>#<fileID>  v:<file>#<fileID>|<key>
  s:<file>#<heading slug>  L:<loop key>  code (the root)
A v: record lives inside its o: record (the client splits on '|'), so values cost no record of their own.

Output: index.json (shard count, root, stats, each loop's pictures), names.json (search), shards/NNN.json.gz (records
by fnv1a(id) % count, gzipped), img/sheetNN.webp (128 px thumbnails of every picture, 256 a sheet). The page loads
index.json once, a shard the first time a ring inside it is big enough to draw, and a sheet the first time it shows a
picture from it. Needs PyYAML and Pillow (pip install pyyaml pillow); uses PyYAML's C loader when present.
"""
from __future__ import annotations

import argparse
import fnmatch
import gzip
import json
import math
import os
import re
import sys
import time
from collections import defaultdict
from pathlib import Path

try:
    import yaml
except ImportError:  # pragma: no cover
    sys.exit("code_atlas.py needs PyYAML: pip install pyyaml")

Loader = getattr(yaml, "CSafeLoader", yaml.SafeLoader)

REPO = Path(__file__).resolve().parents[2]
ATLAS = REPO / "Docs" / "LoopAtlas"
LINKS = ATLAS / "code" / "links.yaml"
DEFAULT_OUT = ATLAS / "code-out"

# What is "the codebase". Third-party code is listed as files only (no members, no values): it is not ours to read.
CS_ROOTS = ["Assets"]
THIRD_PARTY = ["Assets/Plugins/", "Assets/NiceVibrations/", "Assets/ExternalDependencyManager/", "Assets/Samples/",
               "Assets/Effects Library/", "Assets/Unity Assests/", "Assets/YethGameDev/", "Assets/Adaptive Performance/"]
YAML_EXT = {".asset", ".prefab", ".unity"}
DOC_ROOTS = ["Docs"]
SKIP_DIRS = {".git", "Library", "Temp", "Logs", "obj", "node_modules", "code-out"}
MAX_LIST = 64           # list elements kept per serialized list; the rest is counted
MAX_VAL = 160           # characters of a value shown
MAX_DOC_BYTES = 2_000_000  # a YAML document bigger than this (baked meshes, terrain) is summarised, not read
SHARD_TARGET = 900_000  # raw bytes per shard (about 90 KB gzipped)
MAX_SHARDS = 220        # the artifact takes 255 files a publish (shards + atlases + index + names)
KIND_CODE = {"root": "R", "group": "G", "dir": "d", "cs": "c", "asset": "a", "prefab": "p", "unity": "u", "md": "k",
             "img": "i", "type": "t", "ns": "n", "go": "g", "comp": "o", "pi": "I", "sec": "s", "loop": "L",
             "field": "F", "const": "K", "property": "P", "method": "M", "ctor": "C", "event": "E", "enum": "V"}
IMG_EXT = {".png", ".jpg", ".jpeg", ".tga"}
IMG_PER_NODE = 4        # pictures a ring carries
THUMB = 128             # px, square
SHEET = 16              # thumbs per sheet side: 256 a sheet

# Unity class ids worth a readable name (the rest show as "Unity object <id>").
UNITY_CLASS = {1: "GameObject", 4: "Transform", 20: "Camera", 23: "MeshRenderer", 33: "MeshFilter", 54: "Rigidbody",
               64: "MeshCollider", 65: "BoxCollider", 82: "AudioSource", 95: "Animator", 108: "Light", 111: "Animation",
               114: "MonoBehaviour", 135: "SphereCollider", 136: "CapsuleCollider", 137: "SkinnedMeshRenderer",
               198: "ParticleSystem", 199: "ParticleSystemRenderer", 205: "LODGroup", 212: "SpriteRenderer",
               222: "CanvasRenderer", 223: "Canvas", 224: "RectTransform", 225: "CanvasGroup", 1001: "PrefabInstance",
               21: "Material", 43: "Mesh", 74: "AnimationClip", 91: "AnimatorController", 28: "Texture2D",
               104: "RenderSettings", 157: "LightmapSettings", 196: "NavMeshSettings", 29: "OcclusionCullingSettings",
               120: "LineRenderer", 96: "TrailRenderer", 50: "Rigidbody2D", 58: "CircleCollider2D", 61: "BoxCollider2D"}
UNITY_BOILERPLATE = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
                     "m_GameObject", "m_EditorHideFlags", "m_Script", "m_EditorClassIdentifier", "serializedVersion"}


# ------------------------------------------------------------------------------------------------ the graph
class Graph:
    """Canonical nodes. A record is a small dict; 'c' is containment (ordered), 'r' maps a relation to node ids."""

    def __init__(self):
        self.recs: dict[str, dict] = {}

    def node(self, nid: str, kind: str, name: str, parent: str | None = None, **kw) -> dict:
        r = self.recs.get(nid)
        if r is None:
            r = {"k": kind, "n": name}
            self.recs[nid] = r
            if parent:
                r["p"] = parent
                self.child(parent, nid)
        for k, v in kw.items():
            if v not in (None, "", [], {}):
                r[k] = v
        return r

    def child(self, parent: str, nid: str):
        p = self.recs.get(parent)
        if p is None:
            return
        c = p.setdefault("c", [])
        if nid not in c:
            c.append(nid)

    def rel(self, a: str, relation: str, b: str):
        if a == b or a not in self.recs:
            return
        lst = self.recs[a].setdefault("r", {}).setdefault(relation, [])
        if b not in lst:
            lst.append(b)

    def link(self, a: str, rel_ab: str, b: str, rel_ba: str):
        """An edge, stored on both ends: each end gets the other as a neighbour (a copy in its ring)."""
        if a in self.recs and b in self.recs:
            self.rel(a, rel_ab, b)
            self.rel(b, rel_ba, a)


def rel_path(p: Path) -> str:
    return p.relative_to(REPO).as_posix()


def ensure_dirs(g: Graph, path: str) -> str:
    """The folder chain of a repo path, as d: nodes under the root's Folders. Returns the parent dir id."""
    parts = path.split("/")[:-1]
    parent = "code/folders"
    acc = []
    for part in parts:
        acc.append(part)
        did = "d:" + "/".join(acc)
        g.node(did, "dir", part, parent)
        parent = did
    return parent


def third_party(path: str) -> bool:
    return any(path.startswith(t) for t in THIRD_PARTY)


# ------------------------------------------------------------------------------------------------ C#
_KW = {"public", "private", "protected", "internal", "static", "readonly", "const", "override", "virtual", "abstract",
       "sealed", "new", "extern", "unsafe", "volatile", "partial", "async", "event", "required", "ref"}
_TYPE_DECL = re.compile(r"\b(class|struct|interface|enum|record(?:\s+struct|\s+class)?)\s+([A-Za-z_]\w*)")
_IDENT = re.compile(r"[A-Za-z_]\w*")


def mask_cs(src: str) -> str:
    """Same length as src; comments, string and char literals and preprocessor lines blanked (newlines kept)."""
    out = list(src)
    i, n = 0, len(src)

    def blank(a, b):
        for j in range(a, b):
            if out[j] != "\n":
                out[j] = " "

    line_start = True
    while i < n:
        ch = src[i]
        if line_start:
            j = i
            while j < n and src[j] in " \t":
                j += 1
            if j < n and src[j] == "#":
                k = src.find("\n", j)
                k = n if k < 0 else k
                blank(j, k)
                i = k
                continue
        line_start = False
        if ch == "\n":
            line_start = True
            i += 1
        elif src.startswith("//", i):
            k = src.find("\n", i)
            k = n if k < 0 else k
            blank(i, k)
            i = k
        elif src.startswith("/*", i):
            k = src.find("*/", i + 2)
            k = n if k < 0 else k + 2
            blank(i, k)
            i = k
        elif ch == '"' or (ch in "@$" and i + 1 < n and src[i + 1] in '"$@'):
            # regular, verbatim (@"") and interpolated ($"") strings; raw """ strings treated as verbatim-ish
            j = i
            verbatim = False
            while j < n and src[j] in "@$":
                verbatim = verbatim or src[j] == "@"
                j += 1
            if src.startswith('"""', j):
                k = src.find('"""', j + 3)
                k = n if k < 0 else k + 3
            else:
                k = j + 1
                while k < n:
                    c = src[k]
                    if verbatim:
                        if c == '"':
                            if k + 1 < n and src[k + 1] == '"':
                                k += 2
                                continue
                            k += 1
                            break
                    else:
                        if c == "\\":
                            k += 2
                            continue
                        if c == '"' or c == "\n":
                            k += 1
                            break
                    k += 1
            blank(j + 1, max(j + 1, k - 1))
            i = k
        elif ch == "'":
            k = i + 1
            while k < n and src[k] != "'" and src[k] != "\n":
                k += 2 if src[k] == "\\" else 1
            blank(i + 1, min(k, n))
            i = k + 1
        else:
            i += 1
    return "".join(out)


def _split_top(s: str, sep: str, angle: bool = True) -> list[str]:
    out, depth, cur = [], 0, []
    opens, closes = ("<([{", ">)]}") if angle else ("([{", ")]}")
    for ch in s:
        if ch in opens:
            depth += 1
        elif ch in closes:
            depth -= 1
        if ch == sep and depth == 0:
            out.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
    out.append("".join(cur))
    return out


def _strip_attrs(masked: str, orig: str) -> tuple[str, str, list[str]]:
    """Leading [Attribute] blocks off a statement. Returns (masked rest, original rest, attribute texts)."""
    attrs = []
    i = 0
    n = len(masked)
    while True:
        while i < n and masked[i].isspace():
            i += 1
        if i < n and masked[i] == "[":
            depth, j = 0, i
            while j < n:
                if masked[j] == "[":
                    depth += 1
                elif masked[j] == "]":
                    depth -= 1
                    if depth == 0:
                        break
                j += 1
            attrs.append(orig[i + 1:j].strip())
            i = j + 1
        else:
            break
    return masked[i:], orig[i:], attrs


def _attr_value(attrs: list[str], name: str) -> str | None:
    for a in attrs:
        for part in _split_top(a, ","):
            m = re.match(r"\s*(?:\w+\.)*" + name + r"(?:Attribute)?\s*\(\s*\"((?:[^\"\\]|\\.)*)\"", part)
            if m:
                return m.group(1)
    return None


def _has_attr(attrs: list[str], name: str) -> bool:
    return any(re.match(r"\s*(?:\w+\.)*" + name + r"(?:Attribute)?\b", p) for a in attrs for p in _split_top(a, ","))


def parse_cs(src: str) -> dict:
    """A light structural parse: namespaces, types (nested), members with line numbers. Not a compiler; good enough
    to place every declaration, and the self-test pins the shapes it must read."""
    masked = mask_cs(src)
    line_of = [0] * (len(src) + 1)
    ln = 1
    for i, ch in enumerate(src):
        line_of[i] = ln
        if ch == "\n":
            ln += 1
    line_of[len(src)] = ln
    types: list[dict] = []
    file_ns = None
    stack: list[dict] = []   # {"kind": ns|type|other, ...}
    stmt = 0
    i, n = 0, len(masked)

    def cur_ns():
        parts = [s["name"] for s in stack if s["kind"] == "ns"]
        if file_ns:
            parts = [file_ns] + parts
        return ".".join(parts)

    def cur_type():
        for s in reversed(stack):
            if s["kind"] == "type":
                return s
            if s["kind"] == "other":
                return None
        return None

    def first_code(a):
        while a < n and masked[a].isspace():
            a += 1
        return a

    while i < n:
        ch = masked[i]
        if ch == "{":
            a = first_code(stmt)
            m_txt, o_txt, attrs = _strip_attrs(masked[a:i], src[a:i])
            text = " ".join(m_txt.split())
            top = stack[-1]["kind"] if stack else "ns"
            mdecl = _TYPE_DECL.search(text) if top in ("ns", "type") else None
            nsm = re.match(r"namespace\s+([\w.]+)\s*$", text)
            if nsm and top == "ns":
                stack.append({"kind": "ns", "name": nsm.group(1)})
            elif mdecl and "(" not in text[:mdecl.start()] and "=" not in text[:mdecl.start()]:
                kind = mdecl.group(1).split()[0]
                name = mdecl.group(2)
                rest = text[mdecl.end():]
                gen = ""
                if rest.startswith("<"):
                    depth = 0
                    for j, c in enumerate(rest):
                        depth += c == "<"
                        depth -= c == ">"
                        if depth == 0:
                            gen = rest[:j + 1]
                            rest = rest[j + 1:]
                            break
                rest = re.sub(r"^\s*\([^)]*\)", "", rest)  # record primary constructor
                bases = []
                bm = re.match(r"\s*:\s*(.*?)(?:\bwhere\b.*)?$", rest)
                if bm:
                    bases = [b.strip() for b in _split_top(bm.group(1), ",") if b.strip()]
                parent = cur_type()
                mods = set(text[:mdecl.start()].split())
                t = {"kind": kind, "name": name, "gen": gen, "bases": bases, "ns": cur_ns(), "line": line_of[a],
                     "outer": parent["t"] if parent else None, "members": [], "body_start": i + 1,
                     "attrs": attrs, "mods": sorted(mods & _KW), "header": None}
                types.append(t)
                stack.append({"kind": "type", "t": t, "header": None})
            else:
                ct = stack[-1] if stack and stack[-1]["kind"] == "type" else None
                if ct is not None and text:
                    _member(ct, text, o_txt, attrs, line_of[first_code(i - len(m_txt))], body=True)
                stack.append({"kind": "other"})
            stmt = i + 1
        elif ch == "}":
            if stack:
                s = stack.pop()
                if s["kind"] == "type":
                    t = s["t"]
                    t["body_end"] = i
                    if t["kind"] == "enum":
                        body_m = masked[t["body_start"]:i]
                        body_o = src[t["body_start"]:i]
                        off = t["body_start"]
                        pos = 0
                        for part in _split_top(body_m, ",", angle=False):
                            seg_o = body_o[pos:pos + len(part)]
                            p_m, p_o, p_attrs = _strip_attrs(part, seg_o)
                            mm = re.match(r"\s*([A-Za-z_]\w*)\s*(?:=\s*(.+?))?\s*$", p_m, re.S)
                            if mm:
                                val = None
                                if mm.group(2):
                                    vo = re.search(r"=\s*(.+?)\s*$", p_o, re.S)
                                    val = " ".join(vo.group(1).split()) if vo else None
                                at = off + pos + (len(part) - len(p_m)) + mm.start(1)
                                t["members"].append({"kind": "enum", "name": mm.group(1), "value": val,
                                                     "line": line_of[min(at, len(line_of) - 1)]})
                            pos += len(part) + 1
            stmt = i + 1
        elif ch == ";":
            a = first_code(stmt)
            m_txt, o_txt, attrs = _strip_attrs(masked[a:i], src[a:i])
            text = " ".join(m_txt.split())
            top = stack[-1] if stack else None
            fm = re.match(r"namespace\s+([\w.]+)\s*$", text)
            if fm and (top is None or top["kind"] == "ns"):
                file_ns = fm.group(1)
            elif top is not None and top["kind"] == "type" and text:
                _member(top, text, " ".join(o_txt.split()), attrs, line_of[first_code(i - len(m_txt))], body=False)
            stmt = i + 1
        i += 1
    for t in types:
        t.pop("body_start", None)
    return {"types": types, "masked": masked}


def _member(scope: dict, text: str, orig: str, attrs: list[str], line: int, body: bool):
    t = scope["t"]
    header = _attr_value(attrs, "Header")
    if header is not None:
        scope["header"] = header
    tooltip = _attr_value(attrs, "Tooltip")
    words = text.split()
    mods = []
    while words and words[0] in _KW:
        mods.append(words.pop(0))
    rest = " ".join(words)
    if not rest or rest.startswith("=>") or rest.startswith("("):
        return
    m = {"line": line, "mods": mods, "header": scope["header"]}
    if tooltip:
        m["tip"] = tooltip
    eq = _top_index(rest, "=")
    par = rest.find("(")
    arrow = rest.find("=>")
    if par >= 0 and (eq < 0 or par < eq) and (arrow < 0 or par < arrow):
        head = rest[:par].strip()
        hw = head.split()
        if not hw:
            return
        name = re.sub(r"<.*$", "", hw[-1])
        if name in ("if", "for", "foreach", "while", "switch", "using", "lock", "return", "catch", "fixed"):
            return
        if name == "operator" or (len(hw) >= 2 and hw[-2] == "operator"):
            name = "operator " + hw[-1]
        m.update(kind="ctor" if name == t["name"] or name == "~" + t["name"] else "method", name=name,
                 type=" ".join(hw[:-1]), sig=" ".join(rest[:_close_paren(rest, par) + 1].split()))
    elif body or (arrow >= 0 and (eq < 0 or arrow <= eq)):
        head = rest[:arrow].strip() if (arrow >= 0 and not body) else rest.strip()
        head = re.sub(r"\s*=.*$", "", head) if body else head
        hw = head.split()
        if len(hw) < 2:
            return
        name = hw[-1]
        if name == "this" or name.endswith("]"):
            name = "this[]"
        m.update(kind="event" if "event" in mods else "property", name=name, type=" ".join(hw[:-1]))
    else:
        decl = _split_top(rest, ",")[0]
        dm = re.match(r"^(.*?[\w>\]?)])\s+([A-Za-z_]\w*)\s*(?:=\s*(.*))?$", decl.strip(), re.S)
        if not dm:
            return
        ftype, name = dm.group(1).strip(), dm.group(2)
        kind = "const" if "const" in mods else ("event" if "event" in mods else "field")
        m.update(kind=kind, name=name, type=ftype)
        om = re.search(r"\b" + re.escape(name) + r"\s*=\s*(.*)$", orig, re.S)
        if om and dm.group(3) is not None:
            m["value"] = " ".join(_split_top(om.group(1), ",")[0].split())[:MAX_VAL]
        serial = _has_attr(attrs, "SerializeField") or _has_attr(attrs, "SerializeReference") or (
            "public" in mods and "static" not in mods and "const" not in mods and "readonly" not in mods)
        if kind == "field" and serial and not _has_attr(attrs, "NonSerialized") and not _has_attr(attrs, "HideInInspector"):
            m["ser"] = True
        elif kind == "field" and serial:
            m["ser"] = "hidden"
    t["members"].append(m)


def _top_index(s: str, ch: str) -> int:
    depth = 0
    for i, c in enumerate(s):
        if c in "<([{":
            depth += 1
        elif c in ">)]}":
            depth -= 1
        elif c == ch and depth == 0:
            if ch == "=" and (s[i + 1:i + 2] in ("=", ">") or s[i - 1:i] in ("!", "<", ">", "=")):
                continue
            return i
    return -1


def _close_paren(s: str, i: int) -> int:
    depth = 0
    for j in range(i, len(s)):
        depth += s[j] == "("
        depth -= s[j] == ")"
        if depth == 0:
            return j
    return len(s) - 1


def type_idents(text: str) -> list[str]:
    return [w for w in _IDENT.findall(text or "") if w[0].isupper()]


# ------------------------------------------------------------------------------------------------ Unity YAML
_DOC_HEAD = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", re.M)


def unity_docs(text: str):
    heads = list(_DOC_HEAD.finditer(text))
    for i, h in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(text)
        yield int(h.group(1)), h.group(2), text[h.end():end]


def load_doc(body: str):
    if len(body) > MAX_DOC_BYTES:
        first = body.lstrip().split("\n", 1)[0].rstrip(":")
        return first, None
    try:
        d = yaml.load(body, Loader=Loader)
    except yaml.YAMLError:
        return None, None
    if not isinstance(d, dict) or not d:
        return None, None
    cls = next(iter(d))
    return cls, d[cls] if isinstance(d[cls], dict) else {}


def is_ref(v) -> bool:
    return isinstance(v, dict) and "fileID" in v and set(v) <= {"fileID", "guid", "type"}


def flatten(val, key: str, out: list):
    """Serialized data into (key path, value) pairs; references stay dicts for the linker."""
    if is_ref(val):
        out.append((key, val))
    elif isinstance(val, dict):
        if not val:
            out.append((key, "{}"))
        for k, v in val.items():
            flatten(v, f"{key}.{k}" if key else str(k), out)
    elif isinstance(val, list):
        if not val:
            out.append((key, "[]"))
            return
        if all(not isinstance(x, (dict, list)) for x in val) and len(val) <= 8:
            out.append((key, "[" + ", ".join(_scalar(x) for x in val) + "]"))
            return
        for j, x in enumerate(val[:MAX_LIST]):
            flatten(x, f"{key}[{j}]", out)
        if len(val) > MAX_LIST:
            out.append((f"{key}[…]", f"{len(val) - MAX_LIST} more items"))
    else:
        s = _scalar(val)
        if isinstance(val, str) and len(val) > 40 and re.fullmatch(r"[0-9a-f]+", val):
            s = f"<{len(val) // 2} bytes of data>"
        out.append((key, s))


def _scalar(x) -> str:
    if x is None:
        return "null"
    if isinstance(x, bool):
        return "true" if x else "false"
    if isinstance(x, float):
        return repr(round(x, 6)).rstrip("0").rstrip(".") if x != int(x) else str(int(x))
    s = str(x)
    return s if len(s) <= MAX_VAL else s[:MAX_VAL - 1] + "…"


# ------------------------------------------------------------------------------------------------ build
class Builder:
    def __init__(self, log=True):
        self.g = Graph()
        self.log = log
        self.guid_path: dict[str, str] = {}
        self.script_type: dict[str, str] = {}     # .cs path -> type id (the file's main class)
        self.types_by_short: dict[str, list[str]] = defaultdict(list)
        self.type_info: dict[str, dict] = {}
        self.cs_parsed: dict[str, dict] = {}
        self.files: dict[str, str] = {}           # rel path -> f: id
        self.stats = defaultdict(int)

    def say(self, *a):
        if self.log:
            print(*a, file=sys.stderr, flush=True)

    # -- walk
    def walk(self, roots, exts):
        for r in roots:
            base = REPO / r
            for dp, dns, fns in os.walk(base):
                dns[:] = sorted(d for d in dns if d not in SKIP_DIRS and not d.startswith("."))
                for fn in sorted(fns):
                    if os.path.splitext(fn)[1] in exts:
                        yield Path(dp) / fn

    def build(self, include_scenes=True):
        g = self.g
        t0 = time.time()
        g.node("code", "root", "The codebase", None, d="Every folder, file, type, member and value of the repository.")
        g.node("code/folders", "group", "Folders", "code")
        g.node("code/namespaces", "group", "Namespaces", "code")
        g.node("code/loops", "group", "Loops", "code")
        # guids
        for meta in self.walk(CS_ROOTS, {".meta"}):
            try:
                head = meta.read_text(errors="ignore")[:400]
            except OSError:
                continue
            m = re.search(r"^guid:\s*([0-9a-f]{32})", head, re.M)
            if m:
                self.guid_path[m.group(1)] = rel_path(meta)[:-5]
        self.say(f"guids {len(self.guid_path)} ({time.time() - t0:.1f}s)")
        self.cs_pass()
        self.say(f"C# done ({time.time() - t0:.1f}s)")
        self.images_pass()   # before the YAML, so a reference to a picture finds its node
        self.say(f"images done ({time.time() - t0:.1f}s)")
        self.yaml_pass(include_scenes)
        self.say(f"YAML done ({time.time() - t0:.1f}s)")
        self.docs_pass()
        self.say(f"docs done ({time.time() - t0:.1f}s)")
        self.loops_pass()
        self.say(f"loops done ({time.time() - t0:.1f}s)")
        self.imagery()
        self.say(f"imagery done ({time.time() - t0:.1f}s)")
        return self

    def file_node(self, path: str, kind: str, **kw) -> str:
        fid = "f:" + path
        parent = ensure_dirs(self.g, path)
        self.g.node(fid, kind, path.rsplit("/", 1)[-1], parent, path=path, **kw)
        self.files[path] = fid
        return fid

    # -- C#
    def cs_pass(self):
        g = self.g
        files = list(self.walk(CS_ROOTS, {".cs"}))
        for p in files:
            path = rel_path(p)
            tp = third_party(path)
            fid = self.file_node(path, "cs", tp=1 if tp else None)
            if tp:
                self.stats["cs third-party"] += 1
                continue
            src = p.read_text(encoding="utf-8-sig", errors="replace")
            try:
                parsed = parse_cs(src)
            except Exception as e:  # pragma: no cover - a parse bug must not stop the map
                self.say(f"  parse failed {path}: {e}")
                continue
            self.cs_parsed[path] = parsed
            g.recs[fid]["lines"] = src.count("\n") + 1
            for t in parsed["types"]:
                full = (t["outer"]["full"] + "." if t["outer"] else (t["ns"] + "." if t["ns"] else "")) + t["name"]
                t["full"] = full
                tid = "t:" + full
                parent = "t:" + t["outer"]["full"] if t["outer"] else fid
                rec = g.node(tid, "type", t["name"] + (t["gen"] or ""), parent, tk=t["kind"], path=path, l=t["line"],
                             mods=" ".join(t["mods"]) or None)
                if t["outer"] is None and parent != fid:
                    g.child(fid, tid)
                if t["outer"] is None:
                    g.rel(fid, "declares", tid) if parent != fid else None
                self.types_by_short[t["name"]].append(tid)
                self.type_info[tid] = t
                if t["ns"]:
                    self.ns_node(t["ns"])
                    g.link("n:" + t["ns"], "types", tid, "namespace")
                if not t["outer"] and p.stem == t["name"]:
                    self.script_type[path] = tid
                for mb in t["members"]:
                    mid = f"{tid}.{mb['name']}"
                    if mid in g.recs:  # overloads
                        k = 2
                        while f"{mid}~{k}" in g.recs:
                            k += 1
                        mid = f"{mid}~{k}"
                    kw = {"tk": mb["kind"], "l": mb["line"], "ty": mb.get("type"), "v": mb.get("value"),
                          "sig": mb.get("sig"), "tip": mb.get("tip"), "hd": mb.get("header"),
                          "ser": mb.get("ser"), "mods": " ".join(mb.get("mods", [])) or None}
                    g.node("m:" + mid[2:], "member", mb["name"], tid, **kw)
                    self.stats["member " + mb["kind"]] += 1
                self.stats["type " + t["kind"]] += 1
            if path not in self.script_type:
                tops = [t for t in parsed["types"] if not t["outer"]]
                if len(tops) == 1:
                    self.script_type[path] = "t:" + tops[0]["full"]
        # second pass: relations between types (bases, field types, mentions)
        for tid, t in self.type_info.items():
            for b in t["bases"]:
                for bt in self.resolve(type_idents(re.sub(r"<.*", "", b))[:1], t):
                    g.link(tid, "base", bt, "derived")
            for mb in t["members"]:
                if mb["kind"] in ("field", "property", "const", "event", "method"):
                    mid = "m:" + f"{tid}.{mb['name']}"[2:]
                    if mid not in g.recs:
                        continue
                    for ft in self.resolve(type_idents(mb.get("type")), t):
                        g.link(mid, "type", ft, "usedBy")
                        g.link(tid, "uses", ft, "usedByType")
        for path, parsed in self.cs_parsed.items():
            masked = parsed["masked"]
            for t in parsed["types"]:
                if t["outer"]:
                    continue
                tid = "t:" + t["full"]
                body = masked[t.get("body_start", 0):t.get("body_end", len(masked))] if "body_end" in t else masked
                seen = set()
                for w in set(_IDENT.findall(body)):
                    if w in self.types_by_short and w != t["name"]:
                        for ot in self.resolve([w], t):
                            if ot not in seen and ot != tid and not ot.startswith(tid + "."):
                                seen.add(ot)
                                g.link(tid, "mentions", ot, "mentionedBy")

    def ns_node(self, ns: str):
        parent = "code/namespaces"
        acc = []
        for part in ns.split("."):
            acc.append(part)
            nid = "n:" + ".".join(acc)
            self.g.node(nid, "ns", part, parent)
            parent = nid

    def resolve(self, names, ctx: dict) -> list[str]:
        out = []
        ns = ctx.get("ns", "") if ctx else ""
        for w in names:
            cands = self.types_by_short.get(w)
            if not cands:
                continue
            if len(cands) == 1:
                out.append(cands[0])
                continue
            best = sorted(cands, key=lambda c: -_common_prefix(c[2:], ns))
            out.append(best[0])
        return out

    def type_of_script(self, ref) -> str | None:
        if not is_ref(ref) or "guid" not in ref:
            return None
        path = self.guid_path.get(ref["guid"])
        return self.script_type.get(path) if path else None

    def field_member(self, tid: str | None, key: str) -> str | None:
        if not tid:
            return None
        name = re.split(r"[.\[]", key, maxsplit=1)[0]
        seen = set()
        while tid and tid not in seen:
            seen.add(tid)
            mid = "m:" + tid[2:] + "." + name
            if mid in self.g.recs:
                return mid
            bases = self.g.recs.get(tid, {}).get("r", {}).get("base", [])
            tid = bases[0] if bases else None
        return None

    # -- Unity YAML
    def yaml_pass(self, include_scenes: bool):
        g = self.g
        exts = set(YAML_EXT) if include_scenes else YAML_EXT - {".unity"}
        files = [p for p in self.walk(CS_ROOTS, exts)]
        pending_refs = []
        for idx, p in enumerate(files):
            path = rel_path(p)
            if third_party(path):
                self.file_node(path, p.suffix[1:], tp=1)
                continue
            try:
                text = p.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            if not text.startswith("%YAML"):
                self.file_node(path, p.suffix[1:], bin=1)
                continue
            fid = self.file_node(path, p.suffix[1:])
            objs = {}
            for cid, fileid, body in unity_docs(text):
                cls, data = load_doc(body)
                if cls is None:
                    continue
                objs[fileid] = (cid, cls, data)
            self.unity_objects(path, fid, objs, pending_refs)
            if idx % 500 == 0:
                self.say(f"  yaml {idx}/{len(files)}")
        for oid, key, ref in pending_refs:
            tgt = self.ref_target(ref, oid)
            if tgt:
                rec = g.recs[oid]
                for row in rec.get("f", []):
                    if row[0] == key:
                        row[1:] = ["→ " + self.g.recs[tgt]["n"], tgt]
                        break
                g.rel(oid, "refs", tgt)
                g.rel(tgt, "refBy", oid)
                if oid.split("#")[0] != tgt.split("#")[0]:
                    a, b = oid.split("#")[0], tgt.split("#")[0]
                    g.link(a, "assetRefs", b, "assetRefBy")

    def ref_target(self, ref, oid: str) -> str | None:
        fid = str(ref.get("fileID", 0))
        if fid == "0":
            return None
        if "guid" in ref:
            path = self.guid_path.get(ref["guid"])
            if not path:
                return None
            fnode = "f:" + path
            if fnode not in self.g.recs:
                return None
            obj = f"o:{path}#{fid}"
            return obj if obj in self.g.recs else fnode
        local = oid.split("#")[0] + "#" + fid
        return local if local in self.g.recs else None

    def unity_objects(self, path: str, fid: str, objs: dict, pending_refs: list):
        g = self.g
        base = "o:" + path[2:] if path.startswith("f:") else "o:" + path
        go_of = {}
        # GameObjects and their transforms make the hierarchy; everything else hangs off its GameObject
        children_of = defaultdict(list)
        tf_go = {}
        for fileid, (cid, cls, data) in objs.items():
            if data is None:
                continue
            if cls in ("Transform", "RectTransform"):
                go = (data.get("m_GameObject") or {}).get("fileID")
                tf_go[fileid] = str(go) if go is not None else None
        for fileid, (cid, cls, data) in objs.items():
            if data is None:
                continue
            if cls in ("Transform", "RectTransform"):
                father = str((data.get("m_Father") or {}).get("fileID", 0))
                children_of[father].append(fileid)
        def oname(fileid, cid, cls, data):
            if data is None:
                return cls or UNITY_CLASS.get(cid, f"Unity object {cid}")
            if cls == "GameObject":
                return str(data.get("m_Name") or "GameObject")
            if cls == "MonoBehaviour":
                st = self.type_of_script(data.get("m_Script"))
                nm = data.get("m_Name")
                tn = st.rsplit(".", 1)[-1] if st else "Script (unresolved)"
                return f"{nm} ({tn})" if nm and path.endswith(".asset") else tn
            if cls == "PrefabInstance":
                sp = self.guid_path.get(((data.get("m_SourcePrefab") or {}).get("guid")) or "", "")
                return "Prefab: " + (sp.rsplit("/", 1)[-1] if sp else "?")
            return cls
        # create object nodes: GameObjects first so components can hang under them
        order = sorted(objs.items(), key=lambda kv: 0 if kv[1][1] == "GameObject" else 1)
        for fileid, (cid, cls, data) in order:
            oid = f"o:{path}#{fileid}"
            parent = fid
            if data is not None and cls != "GameObject":
                go = (data.get("m_GameObject") or {}).get("fileID")
                if go and f"o:{path}#{go}" in g.recs:
                    parent = f"o:{path}#{go}"
            kind = "go" if cls == "GameObject" else ("pi" if cls == "PrefabInstance" else "comp")
            rec = g.node(oid, kind, oname(fileid, cid, cls, data), parent, uc=cls or UNITY_CLASS.get(cid))
            self.stats["object " + (cls or str(cid))] += 1
            if data is None:
                rec["big"] = 1
                continue
            st = self.type_of_script(data.get("m_Script")) if cls == "MonoBehaviour" else None
            if st:
                g.link(oid, "script", st, "instances")
            rows = []
            if cls == "PrefabInstance":
                mod = (data.get("m_Modification") or {})
                src = self.guid_path.get(((data.get("m_SourcePrefab") or {}).get("guid")) or "")
                if src and "f:" + src in g.recs:
                    g.link(oid, "source", "f:" + src, "instancedIn")
                for mrow in (mod.get("m_Modifications") or [])[:400]:
                    if not isinstance(mrow, dict):
                        continue
                    key = str(mrow.get("propertyPath"))
                    v = mrow.get("objectReference")
                    if is_ref(v) and str(v.get("fileID")) != "0":
                        rows.append([key, "→ reference"])
                        pending_refs.append((oid, key, v))
                    else:
                        rows.append([key, _scalar(mrow.get("value"))])
            else:
                flat = []
                for k, v in data.items():
                    if k in UNITY_BOILERPLATE or (cls == "GameObject" and k in ("m_Component",)):
                        continue
                    if cls in ("Transform", "RectTransform") and k in ("m_Children", "m_Father"):
                        continue
                    flatten(v, str(k), flat)
                for key, v in flat:
                    # the field a value belongs to is found by the page: m:<script type>.<first key>, up the bases
                    if is_ref(v):
                        if str(v.get("fileID")) == "0":
                            rows.append([key, "none"])
                        else:
                            rows.append([key, "→ reference"])
                            pending_refs.append((oid, key, v))
                    else:
                        rows.append([key, v])
            if rows:
                rec["f"] = rows
                self.stats["values"] += len(rows)
        # hierarchy: a GameObject's child GameObjects (through transforms) become its containment children
        for fileid, (cid, cls, data) in objs.items():
            if cls not in ("Transform", "RectTransform") or data is None:
                continue
            go = tf_go.get(fileid)
            if not go:
                continue
            father = str((data.get("m_Father") or {}).get("fileID", 0))
            child_go = f"o:{path}#{go}"
            if father != "0":
                pgo = tf_go.get(father)
                if pgo and f"o:{path}#{pgo}" in g.recs and child_go in g.recs:
                    self.reparent(child_go, f"o:{path}#{pgo}")

    def reparent(self, nid: str, new_parent: str):
        g = self.g
        r = g.recs[nid]
        old = r.get("p")
        if old == new_parent:
            return
        if old and old in g.recs and "c" in g.recs[old] and nid in g.recs[old]["c"]:
            g.recs[old]["c"].remove(nid)
        r["p"] = new_parent
        g.child(new_parent, nid)

    # -- images: every first-party picture is a file node, and the pictures are the map's imagery (OneZoom-style)
    def images_pass(self):
        self.images: list[str] = []          # image index -> repo path
        self.img_score: dict[str, float] = {}
        for p in self.walk(CS_ROOTS, IMG_EXT):
            path = rel_path(p)
            if third_party(path):
                continue
            try:
                from PIL import Image
                with Image.open(p) as im:
                    w, h = im.size
            except Exception:
                continue
            fid = self.file_node(path, "img", wh=f"{w}x{h}")
            self.img_score[fid] = image_score(path, w, h)

    def imagery(self):
        """Each node's pictures, most telling first. A node's own picture is one it references (an asset's card
        background, a vessel's portrait, a type's instances' pictures); a node with none borrows its children's,
        a few from each in turn. Zooming out, a picture follows its node up into the parent; zooming in, the parent's
        pictures split back into the children that own them (Garrett, 2026-11-19, after OneZoom)."""
        g = self.g
        score = self.img_score
        own: dict[str, list[str]] = {}
        for fid in score:
            own[fid] = [fid]
        def best(ids):
            c = sorted({i for i in ids if i in score}, key=lambda i: -score[i])
            return c[:IMG_PER_NODE]
        for oid, r in g.recs.items():
            if oid.startswith("o:"):
                b = best(r.get("r", {}).get("refs", []))
                if b:
                    own[oid] = b
        # an asset or prefab file: the best pictures its objects reference
        files = defaultdict(list)
        for oid, b in own.items():
            if oid.startswith("o:"):
                files["f:" + oid[2:].split("#")[0]].extend(b)
        for fid, lst in files.items():
            if fid in g.recs:
                own[fid] = best(lst)
        # a type: its instances' pictures; then a loop: its code's (both after the files are known)
        for tid, r in g.recs.items():
            if r["k"] == "type":
                lst = [i for o in r.get("r", {}).get("instances", []) for i in own.get(o, own.get("f:" + o[2:].split("#")[0], []))]
                if lst:
                    own[tid] = best(lst)
        # propagate up the containment tree, children before parents
        order, stack = [], ["code"]
        while stack:
            nid = stack.pop()
            order.append(nid)
            stack.extend(g.recs[nid].get("c", []))
        im: dict[str, list[str]] = {}
        loops_first = [i for i in order if i.startswith("L:")]
        for phase in (False, True):
            for nid in reversed(order):
                if nid.startswith("L:") != phase:
                    continue
                r = g.recs[nid]
                mine = list(own.get(nid, []))
                if nid.startswith("L:"):
                    mine += [i for c in r.get("r", {}).get("code", []) for i in im.get(c, [])[:2]]
                    mine = best(mine)
                kids = [im[c] for c in r.get("c", []) if im.get(c)]
                kids.sort(key=lambda l: -score[l[0]])
                out = []
                for i in mine:
                    if i not in out:
                        out.append(i)
                rnd = 0
                while len(out) < IMG_PER_NODE and any(rnd < len(k) for k in kids):
                    for k in kids:
                        if rnd < len(k) and k[rnd] not in out and len(out) < IMG_PER_NODE:
                            out.append(k[rnd])
                    rnd += 1
                if out:
                    im[nid] = out
        index = {}
        used = sorted({i for l in im.values() for i in l} | set(score), key=lambda i: -score[i])
        for i in used:
            index[i] = len(index)
        self.images = [i[2:] for i in used]
        for nid, l in im.items():
            g.recs[nid]["im"] = [index[i] for i in l]
        del loops_first

    # -- docs: markdown files and their heading tree
    def docs_pass(self):
        g = self.g
        for p in self.walk(DOC_ROOTS + ["Assets/_Scripts"], {".md"}):
            path = rel_path(p)
            if "/code-out/" in path:
                continue
            fid = self.file_node(path, "md")
            stack = [(0, fid)]
            slugs = set()
            try:
                lines = p.read_text(encoding="utf-8", errors="replace").split("\n")
            except OSError:
                continue
            fence = False
            for i, line in enumerate(lines, 1):
                if line.startswith("```"):
                    fence = not fence
                if fence:
                    continue
                m = re.match(r"^(#{1,6})\s+(.*?)\s*#*\s*$", line)
                if not m:
                    continue
                lvl, title = len(m.group(1)), m.group(2)
                slug = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")[:60] or "h"
                s0, k = slug, 2
                while slug in slugs:
                    slug = f"{s0}-{k}"
                    k += 1
                slugs.add(slug)
                while stack and stack[-1][0] >= lvl:
                    stack.pop()
                parent = stack[-1][1] if stack else fid
                sid = f"s:{path}#{slug}"
                g.node(sid, "sec", title[:120], parent, l=i)
                stack.append((lvl, sid))
            g.recs[fid]["lines"] = len(lines)

    # -- loops: the Loop Atlas model's loops, linked to the code that implements them
    def loops_pass(self):
        g = self.g
        model = json.loads((ATLAS / "model.json").read_text())
        links = yaml.load(LINKS.read_text(), Loader=Loader) if LINKS.exists() else {}
        links = links or {}
        nodes = {n["id"]: n for n in model["nodes"]}

        def lkey(i):
            return "L:" + i

        for n in model["nodes"]:  # parent first: model order is depth-first from era
            parent = lkey(n["parent"]) if n.get("parent") else "code/loops"
            g.node(lkey(n["id"]), "loop", n["name"], parent, tier=n.get("tier"), lid=n["id"])
        for V in model["fleetOrder"]:
            f = model["fleet"][V]
            vid = "L:vessel/" + V
            g.node(vid, "loop", f["label"], "L:fleet", tier=2, lid="fleet/" + V)
            for E, v in (f.get("verbs") or {}).items():
                g.node(f"L:verb/{V}/{E}", "loop", v.get("ability") or E, vid, tier=1, el=E)
        # automatic links: every repo path a loop cites
        def cite(lid, text):
            for path in re.findall(r"((?:Docs|Assets|Tools)/[^\s,;()]+?\.(?:md|cs|asset|prefab|unity|json|py))", text or ""):
                if "f:" + path in g.recs:
                    g.link(lid, "code", "f:" + path, "loops")
        for n in model["nodes"]:
            for s in n.get("src") or []:
                cite(lkey(n["id"]), s)
        # vessels: their class asset, ability map, prefabs and types named after them
        so_vessel = [i for i, r in g.recs.items() if r["k"] == "comp" and r["n"].endswith("(SO_Vessel)")]
        for V in model["fleetOrder"]:
            f = model["fleet"][V]
            vid = "L:vessel/" + V
            for E, v in (f.get("verbs") or {}).items():
                cite(f"L:verb/{V}/{E}", v.get("source"))
            cite(vid, f.get("doc"))
            for oid in so_vessel:
                if re.search(r"(^|[^A-Za-z])" + re.escape(V) + r"([^A-Za-z]|$)", oid.split("#")[0].rsplit("/", 1)[-1]):
                    g.link(vid, "code", oid.split("#")[0].replace("o:", "f:", 1), "loops")
            amap = "f:Assets/Resources/ElementalAbilityMaps/" + V + ".asset"
            if amap in g.recs:
                g.link(vid, "code", amap, "loops")
            for short, tids in self.types_by_short.items():
                if short.startswith(V) and len(short) > len(V) and short[len(V)].isupper():
                    for tid in tids[:1]:
                        g.link(vid, "code", tid, "loops")
        # games named on the map: their arcade card assets
        cards = {}
        for i, r in g.recs.items():
            if r["k"] == "comp" and r["n"].endswith("(SO_ArcadeGame)"):
                for row in r.get("f", []):
                    if row[0] in ("DisplayName", "Name") and isinstance(row[1], str):
                        cards.setdefault(row[1].lower(), []).append(i.split("#")[0].replace("o:", "f:", 1))
        for V in model["fleetOrder"]:
            f = model["fleet"][V]
            for E, gl in (f.get("games") or {}).items():
                for gname in gl or []:
                    for fpath in cards.get(gname.lower(), []):
                        g.link(f"L:verb/{V}/{E}" if f"L:verb/{V}/{E}" in g.recs else "L:vessel/" + V, "code", fpath, "loops")
        for n in model["nodes"]:
            for fpath in cards.get(n["name"].lower(), []):
                g.link(lkey(n["id"]), "code", fpath, "loops")
        # hand links: Docs/LoopAtlas/code/links.yaml, loop id -> ids or globs over ids
        all_ids = None
        self.missing_links = []
        for lid, pats in links.items():
            lnode = lid if lid.startswith("L:") else lkey(lid)
            if lnode not in g.recs:
                self.missing_links.append(f"{lid}: no such loop")
                continue
            for pat in pats or []:
                if any(ch in pat for ch in "*?["):
                    if all_ids is None:
                        all_ids = list(g.recs)
                    hits = [i for i in all_ids if fnmatch.fnmatchcase(i, pat)][:60]
                else:
                    hits = [pat] if pat in g.recs else []
                if not hits:
                    self.missing_links.append(f"{lid}: {pat} matches nothing")
                for h in hits:
                    g.link(lnode, "code", h, "loops")

    def write_atlases(self, out: Path):
        """Thumbnails of every picture, packed SHEET x SHEET per sheet (webp, alpha kept). Image i is sheet i // 256,
        cell i % 256, row-major."""
        from PIL import Image
        (out / "img").mkdir(parents=True, exist_ok=True)
        for old in (out / "img").glob("*"):
            old.unlink()
        per = SHEET * SHEET
        for sheet_i in range(math.ceil(len(self.images) / per)):
            sheet = Image.new("RGBA", (SHEET * THUMB, SHEET * THUMB), (0, 0, 0, 0))
            for j, path in enumerate(self.images[sheet_i * per:(sheet_i + 1) * per]):
                try:
                    with Image.open(REPO / path) as im:
                        im = im.convert("RGBA")
                        w, h = im.size
                        side = min(w, h)
                        im = im.crop(((w - side) // 2, (h - side) // 2, (w - side) // 2 + side, (h - side) // 2 + side))
                        im = im.resize((THUMB, THUMB), Image.LANCZOS)
                        sheet.paste(im, ((j % SHEET) * THUMB, (j // SHEET) * THUMB))
                except Exception:
                    continue
            sheet.save(out / "img" / f"sheet{sheet_i:02d}.webp", "WEBP", quality=78, method=6)

    # -- write
    def write(self, out: Path):
        g = self.g
        # drop empty relation lists and sort relation members for stable output
        for r in g.recs.values():
            if "r" in r:
                for k in list(r["r"]):
                    r["r"][k] = sorted(set(r["r"][k]))
        # each record carries its children's kinds (one letter each) and, where the id does not say it, their names,
        # so the page can group and label a ring's children before it loads their records
        for r in g.recs.values():
            c = r.get("c")
            if not c:
                continue
            ck, cn, named = [], [], False
            for cid in c:
                cr = g.recs[cid]
                code = KIND_CODE.get(cr.get("tk") if cr["k"] == "member" else cr["k"], "?")
                ck.append("S" if code == "F" and cr.get("ser") is True else code)
                if cid[:2] in ("o:", "s:", "L:") or cid.startswith("code"):
                    cn.append(cr["n"])
                    named = True
                else:
                    cn.append("")
            r["ck"] = "".join(ck)
            if named:
                r["cn"] = cn
        blobs = {i: json.dumps(r, separators=(",", ":"), ensure_ascii=False) for i, r in g.recs.items()}
        total = sum(len(i) + len(b) + 4 for i, b in blobs.items())
        count = max(1, min(MAX_SHARDS, math.ceil(total / SHARD_TARGET)))
        shards = defaultdict(dict)
        for i, b in blobs.items():
            shards[fnv1a(i) % count][i] = b
        (out / "shards").mkdir(parents=True, exist_ok=True)
        for old in (out / "shards").glob("*.json*"):
            old.unlink()
        for s, recs in shards.items():
            body = "{" + ",".join(json.dumps(i, ensure_ascii=False) + ":" + b for i, b in sorted(recs.items())) + "}"
            # gzip: the page inflates with DecompressionStream; mtime 0 keeps a rebuild byte-identical
            (out / "shards" / f"{s:03d}.json.gz").write_bytes(gzip.compress(body.encode("utf-8"), 9, mtime=0))
        kinds = defaultdict(int)
        for r in g.recs.values():
            kinds[r["k"]] += 1
        values = sum(len(r.get("f", [])) for r in g.recs.values())
        loop_im = {r["lid"]: r["im"] for r in g.recs.values() if r["k"] == "loop" and "lid" in r and "im" in r}
        loop_im.update({"verb/" + i[len("L:verb/"):]: r["im"] for i, r in g.recs.items() if i.startswith("L:verb/") and "im" in r})
        index = {"schema": 1, "shards": count, "hash": "fnv1a32", "root": "code", "nodes": len(g.recs),
                 "images": len(self.images), "thumb": THUMB, "sheet": SHEET, "loopImages": loop_im,
                 "loopCode": {**{r["lid"]: len(r.get("r", {}).get("code", [])) for r in g.recs.values() if r["k"] == "loop" and "lid" in r},
                              **{"verb/" + i[len("L:verb/"):]: len(r.get("r", {}).get("code", [])) for i, r in g.recs.items() if i.startswith("L:verb/")}},
                 "values": values, "bytes": total, "kinds": dict(sorted(kinds.items())),
                 "missingLinks": getattr(self, "missing_links", [])}
        (out / "index.json").write_text(json.dumps(index, indent=1))
        self.write_atlases(out)
        names = [[i, r["n"]] for i, r in g.recs.items() if r["k"] in ("type", "loop", "ns") or
                 (r["k"] in ("cs", "asset", "prefab", "unity", "md"))]
        (out / "names.json").write_text(json.dumps(names, separators=(",", ":"), ensure_ascii=False))
        return index


def image_score(path: str, w: int, h: int) -> float:
    """How well a picture stands for what holds it: big illustrations first, interface bits and placeholders last."""
    s = math.log2(max(1, w * h))
    low = path.lower()
    for key, bonus in (("cardbackgrounds", 8), ("pilots/", 7), ("scenes/", 3), ("codex", 5), ("hangar", 4),
                       ("cardimages", 6), ("screenshots", 5), ("silhouettes", 3), ("store", 2), ("vessel", 2),
                       ("placeholder", -9), ("{legacy}", -6), ("/ui/", -3), ("buttons", -4), ("nav bar", -4),
                       ("noise", -8), ("normal", -8), ("mask", -6), ("gradient", -6), ("texture/", -3), ("fonts", -9),
                       ("skybox", -2), ("icons", -1)):
        if key in low:
            s += bonus
    if max(w, h) < 48:
        s -= 6
    return round(s, 3)


def fnv1a(s: str) -> int:
    h = 0x811C9DC5
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def _common_prefix(a: str, b: str) -> int:
    n = 0
    for x, y in zip(a, b):
        if x != y:
            break
        n += 1
    return n


# ------------------------------------------------------------------------------------------------ self-test
SAMPLE_CS = r'''
using UnityEngine;
// class Fake {}
namespace CosmicShore.Game
{
    /* struct NotAType { } */
    public class VesselStatus : MonoBehaviour, IVesselStatus
    {
        [Header("Speed")]
        [SerializeField, Tooltip("Top speed in u/s")] float maxSpeed = 90f;
        public const string Label = "Vessel {name}";
        [SerializeField] private Prism prism;
        private int hidden;
        public int Count { get; private set; }
        public float Ratio => maxSpeed / 2;
        public void Boost(float amount, Prism p = null) { var s = "class Inner {"; if (amount > 0) { } }
        public enum Mode { Idle = 0, Flying = 1 << 2, Dead }
        class Nested { public Prism Other; }
    }
}
'''
SAMPLE_CS_FILE_NS = "namespace CosmicShore.Data;\npublic sealed record Card(string Name) : Base { public const int Max = 3; }\n"


def self_test() -> int:
    fails = []

    def ok(cond, what):
        if not cond:
            fails.append(what)

    p = parse_cs(SAMPLE_CS)
    names = [t["name"] for t in p["types"]]
    ok(names == ["VesselStatus", "Mode", "Nested"], f"types found: {names}")
    vs = p["types"][0]
    ok(vs["ns"] == "CosmicShore.Game", "namespace read")
    ok(vs["bases"] == ["MonoBehaviour", "IVesselStatus"], f"bases read: {vs['bases']}")
    mem = {m["name"]: m for m in vs["members"]}
    ok(set(mem) == {"maxSpeed", "Label", "prism", "hidden", "Count", "Ratio", "Boost"}, f"members: {sorted(mem)}")
    ok(mem["maxSpeed"].get("ser") is True and mem["maxSpeed"].get("value") == "90f", "serialized field with value")
    ok(mem["maxSpeed"].get("header") == "Speed" and mem["maxSpeed"].get("tip") == "Top speed in u/s", "header and tooltip")
    ok(mem["Label"]["kind"] == "const" and mem["Label"].get("value") == '"Vessel {name}"', f"const value kept from source: {mem['Label'].get('value')}")
    ok(not mem["hidden"].get("ser"), "a private field without [SerializeField] is not serialized")
    ok(mem["Count"]["kind"] == "property" and mem["Ratio"]["kind"] == "property", "properties")
    ok(mem["Boost"]["kind"] == "method" and mem["maxSpeed"]["line"] == 10, f"method, line numbers ({mem['maxSpeed']['line']})")
    enum = p["types"][1]
    ok([m["name"] for m in enum["members"]] == ["Idle", "Flying", "Dead"], "enum members")
    ok(enum["members"][1]["value"] == "1 << 2", "enum member value")
    ok(p["types"][2]["outer"] is vs, "nested type knows its outer type")
    q = parse_cs(SAMPLE_CS_FILE_NS)
    ok(q["types"] and q["types"][0]["ns"] == "CosmicShore.Data" and q["types"][0]["bases"] == ["Base"],
       f"file-scoped namespace and record: {q['types'][:1]}")
    # negative control: a comment or string that looks like a type is not one
    ok("Fake" not in names and "NotAType" not in names and "Inner" not in names, "masked text is not parsed")
    # graph: copies are one node
    g = Graph()
    g.node("a", "type", "A")
    g.node("b", "type", "B")
    g.link("a", "uses", "b", "usedBy")
    g.link("b", "uses", "a", "usedBy")
    ok(g.recs["a"]["r"]["uses"] == ["b"] and g.recs["b"]["r"]["uses"] == ["a"], "a cycle is two records, not a chain")
    g.link("a", "uses", "zz", "usedBy")
    ok("zz" not in g.recs and g.recs["a"]["r"]["uses"] == ["b"], "a link to a missing node is refused")
    ok(fnv1a("t:CosmicShore.Game.VesselStatus") == fnv1a("t:CosmicShore.Game.VesselStatus") and fnv1a("") == 0x811C9DC5, "hash")
    # flatten keeps every value and marks references
    out = []
    flatten({"a": 1, "b": [1, 2], "c": {"fileID": 1, "guid": "x"}, "d": list(range(100))}, "", out)
    ok(("a", "1") in out and ("b", "[1, 2]") in out and ("c", {"fileID": 1, "guid": "x"}) in out, "flatten values and refs")
    ok(any(k == "d[…]" for k, _ in out), "long lists are counted, not dropped silently")
    if fails:
        print("code_atlas self-test FAILED:\n  " + "\n  ".join(fails))
        return 1
    print("code_atlas self-test: all checks refuse what they must")
    return 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["build", "stats", "self-test"])
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT)
    ap.add_argument("--no-scenes", action="store_true", help="skip .unity scenes")
    ap.add_argument("--strict", action="store_true", help="fail when a links.yaml entry matches nothing (CI)")
    a = ap.parse_args(argv)
    if a.cmd == "self-test":
        return self_test()
    b = Builder().build(include_scenes=not a.no_scenes)
    if a.cmd == "stats":
        for k, v in sorted(b.stats.items(), key=lambda kv: -kv[1])[:40]:
            print(f"{v:8d}  {k}")
        print(f"{len(b.g.recs):8d}  records")
        return 0
    idx = b.write(a.out)
    print(json.dumps({k: idx[k] for k in ("nodes", "values", "bytes", "shards", "kinds")}, indent=1))
    for m in idx["missingLinks"]:
        print("  link: " + m, file=sys.stderr)
    if a.strict and idx["missingLinks"]:
        print(f"code_atlas: {len(idx['missingLinks'])} hand links in {LINKS.relative_to(REPO)} match nothing", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
