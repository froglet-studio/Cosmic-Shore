#!/usr/bin/env python3
"""audit.py - the world-look audit: which prefabs draw an EFFECT in the world, what each is made of (renderers,
materials, shaders resolved through their guids, particle render modes, mesh depth), and which of the LOCKED rule's
flat shapes each one carries (Docs/SKILL_TRAINING_PLAN.md section 3.2, R0: the audit sheet).

A READER tool: it reads Assets/** (prefabs, .mat, .shader, .shadergraph, .shadersubgraph, .hlsl, mesh .asset files and
every .meta for the guid index) and ProjectSettings/TagManager.asset, and writes only its own two outputs, the report
(Docs/Shaders/WORLD_LOOK_AUDIT.md) and the baseline (Tools/Build/look_kit/audit_baseline.json). Standard library only,
no Unity, no numpy.

    python3 Tools/Build/look_kit/audit.py                 # regenerate Docs/Shaders/WORLD_LOOK_AUDIT.md, print the counts
    python3 Tools/Build/look_kit/audit.py --json          # the whole audit as JSON on stdout (writes nothing)
    python3 Tools/Build/look_kit/audit.py --check         # FAIL (exit 1) on any flat signal not in the baseline: the gate
    python3 Tools/Build/look_kit/audit.py --baseline      # write the baseline from the current tree (the known offenders)
    python3 Tools/Build/look_kit/audit.py --scenes        # also audit scene-placed renderers under Assets/_Scenes
    python3 Tools/Build/look_kit/audit.py --self-test     # negative controls on a temp Assets tree

The rules it measures against: Docs/claude/SHADERS_AND_PERFORMANCE.md § "Nothing in the world looks flat" (LOCKED,
Garrett 2026-10-18: a world effect is a lit, faceted VOLUME, never a flat sheet, an unlit fill, a plain additive ribbon
or a billboard that reads as a sticker) and § "In the world, not on the screen" (LOCKED, Garrett 2026-10-21: a world
fact is shown on the thing itself, in 3D, never as a screen ring or a label).

What is an EFFECT here (the finder): a prefab under Assets/_Prefabs (and, with --scenes, a scene) that has a
ParticleSystem, TrailRenderer, LineRenderer or VisualEffect, or a MeshRenderer / SkinnedMeshRenderer whose material's
shader NAME says Unlit, Particles, Additive, Sprites or UI, or whose folder or file name says one of EFFECT_WORDS.
Anything under a Canvas in screen space (m_RenderMode 0 Overlay or 1 Camera) is UI, not world, and is left out, except
for the one thing the second rule forbids: a world renderer in a screen-space canvas named like a ring or a radius.

The FLAT SIGNALS (each a rule with a one-line reason, SIGNALS below): unlit-shader, unlit-by-content, additive-sheet,
billboard-particles, screen-ring, thin-mesh, no-normals. A signal is a measurement, never a verdict: the owner of an
effect decides, and a deliberate look is recorded where it is made (the rule's own exception, the Kite's ghost prisms).
--check holds the line: every signal in the baseline is owed work; a signal that is NOT in the baseline is a new flat
effect and fails the gate, printed as path:renderer:signal.

What it cannot decide (listed in the report): a mesh that lives in an FBX (its extents are not readable offline, so
the mesh is reported as 'fbx' and thin-mesh / no-normals do not run), a VFX Graph (its outputs are inside the .vfx),
a shader guid no .meta under Assets owns and no table here names, a particle renderer in Stretch mode (a stretched
billboard; the rule names 0/2/3 only) and a nested FBX model instance's importer materials.

Exit codes: 0 ok; 1 --check found a new offender (or --self-test failed); 2 usage; 3 no baseline to check against.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import sys
import tempfile
from collections import Counter, OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
REPORT_REL = os.path.join("Docs", "Shaders", "WORLD_LOOK_AUDIT.md")
BASELINE_REL = os.path.join("Tools", "Build", "look_kit", "audit_baseline.json")
PREFABS_REL = os.path.join("Assets", "_Prefabs")
SCENES_REL = os.path.join("Assets", "_Scenes")

THIN_RATIO = 0.08          # thinnest extent / longest extent under this = a sheet, not a volume
UI_LAYER_NAMES = ("UI",)   # the HUD layer(s): a world renderer parked here draws a world fact on the screen

# The words that make a prefab an effect by its folder or file name (the task's list, then a few kin the Projectile
# and FX folders use: missiles, bombs, bursts, crackles). Matched as a word: 'Starling' holds no 'ring'.
EFFECT_WORDS = ("fx", "effect", "impact", "explosion", "blast", "trail", "petal", "bloom", "beam", "lance", "ring",
                "shield", "wormhole", "tunnel", "crystal")
EFFECT_WORDS_KIN = ("projectile", "missile", "bomb", "burst", "spark", "crackle", "laser", "boost", "jet",
                    "lightning", "ripple", "shockwave", "halo", "glow", "flare")
RING_WORDS = ("ring", "radius", "reticle", "range", "blast", "burst", "target", "circle", "crosshair")

SIGNALS = OrderedDict([
    ("unlit-shader", "an Unlit, Particles/Unlit, Sprites or UI shader on a world renderer: the scene's light cannot shade it, it is a fill"),
    ("unlit-by-content", "the shader reads no normal and no light (an Unlit-sub-target graph without a Normal, Fresnel or Light node, or a .shader with no lighting code): nothing in it can respond to the scene's light"),
    ("additive-sheet", "an additive or transparent unlit material on a sheet (a quad or thin mesh, a billboard, a trail or line ribbon): the plain additive glow the rule names"),
    ("billboard-particles", "particles in Billboard, HorizontalBillboard or VerticalBillboard mode with an unlit material: camera-facing sprites read as stickers, no depth, no facets"),
    ("screen-ring", "a world fact drawn as a mark on the screen (a world renderer on the HUD layer, or a ring/radius/reticle renderer inside a screen-space canvas) instead of on the thing itself"),
    ("thin-mesh", f"the mesh's thinnest extent is under {THIN_RATIO:g} of its longest: a sheet, there is no depth for the light to shade"),
    ("no-normals", "a serialized mesh with no normal channel: the light cannot shade its facets"),
])

RENDER_MODES = {0: "Billboard", 1: "Stretch", 2: "HorizontalBillboard", 3: "VerticalBillboard", 4: "Mesh", 5: "None"}
BILLBOARD_MODES = (0, 2, 3)
SHEET_MODES = (0, 1, 2, 3)

# What the guid index cannot reach: Unity's built-in resources (guid ...f000... shaders / materials, ...e000...
# meshes) and the URP package shaders (their .meta lives in the package cache). A name ending in '?' is a best
# reading, not a fact; a fileID or guid outside these tables is reported as undecided.
BUILTIN_SHADER_GUID = "0000000000000000f000000000000000"
BUILTIN_RESOURCE_GUID = "0000000000000000e000000000000000"
BUILTIN_SHADERS = {
    45: "Standard (Specular setup)", 46: "Standard", 103: "Skybox/Cubemap", 104: "Skybox/6 Sided",
    106: "Skybox/Procedural", 108: "Skybox/Panoramic",
    200: "Legacy Shaders/Particles/Additive", 201: "Legacy Shaders/Particles/~Additive-Multiply",
    202: "Legacy Shaders/Particles/Alpha Blended", 203: "Legacy Shaders/Particles/Multiply",
    205: "Legacy Shaders/Particles/Multiply (Double)", 207: "Legacy Shaders/Particles/Additive (Soft)",
    208: "Legacy Shaders/Particles/Alpha Blended Premultiply", 209: "Legacy Shaders/Particles/Anim Alpha Blended",
    210: "Particles/Standard Surface", 211: "Particles/Standard Unlit",
    10750: "Unlit/Transparent", 10751: "Unlit/Transparent Cutout", 10752: "Unlit/Texture", 10753: "Sprites/Default",
    10755: "Unlit/Color", 10770: "UI/Default",
}
# built-in MATERIALS (fileID on the same guid): (name, shader name)
BUILTIN_MATERIALS = {
    10101: ("Default UI Material", "UI/Default"), 10301: ("Default-Particle", "Legacy Shaders/Particles/Alpha Blended"),
    10302: ("Default-Diffuse", "Legacy Shaders/Diffuse"), 10303: ("Default-Material", "Standard"),
    10306: ("Default-Line", "Particles/Standard Unlit?"), 10308: ("Default-ParticleSystem", "Particles/Standard Unlit"),
    10754: ("Sprites-Default", "Sprites/Default"), 10758: ("Sprites-Mask", "Sprites/Mask"),
}
# built-in MESHES: name and half-extents (x, y, z)
BUILTIN_MESHES = {
    10202: ("Cube", (0.5, 0.5, 0.5)), 10206: ("Plane", (5.0, 0.0, 5.0)), 10207: ("Sphere", (0.5, 0.5, 0.5)),
    10208: ("Cylinder", (0.5, 1.0, 0.5)), 10209: ("Capsule", (0.5, 1.0, 0.5)), 10210: ("Quad", (0.5, 0.5, 0.0)),
}
PACKAGE_SHADERS = {
    "933532a4fcc9baf4fa0491de14d08ed7": "Universal Render Pipeline/Lit",
    "650dd9526735d5b46b79224bc6e94025": "Universal Render Pipeline/Unlit",
    "0406db5a14f94604a8c57ccfbc9f3b46": "Universal Render Pipeline/Particles/Unlit",
    "b7839dad95683814aa64166edc107ae2": "Universal Render Pipeline/Particles/Lit",
    "8516d7a69675844a7a0b7095af7c46af": "Universal Render Pipeline/Particles/Simple Lit",
    "8d2bb70cbf9db8d4da26e15b26e74248": "Universal Render Pipeline/Simple Lit",
    "0ca6dca7396eb48e5849247ffd444914": "Universal Render Pipeline/Baked Lit",
}
LIT_PACKAGE_NAMES = ("/Lit", "Simple Lit", "Baked Lit", "Standard", "Diffuse", "Specular", "Bumped")

# the FINDER's name test (the task's list: a Particles or Additive shader marks an effect even when it is lit) and the
# unlit-shader SIGNAL's (Unlit, Particles/Unlit and the legacy particle shaders, Sprites, UI: Particles/Lit is lit)
FIND_NAME_RE = re.compile(r"unlit|particles|additive|sprites|(^|[/ ])ui([/ ]|$)|ui$", re.I)
UNLIT_NAME_RE = re.compile(r"unlit|sprites|(^|[/ ])ui([/ ]|$)|ui$|legacy shaders/particles|particles/(additive|alpha blended|multiply|anim)", re.I)
LIGHT_CODE_RE = re.compile(
    r"GetMainLight|_MainLightPosition|_MainLightColor|_WorldSpaceLightPos0|_LightColor0|unity_LightColor|"
    r"normalWS|\bNORMAL\b|LightingLambert|LightingPhysicallyBased|UniversalFragment(PBR|BlinnPhong)|"
    r"#pragma\s+surface|Lighting\.hlsl|SampleSH\b|normalize\s*\(\s*cross\s*\(", re.I)
LIGHT_NODE_TYPES = ("NormalVectorNode", "FresnelNode", "MainLightDirectionNode", "NormalFromHeightNode",
                    "NormalFromTextureNode", "NormalUnpackNode", "NormalBlendNode", "NormalReconstructZNode",
                    "NormalStrengthNode", "BakedGINode", "ReflectionProbeNode", "AmbientNode")
LIGHT_FUNCTION_RE = re.compile(r"shade|light|\blit\b|fresnel|facet|normal|rim|lambert", re.I)

DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$", re.M)
# Unity wraps a long flow mapping at 80 columns (`m_Shader: {fileID: -6465566751694194690, guid: ...,\n    type: 3}`),
# so every `{...}` reference is read up to its closing brace across lines, then its fields are picked out.
BRACE_RE_TEMPLATE = r"^[ \t]*{key}: \{{([^}}]*)\}}"
LIST_BRACE_RE = re.compile(r"- \{([^}]*)\}")
MOD_RE = re.compile(
    r"^\s*- target: \{[^}]*\}\s*\n\s*propertyPath: ([^\n]*)\n\s*value: ([^\n]*)\n\s*objectReference: \{([^}]*)\}",
    re.M)
CLASS_GAMEOBJECT, CLASS_TRANSFORM, CLASS_RECT = 1, 4, 224
CLASS_MESHFILTER, CLASS_MESHRENDERER, CLASS_SKINNED = 33, 23, 137
CLASS_PARTICLE, CLASS_PARTICLE_RENDERER = 198, 199
CLASS_TRAIL, CLASS_LINE, CLASS_CANVAS, CLASS_PREFAB_INSTANCE = 96, 120, 223, 1001
CLASS_VFX = 2083052967

WORD_RE_CACHE: dict[tuple, re.Pattern] = {}


def word_re(words: tuple) -> re.Pattern:
    if words not in WORD_RE_CACHE:
        # the words match in any case; the boundaries do not ('FxBoost' is Fx + Boost, 'Starling' holds no ring),
        # so the case flag is scoped to the alternation: under re.I a [a-z] lookahead would also reject 'R'
        WORD_RE_CACHE[words] = re.compile(r"(?<![A-Za-z])(?i:" + "|".join(words) + r")(?![a-z])")
    return WORD_RE_CACHE[words]


def read_text(path: str) -> str:
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def parse_ref(inner: str):
    """(fileID, guid, type) from the inside of a `{fileID: N, guid: G, type: T}` mapping (wrapped or not)."""
    fm = re.search(r"fileID: (-?\d+)", inner)
    if not fm:
        return None
    gm = re.search(r"guid: ([0-9a-f]{32})", inner)
    tm = re.search(r"type: (\d+)", inner)
    return int(fm.group(1)), (gm.group(1) if gm else None), (int(tm.group(1)) if tm else None)


def ref(body: str, key: str):
    """(fileID, guid, type) of a `key: {fileID: N, guid: G, type: T}` reference, or None."""
    m = re.search(BRACE_RE_TEMPLATE.format(key=re.escape(key)), body, re.M)
    return parse_ref(m.group(1)) if m else None


def scalar(body: str, key: str, default=None):
    m = re.search(r"^[ \t]*" + re.escape(key) + r": ([^\n]*)$", body, re.M)
    return m.group(1).strip() if m else default


def int_scalar(body: str, key: str, default=None):
    v = scalar(body, key)
    try:
        return int(v) if v is not None else default
    except ValueError:
        return default


def list_refs(body: str, key: str) -> list:
    """The `{fileID, guid, type}` entries under a `key:` list (m_Materials, m_Children)."""
    m = re.search(r"^([ \t]*)" + re.escape(key) + r":[ \t]*\n", body, re.M)
    if not m:
        return []
    indent = m.group(1)
    block = []
    for line in body[m.end():].splitlines():
        if line.strip() == "" or line.startswith(indent + "- ") or line.startswith(indent + "  "):
            block.append(line)   # an entry, or the wrapped tail of one
        else:
            break
    out = []
    for bm in LIST_BRACE_RE.finditer("\n".join(block)):
        r = parse_ref(bm.group(1))
        if r:
            out.append(r)
    return out


def split_docs(text: str) -> list:
    """Unity YAML documents as (class_id, file_id, stripped, class_name, body)."""
    docs = []
    matches = list(DOC_RE.finditer(text))
    for i, m in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[m.end():end]
        first = body.lstrip("\n").split("\n", 1)[0]
        name = first[:-1] if first.endswith(":") else first.split(":")[0]
        docs.append((int(m.group(1)), int(m.group(2)), bool(m.group(3)), name.strip(), body))
    return docs


class Project:
    """The asset tree: the guid index and the resolvers (material, shader, mesh), each cached."""

    def __init__(self, root: str):
        self.root = root
        self.assets = os.path.join(root, "Assets")
        self._guids = None
        self._materials: dict = {}
        self._shaders: dict = {}
        self._meshes: dict = {}
        self._graph_lit: dict = {}
        self.layers = self._read_layers()
        self.undecided: list = []

    # -- guids --------------------------------------------------------------------------------------------------
    def guid_index(self) -> dict:
        if self._guids is None:
            idx = {}
            for dirpath, dirnames, filenames in os.walk(self.assets):
                dirnames[:] = [d for d in dirnames if not d.startswith(".")]
                for fn in filenames:
                    if not fn.endswith(".meta"):
                        continue
                    p = os.path.join(dirpath, fn)
                    try:
                        with open(p, "rb") as fh:
                            head = fh.read(400).decode("utf-8", "replace")
                    except OSError:
                        continue
                    m = re.search(r"^guid: ([0-9a-f]{32})", head, re.M)
                    if m and m.group(1) not in idx:
                        idx[m.group(1)] = p[:-5]
            self._guids = idx
        return self._guids

    def path_of(self, guid: str):
        return self.guid_index().get(guid)

    def rel(self, path: str) -> str:
        return os.path.relpath(path, self.root).replace(os.sep, "/")

    def _read_layers(self) -> dict:
        p = os.path.join(self.root, "ProjectSettings", "TagManager.asset")
        layers = {}
        if os.path.exists(p):
            text = read_text(p)
            m = re.search(r"^  layers:\n((?:  - .*\n)+)", text, re.M)
            if m:
                for i, line in enumerate(m.group(1).splitlines()):
                    name = line[4:].strip()
                    if name:
                        layers[i] = name
        if not layers:
            layers = {0: "Default", 1: "TransparentFX", 2: "Ignore Raycast", 4: "Water", 5: "UI"}
        return layers

    # -- shaders ------------------------------------------------------------------------------------------------
    def shader(self, file_id: int, guid: str) -> dict:
        key = (file_id, guid)
        if key in self._shaders:
            return self._shaders[key]
        s = {"name": None, "kind": None, "path": None, "unlit_by_name": False, "lit_by_content": None,
             "transparent": None, "additive": False, "subtarget": None, "allow_override": None, "queue": None}
        if guid == BUILTIN_SHADER_GUID:
            s["kind"] = "builtin"
            s["name"] = BUILTIN_SHADERS.get(file_id, f"builtin:{file_id}?")
            s["lit_by_content"] = any(w in s["name"] for w in LIT_PACKAGE_NAMES) if "?" not in s["name"] else None
            if "Skybox" in s["name"]:
                s["lit_by_content"] = True   # a sky is not an effect's fill
            s["additive"] = "Additive" in s["name"]
            s["transparent"] = ("Particles" in s["name"] or "Transparent" in s["name"] or "Sprites" in s["name"]
                                or "UI/" in s["name"])
        elif guid in PACKAGE_SHADERS:
            s["kind"] = "package"
            s["name"] = PACKAGE_SHADERS[guid]
            s["lit_by_content"] = any(w in s["name"] for w in LIT_PACKAGE_NAMES)
            s["transparent"] = None   # the material's _Surface decides
        else:
            path = self.path_of(guid) if guid else None
            if path is None:
                s["kind"] = "unresolved"
                s["name"] = f"unresolved:{guid}"
            elif path.endswith(".shader"):
                self._read_shader_file(s, path)
            elif path.endswith(".shadergraph"):
                self._read_shadergraph(s, path)
            else:
                s["kind"] = "other"
                s["name"] = self.rel(path)
        if s["name"] and "?" not in s["name"] and s["kind"] != "unresolved":
            s["unlit_by_name"] = bool(UNLIT_NAME_RE.search(s["name"]))
        self._shaders[key] = s
        return s

    def _read_shader_file(self, s: dict, path: str) -> None:
        text = read_text(path)
        s["kind"] = "shader"
        s["path"] = self.rel(path)
        m = re.search(r'Shader\s+"([^"]+)"', text)
        s["name"] = m.group(1) if m else os.path.basename(path)[:-7]
        s["lit_by_content"] = bool(LIGHT_CODE_RE.search(text))
        s["declares_surface"] = bool(re.search(r"\b_Surface\s*\(", text))
        bm = re.search(r"^\s*Blend\s+(\w+)\s+(\w+)", text, re.M)
        if bm and bm.group(1).lower() == "one" and bm.group(2).lower() == "one":
            s["additive"] = True
        qm = re.search(r'"Queue"\s*=\s*"([A-Za-z]+)', text)
        s["queue"] = qm.group(1) if qm else None
        s["transparent"] = bool(bm) or (s["queue"] in ("Transparent", "Overlay"))

    def _read_shadergraph(self, s: dict, path: str) -> None:
        s["kind"] = "shadergraph"
        s["path"] = self.rel(path)
        objs = self._graph_objects(path)
        stem = os.path.basename(path).rsplit(".", 1)[0]
        graph = next((o for o in objs if o.get("m_Type") == "UnityEditor.ShaderGraph.GraphData"), None)
        folder = (graph or {}).get("m_Path") or "Shader Graphs"
        s["name"] = f"{folder}/{stem}"
        by_id = {o.get("m_ObjectId"): o for o in objs if o.get("m_ObjectId")}
        target = next((o for o in objs if o.get("m_Type", "").endswith("UniversalTarget")), None)
        sub = None
        if target:
            active = (target.get("m_ActiveSubTarget") or {}).get("m_Id")
            sub_obj = by_id.get(active)
            sub = (sub_obj or {}).get("m_Type", "")
            if not sub:
                subs = [o.get("m_Type", "") for o in objs if o.get("m_Type", "").endswith("SubTarget")]
                sub = subs[0] if subs else ""
            s["subtarget"] = sub.rsplit(".", 1)[-1].replace("Universal", "").replace("SubTarget", "") or None
            s["allow_override"] = bool(target.get("m_AllowMaterialOverride", False))
            st = target.get("m_SurfaceType")
            s["transparent"] = (st == 1) if st is not None else None
            s["additive"] = target.get("m_AlphaMode") == 2 and s["transparent"] is True
        if s["subtarget"] in ("Lit", "SimpleLit", "ComplexLit", "Decal"):
            s["lit_by_content"] = True
        elif s["subtarget"] is None:
            s["lit_by_content"] = None
        else:
            s["lit_by_content"] = self._graph_reads_light(path, objs, depth=0)

    def _graph_objects(self, path: str) -> list:
        objs = []
        for chunk in re.split(r"\n\s*\n", read_text(path)):
            chunk = chunk.strip()
            if not chunk.startswith("{"):
                continue
            try:
                objs.append(json.loads(chunk))
            except json.JSONDecodeError:
                continue
        return objs

    def _graph_reads_light(self, path: str, objs: list, depth: int) -> bool:
        """Does any node in the graph (or a sub graph it uses, one level) read a normal or a light?"""
        if path in self._graph_lit:
            return self._graph_lit[path]
        self._graph_lit[path] = False   # cycle guard
        hit = False
        for o in objs:
            t = o.get("m_Type", "")
            short = t.rsplit(".", 1)[-1]
            if short in LIGHT_NODE_TYPES:
                hit = True
                break
            if short == "CustomFunctionNode":
                if LIGHT_FUNCTION_RE.search(o.get("m_Name", "") or "") or LIGHT_FUNCTION_RE.search(
                        o.get("m_FunctionName", "") or ""):
                    hit = True
                    break
                body = o.get("m_FunctionBody", "") or ""
                src = o.get("m_FunctionSource") or {}
                if isinstance(src, str):
                    try:
                        src = json.loads(src)
                    except json.JSONDecodeError:
                        src = {}
                hp = self.path_of(src.get("guid", "")) if isinstance(src, dict) else None
                if hp and os.path.exists(hp):
                    body += "\n" + read_text(hp)
                if LIGHT_CODE_RE.search(body):
                    hit = True
                    break
            if short == "SubGraphNode" and depth < 2:
                sg = o.get("m_SubGraph") or {}
                if isinstance(sg, str):
                    try:
                        sg = json.loads(sg)
                    except json.JSONDecodeError:
                        sg = {}
                sp = self.path_of(sg.get("guid", "")) if isinstance(sg, dict) else None
                if sp and os.path.exists(sp) and self._graph_reads_light(sp, self._graph_objects(sp), depth + 1):
                    hit = True
                    break
        self._graph_lit[path] = hit
        return hit

    # -- materials ----------------------------------------------------------------------------------------------
    def material(self, file_id: int, guid: str) -> dict:
        key = (file_id, guid)
        if key in self._materials:
            return self._materials[key]
        m = {"name": None, "path": None, "shader": None, "surface": None, "blend": None, "queue": None,
             "render_type": None, "src": None, "dst": None, "zwrite": None, "transparent": None, "additive": None,
             "unlit_by_name": False, "lit_by_content": None}
        if file_id == 0 and not guid:
            m["name"] = "(none)"
            m["shader"] = {"name": "(none)", "kind": "none", "unlit_by_name": False, "lit_by_content": None,
                           "transparent": None, "additive": False, "path": None}
        elif guid == BUILTIN_SHADER_GUID:
            name, shader_name = BUILTIN_MATERIALS.get(file_id, (f"builtin-material:{file_id}?", f"builtin:{file_id}?"))
            m["name"] = name
            m["shader"] = {"name": shader_name, "kind": "builtin", "path": None,
                           "unlit_by_name": bool(UNLIT_NAME_RE.search(shader_name)) if "?" not in shader_name else False,
                           "lit_by_content": (any(w in shader_name for w in LIT_PACKAGE_NAMES) if "?" not in shader_name else None),
                           "transparent": "Standard" not in shader_name and "Diffuse" not in shader_name,
                           "additive": "Additive" in shader_name}
            if shader_name.endswith("?"):
                self.undecided.append(f"built-in material fileID {file_id} ({name}): shader not in the table")
        else:
            path = self.path_of(guid) if guid else None
            if path is None:
                m["name"] = f"missing-material:{guid}"
                m["shader"] = {"name": f"missing-material:{guid}", "kind": "missing", "path": None,
                               "unlit_by_name": False, "lit_by_content": None, "transparent": None, "additive": False}
            elif not path.endswith(".mat"):
                m["name"] = f"embedded-material:{os.path.basename(path)}"
                m["path"] = self.rel(path)
                m["shader"] = {"name": f"embedded-material:{os.path.basename(path)}", "kind": "embedded", "path": self.rel(path),
                               "unlit_by_name": False, "lit_by_content": None, "transparent": None, "additive": False}
            else:
                self._read_material(m, path)
        sh = m["shader"]
        m["unlit_by_name"] = sh["unlit_by_name"]
        m["lit_by_content"] = sh["lit_by_content"]
        # the effective blend: the material's _Surface / _Blend floats count only where the shader has those
        # properties (a URP package shader, a .shader that declares _Surface, a graph that allows the override);
        # on a built-in or other shader they are leftovers the shader never reads
        override_ok = sh.get("kind") == "package" or (sh.get("kind") == "shader" and sh.get("declares_surface")) or bool(sh.get("allow_override"))
        if m["transparent"] is None:
            if override_ok and m["surface"] is not None:
                m["transparent"] = m["surface"] == 1
            elif m["queue"] is not None and m["queue"] >= 3000:
                m["transparent"] = True
            elif m["render_type"] == "Transparent":
                m["transparent"] = True
            else:
                m["transparent"] = sh.get("transparent")
        if m["additive"] is None:
            if override_ok and m["blend"] is not None and m["transparent"]:
                m["additive"] = m["blend"] == 2
            elif override_ok and m["src"] == 1 and m["dst"] == 1:
                m["additive"] = True
            else:
                m["additive"] = bool(sh.get("additive"))
        self._materials[key] = m
        return m

    def _read_material(self, m: dict, path: str) -> None:
        text = read_text(path)
        m["path"] = self.rel(path)
        m["name"] = scalar(text, "m_Name") or os.path.basename(path)[:-4]
        sref = ref(text, "m_Shader")
        m["shader"] = self.shader(sref[0], sref[1]) if sref else self.shader(0, None)
        m["queue"] = int_scalar(text, "m_CustomRenderQueue")
        if m["queue"] is not None and m["queue"] < 0:
            m["queue"] = None
        rt = re.search(r"^\s+RenderType: (\w+)", text, re.M)
        m["render_type"] = rt.group(1) if rt else None
        floats = dict(re.findall(r"^\s+- (_Surface|_Blend|_SrcBlend|_DstBlend|_ZWrite): ([-\d.eE]+)$", text, re.M))

        def f(k):
            try:
                return int(float(floats[k])) if k in floats else None
            except ValueError:
                return None
        m["surface"], m["blend"], m["src"], m["dst"], m["zwrite"] = (f("_Surface"), f("_Blend"), f("_SrcBlend"),
                                                                     f("_DstBlend"), f("_ZWrite"))
        kw = re.search(r"m_ValidKeywords:\n((?:\s+- .*\n)*)", text)
        kws = kw.group(1) if kw else ""
        if "_SURFACE_TYPE_TRANSPARENT" in kws and m["surface"] is None:
            m["surface"] = 1

    # -- meshes -------------------------------------------------------------------------------------------------
    def mesh(self, file_id: int, guid: str) -> dict:
        key = (file_id, guid)
        if key in self._meshes:
            return self._meshes[key]
        r = {"name": None, "kind": None, "path": None, "extent": None, "ratio": None, "has_normals": None}
        if file_id == 0 and not guid:
            r["kind"], r["name"] = "none", "(none)"
        elif guid == BUILTIN_RESOURCE_GUID:
            name, ext = BUILTIN_MESHES.get(file_id, (f"builtin-mesh:{file_id}?", None))
            r["kind"], r["name"], r["extent"] = "builtin", name, ext
            r["has_normals"] = True if ext else None
            if ext:
                r["ratio"] = ratio_of(ext)
        else:
            path = self.path_of(guid) if guid else None
            if path is None:
                r["kind"], r["name"] = "unresolved", f"unresolved:{guid}"
            elif path.lower().endswith((".fbx", ".obj", ".blend", ".dae", ".3ds")):
                r["kind"], r["name"], r["path"] = "fbx", os.path.basename(path), self.rel(path)
            elif path.endswith(".asset"):
                self._read_mesh_asset(r, path, file_id)
            else:
                r["kind"], r["name"], r["path"] = "other", os.path.basename(path), self.rel(path)
        self._meshes[key] = r
        return r

    def _read_mesh_asset(self, r: dict, path: str, file_id: int) -> None:
        r["path"] = self.rel(path)
        docs = [d for d in split_docs(read_text(path)) if d[0] == 43]
        doc = next((d for d in docs if d[1] == file_id), docs[0] if docs else None)
        if doc is None:
            r["kind"], r["name"] = "other", os.path.basename(path)
            return
        body = doc[4]
        r["kind"] = "asset"
        r["name"] = scalar(body, "m_Name") or os.path.basename(path)[:-6]
        em = re.search(r"m_LocalAABB:\n\s*m_Center: \{[^}]*\}\n\s*m_Extent: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}", body)
        if em:
            r["extent"] = tuple(abs(float(em.group(i))) for i in (1, 2, 3))
            r["ratio"] = ratio_of(r["extent"])
        cm = re.search(r"m_Channels:\n((?:\s+- stream: \d+\n\s+offset: \d+\n\s+format: \d+\n\s+dimension: \d+\n)+)", body)
        if cm:
            dims = re.findall(r"dimension: (\d+)", cm.group(1))
            r["has_normals"] = len(dims) > 1 and int(dims[1]) > 0


def ratio_of(extent) -> float:
    longest = max(extent)
    return (min(extent) / longest) if longest > 0 else 0.0


# ---------------------------------------------------------------------------------------------------------------
# One prefab (or scene)
# ---------------------------------------------------------------------------------------------------------------
class Asset:
    """A prefab's (or scene's) documents, indexed; the renderers with their ancestry, UI-ness and signals."""

    def __init__(self, project: Project, path: str):
        self.project = project
        self.path = path
        self.rel = project.rel(path)
        self.is_effect_by_name = bool(word_re(EFFECT_WORDS + EFFECT_WORDS_KIN).search(self.rel.replace("_", " ")))
        self.gos: dict = {}          # id -> {name, layer, transform}
        self.transforms: dict = {}   # id -> {go, father, stripped, instance, rect}
        self.canvases: dict = {}     # go id -> render mode
        self.instances: dict = {}    # id -> {source, parent, name, materials}
        self.meshes_of_go: dict = {}
        self.renderers: list = []
        self._parse(read_text(path))

    def _parse(self, text: str) -> None:
        docs = split_docs(text)
        stripped_gos = set()
        psr_docs = []
        for cls, fid, stripped, name, body in docs:
            if cls == CLASS_GAMEOBJECT:
                if stripped:
                    stripped_gos.add(fid)
                    self.gos[fid] = {"name": "(nested)", "layer": 0, "transform": None, "stripped": True,
                                     "instance": (ref(body, "m_PrefabInstance") or (0,))[0]}
                else:
                    self.gos[fid] = {"name": scalar(body, "m_Name", "?"), "layer": int_scalar(body, "m_Layer", 0),
                                     "transform": None, "stripped": False, "instance": 0}
            elif cls in (CLASS_TRANSFORM, CLASS_RECT):
                go = ref(body, "m_GameObject")
                father = ref(body, "m_Father")
                inst = ref(body, "m_PrefabInstance")
                self.transforms[fid] = {"go": go[0] if go else 0, "father": father[0] if father else 0,
                                        "stripped": stripped, "instance": inst[0] if inst else 0,
                                        "rect": cls == CLASS_RECT}
            elif cls == CLASS_CANVAS:
                go = ref(body, "m_GameObject")
                if go:
                    self.canvases[go[0]] = int_scalar(body, "m_RenderMode", 0)
            elif cls == CLASS_PREFAB_INSTANCE:
                src = ref(body, "m_SourcePrefab")
                parent = ref(body, "m_TransformParent")
                mods = MOD_RE.findall(body)
                name = next((v for pp, v, _ in mods if pp == "m_Name"), None)
                mats = {}
                for pp, _, inner in mods:
                    mm = re.match(r"m_Materials\.Array\.data\[(\d+)\]", pp)
                    oref = parse_ref(inner)
                    if mm and oref and (oref[0] != 0 or oref[1]):
                        mats[int(mm.group(1))] = (oref[0], oref[1], 2)
                self.instances[fid] = {"source": src[1] if src else None, "parent": parent[0] if parent else 0,
                                       "name": name, "materials": mats}
            elif cls == CLASS_MESHFILTER:
                go = ref(body, "m_GameObject")
                mesh = ref(body, "m_Mesh")
                if go:
                    self.meshes_of_go[go[0]] = mesh
            elif cls in (CLASS_MESHRENDERER, CLASS_SKINNED):
                go = ref(body, "m_GameObject")
                self.renderers.append({"kind": "SkinnedMeshRenderer" if cls == CLASS_SKINNED else "MeshRenderer",
                                       "go": go[0] if go else 0, "materials": list_refs(body, "m_Materials"),
                                       "mesh": ref(body, "m_Mesh") if cls == CLASS_SKINNED else None,
                                       "enabled": int_scalar(body, "m_Enabled", 1), "render_mode": None})
            elif cls == CLASS_PARTICLE:
                go = ref(body, "m_GameObject")
                tm = re.search(r"^  TrailModule:\n\s+enabled: (\d)", body, re.M)
                self.renderers.append({"kind": "ParticleSystem", "go": go[0] if go else 0, "materials": [],
                                       "mesh": None, "enabled": 1, "render_mode": None,
                                       "trails": bool(tm and tm.group(1) == "1")})
            elif cls == CLASS_PARTICLE_RENDERER:
                psr_docs.append(body)
            elif cls in (CLASS_TRAIL, CLASS_LINE):
                go = ref(body, "m_GameObject")
                al = int_scalar(body, "alignment")
                self.renderers.append({"kind": "TrailRenderer" if cls == CLASS_TRAIL else "LineRenderer",
                                       "go": go[0] if go else 0, "materials": list_refs(body, "m_Materials"),
                                       "mesh": None, "enabled": int_scalar(body, "m_Enabled", 1),
                                       "render_mode": None, "alignment": {0: "View", 1: "Local"}.get(al, None)})
            elif cls == CLASS_VFX:
                go = ref(body, "m_GameObject")
                asset = ref(body, "m_Asset")
                self.renderers.append({"kind": "VisualEffect", "go": go[0] if go else 0, "materials": [],
                                       "mesh": None, "enabled": int_scalar(body, "m_Enabled", 1), "render_mode": None,
                                       "vfx": self.project.path_of(asset[1]) if asset and asset[1] else None})
        for t_id, t in self.transforms.items():
            if t["go"] in self.gos:
                self.gos[t["go"]]["transform"] = t_id
        # the particle renderer rides the ParticleSystem's GameObject: merge its materials and render mode
        for body in psr_docs:
            go = ref(body, "m_GameObject")
            go_id = go[0] if go else 0
            target = next((r for r in self.renderers if r["kind"] == "ParticleSystem" and r["go"] == go_id), None)
            if target is None:
                target = {"kind": "ParticleSystem", "go": go_id, "materials": [], "mesh": None, "enabled": 1,
                          "render_mode": None, "trails": False}
                self.renderers.append(target)
            target["materials"] = list_refs(body, "m_Materials")
            target["render_mode"] = int_scalar(body, "m_RenderMode", 0)
            target["mesh"] = ref(body, "m_Mesh") if target["render_mode"] == 4 else None
        # nested FBX model instances: a renderer the file cannot see, with the materials it overrides
        for inst_id, inst in self.instances.items():
            src = self.project.path_of(inst["source"]) if inst["source"] else None
            if src and src.lower().endswith((".fbx", ".obj", ".blend")):
                self.renderers.append({"kind": "ModelInstance", "go": 0, "instance": inst_id,
                                       "materials": [inst["materials"][k] for k in sorted(inst["materials"])],
                                       "mesh": (0, inst["source"], 3), "enabled": 1, "render_mode": None,
                                       "name": inst["name"] or os.path.basename(src)})
        self.renderers.sort(key=lambda r: (self.go_name(r), r["kind"]))

    # -- ancestry ------------------------------------------------------------------------------------------------
    def go_name(self, r: dict) -> str:
        if r["kind"] == "ModelInstance":
            return r["name"]
        return self.gos.get(r["go"], {}).get("name", "?")

    def ancestors(self, r: dict) -> list:
        """GameObject ids from the renderer's own GameObject up to the root (nested instances followed up)."""
        out = []
        seen = set()
        if r["kind"] == "ModelInstance":
            t = self.instances.get(r["instance"], {}).get("parent", 0)
        else:
            out.append(r["go"])
            t = self.gos.get(r["go"], {}).get("transform")
            if t is None:
                t = next((tid for tid, tr in self.transforms.items() if tr["go"] == r["go"]), None)
            t = self.transforms.get(t, {}).get("father", 0) if t else 0
        while t and t not in seen:
            seen.add(t)
            tr = self.transforms.get(t)
            if tr is None:
                break
            if tr["stripped"]:
                inst = self.instances.get(tr["instance"])
                t = inst["parent"] if inst else 0
                continue
            out.append(tr["go"])
            t = tr["father"]
        return out

    def ui_reason(self, r: dict):
        for go in self.ancestors(r):
            mode = self.canvases.get(go)
            if mode in (0, 1):
                return f"under a screen-space canvas ({'Overlay' if mode == 0 else 'Camera'}: {self.gos.get(go, {}).get('name', '?')})"
        return None

    def layer_name(self, r: dict) -> str:
        layer = self.gos.get(r["go"], {}).get("layer", 0)
        return self.project.layers.get(layer, str(layer))


