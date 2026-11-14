#!/usr/bin/env python3
"""Colour trace: every colour the game draws, traced to the colour set the player wears.

Garrett, 2026-11-10: "we want every single color in the game to respect the colorset. no pixel anywhere should ever
recieve a color that can be traced somewhere but a colorset ... you should be able to extract any baked in colors and
then determine which domain they are in. then extend the color set to include that property ... for the photic set
you can map the baked value into its colorset. for all the others calculate a new appropriate values."

Docs/VesselModels/COLOR_TRACE.md is the doc. In short:

* SCAN: every colour authored in first-party assets and runtime code: component fields, prefab-instance overrides,
  materials, render settings, volume profiles, shader defaults and constants, C# literals. A record is one colour
  where it is authored, with the context the rules need (its document's other fields).
* STATUS: each record is exactly one of
    inert     it never reaches a pixel; INERT names each rule and the evidence for it;
    token     a UI colour equal to a UI theme token: the theme the worn set gives (ColorSetUITheme) paints it;
    fixed     pure white or pure black, the identity tint over art and the absence of light: the same in every set;
    traced    a TracedColor every colour set carries, found on screen by its value and repainted from the worn set
              (a world colour equal to a field of PhoticColorSetSO takes each set's own value of that field);
    untraced  it reaches a pixel and nothing paints it from the set yet. --check fails when one appears that the
              table does not list (a ratchet: the list only shrinks).
* IDS: `ui:RRGGBB` and `world:RRGGBB`, the colour as authored, in 8-bit channels (`xN.NN` follows when the colour is
  HDR: its brightest channel). `ui` is canvas graphics, text, selectables and UI materials; `world` is materials,
  particles, trails and lines, lights, cameras, sprites and the sky. `ui-token:<Token>` carries a UI theme token the
  set does not derive from its pigments (the tooltip and the element colours).
* VALUES: every colour set holds a colour for every id (SO_ColorSet.TracedColors, written by
  author_colour_set_assets.py; Photic's block by --write-photic). Photic, the set the game was authored in, holds the
  baked value. Every other set computes its own from its pigments (repaint):
    - a grey keeps its value (every grey is Blue's, and a grey has no hue to change);
    - a UI ground (a Deep Sea navy: Blue, chroma 0.10 or less, L 0.70 or less) keeps its lightness and chroma and takes
      the hue of the set's Jade face, exactly as the UI theme re-hues its panels (ColorSetUITheme.Tint);
    - any other colour of family F wears the set's F RIM pigment, keeping its brightness (brightest linear channel),
      its alpha and its whiteness beyond Photic's F rim, which is how the palette writer paints every rim field. The
      rim always names its family (rule 4); a face may be quiet and of any hue, so faces are never used here;
    - a colour that sits in the band between two families is read as the nearest one;
    - a set may override any id (colour_sets.json, set.traced: {id: "#RRGGBB"}).

    python3 Tools/Build/colour_trace.py --scan [--out f.json]   # every record, with its status, as JSON
    python3 Tools/Build/colour_trace.py --summary               # counts by status, rule, role, family
    python3 Tools/Build/colour_trace.py --build                 # write Docs/VesselModels/colour_trace.json
    python3 Tools/Build/colour_trace.py --write-photic          # write PhoticColorSetSO's TracedColors block
    python3 Tools/Build/colour_trace.py --check                 # the gate (scan == table, every set carries it)
    python3 Tools/Build/colour_trace.py --report                # what is untraced, by file
    python3 Tools/Build/colour_trace.py --self-test             # proves --check names each failure it exists for

Colours a SO_ColorSet palette asset holds are the colour sets themselves and are skipped; so are Editor-only and test
code and third-party folders.
"""
import json
import math
import os
import re
import struct
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import colour_domains  # noqa: E402
import colour_sets as cs  # noqa: E402

TABLE = os.path.join(REPO, "Docs", "VesselModels", "colour_trace.json")
FORMAT = "cosmic-shore-colour-trace/1"
FIRST_PARTY = ["Assets/_Graphics", "Assets/_Models", "Assets/_Prefabs", "Assets/_Scenes", "Assets/_SO_Assets",
               "Assets/_Scripts", "Assets/_Animations", "Assets/Resources", "Assets/Settings", "Assets/FTUE",
               "Assets/Effects Library/Froglet Stuff", "Assets/DefaultVolumeProfile.asset"]
PALETTES = "Assets/_SO_Assets/Color Palettes"
PHOTIC_ASSET = os.path.join(REPO, PALETTES, "PhoticColorSetSO.asset")
UI_THEME_ASSET = os.path.join(REPO, "Assets", "_SO_Assets", "UI", "UITheme.asset")
BUILD_SETTINGS = os.path.join(REPO, "ProjectSettings", "EditorBuildSettings.asset")
YAML_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller", ".anim", ".mixer", ".lighting")
NUM = r"(-?[\d.]+(?:e[-+]?\d+)?)"
COLOR_RE = re.compile(r"^(\s*)(?:- )?(\w+): \{r: %s, g: %s, b: %s, a: %s\}" % (NUM, NUM, NUM, NUM))
KEY_RE = re.compile(r"^(\s*)(?:- )?(\w+):(?:\s|$)")
LIST_COLOR_RE = re.compile(r"^(\s*)- \{r: %s, g: %s, b: %s, a: %s\}\s*$" % (NUM, NUM, NUM, NUM))
SCALAR_RE = re.compile(r"^(\s*)(?:- )?(\w+): (.+)$")
RGBA32_RE = re.compile(r"^(\s*)rgba: (\d+)\s*$")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?")
SCRIPT_RE = re.compile(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})")
GO_RE = re.compile(r"m_GameObject: \{fileID: (-?\d+)\}")
NAME_RE = re.compile(r"^  m_Name: (.*)$")
SHADER_RE = re.compile(r"m_Shader: \{fileID: (-?\d+), guid: ([0-9a-f]{32})")
GUID_REF_RE = re.compile(r"guid: ([0-9a-f]{32})")
CLASS_NAMES = {"1": "GameObject", "4": "Transform", "20": "Camera", "23": "MeshRenderer", "96": "TrailRenderer",
               "102": "TextMesh", "104": "RenderSettings", "108": "Light", "114": "MonoBehaviour",
               "120": "LineRenderer", "198": "ParticleSystem", "199": "ParticleSystemRenderer",
               "212": "SpriteRenderer", "222": "CanvasRenderer", "224": "RectTransform", "1001": "PrefabInstance",
               "21": "Material", "137": "SkinnedMeshRenderer", "157": "LightmapSettings", "196": "NavMeshSettings",
               "850595691": "LightingSettings"}

UNITY_SCRIPTS = {   # package scripts the repo has no .meta for (Unity UI, TextMesh Pro)
    "fe87c0e1cc204ed48ad3b37840f39efc": "Image", "f4688fdb7df04437aeb418b961361dc5": "TextMeshProUGUI",
    "4e29b1a8efbd4b44bb3f3716e73f07ff": "Button", "1344c3c82d62a2a41a3576d8abb8e3ea": "RawImage",
    "9085046f02f69544eb97fd06b6048fe2": "Toggle", "67db9e8f0e2ae9c40bc1e2b64352a6b4": "Slider",
    "2a4db7a114972834c8e4117be1d82ba3": "Scrollbar", "5f7201a12d95ffc409449d95f23cf332": "Text",
    "e19747de3f5aca642ab2be37e372fb86": "Outline", "cfabb0440166ab443bba8876756fdfa9": "Shadow",
    "9541d86e2fd84c1d9990edf0852d74ab": "TextMeshPro", "2da0c512f12947e489f739169773d7ca": "TMP_InputField",
    "7b743370ac3e4ec2a1668f5455a8ef8a": "TMP_Dropdown", "d199490a83bb2b844b9695cbf13b01ef": "InputField",
    "0d0b652f32a2cc243917e4028fa0f046": "Dropdown", "1aa08ab6e0800fa44ae55d278d1423e3": "ScrollRect",
    "31394aa05878563488a0c89ff9a4ef77": "Mask", "3312d7739989d2b4e91e6319e9a96d76": "RectMask2D",
    # URP volume components (read from the profiles that hold them)
    "0b2db86121404754db890f4c8dfe81b2": "Bloom", "899c54efeace73346a0a16faa3afe726": "Vignette",
    "66f335fb1ffd8684294ad653bf1c7564": "ColorAdjustments", "06437c1ff663d574d9447842ba0a72e4": "ScreenSpaceLensFlare",
    "70afe9e12c7a7ed47911bb608a23a8ff": "SplitToning"}

# Kit selectables paint their own colours: KitSelectableBase.Awake sets transition = None (the UI kit map, rule K1).
KIT_SELECTABLES = {"KitButton", "KitTile", "KitTextTab", "KitBrandButton"}
SELECTABLES = {"Button", "Toggle", "Slider", "Scrollbar", "TMP_Dropdown", "TMP_InputField", "Dropdown", "InputField"}
GRAPHICS = {"Image", "RawImage", "Text", "TextMeshProUGUI", "TextMeshPro", "Outline", "Shadow"}
# Legacy colour fields a Kit subclass inherits and never reads (it overrides the base hook that reads them; rule K2).
KIT_LEGACY = {
    "KitIntensityPip": {"IntensityColorSelected", "IntensityColorUnselected", "IntensityColorActive",
                        "IntensityColorInactive", "IntensityColorLocked"},
    "KitIntensityNode": {"IntensityColorSelected", "IntensityColorUnselected", "IntensityColorActive",
                         "IntensityColorInactive", "IntensityColorLocked"},
    "KitOnlineRow": {"joinTint", "spectateTint", "onlineColor", "inLobbyColor", "inMatchColor", "lobbyFullColor",
                     "inYourPartyColor", "defaultTint", "pendingInviteTint", "pendingInviteTintBright", "disabledTint"},
    "KitQuestCard": {"lockedTint", "activeQuestGlowColor", "readyToClaimGlowColor"},
    "KitRequestRow": {"friendRequestColor", "partyInviteColor", "expiringSoonColor"},
    "KitGameCard": {"localPickBorderColor", "lockedTintColor"},
    "KitArenaGameCard": {"localPickBorderColor", "lockedTintColor"},
    "KitDomainTile": {"selectedTextColor", "unselectedTextColor"},
    "KitToyVariantCard": {"fallbackCta"},
}
# Shader property types for shaders that ship in packages or the editor (the repo has no source for them).
PACKAGE_SHADERS = {
    "933532a4fcc9baf4fa0491de14d08ed7": {"name": "Universal Render Pipeline/Lit",
                                         "colors": {"_BaseColor", "_SpecColor", "_EmissionColor"}, "unused": {"_Color"}},
    "650dd9526735d5b46b79224bc6e94025": {"name": "Universal Render Pipeline/Unlit", "colors": {"_BaseColor"},
                                         "unused": {"_Color"}},
    "0406db5a14f94604a8c57ccfbc9f3b46": {"name": "Universal Render Pipeline/Particles/Unlit",
                                         "colors": {"_BaseColor", "_EmissionColor"}, "unused": {"_Color"},
                                         "vectors": {"_BaseColorAddSubDiff", "_SoftParticleFadeParams", "_CameraFadeParams"}},
    "b7839dad95683814aa64166edc107ae2": {"name": "Universal Render Pipeline/Particles/Lit",
                                         "colors": {"_BaseColor", "_EmissionColor", "_SpecColor"}, "unused": {"_Color"},
                                         "vectors": {"_BaseColorAddSubDiff", "_SoftParticleFadeParams", "_CameraFadeParams"}},
}
VECTOR_NAMES = {"_BaseColorAddSubDiff", "_SoftParticleFadeParams", "_CameraFadeParams", "_ColorAddSubDiff",
                "_FlightVelocity", "_GrowStartFrac", "_Spread", "_GalacticNormal", "_CoreDirection",
                "_AndromedaDirection", "_Ripple_Origin"}
UI_SHADER_PREFIXES = ("UI/", "TextMeshPro/", "CosmicShore/UI/", "Hidden/UI", "Sprites/")
POST_TYPES = {"Bloom", "Vignette", "ColorAdjustments", "SplitToning", "ScreenSpaceLensFlare", "WhiteBalance",
              "ShadowsMidtonesHighlights", "LiftGammaGain", "ChannelMixer", "Tonemapping", "FilmGrain",
              "ChromaticAberration", "DepthOfField", "ColorCurves", "LensDistortion", "PaniniProjection",
              "MotionBlur"}
NEUTRAL = 0.02               # OKLab chroma under which a colour is a grey (colour_domains: every grey is Blue's)
GROUND = (0.10, 0.70)        # a UI ground: Blue, chroma at most 0.10, L at most 0.70 (the Deep Sea navies)
TOKEN_TOL = 0.6 / 255        # UIThemeLive.Tolerance: a typed token and its float in YAML are one colour
SEA_PROPS = {"_LineColor", "_LineColorMid", "_LineColorEnd", "_GlowColor"}
SET_GIVEN = ("ElementCharge", "ElementMass", "ElementSpace", "ElementTime", "Tooltip")
PALETTE_TOL = 1e-4


def rel(p):
    return os.path.relpath(p, REPO).replace(os.sep, "/")


def read(path):
    try:
        with open(path, encoding="utf-8", errors="ignore") as fh:
            return fh.read()
    except OSError:
        return ""


# ----------------------------------------------------------------------------- the project: guids, scripts, scenes
_GUIDS = None
_REFS = None


def guid_map():
    """guid -> repo path, for every .meta under Assets (and Packages that live in the repo)."""
    global _GUIDS
    if _GUIDS is None:
        _GUIDS = {}
        for root in ("Assets", "Packages"):
            for dp, _, files in os.walk(os.path.join(REPO, root)):
                for f in files:
                    if f.endswith(".meta"):
                        head = read(os.path.join(dp, f))[:300]
                        m = re.search(r"^guid: ([0-9a-f]{32})", head, re.M)
                        if m:
                            _GUIDS[m.group(1)] = rel(os.path.join(dp, f[:-5]))
    return _GUIDS


_REACH = None
GRAPH_EXT = YAML_EXT + (".overrideController", ".playable", ".signal", ".preset", ".shadergraph", ".shadersubgraph",
                        ".vfx", ".spriteatlas", ".spriteatlasv2", ".terrainlayer", ".renderTexture", ".cubemap",
                        ".flare", ".guiskin", ".fontsettings", ".physicMaterial", ".physicsMaterial2D")


def reachable():
    """Every asset path a player build can load: the enabled scenes, every asset under a Resources folder (outside
    Editor), every guid ProjectSettings names (the render pipeline, preloaded assets, always-included shaders) and
    the Addressables groups, closed over every `guid:` reference in text assets."""
    global _REACH
    if _REACH is not None:
        return _REACH
    gm = guid_map()
    edges = {}
    for root in ("Assets", "ProjectSettings"):
        for dp, _, files in os.walk(os.path.join(REPO, root)):
            for f in files:
                if f.endswith(GRAPH_EXT):
                    path = os.path.join(dp, f)
                    edges[rel(path)] = set(GUID_REF_RE.findall(read(path)))
    todo = list(build_scenes())
    for p_ in list(gm.values()):
        if "/Resources/" in p_ and "/Editor/" not in p_:
            todo.append(p_)
        if p_.startswith("Assets/AddressableAssetsData/"):
            todo.append(p_)
    for path in edges:
        if path.startswith("ProjectSettings/"):
            todo.append(path)
    todo.append("Assets/DefaultVolumeProfile.asset")
    seen = set()
    while todo:
        cur = todo.pop()
        if cur in seen:
            continue
        seen.add(cur)
        for g in edges.get(cur, ()):
            nxt = gm.get(g)
            if nxt and nxt not in seen:
                todo.append(nxt)
    _REACH = seen
    return _REACH


def referenced_guids():
    """guid -> number of YAML assets under Assets that reference it (a material nothing references never draws)."""
    global _REFS
    if _REFS is None:
        _REFS = Counter()
        for root in ("Assets", "ProjectSettings"):        # ProjectSettings: preloaded assets, always-included shaders
            for dp, _, files in os.walk(os.path.join(REPO, root)):
                for f in files:
                    if f.endswith(YAML_EXT + (".overrideController", ".playable", ".signal", ".preset")):
                        for g in set(GUID_REF_RE.findall(read(os.path.join(dp, f)))):
                            _REFS[g] += 1
    return _REFS


def script_name(guid):
    if guid in UNITY_SCRIPTS:
        return UNITY_SCRIPTS[guid]
    p = guid_map().get(guid)
    return os.path.splitext(os.path.basename(p))[0] if p else "script:" + guid[:8]


_SCENES = None


def build_scenes():
    """Scene paths the player build ships (EditorBuildSettings, enabled: 1)."""
    global _SCENES
    if _SCENES is None:
        _SCENES = set(re.findall(r"- enabled: 1\n\s+path: (\S.*\.unity)", read(BUILD_SETTINGS)))
    return _SCENES


# ----------------------------------------------------------------------------- shaders: which properties are colours
_SHADERS = None


def shader_decls():
    """shader guid -> {"name", "colors", "vectors", "declared"} for every first-party shader (and known package ones)."""
    global _SHADERS
    if _SHADERS is not None:
        return _SHADERS
    _SHADERS = dict(PACKAGE_SHADERS)
    for g, p in guid_map().items():
        if p.endswith(".shader"):
            text = read(os.path.join(REPO, p))
            name = re.search(r'Shader\s+"([^"]+)"', text)
            block = re.search(r"Properties\s*\{(.*?)\n\s*\}", text, re.S)
            colors, vectors, declared = set(), set(), set()
            for m in re.finditer(r"(\w+)\s*\(\s*\"[^\"]*\"\s*,\s*(\w+)", block.group(1) if block else ""):
                declared.add(m.group(1))
                kind = m.group(2).lower()                  # ShaderLab type names are case-insensitive
                if kind == "color":
                    colors.add(m.group(1))
                elif kind == "vector":
                    vectors.add(m.group(1))
            _SHADERS[g] = {"name": name.group(1) if name else os.path.basename(p), "colors": colors,
                           "vectors": vectors, "declared": declared}
        elif p.endswith(".shadergraph"):
            text = read(os.path.join(REPO, p))
            colors, vectors, declared = set(), set(), set()
            for chunk in re.split(r"\n(?=\{)", text):
                try:
                    o = json.loads(chunk)
                except ValueError:
                    continue
                t = o.get("m_Type", "")
                if t.endswith("ShaderProperty"):
                    ref = o.get("m_OverrideReferenceName") or o.get("m_DefaultReferenceName") or ""
                    if not ref:
                        continue
                    declared.add(ref)
                    if t.endswith("ColorShaderProperty"):
                        colors.add(ref)
                    elif "Vector" in t:
                        vectors.add(ref)
            _SHADERS[g] = {"name": "Shader Graphs/" + os.path.splitext(os.path.basename(p))[0], "colors": colors,
                           "vectors": vectors, "declared": declared}
    return _SHADERS


def is_ui_shader(name):
    return any(name.startswith(x) for x in UI_SHADER_PREFIXES)


# ----------------------------------------------------------------------------- colour: family, 8-bit ids
_TRUTH = None


def truth():
    global _TRUTH
    if _TRUTH is None:
        _TRUTH = colour_domains.load()
    return _TRUTH


def unit(rgba):
    """(rgb with brightest channel at most 1, the brightest channel when it was above 1, else 1)."""
    r, g, b = (max(0.0, v) for v in rgba[:3])
    peak = max(r, g, b)
    if peak > 1:
        return (r / peak, g / peak, b / peak), peak
    return (r, g, b), 1.0


def family_of(rgba):
    """(family or 'band', nearest family, grey?) of an authored colour as the screen shows it: HDR is taken at
    brightest channel 1, the channels are read as sRGB (how the game's anchors are read)."""
    rgb, _ = unit(rgba)
    rgb = tuple(min(1.0, v) for v in rgb)
    fam, top, _ = colour_domains.classify(truth(), rgb)
    L, a, bb = cs.oklab(rgb)
    return fam or "band", top, math.hypot(a, bb) < NEUTRAL


def f32(v):
    return struct.unpack("f", struct.pack("f", v))[0]


def byte(v):
    """One channel as ColorTrace packs it: Mathf.RoundToInt(v * 255f) in float32, clamped (round half to even)."""
    return max(0, min(255, round(f32(f32(v) * 255.0))))


def hex_of(rgb):
    return "%02X%02X%02X" % tuple(byte(v) for v in rgb[:3])


def value_id(role, rgba):
    """The id a colour is found by on screen: role:RRGGBB, with xN.NN (its brightest channel, in hundredths rounded
    half up) when it is HDR. ColorTrace.ValueId computes the same string from the same float32 values."""
    r, g, b = (max(0.0, f32(v)) for v in rgba[:3])
    peak = max(r, g, b)
    if peak <= 1:
        return "%s:%s" % (role, hex_of((r, g, b)))
    n = math.floor(peak * 100.0 + 0.5)
    return "%s:%sx%d.%02d" % (role, hex_of(tuple(f32(v / peak) for v in (r, g, b))), n // 100, n % 100)


def same(a, b, tol):
    return all(abs(x - y) <= tol for x, y in zip(a[:3], b[:3]))


# ----------------------------------------------------------------------------- YAML assets
class Doc:
    __slots__ = ("cls", "fid", "stripped", "lines", "typ", "flat", "script", "go", "body")

    def __init__(self, cls, fid, stripped):
        self.cls, self.fid, self.stripped, self.lines = cls, fid, stripped, []
        self.typ, self.flat, self.script, self.go, self.body = None, {}, None, None, ""


def yaml_docs(text):
    text = re.sub(r",\r?\n\s+", ", ", text)          # Unity folds long colour lines when it saves
    docs, cur = [], None
    for line in text.splitlines():
        m = DOC_RE.match(line)
        if m:
            cur = Doc(m.group(1), m.group(2), bool(m.group(3)))
            docs.append(cur)
        elif cur is not None:
            cur.lines.append(line)
    for d in docs:
        d.body = "\n".join(d.lines)
        d.typ = CLASS_NAMES.get(d.cls, "class" + d.cls)
        if d.cls == "114":
            m = SCRIPT_RE.search(d.body)
            d.script = m.group(1) if m else None
            d.typ = script_name(d.script) if d.script else "MonoBehaviour"
        go = GO_RE.search(d.body)
        d.go = go.group(1) if go else None
        stack = []
        for ln in d.lines:
            km = KEY_RE.match(ln)
            if km:
                ind = len(km.group(1))
                while stack and stack[-1][0] >= ind:
                    stack.pop()
                sm = SCALAR_RE.match(ln)
                if sm:
                    d.flat[".".join([k for _, k in stack] + [sm.group(2)])] = sm.group(3).strip()
                stack.append((ind, km.group(2)))
    return docs


_PREFABS = {}


def prefab_docs(path):
    """{fileID: Doc} of a prefab (cached), for resolving what an override targets."""
    if path not in _PREFABS:
        _PREFABS[path] = {d.fid: d for d in yaml_docs(read(os.path.join(REPO, path)))} if path.endswith(".prefab") else {}
    return _PREFABS[path]


MASK63 = 0x7FFFFFFFFFFFFFFF


def resolve_target(fid, guid, depth=0):
    """The Doc an override's target names: a document of the prefab, a stripped placeholder followed into the prefab
    it came from, or an object a variant or nested instance inherits, whose fileID is the instance's XOR the source
    object's (Unity's PrefabUtility rule, masked to 63 bits)."""
    path = guid_map().get(guid)
    if not path or depth > 8:
        return None, path
    docs = prefab_docs(path)
    d = docs.get(fid)
    if d is None:
        want = int(fid) & MASK63
        d = next((x for k, x in docs.items() if int(k) & MASK63 == want), None)
    if d is None:
        for inst in docs.values():
            if inst.cls != "1001":
                continue
            m = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})", inst.body)
            if not m:
                continue
            x = ((int(fid) & 0xFFFFFFFFFFFFFFFF) ^ (int(inst.fid) & 0xFFFFFFFFFFFFFFFF)) & MASK63
            found, src = resolve_target(str(x), m.group(1), depth + 1)
            if found is not None:
                return found, src
        return None, path
    if d.stripped:
        m = re.search(r"m_CorrespondingSourceObject: \{fileID: (-?\d+), guid: ([0-9a-f]{32})", d.body)
        if m:
            return resolve_target(m.group(1), m.group(2), depth + 1)
    return d, path


def record(out, **kw):
    fam, near, neutral = family_of(kw["rgba"])
    kw.update(family=fam, near=near, neutral=neutral)
    out.append(kw)


def parse_yaml(path, out):
    text = read(path)
    if not text:
        return
    docs = yaml_docs(text)
    p = rel(path)
    ctx = {"file": p, "names": {}, "by_go": defaultdict(list)}
    for d in docs:
        if d.cls == "1":
            for ln in d.lines:
                m = NAME_RE.match(ln)
                if m:
                    ctx["names"][d.fid] = m.group(1).strip()
                    break
        if d.go:
            ctx["by_go"][d.go].append(d)
    for d in docs:
        if d.cls == "1001":
            parse_overrides(ctx, d, out)
            continue
        if d.stripped:
            continue
        is_material = d.cls == "21"                     # a .mat, or a material inside a font asset or prefab
        mat_name = (re.search(r"^  m_Name: (.*)$", d.body, re.M) or [None, os.path.basename(p)])[1]
        obj = ctx["names"].get(d.go, "") if d.go else (mat_name if is_material else "")
        typ = d.typ
        shader = None
        if is_material:
            sm = SHADER_RE.search(d.body)
            shader = sm.group(2) if sm else None
            typ = "Material(%s)" % shader_decls().get(shader, {}).get("name", "?")
        stack, listed = [], defaultdict(int)
        for ln in d.lines:
            m = COLOR_RE.match(ln)
            km = KEY_RE.match(ln)
            lm = LIST_COLOR_RE.match(ln) if not m and not km else None
            if lm and stack:
                parent = ".".join(k for _, k in stack)
                i = listed[parent]
                listed[parent] += 1
                record(out, source="material" if is_material else "component", file=p, object=obj, component=typ,
                       field="%s[%d]" % (parent, i), rgba=[float(lm.group(k)) for k in range(2, 6)],
                       _doc=d, _ctx=ctx, _shader=shader)
                continue
            if km:
                ind = len(km.group(1))
                while stack and stack[-1][0] >= ind:
                    stack.pop()
            if m:
                key = m.group(2)
                if not key.endswith("32"):
                    field = ".".join([k for _, k in stack] + [key])
                    rgba = [float(m.group(i)) for i in range(3, 7)]
                    record(out, source="material" if is_material else ("renderSettings" if d.cls == "104" else "component"),
                           file=p, object=obj, component=typ, field=field, rgba=rgba,
                           _doc=d, _ctx=ctx, _shader=shader)
                stack.append((len(m.group(1)), key))
                continue
            r32 = RGBA32_RE.match(ln)
            if r32 and stack:
                parent = stack[-1][1]
                if not (parent.endswith("32") and parent.startswith("m_fontColor")):   # TMP's twin of m_fontColor
                    v = int(r32.group(2))
                    rgba = [(v & 0xFF) / 255, ((v >> 8) & 0xFF) / 255, ((v >> 16) & 0xFF) / 255, ((v >> 24) & 0xFF) / 255]
                    record(out, source="component", file=p, object=obj, component=typ,
                           field=".".join(k for _, k in stack), rgba=rgba, _doc=d, _ctx=ctx, _shader=None)
                continue
            if km:
                stack.append((len(km.group(1)), km.group(2)))