# ---------------------------------------------------------------------------------------------------------------
# The audit
# ---------------------------------------------------------------------------------------------------------------
def renderer_record(asset: Asset, r: dict) -> dict:
    p = asset.project
    mats = [p.material(fid, guid) for fid, guid, _ in r["materials"]]
    mesh_ref = r.get("mesh")
    if r["kind"] == "MeshRenderer":
        mesh_ref = asset.meshes_of_go.get(r["go"])
    mesh = p.mesh(mesh_ref[0], mesh_ref[1]) if mesh_ref else None
    rec = {
        "id": None, "kind": r["kind"], "object": asset.go_name(r), "layer": asset.layer_name(r) if r["kind"] != "ModelInstance" else "?",
        "enabled": bool(r.get("enabled", 1)),
        "materials": [{"name": m["name"], "path": m["path"], "shader": m["shader"]["name"],
                       "shader_kind": m["shader"]["kind"], "shader_path": m["shader"].get("path"),
                       "subtarget": m["shader"].get("subtarget"), "transparent": m["transparent"],
                       "additive": m["additive"], "queue": m["queue"], "surface": m["surface"], "blend": m["blend"],
                       "unlit_by_name": m["unlit_by_name"], "lit_by_content": m["lit_by_content"]} for m in mats],
        "mesh": ({"name": mesh["name"], "kind": mesh["kind"], "path": mesh["path"],
                  "ratio": (round(mesh["ratio"], 4) if mesh["ratio"] is not None else None),
                  "has_normals": mesh["has_normals"]} if mesh else None),
        "render_mode": (RENDER_MODES.get(r["render_mode"], str(r["render_mode"])) if r.get("render_mode") is not None else None),
        "trails": r.get("trails"), "alignment": r.get("alignment"), "vfx": (p.rel(r["vfx"]) if r.get("vfx") else None),
        "ui": asset.ui_reason(r), "signals": [], "undecided": [],
    }
    return rec