def parse_overrides(ctx, inst, out):
    """A prefab instance's colour overrides: Unity stores each channel as its own modification."""
    mods, cur = [], {}
    for ln in inst.lines:
        s = ln.strip()
        if s.startswith("- target:"):
            if cur:
                mods.append(cur)
            cur = {"target": s}
        elif s.startswith("propertyPath:"):
            cur["path"] = s.split(":", 1)[1].strip()
        elif s.startswith("value:"):
            cur["value"] = s.split(":", 1)[1].strip()
    if cur:
        mods.append(cur)
    by_target = defaultdict(dict)
    for m in mods:
        by_target[m["target"]][m.get("path", "")] = m.get("value", "")
    for target, props in by_target.items():
        tm = re.search(r"fileID: (-?\d+), guid: ([0-9a-f]{32})", target)
        if not tm:
            continue
        tdoc, src = resolve_target(tm.group(1), tm.group(2))
        groups = defaultdict(dict)
        for prop, val in props.items():
            mm = re.match(r"(.+)\.([rgba])$", prop)
            if mm:
                try:
                    groups[mm.group(1)][mm.group(2)] = float(val)
                except ValueError:
                    pass
        for prop, ch in groups.items():
            if not ({"r", "g", "b"} & set(ch)):
                continue                                # alpha only: the colour is the prefab's
            top = tdoc.typ if tdoc else "?"
            head = "MonoBehaviour" if tdoc is not None and tdoc.cls == "114" else top
            base = None
            if tdoc is not None:
                want = prop.split(".")[-1]
                for ln in tdoc.lines:                   # the prefab's own value fills channels left alone
                    cm = COLOR_RE.match(ln)
                    if cm and cm.group(2) == want:
                        base = [float(cm.group(i)) for i in range(3, 7)]
                        break
            base = base or [1.0, 1.0, 1.0, 1.0]
            rgba = [ch.get(k, base[i]) for i, k in enumerate("rgba")]
            flat = dict(tdoc.flat) if tdoc is not None else {}
            for pp, vv in props.items():                # the instance's other overrides of the same component
                flat[head + "." + pp] = vv
            shim = Doc(tdoc.cls if tdoc else "?", tdoc.fid if tdoc else "0", False)
            shim.typ, shim.flat, shim.script, shim.go = top, flat, tdoc.script if tdoc else None, None
            record(out, source="override", file=ctx["file"], object=os.path.basename(src or "?"), component=top,
                   field=head + "." + prop, rgba=rgba, _doc=shim, _ctx=ctx, _shader=None, _src=src)


# ----------------------------------------------------------------------------- shaders
def graph_live_nodes(objs):
    """Node ids with an edge path to a Block node or a sub-graph's output: the only nodes whose values are drawn."""
    edges = defaultdict(set)                            # input node -> nodes feeding it
    for o in objs:
        for e in o.get("m_Edges", []) or []:
            try:
                edges[e["m_InputSlot"]["m_Node"]["m_Id"]].add(e["m_OutputSlot"]["m_Node"]["m_Id"])
            except (KeyError, TypeError):
                pass
    todo = [o.get("m_ObjectId") for o in objs
            if o.get("m_Type", "").endswith(("BlockNode", "SubGraphOutputNode"))]
    live = set()
    while todo:
        n = todo.pop()
        if n in live:
            continue
        live.add(n)
        todo.extend(edges.get(n, ()))
    return live


def parse_shadergraph(path, out):
    text = read(path)
    objs = []
    for chunk in re.split(r"\n(?=\{)", text):
        try:
            objs.append(json.loads(chunk))
        except ValueError:
            continue
    live = graph_live_nodes(objs)
    for o in objs:
        t = o.get("m_Type", "")
        if t.endswith("ColorShaderProperty"):
            v = o.get("m_Value") or {}
            if all(k in v for k in "rgba"):
                record(out, source="shadergraph", file=rel(path), object=o.get("m_Name", ""), component="ColorProperty",
                       field=o.get("m_OverrideReferenceName") or o.get("m_DefaultReferenceName", o.get("m_Name", "")),
                       rgba=[v["r"], v["g"], v["b"], v["a"]])
        elif t.endswith("ColorNode"):
            c = (o.get("m_Color") or {}).get("color") or {}
            if all(k in c for k in "rgba"):
                record(out, source="shadergraph", file=rel(path), object=o.get("m_Name", ""), component="ColorNode",
                       field=o.get("m_ObjectId", ""), rgba=[c["r"], c["g"], c["b"], c["a"]],
                       _live=o.get("m_ObjectId") in live)


SHADER_PROP_RE = re.compile(r"(\w+)\s*\(\s*\"[^\"]*\"\s*,\s*Color\s*\)\s*=\s*\(\s*%s\s*,\s*%s\s*,\s*%s\s*,\s*%s\s*\)"
                            % (NUM, NUM, NUM, NUM))
HLSL_CONST_RE = re.compile(r"\b(?:half|float|fixed)([34])\s*\(\s*%s\s*,\s*%s\s*,\s*%s\s*(?:,\s*%s\s*)?\)" % (NUM, NUM, NUM, NUM))
HLSL_COLOUR_WORDS = re.compile(r"col|tint|glow|rgb|albedo|emiss|fog|sky|star|hue|shade|light|dark|bright|blue|red|"
                               r"white|yellow|green|orange|purple|violet|cyan|magenta|pink|gold|amber|ember|cream", re.I)
LUMA = ({0.299, 0.587, 0.114}, {0.2126, 0.7152, 0.0722}, {0.3, 0.59, 0.11}, {0.22, 0.707, 0.071})


def parse_shader(path, out):
    text = read(path)
    p = rel(path)
    for m in SHADER_PROP_RE.finditer(text):
        record(out, source="shader", file=p, object=os.path.basename(path), component="Properties",
               field=m.group(1), rgba=[float(m.group(i)) for i in range(2, 6)])
    # constants in the code that look like colours: a 3- or 4-vector, every component in 0..1 and not one value
    # repeated, assigned to or combined with something named like a colour
    for i, ln in enumerate(text.splitlines(), 1):
        s = ln.strip()
        if s.startswith("//") or re.match(r"\w+\s*\(\s*\"", s):
            continue
        for m in HLSL_CONST_RE.finditer(ln):
            vals = [float(x) for x in m.groups()[1:] if x is not None]
            if not all(0.0 <= v <= 1.0 for v in vals[:3]) or len(set(vals[:3])) == 1:
                continue
            if set(round(v, 4) for v in vals[:3]) in LUMA:
                continue                                # luminance weights, not a colour
            before = ln[:m.start()]
            if re.search(r"\b(?:dot|normalize|cross|reflect)\s*\([^()]*$", before):
                continue                                # a direction or a weight inside dot/normalize
            if not HLSL_COLOUR_WORDS.search(before):
                continue
            record(out, source="hlsl", file=p, object=os.path.basename(path), component="constant",
                   field=s[:160], rgba=(vals[:3] + [vals[3] if len(vals) > 3 else 1.0]), line=i)


# ----------------------------------------------------------------------------- C#
NAMED = {"red": (1, 0, 0), "green": (0, 1, 0), "blue": (0, 0, 1), "white": (1, 1, 1), "black": (0, 0, 0),
         "yellow": (1, 0.92, 0.016), "cyan": (0, 1, 1), "magenta": (1, 0, 1), "gray": (0.5, 0.5, 0.5),
         "grey": (0.5, 0.5, 0.5), "clear": (0, 0, 0)}
CS_NUM = r"\s*(-?(?:0[xX][0-9A-Fa-f]+|[\d.]+[fFdD]?(?:\s*/\s*[\d.]+[fFdD]?)?))\s*"
CS_ALPHA = r"\s*([^,()]+?)\s*"                    # the alpha may be an expression (0.35f * lit): read as 1 when it is not a number
ARGS = CS_NUM + "," + CS_NUM + "," + CS_NUM + "(?:," + CS_ALPHA + ")?\\)"
CS_COLOR_RE = re.compile(
    r"new\s+(Color32|Color)\s*\(" + ARGS +
    r"|\bColor\.(red|green|blue|white|black|yellow|cyan|magenta|gray|grey|clear)\b"
    r"|(?<=[\"=])#([0-9A-Fa-f]{6})([0-9A-Fa-f]{2})?(?=[\">])")
# a target-typed new(...) whose declared type is Color or Color32, the first declarator and any after it:
# `Color x = new(...)`, `static readonly Color Backdrop = new(0.043f, ...)`, `Color hd = new(...), hb = new(...)`
CS_TYPED_RE = re.compile(r"\b(Color32|Color)\s+(\w+)\s*(?:=|=>)\s*(new\s*\(" + ARGS + ")")
CS_MORE_RE = re.compile(r"\s*,\s*(\w+)\s*=\s*(new\s*\(" + ARGS + ")")
CS_DEBUG = re.compile(r"Debug\.Draw|Gizmos\.|CSDebug|\bLog(?:Warning|Error|Verbose)?\(|OnDrawGizmos|Handles\.")
TRACE_CALL = re.compile(r"ColorTrace\.(?:Retrace|Get|TryRetrace)\s*\(")
DECL_RE = re.compile(r"(\w+)\s*(?:=|=>)\s*$")
REVIEW = os.path.join(REPO, "Docs", "VesselModels", "colour_trace_review.json")


def cs_number(x):
    """A C# numeric literal (hex, f/d suffix, or one literal divided by another); None when it is an expression."""
    x = x.strip()
    if "/" in x:
        a, b = (cs_number(v) for v in x.split("/", 1))
        return a / b if a is not None and b else None
    try:
        return float(int(x, 16)) if x.lower().startswith(("0x", "-0x")) else float(x.rstrip("fFdD"))
    except ValueError:
        return None


def cs_value(groups, kind):
    vals = [cs_number(x) for x in groups if x is not None]
    if len(vals) > 3 and vals[3] is None:
        vals[3] = 255.0 if kind == "Color32" else 1.0       # an alpha expression: the colour is its rgb
    vals = [v if v is not None else 0.0 for v in vals]
    if kind == "Color32":
        vals = [v / 255 for v in vals]
    return vals + [1.0] * (4 - len(vals))


def strip_comments(text):
    """C# with its comments blanked (newlines kept, so offsets and line numbers survive); strings are kept."""
    out, i, n, quote = [], 0, len(text), None
    while i < n:
        c = text[i]
        if quote:
            out.append(c)
            if c == "\\" and i + 1 < n:
                out.append(text[i + 1])
                i += 2
                continue
            if c == quote or (c == "\n" and quote == "'"):
                quote = None
            i += 1
            continue
        if c in "\"'":
            quote = c
            out.append(c)
            i += 1
            continue
        if text.startswith("//", i):
            j = text.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
            continue
        if text.startswith("/*", i):
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", text[i:j]))
            i = j
            continue
        out.append(c)
        i += 1
    return "".join(out)


_REVIEW = None


def code_review():
    """{(file, literal without spaces): [(line, bucket, why)]}: the compiler-checked reading of every C# colour literal
    (Docs/VesselModels/colour_trace_review.json): INERT (never shows), FALLBACK (shows only when a reference is
    unwired), EXCLUDED (editor, development build, gizmo, debug), TRACED (another path paints it from the set)."""
    global _REVIEW
    if _REVIEW is None:
        _REVIEW = defaultdict(list)
        try:
            with open(REVIEW, encoding="utf-8") as fh:
                for e in json.load(fh)["literals"]:
                    _REVIEW[(e["file"], re.sub(r"\s+", "", e["literal"]))].append((e["line"], e["bucket"], e["why"]))
        except (OSError, ValueError, KeyError):
            pass
    return _REVIEW


_FIELD_REVIEW = None


def field_review():
    """{(asset, field path without the MonoBehaviour. head): (bucket, why)} from colour_trace_review.json `fields`."""
    global _FIELD_REVIEW
    if _FIELD_REVIEW is None:
        _FIELD_REVIEW = {}
        try:
            with open(REVIEW, encoding="utf-8") as fh:
                for e in json.load(fh).get("fields", []):
                    _FIELD_REVIEW[(e["asset"], e["field"])] = (e["bucket"], e["why"])
        except (OSError, ValueError, KeyError):
            pass
    return _FIELD_REVIEW


def reviewed(path, literal, line):
    best = None
    for ln, bucket, why in code_review().get((path, re.sub(r"\s+", "", literal)), []):
        if abs(ln - line) <= 60 and (best is None or abs(ln - line) < abs(best[0] - line)):
            best = (ln, bucket, why)
    return best


def inside_trace(calls, text, pos):
    """True when `pos` is inside the argument list of a ColorTrace call that opens before it."""
    for m in calls:
        if m.end() > pos:
            break
        if pos - m.end() > 4000:
            continue
        depth = 1
        for ch in text[m.end():pos]:
            depth += 1 if ch == "(" else -1 if ch == ")" else 0
            if depth <= 0:
                break
        if depth >= 1:
            return True
    return False


def parse_csharp(path, out):
    p = rel(path)
    if "/Editor/" in p or "/Tests/" in p or p.endswith("Tests.cs"):
        return
    raw = read(path)
    text = strip_comments(raw)
    lines = raw.splitlines()
    calls = list(TRACE_CALL.finditer(text))
    hits = []                                            # (offset, literal, rgba, declared name)
    for m in CS_COLOR_RE.finditer(text):
        if m.group(1):
            hits.append((m.start(), m.group(0), cs_value(m.groups()[1:5], m.group(1)), None))
        elif m.group(6):
            hits.append((m.start(), m.group(0), list(NAMED[m.group(6)]) + [0.0 if m.group(6) == "clear" else 1.0], None))
        elif m.group(7):
            h = m.group(7)
            hits.append((m.start(), m.group(0), [int(h[k:k + 2], 16) / 255 for k in (0, 2, 4)] +
                         [int(m.group(8), 16) / 255 if m.group(8) else 1.0], None))
    for m in CS_TYPED_RE.finditer(text):
        hits.append((m.start(3), m.group(3), cs_value(m.groups()[3:7], m.group(1)), m.group(2)))
        tail = m.end()
        while True:                                     # `, hb = new(...)` declarators after the first
            mm = CS_MORE_RE.match(text, tail)
            if not mm:
                break
            hits.append((mm.start(2), mm.group(2), cs_value(mm.groups()[2:6], m.group(1)), mm.group(1)))
            tail = mm.end()
    seen = set()
    for pos, literal, rgba, name in sorted(hits, key=lambda h: h[0]):
        if pos in seen:
            continue
        seen.add(pos)
        line = text.count("\n", 0, pos) + 1
        src = lines[line - 1].strip() if line - 1 < len(lines) else ""
        if name is None and not literal.startswith("#"):
            dm = DECL_RE.search(text[max(0, pos - 160):pos])
            name = dm.group(1) if dm else None
        wrapped = inside_trace(calls, text, pos)
        record(out, source="csharp", file=p, object=os.path.basename(path),
               component="debug" if CS_DEBUG.search(src) else "code", field=src[:160], rgba=rgba, line=line,
               _wrapped=wrapped, _literal=literal, _name=name)


# ----------------------------------------------------------------------------- the walk
def scan():
    out = []
    for top in FIRST_PARTY:
        base = os.path.join(REPO, top)
        if os.path.isfile(base):
            parse_yaml(base, out)
            continue
        if not os.path.isdir(base):
            continue
        for dp, dirs, files in os.walk(base):
            dirs.sort()
            if rel(dp).startswith(PALETTES):
                continue
            for f in sorted(files):
                path = os.path.join(dp, f)
                if f.endswith(YAML_EXT):
                    parse_yaml(path, out)
                elif f.endswith((".shadergraph", ".shadersubgraph")):
                    parse_shadergraph(path, out)
                elif f.endswith((".shader", ".hlsl", ".cginc")):
                    parse_shader(path, out)
                elif f.endswith(".cs"):
                    parse_csharp(path, out)
    classify_all(out)
    return out


# ----------------------------------------------------------------------------- the rules
INERT = {
    "scene-not-in-build": "a colour authored in a scene the player build does not ship (EditorBuildSettings)",
    "tmp-colour-twin": "TextMeshPro draws m_fontColor; its m_Color is the Graphic field TMP_Text overrides and never reads",
    "tmp-face-cache": "m_faceColor and m_outlineColor cache the font material's colours; TMP draws the material's",
    "tmp-gradient-off": "m_fontColorGradient is drawn only with m_enableVertexGradient on (or a gradient preset)",
    "kit-selectable": "a Kit selectable paints its own graphics; KitSelectableBase.Awake sets its transition to None",
    "kit-legacy-field": "a colour field a Kit subclass inherits and never reads (it overrides the base hook)",
    "selectable-no-tint": "a Selectable's ColorBlock tints its target only with the ColorTint transition and a target",
    "mask-hidden": "a Mask with showMaskGraphic off draws its graphic into the stencil only",
    "ps-module-off": "a particle module's colours are drawn only while the module is enabled",
    "ps-mode-unused": "a MinMaxGradient draws only the parts its minMaxState names",
    "gradient-key-unused": "a gradient draws only its first m_NumColorKeys colour keys",
    "ps-custom-not-colour": "custom particle data is a colour only in mode 2",
    "material-vector": "the material's shader declares this property as a vector, not a colour",
    "material-stale": "the material's shader does not declare this property (left from an earlier shader)",
    "material-unused-prop": "the property is declared for compatibility and the shader never reads it (URP's _Color)",
    "camera-no-clear": "a camera's background colour is drawn only when it clears to a solid colour",
    "fog-off": "the fog colour is drawn only with fog on (no runtime code turns it on)",
    "ambient-mode": "the scene's ambient mode does not read this ambient colour (or adds black)",
    "subtractive-off": "the subtractive shadow colour is read only by Mixed lights in Subtractive mode (no scene has one)",
    "gi-output": "an output of the GI bake that Unity serializes, read by nothing",
    "post-inactive": "a volume component that is not active, or a parameter whose override is off",
    "post-identity": "a post-processing parameter at its identity (white tint or filter, zero intensity, grey toning)",
    "shader-default": "a shader default every material saves over; no code or asset builds a material from the shader",
    "debug": "logs, gizmos, debug draws, editor-only and development-build-only code and diagnostic overlays (CLAUDE.md "
             "excludes them from player-facing rules)",
    "code-inert": "a C# colour literal that never reaches a pixel (a serialized default every instance overrides, an "
                  "alpha-0 sentinel, a value overwritten before it draws): colour_trace_review.json says which and why",
    "code-fallback": "a C# colour literal shown only when a reference is unwired, which no shipped scene leaves "
                     "unwired: colour_trace_review.json says which and why",
    "light-off": "a disabled light",
    "stale-field": "a serialized key the component's class no longer declares (nor names in FormerlySerializedAs): "
                   "Unity never reads it",
    "field-reviewed": "a serialized colour field that never reaches a pixel or is shown only when a reference is unwired: "
                      "colour_trace_review.json says which and why",
    "shader-node-dead": "a ShaderGraph colour node with no edge path to a Block node or a sub-graph's output: never drawn",
    "shader-not-drawn": "shader code no reachable material, asset or Shader.Find uses (directly or as a sub-graph or "
                        "include): compiled at most, never drawn",
    "unreachable": "an asset no player build can load: no enabled scene, Resources folder, project setting or "
                   "Addressables group reaches it through any chain of references (colour_trace.py reachable)",
    "script-missing": "a component whose script is not in the repository (deleted): it never runs",
    "tmp-feature-off": "a TextMesh Pro material colour whose feature is off (no GLOW_ON, UNDERLAY_ON or BEVEL_ON keyword, "
                       "outline width 0)",
}

PS_PARTS = {"0": {"maxColor"}, "1": {"maxGradient"}, "2": {"minColor", "maxColor"}, "3": {"minGradient", "maxGradient"},
            "4": {"maxGradient"}}
PS_MODULES = ("ColorModule", "ColorBySpeedModule", "TrailModule", "CustomDataModule")


def flat_get(doc, *paths):
    for p in paths:
        if p in doc.flat:
            return doc.flat[p]
    return None


def gradient_rule(parts, doc):
    m = re.match(r"key(\d)$", parts[-1])
    if m:
        n = flat_get(doc, ".".join(parts[:-1] + ["m_NumColorKeys"]))
        if n is not None and int(m.group(1)) >= int(n):
            return "gradient-key-unused"
    return None


def ps_rule(r, doc):
    parts = r["field"].split(".")
    for mod in PS_MODULES:
        if mod in parts and flat_get(doc, "ParticleSystem.%s.enabled" % mod) == "0":
            return "ps-module-off"
    if "CustomDataModule" in parts:
        k = "0" if "color0" in parts else "1" if "color1" in parts else None
        if k and flat_get(doc, "ParticleSystem.CustomDataModule.mode" + k) != "2":
            return "ps-custom-not-colour"
    for i, part in enumerate(parts):
        if part in ("minColor", "maxColor", "minGradient", "maxGradient"):
            state = flat_get(doc, ".".join(parts[:i] + ["minMaxState"]))
            if state in PS_PARTS and part not in PS_PARTS[state]:
                return "ps-mode-unused"
            break
    return gradient_rule(parts, doc)


def scene_of(r):
    return r["file"] if r["file"].endswith(".unity") else None


_ASSET_GUIDS = {}


def asset_guid(path):
    if path not in _ASSET_GUIDS:
        m = re.search(r"^guid: ([0-9a-f]{32})", read(os.path.join(REPO, path + ".meta"))[:300], re.M)
        _ASSET_GUIDS[path] = m.group(1) if m else None
    return _ASSET_GUIDS[path]


def post_rule(r, doc):
    if not r["file"].endswith(".asset"):
        return None
    if flat_get(doc, "MonoBehaviour.active") == "0":
        return "post-inactive"
    parts = r["field"].split(".")
    if len(parts) >= 3 and parts[-1] == "m_Value":
        if flat_get(doc, ".".join(parts[:-1] + ["m_OverrideState"])) == "0":
            return "post-inactive"
        param, rgb = parts[-2], r["rgba"][:3]
        if param in ("tint", "colorFilter") and all(abs(v - 1) < 1e-6 for v in rgb):
            return "post-identity"
        if param in ("shadows", "highlights") and r["component"] == "SplitToning" and all(abs(v - 0.5) < 1e-6 for v in rgb):
            return "post-identity"
        if param in ("color", "tintColor"):
            inten = flat_get(doc, "MonoBehaviour.intensity.m_Value")
            on = flat_get(doc, "MonoBehaviour.intensity.m_OverrideState")
            if on == "0" or (inten is not None and float(inten) == 0.0):
                return "post-identity"
    if unreachable(r["file"]):
        return "unreachable"
    return None


def unreachable(path):
    """An asset no player build can load (reachable() never reaches it): nothing it holds reaches a pixel."""
    return (path.endswith((".prefab", ".asset", ".mat", ".controller", ".anim")) and
            os.path.exists(os.path.join(REPO, path)) and path not in reachable())


def rule_component(r):
    doc, ctx = r["_doc"], r["_ctx"]
    typ, f = r["component"], r["field"]
    last = f.split(".")[-1]
    scene = scene_of(r)
    if scene and scene not in build_scenes():
        return "scene-not-in-build"
    if unreachable(r["file"]):
        return "unreachable"
    if typ.startswith("script:"):
        return "script-missing"
    if stale_field(r):
        return "stale-field"
    if typ in ("TextMeshProUGUI", "TextMeshPro"):
        if last == "m_Color":
            return "tmp-colour-twin"
        if last in ("m_faceColor", "m_outlineColor") or ".m_faceColor" in f or ".m_outlineColor" in f:
            return "tmp-face-cache"
        if "m_fontColorGradient" in f:
            preset = flat_get(doc, "MonoBehaviour.m_fontColorGradientPreset") or "{fileID: 0}"
            if flat_get(doc, "MonoBehaviour.m_enableVertexGradient") != "1" and "fileID: 0}" in preset:
                return "tmp-gradient-off"
    if ".m_Colors." in f:
        if typ in KIT_SELECTABLES:
            return "kit-selectable"
        if typ in SELECTABLES or flat_get(doc, "MonoBehaviour.m_Transition") is not None:
            if flat_get(doc, "MonoBehaviour.m_Transition") != "1":
                return "selectable-no-tint"
            if (flat_get(doc, "MonoBehaviour.m_TargetGraphic") or "{fileID: 0}") == "{fileID: 0}":
                return "selectable-no-tint"
    if typ in KIT_LEGACY and last in KIT_LEGACY[typ]:
        return "kit-legacy-field"
    if typ == "Image" and last == "m_Color" and doc.go:
        for other in ctx["by_go"].get(doc.go, []):
            if other.typ == "Mask" and flat_get(other, "MonoBehaviour.m_ShowMaskGraphic") == "0":
                return "mask-hidden"
    if typ == "ParticleSystem":
        return ps_rule(r, doc)
    if typ in ("TrailRenderer", "LineRenderer"):
        return gradient_rule(f.split("."), doc)
    if typ == "Camera" and flat_get(doc, "Camera.m_ClearFlags") != "2":
        return "camera-no-clear"
    if typ == "Light" and flat_get(doc, "Light.m_Enabled") == "0":
        return "light-off"
    if typ in POST_TYPES:
        return post_rule(r, doc)
    return None


TMP_FEATURES = {"_GlowColor": ("GLOW_ON",), "_UnderlayColor": ("UNDERLAY_ON", "UNDERLAY_INNER"),
                "_SpecularColor": ("BEVEL_ON",), "_ReflectFaceColor": ("BEVEL_ON",), "_ReflectOutlineColor": ("BEVEL_ON",)}


def rule_material(r):
    decl = shader_decls().get(r.get("_shader"))
    prop = r["field"].split(".")[-1]
    if prop in VECTOR_NAMES or (decl and prop in decl.get("vectors", ())):
        return "material-vector"
    if decl and decl.get("name", "").startswith("TextMeshPro/"):
        body = r["_doc"].body
        if prop in TMP_FEATURES and not any(k in body for k in TMP_FEATURES[prop]):
            return "tmp-feature-off"
        if prop == "_OutlineColor" and float(flat_get(r["_doc"], "Material.m_SavedProperties.m_Floats._OutlineWidth") or
                                             _float_in(body, "_OutlineWidth") or 0) == 0:
            return "tmp-feature-off"
    if decl:
        if prop in decl.get("unused", ()):
            return "material-unused-prop"
        known = decl.get("declared") or (decl.get("colors", set()) | decl.get("vectors", set()))
        if known and prop not in known:
            return "material-stale"
    if unreachable(r["file"]):
        return "unreachable"
    return None


def _float_in(body, name):
    m = re.search(r"- %s: (%s)" % (re.escape(name), NUM), body)
    return m.group(1) if m else None


def rule_render_settings(r):
    doc = r["_doc"]
    last = r["field"].split(".")[-1]
    if scene_of(r) and scene_of(r) not in build_scenes():
        return "scene-not-in-build"
    if last == "m_FogColor" and flat_get(doc, "RenderSettings.m_Fog") != "1":
        return "fog-off"
    mode = flat_get(doc, "RenderSettings.m_AmbientMode") or "0"
    if last in ("m_AmbientEquatorColor", "m_AmbientGroundColor") and mode != "1":
        return "ambient-mode"
    if last == "m_AmbientSkyColor" and (mode not in ("1", "3") or (mode == "3" and max(r["rgba"][:3]) == 0)):
        return "ambient-mode"
    if last == "m_SubtractiveShadowColor":
        return "subtractive-off"
    if last == "m_IndirectSpecularColor":
        return "gi-output"
    return None