def signals_for(rec: dict, asset: Asset) -> list:
    """The flat signals of one world renderer record (the rules in SIGNALS)."""
    out = []
    mats = rec["materials"]
    unlit_name = any(m["unlit_by_name"] for m in mats)
    unlit_content = any(m["lit_by_content"] is False and not m["unlit_by_name"] for m in mats)
    unlit = unlit_name or unlit_content
    glowing = any((m["additive"] or m["transparent"]) and (m["unlit_by_name"] or m["lit_by_content"] is False) for m in mats)
    kind = rec["kind"]
    mode = rec["render_mode"]
    mode_id = next((k for k, v in RENDER_MODES.items() if v == mode), None)
    mesh = rec["mesh"]
    thin = mesh is not None and mesh["ratio"] is not None and mesh["ratio"] < THIN_RATIO
    sheet = ((kind == "ParticleSystem" and mode_id in SHEET_MODES) or kind in ("TrailRenderer", "LineRenderer")
             or (kind in ("MeshRenderer", "SkinnedMeshRenderer") and thin))
    if unlit_name:
        out.append("unlit-shader")
    if unlit_content:
        out.append("unlit-by-content")
    if glowing and sheet:
        out.append("additive-sheet")
    if kind == "ParticleSystem" and mode_id in BILLBOARD_MODES and unlit:
        out.append("billboard-particles")
    on_hud_layer = rec["layer"] in UI_LAYER_NAMES and kind != "ModelInstance"
    ringish = bool(word_re(RING_WORDS).search(rec["object"]))
    if (on_hud_layer and kind in ("LineRenderer", "TrailRenderer", "MeshRenderer", "ParticleSystem")) or (rec["ui"] and ringish):
        out.append("screen-ring")
    if thin and kind in ("MeshRenderer", "SkinnedMeshRenderer", "ParticleSystem"):
        out.append("thin-mesh")
    if mesh is not None and mesh["kind"] == "asset" and mesh["has_normals"] is False:
        out.append("no-normals")
    return out