_DRAWN = None


def drawn_shaders():
    """Shader sources a player draws: every shader or graph a reachable material uses, a reachable asset names (a
    serialized Shader field) or code finds by name, closed over the sub-graphs and HLSL files they pull in. A graph
    that only Project Settings lists (always-included) is compiled but never drawn without a material."""
    global _DRAWN
    if _DRAWN is not None:
        return _DRAWN
    gm, decls = guid_map(), shader_decls()
    shader_guids = {g for g, p in gm.items() if p.endswith((".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc"))}
    todo = set()
    for path in reachable():
        if path.startswith("ProjectSettings/"):
            continue
        text = read(os.path.join(REPO, path))
        if path.endswith(".mat"):
            m = SHADER_RE.search(text)
            if m:
                todo.add(m.group(2))
        elif path.endswith((".prefab", ".unity", ".asset")):
            todo |= set(GUID_REF_RE.findall(text)) & shader_guids
    found = set()
    for dp, _, files in os.walk(os.path.join(REPO, "Assets", "_Scripts")):
        for f in files:
            if f.endswith(".cs"):
                found.update(re.findall(r'Shader\.Find\(\s*"([^"]+)"', read(os.path.join(dp, f))))
    todo |= {g for g, d in decls.items() if d.get("name") in found}
    drawn = set()
    stack = [gm[g] for g in todo if g in gm]
    while stack:
        path = stack.pop()
        if path in drawn:
            continue
        drawn.add(path)
        text = read(os.path.join(REPO, path))
        for g in set(GUID_REF_RE.findall(text)) & shader_guids:
            stack.append(gm[g])
        for inc in re.findall(r'#include\s+"([^"]+)"', text):
            cand = os.path.normpath(os.path.join(os.path.dirname(path), inc)).replace(os.sep, "/")
            if os.path.exists(os.path.join(REPO, cand)):
                stack.append(cand)
            elif inc.startswith("Assets/") and os.path.exists(os.path.join(REPO, inc)):
                stack.append(inc)
    _DRAWN = drawn
    return _DRAWN


_SHADER_USES = None


def shader_used_live(guid, name):
    """A shader's defaults reach a pixel only when code or a non-material asset builds a material from it."""
    global _SHADER_USES
    if _SHADER_USES is None:
        _SHADER_USES = {"find": set(), "asset": Counter()}
        for dp, _, files in os.walk(os.path.join(REPO, "Assets", "_Scripts")):
            for f in files:
                if f.endswith(".cs"):
                    _SHADER_USES["find"].update(re.findall(r'Shader\.Find\(\s*"([^"]+)"', read(os.path.join(dp, f))))
        for dp, _, files in os.walk(os.path.join(REPO, "Assets")):
            for f in files:
                if f.endswith((".prefab", ".unity", ".asset")):
                    for g in set(GUID_REF_RE.findall(read(os.path.join(dp, f)))):
                        _SHADER_USES["asset"][g] += 1
    return name in _SHADER_USES["find"] or _SHADER_USES["asset"].get(guid, 0) > 0


def ui_tokens():
    """{token: rgba} the authored UI theme holds (UITheme.asset: the values prefabs were authored in)."""
    out = {}
    for ln in read(UI_THEME_ASSET).splitlines():
        m = re.match(r"\s*(\w+): \{r: %s, g: %s, b: %s, a: %s\}" % (NUM, NUM, NUM, NUM), ln)
        if m:
            out[m.group(1)] = [float(m.group(i)) for i in range(2, 6)]
    return out


# Tokens ColorSetUITheme derives from the set's pigments. The rest (tooltip, elements) take per-set ui-token ids.
DERIVED_TOKENS = {"textLight", "textInactive", "inactiveLight", "surfaceBlack", "surfaceVeryDark", "surfaceDark",
                  "surfaceLight", "neutralLightest", "cta", "danger", "controlActive", "purchase", "selection"}
TOKEN_ENUM = {"textLight": "TextLight", "textInactive": "TextInactive", "inactiveLight": "InactiveLight",
              "surfaceBlack": "SurfaceBlack", "surfaceVeryDark": "SurfaceVeryDark", "surfaceDark": "SurfaceDark",
              "surfaceLight": "SurfaceLight", "neutralLightest": "NeutralLightest", "cta": "Cta", "danger": "Danger",
              "controlActive": "ControlActive", "purchase": "Purchase", "tooltip": "Tooltip",
              "elementCharge": "ElementCharge", "elementMass": "ElementMass", "elementSpace": "ElementSpace",
              "elementTime": "ElementTime", "selection": "Selection"}

# Shombith, 2026-11-05 (D3, Docs/UI_STYLE_GUIDE.md section 13): the selection and the focus ring are Jade
# (controlActive). UITheme.asset's selection still holds the old sky blue, the colour prefabs were authored in, so a
# raw colour typed from it is still found as the Selection token; Photic's Selection is the decided colour, as the style
# guide's Deep Sea (Docs/UI/froglet-tokens.json) has it.
DECIDED = {"selection": "controlActive"}
# The menu's sea lines (CosmicShore/UI/SeaLines): UIThemeLive paints a copy of the material with the set's four sea
# colours (ColorSetUITheme.Build). Photic's are the material's own; every other set's the rims Shombith chose.
SEA_MATERIAL = os.path.join(REPO, "Assets", "_Graphics", "Materials", "Shaders", "UI", "Sea_Window.mat")
SEA_TOKENS = {"SeaLine": ("_LineColor", "JadeColors"), "SeaMid": ("_LineColorMid", "BlueColors"),
              "SeaEnd": ("_LineColorEnd", "RubyColors"), "SeaGlow": ("_GlowColor", "JadeColors")}


def authored_token(name):
    """Photic's colour for UI theme token `name` (an enum name, or a sea colour): the authored theme's (UITheme.asset,
    as the style guide decided it: DECIDED), or the sea material's own."""
    if name in SEA_TOKENS:
        m = re.search(r"- %s: \{r: %s, g: %s, b: %s, a: %s\}" % (SEA_TOKENS[name][0], NUM, NUM, NUM, NUM), read(SEA_MATERIAL))
        return [float(m.group(i)) for i in range(1, 4)] + [1.0]
    key = next(k for k, v in TOKEN_ENUM.items() if v == name)
    return list(ui_tokens()[DECIDED.get(key, key)])


_PALETTE = None
COLOR_RE_RAW = re.compile(r"\{r: %s, g: %s, b: %s, a: %s\}" % (NUM, NUM, NUM, NUM))


def palette_fields(pal):
    """{block.field: rgba} of a palette (author_colour_set_assets.parse/palette), ElementalCrystalColors[i] included."""
    out = {}
    for block, fields in pal.items():
        for f, v in fields.items():
            if f == "ElementalCrystalColors":
                for i, raw in enumerate(v):
                    m = COLOR_RE_RAW.search(raw) if isinstance(raw, str) else None
                    c = [float(x) for x in m.groups()] if m else (list(raw) if isinstance(raw, (tuple, list)) else None)
                    if c:
                        out["%s.ElementalCrystalColors[%d]" % (block, i)] = c
            elif isinstance(v, tuple) and len(v) == 4:
                out["%s.%s" % (block, f)] = list(v)
    return out


def photic_palette():
    """[(block.field, rgba)] of PhoticColorSetSO's colour fields: a world colour that is one of them, or a multiple
    of one (a material baked from the field at a brightness), wears that field of the worn set at the same scale."""
    global _PALETTE
    if _PALETTE is None:
        import author_colour_set_assets as writer
        _PALETTE = [(k, v) for k, v in palette_fields(writer.read_original()).items() if max(v[:3]) > 1e-4]
    return _PALETTE


def palette_scale(rgb, field_rgb):
    """1 when rgb IS the field; k when rgb is k times a field that has a real hue (two channels or more), rgb is not
    dark (brightest channel 0.2 or more: a dark colour is proportional to too many things) and k is 0.25 to 4;
    else None."""
    if sum(v > 1e-4 for v in rgb[:3]) < 2:
        return None                                    # a pure primary equals too many fields by chance
    if same(rgb, field_rgb, PALETTE_TOL):
        return 1.0
    peak = max(field_rgb[:3])
    if peak <= 1e-4 or max(rgb[:3]) < 0.2 or sum(v > 0.01 * peak for v in field_rgb[:3]) < 2:
        return None
    j = max(range(3), key=lambda i: field_rgb[i])
    k = rgb[j] / field_rgb[j]
    if not 0.25 <= k <= 4:
        return None
    if all(abs(rgb[i] - k * field_rgb[i]) <= 0.002 + 0.002 * k * peak for i in range(3)):
        return k
    return None


UI_DIRS = ("Assets/_Scripts/UI/", "Assets/_Prefabs/UI", "Assets/_SO_Assets/UI", "Assets/FTUE")
WORLD_TYPES = {"ParticleSystem", "TrailRenderer", "LineRenderer", "Light", "Camera", "SpriteRenderer"}


def role_of(r):
    src, typ = r["source"], r["component"]
    if src == "material":
        return "ui" if is_ui_shader(shader_decls().get(r.get("_shader"), {}).get("name", "")) else "world"
    if src == "csharp":
        return "ui" if r["file"].startswith(UI_DIRS) or "/UI/" in r["file"] else "world"
    if typ in WORLD_TYPES or typ in POST_TYPES or src == "renderSettings":
        return "world"
    if typ in GRAPHICS or typ in SELECTABLES or typ in KIT_SELECTABLES or typ.startswith("Kit") or typ == "TextMesh":
        return "ui"
    if src in ("component", "override") and (is_selectable(r) and ".m_Colors." in r["field"] or
                                             is_graphic(r) and r["field"].endswith(".m_Color")):
        return "ui"
    if src in ("component", "override") and (r["file"].startswith(UI_DIRS) or "/UI/" in r["file"]):
        return "ui"
    return "world"


_FIELD_READS = None
_CS_TEXT = None
_BASES = None
CLASS_RE = re.compile(r"\b(?:class|struct)\s+(\w+)(?:\s*<[^>{]*>)?\s*:\s*([\w.]+)")


def cs_sources():
    """{path: text} of runtime C# (not Editor, not Tests)."""
    global _CS_TEXT
    if _CS_TEXT is None:
        _CS_TEXT = {}
        for top in ("Assets/_Scripts", "Assets/FTUE"):
            for dp, _, files in os.walk(os.path.join(REPO, top)):
                if "/Editor" in dp or "/Tests" in dp:
                    continue
                for f in files:
                    if f.endswith(".cs"):
                        _CS_TEXT[rel(os.path.join(dp, f))] = read(os.path.join(dp, f))
    return _CS_TEXT


def traced_by_file():
    """{path: identifiers read inside a ColorTrace call in that file} (`ColorTrace.Retrace("ui", fireColor)`)."""
    global _FIELD_READS
    if _FIELD_READS is None:
        _FIELD_READS = {}
        for path, text in cs_sources().items():
            names = set()
            for m in TRACE_CALL.finditer(text):
                depth, j = 1, m.end()
                while j < len(text) and depth:
                    depth += {"(": 1, ")": -1}.get(text[j], 0)
                    j += 1
                names.update(re.findall(r"\b([A-Za-z_]\w*)\b", text[m.end():j]))
            if names:
                _FIELD_READS[path] = names
    return _FIELD_READS


def traced_fields():
    """Every identifier read inside a ColorTrace call anywhere (for C# literals: a static declared here and wrapped
    where it is used)."""
    out = set()
    for names in traced_by_file().values():
        out |= names
    return out


def ancestors(typ):
    """typ and its base classes, as declared in runtime C#."""
    global _BASES
    if _BASES is None:
        _BASES = {}
        for text in cs_sources().values():
            for m in CLASS_RE.finditer(text):
                _BASES.setdefault(m.group(1), m.group(2).split(".")[-1])
    out, cur = [], typ
    while cur and cur not in out and len(out) < 12:
        out.append(cur)
        cur = _BASES.get(cur)
    return out


_CLASS_FILE = None


def class_files(typ):
    """The runtime C# files that declare `typ` and its base classes."""
    global _CLASS_FILE
    if _CLASS_FILE is None:
        _CLASS_FILE = defaultdict(set)
        for path, text in cs_sources().items():
            for m in re.finditer(r"\b(?:class|struct)\s+(\w+)", text):
                _CLASS_FILE[m.group(1)].add(path)
    out = set()
    for k in ancestors(typ):
        out |= _CLASS_FILE.get(k, set())
    return out


def stale_field(r):
    """A serialized key no class of the component declares (nor names in FormerlySerializedAs): Unity never reads
    it. Only for first-party script fields (Unity's own m_ fields belong to engine classes)."""
    if r["source"] not in ("component", "override") or r["_doc"] is None or r["_doc"].cls != "114":
        return False
    parts = r["field"].split(".")
    if len(parts) < 2 or parts[1].startswith("m_"):
        return False
    files = class_files(r["component"])
    if not files:
        return False                                    # a package class: its fields are not ours to judge
    name = re.sub(r"\[.*$", "", parts[1])
    return not any(re.search(r"\b%s\b" % re.escape(name), cs_sources()[f]) for f in files)


def class_traces(typ, names):
    """True when a file that names `typ` or one of its base classes reads one of `names` through ColorTrace: the class
    itself (its accessor), a base class, or a reader that paints with the field."""
    kinds = ancestors(typ)
    pat = re.compile(r"\b(?:%s)\b" % "|".join(re.escape(k) for k in kinds))
    for path, traced in traced_by_file().items():
        if names & traced and pat.search(cs_sources()[path]):
            return True
    return False