def undecided_for(rec: dict) -> list:
    out = []
    empty = sum(1 for m in rec["materials"] if m["shader_kind"] == "none")
    if empty and rec["kind"] != "VisualEffect":
        out.append(f"{empty} empty material slot{'s' if empty > 1 else ''}: the material is assigned at runtime, so its shader is unknown offline")
    if rec["mesh"] and rec["mesh"]["kind"] == "fbx":
        out.append("mesh in an FBX: extents not readable offline (thin-mesh / no-normals skipped)")
    if rec["mesh"] and rec["mesh"]["kind"] == "unresolved":
        out.append(f"mesh guid unresolved ({rec['mesh']['name']})")
    for m in rec["materials"]:
        if m["shader_kind"] == "missing":
            out.append(f"material guid owned by no .meta under Assets, a dangling reference that renders nothing or magenta ({m['name'][17:]})")
        elif m["shader_kind"] == "embedded":
            out.append(f"material embedded in a model file, the importer's own: its shader is not readable offline ({m['path']})")
        elif m["shader_kind"] == "unresolved":
            out.append(f"shader guid unresolved ({m['shader']})")
        elif m["shader"] and m["shader"].endswith("?"):
            out.append(f"built-in shader not in the table ({m['shader']})")
        elif m["lit_by_content"] is None and not m["unlit_by_name"] and m["shader_kind"] not in ("none",):
            out.append(f"cannot tell lit from unlit ({m['shader']})")
    if rec["kind"] == "VisualEffect":
        out.append(f"VFX Graph ({rec['vfx'] or 'asset unresolved'}): its outputs live inside the .vfx")
    if rec["kind"] == "ParticleSystem" and rec["render_mode"] == "Stretch":
        out.append("Stretch billboard: the billboard rule names modes 0/2/3 only")
    if rec["kind"] == "ModelInstance" and not rec["materials"]:
        out.append("nested FBX model instance: materials come from the importer, not readable here")
    return out


def is_effect_renderer(asset: Asset, rec: dict) -> bool:
    if rec["kind"] in ("ParticleSystem", "TrailRenderer", "LineRenderer", "VisualEffect"):
        return True
    if any(m["unlit_by_name"] or FIND_NAME_RE.search(m["shader"] or "") for m in rec["materials"]):
        return True
    return asset.is_effect_by_name


def renderer_id(rec: dict, taken: Counter) -> str:
    base = f"{rec['object']}/{rec['kind']}"
    taken[base] += 1
    return base if taken[base] == 1 else f"{base}#{taken[base]}"


def audit_asset(asset: Asset) -> dict | None:
    taken: Counter = Counter()
    world, excluded = [], []
    for r in asset.renderers:
        rec = renderer_record(asset, r)
        rec["id"] = renderer_id(rec, taken)
        if rec["ui"]:
            rec["signals"] = ["screen-ring"] if "screen-ring" in signals_for(rec, asset) else []
            if rec["signals"]:
                rec["undecided"] = undecided_for(rec)
                world.append(rec)
            else:
                excluded.append(rec)
            continue
        if not is_effect_renderer(asset, rec):
            continue
        rec["signals"] = signals_for(rec, asset)
        rec["undecided"] = undecided_for(rec)
        world.append(rec)
    if not world:
        return None
    return {"path": asset.rel, "by_name": asset.is_effect_by_name, "renderers": world,
            "excluded_ui": [{"id": e["id"], "why": e["ui"]} for e in excluded]}


def iter_assets(root: str, scenes: bool) -> list:
    paths = []
    for base, ext in ((os.path.join(root, PREFABS_REL), ".prefab"),) + (((os.path.join(root, SCENES_REL), ".unity"),) if scenes else ()):
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = sorted(d for d in dirnames if not d.startswith("."))
            for fn in sorted(filenames):
                if fn.endswith(ext):
                    paths.append(os.path.join(dirpath, fn))
    return sorted(paths)


def run_audit(root: str, scenes: bool = False) -> dict:
    project = Project(root)
    effects = []
    for path in iter_assets(root, scenes):
        try:
            asset = Asset(project, path)
        except (OSError, UnicodeDecodeError) as exc:
            project.undecided.append(f"{project.rel(path)}: unreadable ({exc})")
            continue
        rec = audit_asset(asset)
        if rec:
            effects.append(rec)
    return summarize(project, effects, scenes)


def fold_undecided(notes: list) -> list:
    """Project-level strings stay as they are; (path, renderer, note) triples fold per prefab and note, so ninety
    DerbyBlock clones with the same empty slot are one line."""
    out = [n for n in notes if isinstance(n, str)]
    groups: dict = OrderedDict()
    for n in notes:
        if isinstance(n, tuple):
            groups.setdefault((n[0], n[2]), []).append(n[1])
    for (path, note), ids in groups.items():
        if len(ids) == 1:
            out.append(f"{path}:{ids[0]}: {note}")
        else:
            out.append(f"{path}: {len(ids)} renderers ({ids[0]}, ...): {note}")
    return sorted(set(out))


def summarize(project: Project, effects: list, scenes: bool) -> dict:
    per_signal: Counter = Counter()
    shaders: Counter = Counter()
    shader_kinds: Counter = Counter()
    renderer_kinds: Counter = Counter()
    modes: Counter = Counter()
    entries = []
    undecided = list(project.undecided)
    empty_slots = 0
    blind = []   # effects with no signal and not one readable material: nothing was decided about them
    for e in effects:
        readable = False
        for r in e["renderers"]:
            renderer_kinds[r["kind"]] += 1
            if r["render_mode"]:
                modes[r["render_mode"]] += 1
            for m in r["materials"]:
                shader_kinds[m["shader_kind"]] += 1
                if m["shader_kind"] == "none":
                    empty_slots += 1
                    continue
                shaders[m["shader"]] += 1
                if m["shader_kind"] not in ("unresolved", "missing", "embedded"):
                    readable = True
            for s in r["signals"]:
                per_signal[s] += 1
                entries.append(f"{e['path']}:{r['id']}:{s}")
            for u in r["undecided"]:
                undecided.append((e["path"], r["id"], u))
        if not readable and not any(r["signals"] for r in e["renderers"]):
            blind.append(e["path"])
    ranked = sorted(effects, key=lambda e: (-sum(len(r["signals"]) for r in e["renderers"]), e["path"]))
    worst = [{"path": e["path"], "signals": sum(len(r["signals"]) for r in e["renderers"]),
              "distinct": sorted({s for r in e["renderers"] for s in r["signals"]})}
             for e in ranked if sum(len(r["signals"]) for r in e["renderers"]) > 0]
    return {
        "generated": _dt.date.today().isoformat(),
        "scope": {"prefabs": PREFABS_REL.replace(os.sep, "/"), "scenes": SCENES_REL.replace(os.sep, "/") if scenes else None},
        "counts": {
            "effects": len(effects),
            "effects_with_signals": len(worst),
            "effects_clean": len(effects) - len(worst) - len(blind),
            "effects_blind": len(blind),
            "empty_material_slots": empty_slots,
            "renderers": sum(len(e["renderers"]) for e in effects),
            "renderer_kinds": dict(renderer_kinds.most_common()),
            "render_modes": dict(modes.most_common()),
            "shader_kinds": dict(shader_kinds.most_common()),
            "signals_total": len(entries),
            "per_signal": {s: per_signal.get(s, 0) for s in SIGNALS},
            "excluded_ui_renderers": sum(len(e["excluded_ui"]) for e in effects),
        },
        "top_shaders": [{"shader": s, "uses": n} for s, n in shaders.most_common(10)],
        "worst": worst[:15],
        "undecided": fold_undecided(undecided),
        "blind": sorted(blind),
        "entries": sorted(entries),
        "effects": effects,
    }


# ---------------------------------------------------------------------------------------------------------------
# Outputs
# ---------------------------------------------------------------------------------------------------------------
def short_path(path: str) -> str:
    for prefix in ("Assets/_Prefabs/", "Assets/_Scenes/"):
        if path.startswith(prefix):
            return path[len(prefix):]
    return path


def short_shader(name: str) -> str:
    return (name.replace("Universal Render Pipeline/", "URP/").replace("Shader Graphs/", "SG/")
            .replace("Legacy Shaders/", "Legacy/").replace("CosmicShore/", "CS/"))


def cell(text: str) -> str:
    return text.replace("|", "\\|")