PAINTED_TYPES = GRAPHICS | WORLD_TYPES | SELECTABLES | {"TextMesh"}


def is_selectable(r):
    return r["component"] in SELECTABLES or (r.get("_doc") is not None and
                                             flat_get(r["_doc"], "MonoBehaviour.m_Transition") is not None)


def is_graphic(r):
    """A uGUI Graphic of any class (it serializes m_RaycastTarget): UIThemeLive.Swap paints its m_Color."""
    return r["component"] in GRAPHICS or (r.get("_doc") is not None and
                                          flat_get(r["_doc"], "MonoBehaviour.m_RaycastTarget") is not None)


def classify(r, tokens):
    """Sets r["status"] (and rule, role, id, token, palette, why) on one record."""
    src = r["source"]
    rule = None
    if src in ("component", "override"):
        rule = rule_component(r)
        if rule is None:
            rev = field_review().get((r["file"], r["field"].split(".", 1)[-1]))
            if rev and rev[0] in ("INERT", "FALLBACK"):
                rule, r["review"] = "field-reviewed", rev[1]
    elif src == "material":
        rule = rule_material(r)
    elif src == "renderSettings":
        rule = rule_render_settings(r)
    elif src == "csharp" and not r.get("_wrapped") and not (r.get("_name") and r["_name"] in traced_fields()):
        rev = reviewed(r["file"], r.get("_literal", ""), r.get("line", 0))
        if rev:
            r["review"] = rev[2]
            if rev[1] == "TRACED":
                r.update(status="traced", role=role_of(r), why="")
                if not (all(v == 0 for v in r["rgba"][:3]) or all(abs(v - 1) < 1e-6 for v in r["rgba"][:3])):
                    r["id"] = value_id(r["role"], r["rgba"])
                else:
                    r["status"] = "fixed"
                return
            rule = {"INERT": "code-inert", "FALLBACK": "code-fallback", "EXCLUDED": "debug"}.get(rev[1])
        elif r["component"] == "debug":
            rule = "debug"
    elif src == "shadergraph" and r["component"] == "ColorNode" and (not r.get("_live", True) or
                                                                     r["file"] not in drawn_shaders()):
        rule = "shader-node-dead" if not r.get("_live", True) else "shader-not-drawn"
    elif src == "hlsl" and r["file"] not in drawn_shaders():
        rule = "shader-not-drawn"
    elif src in ("shader", "shadergraph") and r["component"] in ("Properties", "ColorProperty"):
        g = asset_guid(r["file"])
        if not shader_used_live(g, shader_decls().get(g, {}).get("name", "")):
            rule = "shader-default"
    if rule:
        r.update(status="inert", rule=rule)
        return
    rgb = r["rgba"][:3]
    role = role_of(r)
    r["role"] = role
    if all(v == 0 for v in rgb) or all(abs(v - 1) < 1e-6 for v in rgb):
        r["status"] = "fixed"
        return
    if src == "material" and r["component"] == "Material(CosmicShore/UI/SeaLines)" and \
            r["field"].split(".")[-1] in SEA_PROPS:
        r.update(status="token", token="Sea")          # UIThemeLive.SwapSea paints a copy with the set's rims
        return
    if role == "ui":
        for name, c in tokens.items():
            if same(rgb, c, TOKEN_TOL):
                # UIThemeLive swaps a Graphic's token colour; anything else that holds it (a material, a tint, a code
                # literal) finds it through the trace, whose value for it is the set's own token
                r.update(status="token", token=TOKEN_ENUM.get(name, name), id=value_id("ui", r["rgba"]))
                return
    r["id"] = value_id(role, r["rgba"])
    if role == "world" and max(rgb) > 1e-4:
        for field, c in photic_palette():
            k = palette_scale(rgb, c)
            if k is not None:
                r["palette"] = field
                r["palette_scale"] = round(k, 6)
                break
    if src == "csharp":
        named = r.get("_name") and r["_name"] in traced_fields()
        r["status"] = "traced" if r.get("_wrapped") or named else "untraced"
        if r["status"] == "untraced":
            r["why"] = "a colour literal in runtime code: read it through ColorTrace.Retrace(role, colour) where it paints"
    elif src in ("shadergraph", "hlsl", "shader"):
        r["status"] = "untraced"
        r["why"] = ("a shader default a material is built from in code or an asset: give the material the colour, or "
                    "read it through ColorTrace" if src == "shader" or r["component"] == "ColorProperty" else
                    "a colour baked into a shader: make it a Color property the material saves, so the world painter "
                    "repaints it")
    elif src in ("material", "renderSettings") or r["component"] in PAINTED_TYPES or r["component"] in POST_TYPES:
        r["status"] = "traced"
    elif is_selectable(r) and ".m_Colors." in r["field"]:
        r["status"] = "traced"                         # UIThemeLive.RetraceTint (every ColorTint Selectable)
    elif is_graphic(r) and r["field"].endswith(".m_Color"):
        r["status"] = "traced"                         # UIThemeLive.Swap (every Graphic)
    else:
        parts = r["field"].split(".")
        names = {re.sub(r"\[.*$", "", parts[-1])} | ({re.sub(r"\[.*$", "", parts[-2])} if len(parts) > 2 else set())
        names.discard("MonoBehaviour")
        if class_traces(r["component"], names):
            r["status"] = "traced"
        else:
            r["status"] = "untraced"
            r["why"] = ("a colour field on %s: the code that paints with it reads it through ColorTrace.Retrace"
                        % r["component"])


def classify_all(recs):
    tokens = ui_tokens()
    for r in recs:
        classify(r, tokens)


def public(r):
    return {k: v for k, v in r.items() if not k.startswith("_")}


# ----------------------------------------------------------------------------- repaint: each set's value for an id
def jade_face_screen(s):
    return cs.swatch(s["domains"]["jade"]["face"])[0]


def rim_linear(p):
    lin = cs.linear_of(*cs.at(p, cs.mid(p)))
    peak = max(lin)
    return tuple(max(0.0, v) / peak for v in lin)


def whiteness(c):
    peak = max(c[:3])
    return min(c[:3]) / peak if peak > 1e-9 else 0.0


def tint(rgb, face_rgb, cap):
    """ColorSetUITheme.Tint: the colour's lightness with the face's hue, at its own chroma (never more than cap)."""
    L, a, b = cs.oklab(rgb)
    _, fa, fb = cs.oklab(face_rgb)
    c, h = min(math.hypot(a, b), cap), math.atan2(fb, fa)
    for _ in range(40):                               # keep it on screen: lose chroma, never lightness
        lin = cs.linear_of(L, c * math.cos(h), c * math.sin(h))
        if cs.in_gamut(lin, 1e-4):
            break
        c *= 0.95
    return tuple(min(1.0, max(0.0, cs.to_gamma(max(0.0, v)))) for v in lin)


def paint(rgb_srgb, photic_rim, set_rim):
    """A colour of a family, painted with the set's rim of that family: its brightness (brightest linear channel) and
    its whiteness beyond Photic's rim kept, the hue the set's rim's (author_colour_set_assets.paint, in sRGB storage)."""
    lin = [cs.to_linear(v) for v in rgb_srgb]
    peak = max(lin)
    if peak <= 1e-9:
        return tuple(rgb_srgb)
    w0 = whiteness(rim_linear(photic_rim))
    w = whiteness(lin)
    s = max(0.0, (w - w0) / (1 - w0)) if w0 < 1 else 0.0
    hue = rim_linear(set_rim)
    out = [peak * (h + (1 - h) * s) for h in hue]
    return tuple(min(1.0, max(0.0, cs.to_gamma(v))) for v in out)


def repaint(role, rgba, s, photic):
    """Set `s`'s colour for a colour authored as `rgba` (Photic's), in the same storage (sRGB floats, HDR kept)."""
    rgb, peak = unit(rgba)
    rgb = tuple(min(1.0, v) for v in rgb)
    L, a, b = cs.oklab(rgb)
    C = math.hypot(a, b)
    if max(rgb) <= 0 or C < NEUTRAL:
        return list(rgba)
    fam, near, _ = family_of(rgba)
    fam = near if fam == "band" else fam
    if role == "ui" and fam == "blue" and C <= GROUND[0] and L <= GROUND[1]:
        out = tint(rgb, jade_face_screen(s), C)
    else:
        out = paint(rgb, photic["domains"][fam]["rim"], s["domains"][fam]["rim"])
    return [v * peak for v in out] + [rgba[3]]