def render_report(audit: dict, baseline: dict | None) -> str:
    c = audit["counts"]
    out = []
    out.append("# World look audit (generated)")
    out.append("")
    out.append(f"> Generated by `python3 Tools/Build/look_kit/audit.py` on {audit['generated']} from `{audit['scope']['prefabs']}`"
               + (f" and `{audit['scope']['scenes']}`" if audit["scope"]["scenes"] else "")
               + ". Do not edit by hand: re-run the tool. `--check` is the gate (a flat signal not in"
               " `Tools/Build/look_kit/audit_baseline.json` fails), `--baseline` rewrites the known-offender list.")
    out.append(">")
    out.append("> The rules measured: `Docs/claude/SHADERS_AND_PERFORMANCE.md` § Nothing in the world looks flat (LOCKED, Garrett"
               " 2026-10-18) and § In the world, not on the screen (LOCKED, Garrett 2026-10-21). The plan: `Docs/SKILL_TRAINING_PLAN.md`"
               " §3.2 (R0, the audit sheet). A signal is a measurement, never a verdict: the effect's owner decides, and a deliberate"
               " look is recorded where it is made.")
    out.append("")
    out.append("## Counts")
    out.append("")
    out.append(f"- Effects found: **{c['effects']}**: {c['effects_with_signals']} carry at least one flat signal, {c['effects_clean']} carry none,"
               f" {c['effects_blind']} could not be read (every material slot empty or unresolved: assigned at runtime)")
    out.append(f"- Empty material slots on world renderers (the material is assigned at runtime, the shader unknown offline): {c['empty_material_slots']}")
    out.append(f"- World renderers on them: {c['renderers']} (" + ", ".join(f"{k} {n}" for k, n in c["renderer_kinds"].items()) + ")")
    if c["render_modes"]:
        out.append("- Particle render modes: " + ", ".join(f"{k} {n}" for k, n in c["render_modes"].items()))
    out.append("- Shader kinds on their materials: " + ", ".join(f"{k} {n}" for k, n in c["shader_kinds"].items()))
    out.append(f"- Renderers left out as UI (under a screen-space canvas): {c['excluded_ui_renderers']}")
    out.append(f"- Flat signals: **{c['signals_total']}** in all")
    out.append("")
    out.append("| signal | count | the rule |")
    out.append("|---|---:|---|")
    for s, reason in SIGNALS.items():
        out.append(f"| `{s}` | {c['per_signal'].get(s, 0)} | {reason} |")
    out.append("")
    if baseline is not None:
        today = set(audit["entries"])
        base = set(baseline.get("entries", []))
        out.append(f"Baseline (`audit_baseline.json`, written {baseline.get('written', '?')}): {len(base)} known offenders; "
                   f"today {len(today - base)} new, {len(base - today)} fixed since.")
        out.append("")
    out.append("## The effects")
    out.append("")
    out.append("One row per prefab (or scene) that draws an effect in the world. `renderers` counts the world renderers that are"
               " part of the effect; `shader` is every shader on them (resolved through the material's guid); `render mode` is the"
               " particle renderer's mode (and a trail's alignment); `signals` are the flat shapes found, with the renderer count"
               " in brackets. A mesh in an FBX is marked `fbx` (not measurable offline).")
    out.append("")
    out.append("| effect | renderers | shader | render mode | signals |")
    out.append("|---|---|---|---|---|")
    for e in sorted(audit["effects"], key=lambda e: e["path"]):
        rk: Counter = Counter(r["kind"] for r in e["renderers"])
        renderers = ", ".join(f"{k}" + (f" x{n}" if n > 1 else "") for k, n in rk.most_common())
        shader_names: Counter = Counter(short_shader(m["shader"]) for r in e["renderers"] for m in r["materials"])
        shader_txt = "; ".join(f"`{s}`" + (f" x{n}" if n > 1 else "") for s, n in shader_names.most_common()) or "(none)"
        modes: list = []
        for r in e["renderers"]:
            if r["render_mode"]:
                modes.append(r["render_mode"] + (" +trails" if r.get("trails") else ""))
            elif r["alignment"]:
                modes.append(f"{r['kind'][:5].lower()} {r['alignment']}")
            if r["mesh"] and r["mesh"]["kind"] == "fbx":
                modes.append("fbx")
            elif r["mesh"] and r["mesh"]["ratio"] is not None and r["kind"] != "ParticleSystem":
                modes.append(f"mesh {r['mesh']['ratio']:.2f}")
        mode_txt = ", ".join(f"{k}" + (f" x{n}" if n > 1 else "") for k, n in Counter(modes).most_common()) or ""
        sig: Counter = Counter(s for r in e["renderers"] for s in r["signals"])
        sig_txt = ", ".join(f"`{s}`" + (f" ({n})" if n > 1 else "") for s, n in sig.most_common()) or "none"
        out.append(f"| `{cell(short_path(e['path']))}` | {renderers} | {cell(shader_txt)} | {cell(mode_txt)} | {sig_txt} |")
    out.append("")
    out.append("## The ten most-used shaders on world effects")
    out.append("")
    out.append("| shader | uses |")
    out.append("|---|---:|")
    for row in audit["top_shaders"]:
        out.append(f"| `{cell(row['shader'])}` | {row['uses']} |")
    out.append("")
    out.append("## The effects with the most signals (the audit's \"does not read\" set)")
    out.append("")
    if audit["worst"]:
        out.append("| effect | signals | which |")
        out.append("|---|---:|---|")
        for w in audit["worst"]:
            out.append(f"| `{cell(short_path(w['path']))}` | {w['signals']} | " + ", ".join(f"`{s}`" for s in w["distinct"]) + " |")
    else:
        out.append("None: every effect found passes every rule.")
    out.append("")
    out.append("## Offenders by renderer (what `--check` holds against the baseline)")
    out.append("")
    out.append("Each line is `path:renderer:signal`, the form the gate prints for a NEW offender.")
    out.append("")
    for line in audit["entries"]:
        out.append(f"- `{cell(line)}`")
    if not audit["entries"]:
        out.append("- (none)")
    out.append("")
    out.append("## What the heuristics could not decide")
    out.append("")
    if audit["blind"]:
        out.append("Effects nothing could be read on (no signal, and not one material resolved: the slots are filled at runtime):")
        out.append("")
        for p_ in audit["blind"]:
            out.append(f"- `{cell(short_path(p_))}`")
        out.append("")
    if audit["undecided"]:
        for line in audit["undecided"]:
            out.append(f"- {cell(line)}")
    else:
        out.append("- (nothing)")
    out.append("")
    out.append("## How an effect is found, and what is left out")
    out.append("")
    out.append("- A prefab is an effect when it has a ParticleSystem, TrailRenderer, LineRenderer or VisualEffect, or a MeshRenderer /"
               " SkinnedMeshRenderer whose material's shader name says Unlit, Particles, Additive, Sprites or UI, or whose folder or file"
               " name says one of: " + ", ".join(EFFECT_WORDS) + " (and the kin: " + ", ".join(EFFECT_WORDS_KIN) + ").")
    out.append("- On a prefab found by its name every world renderer is part of the effect; on one found by a particle, trail, line or"
               " unlit material only those renderers are (a vessel's hull is not an effect because the vessel carries a particle burst).")
    out.append("- Anything under a Canvas in screen space (Overlay or Camera) is UI and left out; the one exception is a world renderer"
               " inside such a canvas named like a ring, radius or reticle, which is the `screen-ring` shape the second rule forbids.")
    out.append("- A nested FBX model instance is listed with the materials its instance overrides; its importer materials are not readable here.")
    out.append("- Lit or unlit is read from the shader: the name (Unlit, Particles/Unlit, Sprites, UI), a Shader Graph's sub-target and"
               " whether any node (or a custom function's HLSL, or a sub graph) reads a normal or a light, and a .shader's lighting code."
               " Every project graph in this tree sits on the Unlit sub-target and shades itself: the node check is what tells a prism"
               " graph (reads Normal Vector) from a flat fill.")
    out.append("")
    return "\n".join(out) + "\n"


def load_baseline(path: str) -> dict | None:
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def write_baseline(path: str, audit: dict) -> None:
    data = {"written": audit["generated"], "note": "The known flat effects, each owed work; --check fails on any signal not listed here. "
                                                    "Rewrite with: python3 Tools/Build/look_kit/audit.py --baseline",
            "counts": audit["counts"]["per_signal"], "entries": audit["entries"]}
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=2)
        fh.write("\n")


def check(audit: dict, baseline: dict | None) -> int:
    if baseline is None:
        print("look_kit audit --check: no baseline; write one with --baseline", file=sys.stderr)
        return 3
    today = set(audit["entries"])
    base = set(baseline.get("entries", []))
    new = sorted(today - base)
    fixed = sorted(base - today)
    for line in new:
        print(f"NEW flat effect: {line}")
    print(f"look_kit audit --check: {len(today)} signals today, {len(base)} in the baseline, {len(new)} new, {len(fixed)} fixed"
          + (" - FAIL" if new else " - ok"))
    if fixed:
        print("  fixed since the baseline (rewrite it with --baseline to retire them): " + ", ".join(fixed[:10])
              + (" ..." if len(fixed) > 10 else ""))
    return 1 if new else 0


def print_summary(audit: dict) -> None:
    c = audit["counts"]
    print(f"effects {c['effects']} (with signals {c['effects_with_signals']}, clean {c['effects_clean']}, unreadable {c['effects_blind']}); "
          f"renderers {c['renderers']}; signals {c['signals_total']}; empty material slots {c['empty_material_slots']}")
    print("  per signal: " + ", ".join(f"{s} {n}" for s, n in c["per_signal"].items()))
    print("  top shaders: " + ", ".join(f"{short_shader(r['shader'])} ({r['uses']})" for r in audit["top_shaders"]))
    print("  undecided: " + str(len(audit["undecided"])))


# ---------------------------------------------------------------------------------------------------------------
# Self-test: a temp Assets tree with one case per rule, each with its control
# ---------------------------------------------------------------------------------------------------------------
def _guid(seed: str) -> str:
    import hashlib
    return hashlib.md5(f"look_kit/{seed}".encode()).hexdigest()