# ----------------------------------------------------------------------------- the table
def load_table(path=TABLE):
    try:
        with open(path, encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return None


def set_defs():
    return cs.load()["sets"]


def build_table(recs):
    """{"ids": {id: {...}}, "untraced": [...], "inert": {rule: count}, ...} from the scanned records."""
    tokens = ui_tokens()
    ids = {}
    for r in recs:
        if "id" not in r or r["status"] not in ("traced", "untraced", "token"):
            continue
        photic = list(r["rgba"])
        e = ids.setdefault(r["id"], {"role": r["id"].split(":")[0], "photic": photic,
                                     "family": r["family"] if r["family"] != "band" else "band~" + r["near"],
                                     "uses": 0, "where": []})
        if r.get("palette"):
            e["palette"] = r["palette"]
            e["palette_scale"] = r.get("palette_scale", 1.0)
        if r["status"] == "token":
            e["token"] = r["token"]
        e["uses"] += 1
        w = "%s %s" % (r["file"], r["component"])
        if w not in e["where"]:
            e["where"].append(w)
    for name in list(TOKEN_ENUM.values()) + list(SEA_TOKENS):   # ColorSetUITheme.Build reads every one from the set
        authored = authored_token(name)
        e = ids.setdefault("ui-token:" + name, {"role": "ui-token", "photic": [round(v, 6) for v in authored],
                                                "family": family_of(authored)[0], "uses": 0, "where": []})
        e["token"] = name
        if not e["where"]:
            e["where"] = ["Assets/_Graphics/Materials/Shaders/UI/Sea_Window.mat Material(CosmicShore/UI/SeaLines)"
                          if name in SEA_TOKENS else "Assets/_SO_Assets/UI/UITheme.asset UIThemeSO"]
    for e in ids.values():
        e["where"] = sorted(e["where"])[:4]
    untraced = sorted({(r["file"], r.get("line", 0), r["component"], r["field"][:120], r.get("id", ""), r.get("why", ""))
                       for r in recs if r["status"] == "untraced"})
    return {
        "format": FORMAT,
        "doc": "Docs/VesselModels/COLOR_TRACE.md",
        "tool": "Tools/Build/colour_trace.py",
        "rule": ("Every colour that reaches a pixel comes from the worn colour set. Photic holds the baked values; every "
                 "other set computes its own from its pigments (colour_trace.py repaint). A colour is inert, a UI theme "
                 "token, fixed (pure white, pure black), traced (an id below), or untraced (listed below: the work "
                 "left)."),
        "counts": dict(sorted(Counter(r["status"] for r in recs).items())),
        "inert": dict(sorted(Counter(r["rule"] for r in recs if r["status"] == "inert").items())),
        "inert_why": INERT,
        "tokens": dict(sorted(Counter(r["token"] for r in recs if r["status"] == "token").items())),
        "ids": dict(sorted(ids.items())),
        "untraced": [{"file": f, "line": ln, "component": c, "field": fd, "id": i, "why": w}
                     for f, ln, c, fd, i, w in untraced],
    }


_MT = None


def make_themes():
    global _MT
    if _MT is None:
        sys.path.insert(0, os.path.join(REPO, "Tools", "Analytics", "console"))
        import make_themes as mt
        _MT = mt
    return _MT


def ui_theme(pal):
    """{token: (r, g, b)} the UI theme a palette gives: ColorSetUITheme.Build, line for line (Tint, Fit and Screen are
    make_themes.py's, which Build ports and ColorSetUIThemeTests holds it to)."""
    mt, tok = make_themes(), ui_tokens()
    base = {k: tuple(v[:3]) for k, v in tok.items()}
    face = mt.screen(pal["JadeColors"]["OutsideBlockColor"][:3])
    rim = {b: mt.screen(pal[b]["TrailHighlightColor"][:3]) for b in ("JadeColors", "GoldColors")}
    env = pal["EnvironmentColors"]
    ground = mt.tint(base["surfaceVeryDark"], face, 0.05)
    surface = mt.tint(base["surfaceDark"], face, 0.06)

    def mark(c):
        return mt.fit(mt.fit(c, ground, mt.MARK), surface, mt.MARK)
    jade, gold = mark(rim["JadeColors"]), mark(rim["GoldColors"])
    return {"SurfaceVeryDark": ground, "SurfaceBlack": mt.tint(base["surfaceBlack"], face, 0.035),
            "SurfaceDark": surface, "SurfaceLight": mt.tint(base["surfaceLight"], face, 0.08),
            "TextLight": mt.tint(base["textLight"], face, 0.02),
            "NeutralLightest": mt.fit(mt.tint(base["neutralLightest"], face, 0.05), surface, mt.TEXT),
            "InactiveLight": mt.tint(base["inactiveLight"], face, 0.03),
            "TextInactive": mt.tint(base["textInactive"], face, 0.02),
            "Cta": mt.fit(mt.screen(env["BrightCTA"][:3]), surface, mt.MARK),
            "Danger": mt.fit(mt.screen(env["Danger"][:3]), surface, mt.TEXT),
            "Selection": jade, "ControlActive": jade, "Purchase": gold}


def token_value(key, name, s, photic, set_pal):
    """Set `key`'s colour for UI theme token `name` (or a sea colour). Photic's is the authored one (authored_token:
    the colour set the game was authored in holds the values it was authored in). Every other set's is its own: the
    ones the UI theme derives from the set's pigments (the panels, text, call to action, danger, selection, purchase,
    the sea), the element colours and the tooltip painted with its family's rim; a set may retune any of them
    (colour_sets.json traced: {"ui-token:<Token>": "#RRGGBB"})."""
    if key == "photic":
        return authored_token(name)
    if s and "ui-token:" + name in s.get("traced", {}):
        v = s["traced"]["ui-token:" + name]
        return list(cs.rgb_of_hex(v)) + [1.0] if isinstance(v, str) else list(v)
    if name in SEA_TOKENS:
        return list(make_themes().screen(set_pal[SEA_TOKENS[name][1]]["TrailHighlightColor"][:3])) + [1.0]
    if name in SET_GIVEN:
        tok = ui_tokens()
        return repaint("ui", tok[next(k for k, v in TOKEN_ENUM.items() if v == name)], s, photic)
    return list(ui_theme(set_pal)[name]) + [1.0]


def set_values(key, s, table, photic, set_pal=None):
    """[(id, rgba, note)] set `key` computes, in the table's id order (Photic: the baked values), before the values of
    every set are kept apart (all_values)."""
    overrides = (s or {}).get("traced", {})
    out = []
    for i, e in table["ids"].items():
        role = "ui" if e["role"].startswith("ui") else "world"
        if e.get("token") and e["token"] != "Sea":
            c = token_value(key, e["token"], s, photic, set_pal)
            c = c[:3] + [e["photic"][3]]
        elif key == "photic":
            c = list(e["photic"])
        elif i in overrides:
            v = overrides[i]
            c = (list(cs.rgb_of_hex(v)) + [e["photic"][3]]) if isinstance(v, str) else list(v)
        elif e.get("palette") and set_pal is not None:
            f = palette_fields(set_pal).get(e["palette"])
            k = e.get("palette_scale", 1.0)
            c = [v * k for v in f[:3]] + [e["photic"][3]] if f else repaint(role, e["photic"], s, photic)
        else:
            c = repaint(role, e["photic"], s, photic)
        note = "%d use%s: %s" % (e["uses"], "" if e["uses"] == 1 else "s", "; ".join(e["where"][:2]))
        if e.get("token"):
            note = "UI theme token %s; %s" % (e["token"], note)
        out.append((i, [f32(v) for v in c], note[:240]))
    return out


def nudge(c):
    """One 8-bit step on the channel with the most room (HDR scale kept)."""
    rgb, peak = unit(c)
    k = min(range(3), key=lambda j: rgb[j]) if max(rgb) >= 1 - 1e-9 else max(range(3), key=lambda j: rgb[j])
    step = 1.0 / 255 if rgb[k] <= 1 - 1.0 / 255 else -1.0 / 255
    rgb = list(rgb)
    rgb[k] = min(1.0, max(0.0, rgb[k] + step))
    return [v * peak for v in rgb] + [c[3]]


def identity(i):
    """The colour an id names, whichever role it is drawn in: ui:H and world:H are one colour (its hex and HDR peak)."""
    return i.split(":", 1)[1]


def bare(vid):
    """A value id without its role: how ColorTrace finds a colour in either role (its own first, then the other)."""
    return vid.split(":", 1)[1]


def claims(table):
    """{bare value id: identity} every id claims as authored: the id itself (ColorTrace names it from the id)."""
    return {identity(i): identity(i) for i in table["ids"] if i.split(":")[0] in ("ui", "world")}


def unambiguous(values_by_set, table, avoid=()):
    """Every set's values, kept apart: ColorTrace names a colour on screen by its value, from the ids (the authored
    colours) and from every set worn in a session (what each one painted), in either role. So no value of any set may
    land on another colour's id or on any set's value of another colour; a clash moves to the nearest free 8-bit value,
    the sets taken in order (Photic first: its baked values are its ids and never move; its UI theme tokens are derived
    and may). A UI value that is no theme token also stays out of UIThemeLive's tolerance of every token value in
    `avoid` (live_tokens: UIThemeLive asks for a token before it asks the trace, so a value there would be read as the
    token)."""
    taken = claims(table)
    out = {}
    for key, values in values_by_set.items():
        res = []
        for i, c, note in values:
            role = i.split(":")[0]
            if role in ("ui", "world"):
                me = identity(i)
                clear = avoid if role == "ui" and not table["ids"].get(i, {}).get("token") else ()
                k = bare(value_id(role, c))
                if taken.get(k, me) != me or near_token(c, clear):
                    c, k = nearest_free(c, role, me, taken, clear)
                taken[k] = me
            res.append((i, c, note))
        out[key] = res
    return out


def near_token(c, tokens):
    """True when `c` is one of `tokens` to UIThemeLive (every channel within its Tolerance, 0.6/255; a hair more, so
    the C# palette's floats, which match the tool's to about 1e-6, cannot fall inside)."""
    lim = TOKEN_TOL + 1e-4
    return any(abs(c[0] - t[0]) <= lim and abs(c[1] - t[1]) <= lim and abs(c[2] - t[2]) <= lim for t in tokens)


def live_tokens(table=None):
    """Every UI theme token value UIThemeLive.TokenOf can meet: the authored theme's, and each shipped set's palette
    (ColorSetUITheme.Build: the derived tokens and the ones the set gives)."""
    import author_colour_set_assets as writer
    defs, orig, ref = set_defs(), writer.read_original(), cs.photic_reference()
    out = [tuple(v[:3]) for k, v in ui_tokens().items() if k in TOKEN_ENUM]
    for key in set_order():
        s = defs.get(key)
        pal = orig if key == "photic" else writer.palette(s, orig)
        out += [tuple(token_value(key, name, s, ref, pal)[:3]) for name in TOKEN_ENUM.values()]
    return out


_DELTAS = []


def deltas(radius=5):
    """8-bit steps around a colour, nearest first: many colours a set paints alike (a family's rim at one brightness)
    spread over a few steps on every channel instead of walking one channel far from its hue."""
    if not _DELTAS:
        r = range(-radius, radius + 1)
        _DELTAS.extend(sorted(((x, y, z) for x in r for y in r for z in r if (x, y, z) != (0, 0, 0)),
                              key=lambda d: (d[0] * d[0] + d[1] * d[1] + d[2] * d[2], max(map(abs, d)), d)))
    return _DELTAS


def nearest_free(c, role, me, taken, clear=()):
    """The value nearest `c` (in 8-bit steps of its colour at brightest channel 1, its HDR peak kept) that no other
    colour holds and that is no token value of `clear`, and its bare value id."""
    rgb, peak = unit(c)
    base = [byte(v) for v in rgb]
    for d in deltas():
        q = [base[j] + d[j] for j in range(3)]
        if min(q) < 0 or max(q) > 255:
            continue
        cand = [f32(v / 255.0 * peak) for v in q] + [c[3]]
        k = bare(value_id(role, cand))
        if taken.get(k, me) == me and not near_token(cand, clear):
            return cand, k
    for _ in range(255):                              # a cluster wider than the neighbourhood: walk one channel
        c = nudge(c)
        k = bare(value_id(role, c))
        if taken.get(k, me) == me and not near_token(c, clear):
            return c, k
    raise ValueError("no free value near %s for %s" % (value_id(role, c), me))


def ambiguities(values_by_set, table, avoid=()):
    """[(set, id, other id or 'a UI theme token')] whose values ColorTrace (or UIThemeLive, which asks for a token
    first) could not tell apart: the gate's check of `unambiguous`."""
    seen, bad = claims(table), []
    for key, values in values_by_set.items():
        for i, c, _ in values:
            role = i.split(":")[0]
            if role not in ("ui", "world"):
                continue
            me, k = identity(i), bare(value_id(role, c))
            if seen.get(k, me) != me:
                bad.append((key, i, seen[k]))
            elif role == "ui" and not table["ids"].get(i, {}).get("token") and near_token(c, avoid):
                bad.append((key, i, "a UI theme token"))
            seen.setdefault(k, me)
    return bad


SET_ORDER = ("photic",)                              # then colour_sets.json's sets, in the file's order


def set_order():
    return list(SET_ORDER) + [k for k in set_defs() if k not in SET_ORDER]


_ALL = None


def all_values(table=None):
    """{set key: [(id, rgba, note)]} every shipped set's TracedColors, kept apart (unambiguous), Photic first. Cached."""
    global _ALL
    cache = table is None
    if cache and _ALL is not None:
        return _ALL
    import author_colour_set_assets as writer
    table = load_table() if table is None else table
    defs, orig, ref = set_defs(), writer.read_original(), cs.photic_reference()
    raw = {}
    for key in set_order():
        if key == "photic":
            raw[key] = set_values("photic", None, table, None, orig)
        else:
            raw[key] = set_values(key, defs[key], table, ref, writer.palette(defs[key], orig))
    out = unambiguous(raw, table, live_tokens(table))
    if cache:
        _ALL = out
    return out


def fmt(v):
    """A float as Unity writes one: integers bare, others in nine significant digits (a float32 round-trips)."""
    v = f32(v)
    return "%d" % v if v == int(v) and abs(v) < 1e9 else "%.9g" % v


def yaml_quote(s):
    if re.match(r"^[A-Za-z0-9_][A-Za-z0-9_ .:/#,;()+-]*$", s) and ": " not in s and " #" not in s and not s.endswith(":"):
        return s
    return "'" + s.replace("'", "''") + "'"


def traced_yaml(values):
    """The TracedColors block of a palette asset."""
    if not values:
        return ["  TracedColors: []"]
    lines = ["  TracedColors:"]
    for i, c, note in values:
        lines += ["  - Id: " + yaml_quote(i), "    Color: {r: %s, g: %s, b: %s, a: %s}" % tuple(fmt(v) for v in c),
                  "    Note: " + yaml_quote(note)]
    return lines


def splice_traced(text, block_lines):
    """`text` (a palette asset) with its TracedColors block replaced by `block_lines` (appended when absent)."""
    lines = text.rstrip("\n").split("\n")
    start = next((k for k, ln in enumerate(lines) if ln.startswith("  TracedColors:")), None)
    if start is None:
        return "\n".join(lines + block_lines) + "\n"
    end = start + 1
    while end < len(lines) and (lines[end].startswith("  - ") or lines[end].startswith("    ")):
        end += 1
    return "\n".join(lines[:start] + block_lines + lines[end:]) + "\n"


def photic_text(table):
    return splice_traced(read(PHOTIC_ASSET), traced_yaml(all_values(table)["photic"]))


# ----------------------------------------------------------------------------- the gate
def check(recs=None, table=None):
    """Problems: the scan and the table disagree, Photic's block is stale, or a set asset lacks the table's ids."""
    recs = scan() if recs is None else recs
    table = load_table() if table is None else table
    if table is None:
        return ["colour_trace.json is missing: run python3 Tools/Build/colour_trace.py --build"]
    errs = []
    now = build_table(recs)
    for i in sorted(set(now["ids"]) - set(table["ids"])):
        e = now["ids"][i]
        errs.append("new colour %s (%s, %d use%s: %s) is not in colour_trace.json: run --build, then the writers"
                    % (i, e["family"], e["uses"], "" if e["uses"] == 1 else "s", "; ".join(e["where"][:2])))
    for i in sorted(set(table["ids"]) - set(now["ids"])):
        errs.append("colour_trace.json traces %s, which nothing authors any more: run --build" % i)
    for i in sorted(set(now["ids"]) & set(table["ids"])):
        if not same(now["ids"][i]["photic"], table["ids"][i]["photic"], 1e-6):
            errs.append("%s: the baked value moved (%s, was %s): run --build" % (i, now["ids"][i]["photic"], table["ids"][i]["photic"]))
    old = {(u["file"], u["component"], u["field"], u["id"]) for u in table.get("untraced", [])}
    for u in now["untraced"]:
        if (u["file"], u["component"], u["field"], u["id"]) not in old:
            errs.append("untraced colour %s:%s %s (%s): %s" % (u["file"], u["line"], u["field"][:80], u["id"], u["why"]))
    for i, e in table["ids"].items():                   # a baked value packs to its own id (how it is found on screen)
        role = i.split(":")[0]
        if role in ("ui", "world") and not e.get("token") and value_id(role, e["photic"]) != i:
            errs.append("%s: its Photic value %s packs to %s" % (i, e["photic"], value_id(role, e["photic"])))
    decided = {TOKEN_ENUM[k] for k in DECIDED}
    for i, c, _ in all_values(table)["photic"]:        # Photic holds the values the game was authored in
        role = i.split(":")[0]
        if role in ("ui", "world") and table["ids"].get(i, {}).get("token") not in decided and value_id(role, c) != i:
            errs.append("Photic's value for %s is %s, not the colour it names: Photic holds the values the game was "
                        "authored in" % (i, value_id(role, c)))
    if os.path.exists(PHOTIC_ASSET) and read(PHOTIC_ASSET) != photic_text(table):
        errs.append("PhoticColorSetSO.asset's TracedColors are stale: run python3 Tools/Build/colour_trace.py --write-photic")
    errs += lazy_loads_unpainted(recs)
    import author_colour_set_assets as writer
    for key, sd in set_defs().items():
        for i in sorted(set(sd.get("traced", {})) - set(table["ids"])):
            errs.append("colour_sets.json %s retunes %s, which nothing draws any more: drop it" % (key, i))
    shipped = {}
    for key in set_order():
        path = PHOTIC_ASSET if key == "photic" else writer.asset_path(key)
        if os.path.exists(path):
            entries = writer.parse(read(path)).get("TracedColors", {}).get("_entries", [])
            shipped[key] = [(i, list(c), n) for i, c, n in entries if c]
    tokens = live_tokens(table)
    for key, i, other in ambiguities(shipped, table, tokens)[:5]:
        errs.append("%s's %s takes a value ColorTrace also reads as %s (in that set or one before it): run the writers"
                    % ("PhoticColorSetSO" if key == "photic" else writer.asset_name(key), i, other))
    for i, e in sorted(table["ids"].items()):          # an authored UI colour cannot move: a set's token must not reach it
        if i.startswith("ui:") and not e.get("token") and near_token(e["photic"], tokens):
            errs.append("%s is within UIThemeLive's tolerance of a colour set's UI theme token, so it would be read as "
                        "that token: retune the set (colour_sets.json) or make the colour that token" % i)
    for key in set_defs():
        text = read(writer.asset_path(key))
        got = {m.strip("'") for m in re.findall(r"^  - Id: (.+)$", text, re.M)}
        missing = set(table["ids"]) - got
        if missing:
            errs.append("%s carries %d of the table's %d traced colours (missing e.g. %s): run "
                        "python3 Tools/Build/author_colour_set_assets.py"
                        % (writer.asset_name(key), len(got & set(table["ids"])), len(table["ids"]), sorted(missing)[0]))
    return errs


LOAD_RE = re.compile(r"Resources\.Load(?:<\w+>)?\s*\(")


def lazy_loads_unpainted(recs, sources=None):
    """A material in a Resources folder that carries a traced colour, loaded by name from code, is loaded after the
    scene's repaint: its loader paints it (WorldColorTrace.Paint) or it shows Photic's colours under every set."""
    errs, sources = [], cs_sources() if sources is None else sources
    mats = sorted({r["file"] for r in recs if r["status"] in ("traced", "token") and r["file"].endswith(".mat")
                   and "/Resources/" in r["file"]})
    for f in mats:
        name = os.path.splitext(f.split("/Resources/", 1)[1])[0]
        literal = '"%s"' % name
        loaders = [p for p, text in sources.items() if literal in text and LOAD_RE.search(text)]
        for p in loaders:
            if "WorldColorTrace.Paint(" not in sources[p]:
                errs.append("%s loads %s (a traced colour) after the scene's repaint and does not paint it: call "
                            "WorldColorTrace.Paint(material) where it is loaded" % (p, f))
    return errs


# ----------------------------------------------------------------------------- reports
def summary(recs):
    print("%d authored colours outside the colour sets" % len(recs))
    print("by status:", dict(Counter(r["status"] for r in recs).most_common()))
    print("inert by rule:")
    for k, n in Counter(r["rule"] for r in recs if r["status"] == "inert").most_common():
        print("  %6d  %-22s %s" % (n, k, INERT[k][:90]))
    print("tokens:", dict(Counter(r["token"] for r in recs if r["status"] == "token").most_common()))
    print("traced by role:", dict(Counter(r["role"] for r in recs if r["status"] == "traced").most_common()))
    ids = {r["id"] for r in recs if r.get("id") and r["status"] in ("traced", "untraced", "token")}
    print("distinct ids:", len(ids), dict(Counter(i.split(":")[0] for i in ids).most_common()))
    un = [r for r in recs if r["status"] == "untraced"]
    print("untraced: %d records" % len(un))
    for (src, comp), n in Counter((r["source"], r["component"]) for r in un).most_common(30):
        print("  %6d  %-12s %s" % (n, src, comp))


def report(recs):
    un = defaultdict(list)
    for r in recs:
        if r["status"] == "untraced":
            un[r["file"]].append(r)
    for f in sorted(un, key=lambda k: -len(un[k])):
        rs = un[f]
        print("%s (%d)" % (f, len(rs)))
        for r in rs[:8]:
            print("   %s%s  %s  %s" % ("line %d " % r["line"] if r.get("line") else "", r["component"], r["field"][:90],
                                      r.get("id", "")))
        if len(rs) > 8:
            print("   ... %d more" % (len(rs) - 8))


# ----------------------------------------------------------------------------- self-test
def self_test():
    bad = []

    def expect(name, cond):
        if not cond:
            bad.append(name)

    expect("value id of a token", value_id("ui", [0.4549, 0.4823, 0.6784, 1]) == "ui:747BAD")
    expect("HDR id carries its peak", value_id("world", [2.0, 1.0, 0.0, 1]) == "world:FF8000x2.00")
    d = Doc("198", "1", False)
    d.flat = {"ParticleSystem.ColorModule.enabled": "0", "ParticleSystem.InitialModule.startColor.minMaxState": "0",
              "ParticleSystem.InitialModule.startColor.maxGradient.m_NumColorKeys": "2"}
    base = {"source": "component", "file": "Assets/_Prefabs/x.prefab", "object": "o", "component": "ParticleSystem",
            "rgba": [1, 0, 0, 1], "_doc": d, "_ctx": {"by_go": {}}}
    expect("a disabled module is inert",
           rule_component(dict(base, field="ParticleSystem.ColorModule.gradient.maxGradient.key0")) == "ps-module-off")
    expect("an unused min colour is inert",
           rule_component(dict(base, field="ParticleSystem.InitialModule.startColor.minColor")) == "ps-mode-unused")
    expect("the max colour is live", rule_component(dict(base, field="ParticleSystem.InitialModule.startColor.maxColor")) is None)
    d.flat["ParticleSystem.InitialModule.startColor.minMaxState"] = "1"
    expect("an unused gradient key is inert",
           rule_component(dict(base, field="ParticleSystem.InitialModule.startColor.maxGradient.key5")) == "gradient-key-unused")
    expect("a used gradient key is live",
           rule_component(dict(base, field="ParticleSystem.InitialModule.startColor.maxGradient.key1")) is None)
    t = Doc("114", "2", False)
    t.typ, t.flat = "TextMeshProUGUI", {"MonoBehaviour.m_enableVertexGradient": "0"}
    tb = dict(base, component="TextMeshProUGUI", _doc=t)
    expect("TMP's m_Color is its twin", rule_component(dict(tb, field="MonoBehaviour.m_Color")) == "tmp-colour-twin")
    expect("a TMP gradient off is inert",
           rule_component(dict(tb, field="MonoBehaviour.m_fontColorGradient.topLeft")) == "tmp-gradient-off")
    expect("TMP's vertex colour is live", rule_component(dict(tb, field="MonoBehaviour.m_fontColor")) is None)
    b = Doc("114", "3", False)
    b.typ, b.flat = "Button", {"MonoBehaviour.m_Transition": "1", "MonoBehaviour.m_TargetGraphic": "{fileID: 5}"}
    bb = dict(base, component="Button", _doc=b, field="MonoBehaviour.m_Colors.m_NormalColor")
    expect("a colour tint is live", rule_component(bb) is None)
    b.flat["MonoBehaviour.m_Transition"] = "2"
    expect("a sprite swap's colours are inert", rule_component(bb) == "selectable-no-tint")
    expect("a kit selectable's colours are inert", rule_component(dict(bb, component="KitButton")) == "kit-selectable")
    defs = set_defs()
    photic = cs.photic_reference()
    expect("a grey keeps", repaint("world", [0.5, 0.5, 0.5, 1], defs["nocturne"], photic) == [0.5, 0.5, 0.5, 1])
    gold = repaint("world", [1.0, 0.68, 0.0, 1], defs["nocturne"], photic)
    expect("a gold stays gold", family_of(gold)[0] in ("gold", "band"))
    ground = repaint("ui", [0.1333, 0.149, 0.2706, 1], defs["nocturne"], photic)
    expect("a UI ground keeps its lightness", abs(cs.oklab(ground[:3])[0] - cs.oklab((0.1333, 0.149, 0.2706))[0]) < 0.01)
    expect("an HDR colour keeps its brightness", abs(max(repaint("world", [2.0, 0.2, 0.1, 1], defs["nacre"], photic)[:3]) - 2.0) < 1e-6)
    recs = [{"source": "component", "file": "Assets/_Prefabs/a.prefab", "object": "o", "component": "Image",
             "field": "MonoBehaviour.m_Color", "rgba": [0.9, 0.2, 0.1, 1], "family": "fire", "near": "fire",
             "neutral": False, "status": "traced", "role": "ui", "id": "ui:E6331A"}]
    table = build_table(recs)
    expect("the table holds the id", "ui:E6331A" in table["ids"])
    recs2 = recs + [dict(recs[0], rgba=[0.1, 0.9, 0.2, 1], id="ui:1AE633", family="lime", near="lime")]
    expect("a new colour fails the gate", any("new colour ui:1AE633" in e for e in check(recs2, table)))
    expect("a stale id fails the gate", any("nothing authors" in e for e in check([], table)))
    un = recs + [dict(recs[0], status="untraced", source="csharp", file="Assets/_Scripts/x.cs", line=3, why="w",
                      id="world:102030", rgba=[0.06, 0.12, 0.19, 1])]
    expect("a new untraced colour fails the gate", any(e.startswith("untraced colour") for e in check(un, build_table(recs))))
    moved = [dict(recs[0], rgba=[0.9, 0.2, 0.1001, 1])]
    expect("a moved baked value fails the gate", any("baked value moved" in e for e in check(moved, table)))
    two = {"ids": {"ui:E6331A": {"photic": [0.9, 0.2, 0.1, 1]}, "ui:1AE633": {"photic": [0.1, 0.9, 0.2, 1]},
                   "world:E6331A": {"photic": [0.9, 0.2, 0.1, 1]}, "world:3366CC": {"photic": [0.2, 0.4, 0.8, 1]}}}
    clash = {"a": [("ui:E6331A", [0.5, 0.5, 0.9, 1], ""), ("ui:1AE633", [0.5, 0.5, 0.9, 1], "")]}
    expect("two ids sharing a value are caught", bool(ambiguities(clash, two)))
    expect("the nudge parts them", not ambiguities(unambiguous(clash, two), two))
    onto = {"a": [("ui:E6331A", [0.1, 0.9, 0.2, 1], "")]}
    expect("a value on another id's authored colour is caught", bool(ambiguities(onto, two)))
    across = {"a": [("ui:E6331A", [0.5, 0.5, 0.9, 1], "")], "b": [("ui:1AE633", [0.5, 0.5, 0.9, 1], "")]}
    expect("a value another set gives another id is caught", bool(ambiguities(across, two)))
    expect("the nudge parts sets", not ambiguities(unambiguous(across, two), two))
    roles = {"a": [("ui:1AE633", [0.2, 0.4, 0.8, 1], "")]}
    expect("a value on another role's colour is caught", bool(ambiguities(roles, two)))
    twins = {"a": [("ui:E6331A", [0.5, 0.5, 0.9, 1], ""), ("world:E6331A", [0.5, 0.5, 0.9, 1], "")]}
    expect("one colour in two roles is no clash", not ambiguities(twins, two))
    tok = [(0.5, 0.5, 0.9)]
    on = {"a": [("ui:E6331A", [0.5 + 0.4 / 255, 0.5, 0.9, 1], "")]}
    expect("a UI value inside a token's tolerance is caught", bool(ambiguities(on, two, tok)))
    moved = unambiguous(on, two, tok)
    expect("the writer moves it out", not ambiguities(moved, two, tok) and not near_token(moved["a"][0][1], tok))
    expect("a world value may sit on a token", not ambiguities({"a": [("world:3366CC", [0.5, 0.5, 0.9, 1], "")]}, two, tok))
    lazy = [dict(recs[0], source="material", file="Assets/Resources/Glow.mat", component="Material(x)")]
    loader = 'const string Path = "Glow"; var m = Resources.Load<Material>(Path);'
    expect("a lazily loaded traced material nobody paints is caught",
           bool(lazy_loads_unpainted(lazy, {"Assets/_Scripts/a.cs": loader})))
    expect("one its loader paints passes",
           not lazy_loads_unpainted(lazy, {"Assets/_Scripts/a.cs": loader + " WorldColorTrace.Paint(m);"}))
    blk = traced_yaml([("ui:E6331A", [0.9, 0.2, 0.1, 1], "1 use: a")])
    text = splice_traced("x\n  EnvironmentColors:\n    Danger: {r: 1, g: 0, b: 0, a: 0}\n", blk)
    expect("splice appends the block", text.rstrip().endswith("Note: '1 use: a'"))
    expect("splice replaces the block", splice_traced(text, ["  TracedColors: []"]).count("TracedColors") == 1)
    print("colour_trace self-test: " + ("ok" if not bad else "FAILED " + "; ".join(bad)))
    return 0 if not bad else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    recs = scan()
    if "--scan" in argv:
        out = argv[argv.index("--out") + 1] if "--out" in argv else None
        text = json.dumps([public(r) for r in recs], indent=0)
        if out:
            with open(out, "w", encoding="utf-8") as fh:
                fh.write(text)
            print("wrote %d records to %s" % (len(recs), out))
        else:
            print(text)
        return 0
    if "--build" in argv:
        table = build_table(recs)
        with open(TABLE, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(table, fh, indent=1, ensure_ascii=False)
            fh.write("\n")
        print("colour_trace: wrote %s (%d ids, %d untraced records)" % (rel(TABLE), len(table["ids"]), len(table["untraced"])))
        return 0
    if "--write-photic" in argv:
        table = load_table()
        text = photic_text(table)                        # read before the file is opened for writing
        if not text.startswith("%YAML"):
            print("colour_trace: PhoticColorSetSO.asset did not read as a Unity asset; nothing written")
            return 1
        with open(PHOTIC_ASSET, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        print("colour_trace: wrote PhoticColorSetSO.asset's TracedColors (%d)" % len(table["ids"]))
        return 0
    if "--check" in argv:
        errs = check(recs)
        for e in errs[:60]:
            print("colour_trace: " + e)
        if len(errs) > 60:
            print("colour_trace: ... %d more" % (len(errs) - 60))
        if not errs:
            t = load_table()
            print("colour_trace --check: ok (%d traced ids, %d untraced records listed)" % (len(t["ids"]), len(t["untraced"])))
        return 1 if errs else 0
    if "--report" in argv:
        report(recs)
        return 0
    summary(recs)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