def _write(root: str, rel: str, text: str, guid: str | None = None) -> None:
    p = os.path.join(root, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8") as fh:
        fh.write(text)
    if guid:
        with open(p + ".meta", "w", encoding="utf-8") as fh:
            fh.write(f"fileFormatVersion: 2\nguid: {guid}\n")


def _mat(name: str, shader_fid: int, shader_guid: str, surface: int = 0, blend: int = 0, queue: int = -1) -> str:
    return (f"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_Name: {name}\n"
            f"  m_Shader: {{fileID: {shader_fid}, guid: {shader_guid}, type: 3}}\n  m_ValidKeywords: []\n  m_CustomRenderQueue: {queue}\n"
            f"  stringTagMap: {{}}\n  m_SavedProperties:\n    serializedVersion: 3\n    m_Floats:\n    - _Blend: {blend}\n"
            f"    - _Surface: {surface}\n    - _SrcBlend: {5 if surface else 1}\n    - _DstBlend: {10 if surface else 0}\n"
            f"    - _ZWrite: {0 if surface else 1}\n    m_Colors: []\n")


def _mesh_asset(name: str, extent: tuple, normals: bool) -> str:
    ndim = 3 if normals else 0
    return (f"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!43 &4300000\nMesh:\n  m_ObjectHideFlags: 0\n  m_Name: {name}\n"
            f"  m_VertexData:\n    serializedVersion: 3\n    m_VertexCount: 3\n    m_Channels:\n"
            f"    - stream: 0\n      offset: 0\n      format: 0\n      dimension: 3\n"
            f"    - stream: 0\n      offset: 12\n      format: 0\n      dimension: {ndim}\n"
            f"    m_DataSize: 0\n  m_LocalAABB:\n    m_Center: {{x: 0, y: 0, z: 0}}\n"
            f"    m_Extent: {{x: {extent[0]}, y: {extent[1]}, z: {extent[2]}}}\n  m_MeshUsageFlags: 0\n")


def _graph(name: str, with_normal_node: bool, surface: int = 1, alpha: int = 2) -> str:
    objs = [
        {"m_SGVersion": 3, "m_Type": "UnityEditor.ShaderGraph.GraphData", "m_ObjectId": "g1", "m_Path": "Shader Graphs",
         "m_ActiveTargets": [{"m_Id": "t1"}]},
        {"m_SGVersion": 1, "m_Type": "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget", "m_ObjectId": "t1",
         "m_ActiveSubTarget": {"m_Id": "s1"}, "m_AllowMaterialOverride": False, "m_SurfaceType": surface, "m_AlphaMode": alpha},
        {"m_SGVersion": 2, "m_Type": "UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget", "m_ObjectId": "s1"},
        {"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.MultiplyNode", "m_ObjectId": "n1", "m_Name": "Multiply"},
    ]
    if with_normal_node:
        objs.append({"m_SGVersion": 0, "m_Type": "UnityEditor.ShaderGraph.NormalVectorNode", "m_ObjectId": "n2", "m_Name": "Normal Vector"})
    return "\n\n".join(json.dumps(o, indent=4) for o in objs) + "\n"


def _prefab(objects: list) -> str:
    """objects: list of dicts {id, name, layer, parent, components: [(class, fid, yaml body)]}; the Transform is made here."""
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
    for o in objects:
        comps = "".join(f"  - component: {{fileID: {o['id'] + 1}}}\n") + "".join(f"  - component: {{fileID: {fid}}}\n" for _, fid, _ in o["components"])
        out.append(f"--- !u!1 &{o['id']}\nGameObject:\n  m_ObjectHideFlags: 0\n  serializedVersion: 6\n  m_Component:\n{comps}"
                   f"  m_Layer: {o.get('layer', 0)}\n  m_Name: {o['name']}\n  m_IsActive: 1")
        children = "".join(f"  - {{fileID: {c['id'] + 1}}}\n" for c in objects if c.get("parent") == o["id"])
        out.append(f"--- !u!4 &{o['id'] + 1}\nTransform:\n  m_GameObject: {{fileID: {o['id']}}}\n  m_Children:\n{children}"
                   f"  m_Father: {{fileID: {o['parent'] + 1 if o.get('parent') else 0}}}")
        for cls, fid, body in o["components"]:
            out.append(f"--- !u!{cls} &{fid}\n{body.rstrip()}")
    return "\n".join(out) + "\n"


def _mesh_renderer(go: int, fid: int, mat: tuple) -> tuple:
    return (CLASS_MESHRENDERER, fid, f"MeshRenderer:\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Materials:\n"
                                     f"  - {{fileID: {mat[0]}, guid: {mat[1]}, type: {mat[2]}}}\n  m_StaticBatchInfo:\n    firstSubMesh: 0\n")


def _mesh_filter(go: int, fid: int, mesh: tuple) -> tuple:
    return (CLASS_MESHFILTER, fid, f"MeshFilter:\n  m_GameObject: {{fileID: {go}}}\n  m_Mesh: {{fileID: {mesh[0]}, guid: {mesh[1]}, type: {mesh[2]}}}\n")


def _particle(go: int, fid: int, mat: tuple, mode: int) -> list:
    return [(CLASS_PARTICLE, fid, f"ParticleSystem:\n  m_GameObject: {{fileID: {go}}}\n  lengthInSec: 1\n  TrailModule:\n    enabled: 0\n"),
            (CLASS_PARTICLE_RENDERER, fid + 1, f"ParticleSystemRenderer:\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Materials:\n"
                                               f"  - {{fileID: {mat[0]}, guid: {mat[1]}, type: {mat[2]}}}\n  m_RenderMode: {mode}\n  m_Mesh: {{fileID: 0}}\n")]


def _line(go: int, fid: int, mat: tuple, cls: int = CLASS_LINE) -> tuple:
    name = "LineRenderer" if cls == CLASS_LINE else "TrailRenderer"
    return (cls, fid, f"{name}:\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Materials:\n"
                      f"  - {{fileID: {mat[0]}, guid: {mat[1]}, type: {mat[2]}}}\n  m_Parameters:\n    widthMultiplier: 1\n    alignment: 0\n")


def _canvas(go: int, fid: int, mode: int) -> tuple:
    return (CLASS_CANVAS, fid, f"Canvas:\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_RenderMode: {mode}\n")


def build_fixture(root: str) -> dict:
    """The temp Assets tree: returns the expected signals per prefab (relative path -> set of 'renderer:signal')."""
    g = {k: _guid(k) for k in ("lit.shader", "unlit.shader", "flat.graph", "facet.graph", "lit.mat", "unlit.mat", "flat.mat",
                                "facet.mat", "sprite.mat", "thin.asset", "bare.asset", "lit-add.mat")}
    _write(root, "ProjectSettings/TagManager.asset", "TagManager:\n  layers:\n  - Default\n  - TransparentFX\n  - Ignore Raycast\n  - \n  - Water\n  - UI\n")
    _write(root, "Assets/Shaders/LitFacet.shader",
           'Shader "Test/LitFacet" { SubShader { Tags { "Queue" = "Geometry" } Pass { HLSLPROGRAM\n'
           "struct a { float3 normal : NORMAL; }; Light l = GetMainLight(); ENDHLSL } } }\n", g["lit.shader"])
    _write(root, "Assets/Shaders/UnlitGlow.shader",
           'Shader "Test/Glow" { SubShader { Tags { "Queue" = "Transparent" } Pass { Blend One One\n'
           "HLSLPROGRAM float4 frag() : SV_Target { return 1; } ENDHLSL } } }\n", g["unlit.shader"])
    _write(root, "Assets/Shaders/FlatGraph.shadergraph", _graph("FlatGraph", False), g["flat.graph"])
    _write(root, "Assets/Shaders/FacetGraph.shadergraph", _graph("FacetGraph", True, surface=0, alpha=0), g["facet.graph"])
    _write(root, "Assets/Materials/Lit.mat", _mat("Lit", 4800000, g["lit.shader"]), g["lit.mat"])
    _write(root, "Assets/Materials/LitAdditive.mat", _mat("LitAdditive", 4800000, g["lit.shader"], surface=1, blend=2, queue=3000), g["lit-add.mat"])
    _write(root, "Assets/Materials/Unlit.mat", _mat("UnlitGlow", 4800000, g["unlit.shader"], surface=1, blend=2, queue=3000), g["unlit.mat"])
    _write(root, "Assets/Materials/Flat.mat", _mat("Flat", -6465566751694194690, g["flat.graph"]), g["flat.mat"])
    _write(root, "Assets/Materials/Facet.mat", _mat("Facet", -6465566751694194690, g["facet.graph"]), g["facet.mat"])
    _write(root, "Assets/Materials/Sprite.mat", _mat("Sprite", 10753, BUILTIN_SHADER_GUID), g["sprite.mat"])
    _write(root, "Assets/Materials/ParticlesLit.mat", _mat("ParticlesLit", 4800000, "b7839dad95683814aa64166edc107ae2", surface=1, blend=0), _guid("plit.mat"))
    _write(root, "Assets/Models/Hull.fbx", "not a real fbx", _guid("hull.fbx"))
    _write(root, "Assets/Models/ThinSheet.asset", _mesh_asset("ThinSheet", (1.0, 0.01, 1.0), True), g["thin.asset"])
    _write(root, "Assets/Models/Bare.asset", _mesh_asset("Bare", (1.0, 1.0, 1.0), False), g["bare.asset"])
    cube = (10202, BUILTIN_RESOURCE_GUID, 0)
    sphere = (10207, BUILTIN_RESOURCE_GUID, 0)
    quad = (10210, BUILTIN_RESOURCE_GUID, 0)
    M = lambda k: (2100000, g[k], 2)  # noqa: E731
    expected = {}

    def prefab(rel: str, objects: list, expect: set):
        _write(root, os.path.join("Assets", "_Prefabs", rel), _prefab(objects))
        expected["Assets/_Prefabs/" + rel] = expect

    # the asked-for cases
    prefab("FX/UnlitCube.prefab", [{"id": 100, "name": "UnlitCube", "components": [_mesh_filter(100, 110, cube), _mesh_renderer(100, 111, M("unlit.mat"))]}],
           {"UnlitCube/MeshRenderer:unlit-by-content"})   # 'Test/Glow' is not named Unlit; its code has no light: by content
    prefab("FX/LitCube.prefab", [{"id": 200, "name": "LitCube", "components": [_mesh_filter(200, 210, cube), _mesh_renderer(200, 211, M("lit.mat"))]}],
           set())
    prefab("FX/Burst.prefab", [{"id": 300, "name": "Burst", "components": _particle(300, 310, M("sprite.mat"), 0)}],
           {"Burst/ParticleSystem:unlit-shader", "Burst/ParticleSystem:billboard-particles", "Burst/ParticleSystem:additive-sheet"})
    prefab("UI/HudQuad.prefab", [{"id": 400, "name": "Hud", "components": [_canvas(400, 410, 0)]},
                                 {"id": 420, "name": "Quad", "parent": 400, "components": [_mesh_filter(420, 430, quad), _mesh_renderer(420, 431, M("sprite.mat"))]}],
           None)   # excluded: never found
    # the other rules, each with its control
    prefab("FX/MeshBurst.prefab", [{"id": 500, "name": "MeshBurst", "components": _particle(500, 510, M("sprite.mat"), 4)}],
           {"MeshBurst/ParticleSystem:unlit-shader"})   # mesh particles: no billboard, no sheet
    prefab("FX/LitBurst.prefab", [{"id": 550, "name": "LitBurst", "components": _particle(550, 560, M("lit.mat"), 0)}],
           set())   # a billboard on a LIT material raises nothing: the rule wants both
    prefab("Pets/Glitter.prefab", [{"id": 570, "name": "Glitter", "components": [_mesh_filter(570, 580, cube), _mesh_renderer(570, 581, (2100000, _guid("plit.mat"), 2))]}],
           set())   # URP Particles/Lit: 'Particles' finds it, '/Lit' keeps it off the unlit-shader signal
    prefab("FX/Dangling.prefab", [{"id": 590, "name": "Dangling", "components": [_mesh_filter(590, 595, cube), _mesh_renderer(590, 596, (2100000, _guid("nobody-owns-this"), 2))]}],
           set())   # a material guid no .meta owns: undecided, never a crash
    prefab("FX/Embedded.prefab", [{"id": 597, "name": "Embedded", "components": [_mesh_filter(597, 598, cube), _mesh_renderer(597, 599, (-1234, _guid("hull.fbx"), 3))]}],
           set())   # a material inside an FBX: undecided
    prefab("FX/ThinSheet.prefab", [{"id": 600, "name": "Sheet", "components": [_mesh_filter(600, 610, (4300000, g["thin.asset"], 2)), _mesh_renderer(600, 611, M("lit.mat"))]}],
           {"Sheet/MeshRenderer:thin-mesh"})
    prefab("FX/ThinGlow.prefab", [{"id": 650, "name": "Glow", "components": [_mesh_filter(650, 660, quad), _mesh_renderer(650, 661, M("unlit.mat"))]}],
           {"Glow/MeshRenderer:thin-mesh", "Glow/MeshRenderer:unlit-by-content", "Glow/MeshRenderer:additive-sheet"})
    prefab("FX/LitQuad.prefab", [{"id": 680, "name": "LitQuad", "components": [_mesh_filter(680, 690, quad), _mesh_renderer(680, 691, M("lit-add.mat"))]}],
           {"LitQuad/MeshRenderer:thin-mesh"})   # additive but LIT: thin, not an additive-sheet
    prefab("FX/Bare.prefab", [{"id": 700, "name": "Bare", "components": [_mesh_filter(700, 710, (4300000, g["bare.asset"], 2)), _mesh_renderer(700, 711, M("lit.mat"))]}],
           {"Bare/MeshRenderer:no-normals"})
    prefab("FX/FlatGraph.prefab", [{"id": 800, "name": "FlatGraph", "components": [_mesh_filter(800, 810, sphere), _mesh_renderer(800, 811, M("flat.mat"))]}],
           {"FlatGraph/MeshRenderer:unlit-by-content"})
    prefab("FX/FacetGraph.prefab", [{"id": 850, "name": "FacetGraph", "components": [_mesh_filter(850, 860, sphere), _mesh_renderer(850, 861, M("facet.mat"))]}],
           set())
    prefab("FX/Ribbon.prefab", [{"id": 900, "name": "Ribbon", "components": [_line(900, 910, M("unlit.mat"), CLASS_TRAIL)]}],
           {"Ribbon/TrailRenderer:unlit-by-content", "Ribbon/TrailRenderer:additive-sheet"})
    prefab("Hud/WorldMark.prefab", [{"id": 1000, "name": "AimLine", "layer": 5, "components": [_line(1000, 1010, M("lit.mat"))]}],
           {"AimLine/LineRenderer:screen-ring"})
    prefab("UI/HudRing.prefab", [{"id": 1100, "name": "Hud", "components": [_canvas(1100, 1110, 0)]},
                                 {"id": 1120, "name": "BlastRing", "parent": 1100, "components": [_line(1120, 1130, M("unlit.mat"))]}],
           {"BlastRing/LineRenderer:screen-ring"})
    prefab("Pets/Starling.prefab", [{"id": 1200, "name": "Starling", "components": [_mesh_filter(1200, 1210, cube), _mesh_renderer(1200, 1211, M("lit.mat"))]}],
           None)   # 'ring' inside 'Starling' is not the word
    prefab("Vessels/Hull.prefab", [{"id": 1300, "name": "Hull", "components": [_mesh_filter(1300, 1310, cube), _mesh_renderer(1300, 1311, M("lit.mat"))]},
                                   {"id": 1320, "name": "Spark", "parent": 1300, "components": _particle(1320, 1330, M("sprite.mat"), 2)}],
           {"Spark/ParticleSystem:unlit-shader", "Spark/ParticleSystem:billboard-particles", "Spark/ParticleSystem:additive-sheet"})
    # the hull's MeshRenderer is not part of the effect: the vessel is found by its spark only
    return expected


def self_test() -> int:
    ok = True
    with tempfile.TemporaryDirectory() as td:
        expected = build_fixture(td)
        audit = run_audit(td)
        found = {e["path"]: {f"{r['id']}:{s}" for r in e["renderers"] for s in r["signals"]} for e in audit["effects"]}
        for path, want in sorted(expected.items()):
            got = found.get(path)
            if want is None:
                hit = got is None
                print(f"self-test {short_path(path):<22} {'left out' if hit else 'FOUND (should be left out)'}")
            else:
                hit = got == want
                print(f"self-test {short_path(path):<22} {'fires as expected' if hit else 'MISMATCH'}: "
                      + (", ".join(sorted(got)) if got else "(none / not found)") + ("" if hit else f"  wanted {sorted(want)}"))
            ok &= hit
        hull = next((e for e in audit["effects"] if e["path"].endswith("Vessels/Hull.prefab")), None)
        hull_ok = hull is not None and [r["id"] for r in hull["renderers"]] == ["Spark/ParticleSystem"]
        print(f"self-test hull renderer      {'left out of the effect' if hull_ok else 'FAIL: ' + str(hull and [r['id'] for r in hull['renderers']])}")
        ok &= hull_ok
        notes = "\n".join(audit["undecided"])
        und_ok = ("FX/Dangling.prefab:Dangling/MeshRenderer: material guid owned by no .meta" in notes
                  and "FX/Embedded.prefab:Embedded/MeshRenderer: material embedded in a model file" in notes
                  and any(b.endswith("FX/Dangling.prefab") for b in audit["blind"])
                  and any(b.endswith("FX/Embedded.prefab") for b in audit["blind"]))
        print(f"self-test undecided          {'dangling and embedded materials are named, and blind' if und_ok else 'FAIL: ' + notes}")
        ok &= und_ok
        hud = next((e for e in audit["effects"] if e["path"].endswith("UI/HudRing.prefab")), None)
        hud_ok = hud is not None and hud["renderers"][0]["ui"] is not None
        print(f"self-test canvas ring        {'kept as screen-ring with its canvas named' if hud_ok else 'FAIL'}")
        ok &= hud_ok
        # the baseline: an offender in the baseline passes --check, a new one fails and is named
        bp = os.path.join(td, "baseline.json")
        write_baseline(bp, audit)
        rc_same = check(audit, load_baseline(bp))
        _write(td, "Assets/_Prefabs/FX/Burst2.prefab", read_text(os.path.join(td, "Assets/_Prefabs/FX/Burst.prefab")).replace("m_Name: Burst", "m_Name: Burst2"))
        audit2 = run_audit(td)
        rc_new = check(audit2, load_baseline(bp))
        new_named = any(e.startswith("Assets/_Prefabs/FX/Burst2.prefab:Burst2/ParticleSystem:") for e in set(audit2["entries"]) - set(audit["entries"]))
        print(f"self-test baseline           same tree {'passes' if rc_same == 0 else 'FAIL rc=' + str(rc_same)}; "
              f"new offender {'fails and is named' if rc_new == 1 and new_named else 'FAIL rc=' + str(rc_new)}")
        ok &= rc_same == 0 and rc_new == 1 and new_named
        rc_none = check(audit, None)
        print(f"self-test no baseline        {'exit 3' if rc_none == 3 else 'FAIL rc=' + str(rc_none)}")
        ok &= rc_none == 3
        # the report renders and names every rule
        report = render_report(audit2, load_baseline(bp))
        rep_ok = all(f"`{s}`" in report for s in SIGNALS) and "FX/Burst2.prefab" in report
        print(f"self-test report             {'renders every rule' if rep_ok else 'FAIL'}")
        ok &= rep_ok
    print("look_kit audit: self-test " + ("OK" if ok else "FAILED"))
    return 0 if ok else 1


# ---------------------------------------------------------------------------------------------------------------
def main(argv: list | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", default=ROOT, help="the repository root (default: this checkout)")
    ap.add_argument("--json", action="store_true", help="print the audit as JSON and write nothing")
    ap.add_argument("--check", action="store_true", help="fail on any flat signal not in the baseline")
    ap.add_argument("--baseline", action="store_true", help="write the baseline from the current tree")
    ap.add_argument("--scenes", action="store_true", help="also audit scene-placed renderers under Assets/_Scenes")
    ap.add_argument("--out", default=None, help="the report path (default Docs/Shaders/WORLD_LOOK_AUDIT.md)")
    ap.add_argument("--baseline-path", default=None, help="the baseline path (default Tools/Build/look_kit/audit_baseline.json)")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        return self_test()
    root = os.path.abspath(args.root)
    if not os.path.isdir(os.path.join(root, PREFABS_REL)):
        print(f"look_kit audit: no {PREFABS_REL} under {root}", file=sys.stderr)
        return 2
    audit = run_audit(root, scenes=args.scenes)
    baseline_path = args.baseline_path or os.path.join(root, BASELINE_REL)
    if args.json:
        print(json.dumps(audit, indent=2))
        return 0
    if args.baseline:
        write_baseline(baseline_path, audit)
        print(f"look_kit audit: baseline written to {os.path.relpath(baseline_path, root)} ({len(audit['entries'])} signals)")
    if args.check:
        return check(audit, load_baseline(baseline_path))
    out = args.out or os.path.join(root, REPORT_REL)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8") as fh:
        fh.write(render_report(audit, load_baseline(baseline_path)))
    print(f"look_kit audit: wrote {os.path.relpath(out, root)}")
    print_summary(audit)
    return 0


if __name__ == "__main__":
    sys.exit(main())
